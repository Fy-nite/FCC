using System.Text;
using ObjectIR.AST;

namespace ObjectIR.FobCompiler;

/// <summary>
/// Compiles a pre-lowered module into a FOB/IR v3 binary.
/// </summary>
/// <remarks>
/// <para>
/// <b>Compilation pipeline:</b><br/>
/// <c>TextIR text</c>
///   → <c>TextIrParser.ParseModule</c> → <c>ModuleNode</c> (AST)
///   → <b>caller-supplied lowering</b> → binary payload from <c>ModuleBinaryWriter.Write</c>
///   → <c>FobIrCompiler.CompileFromPayload</c> → <c>.fobir</c> binary
/// </para>
/// <para>
/// The library only depends on <c>ObjectIR.AST</c> for parsing TextIR into a
/// <see cref="ModuleNode"/>.  Lowering (AST → <c>ModuleDto</c>) and binary
/// serialisation (<c>ModuleBinaryWriter</c>) happen in the Lattice runtime project.
/// </para>
/// </remarks>
public sealed class FobIrCompiler
{
	private const string Magic = "FOB/IR";

	// ── Public API ────────────────────────────────────────────────────────────

	/// <summary>
	/// Compiles a raw binary payload into a FOB/IR v3 binary file.
	/// The payload is the output of <c>ModuleBinaryWriter.Write(moduleDto)</c>.
	/// </summary>
	/// <param name="payload">Binary bytecode produced by the lattice runtime's <c>ModuleBinaryWriter</c>.</param>
	/// <param name="includes">Optional external type names to embed in the includes section.</param>
	public byte[] CompileFromPayload(byte[] payload, IEnumerable<string>? includes = null)
	{
		var includeList = (includes ?? []).Distinct(StringComparer.Ordinal)
		                                  .OrderBy(s => s, StringComparer.Ordinal)
		                                  .ToList();

		var stringTable  = BuildStringTable(includeList);
		var payloadBytes = payload;

		// ── Compute section offsets ──────────────────────────────────────
		// Header: 6 (magic) + 2 (version) + 4×4 (four uint fields) = 24 bytes
		var headerSize       = Magic.Length + sizeof(ushort) + sizeof(uint) * 4;
		var includesSize     = (uint)(sizeof(uint) + includeList.Count * sizeof(uint));
		var stringDataSize   = (uint)(sizeof(uint) + stringTable.Bytes.Length);

		var includesOffset   = (uint)headerSize;
		var stringDataOffset = includesOffset   + includesSize;
		var payloadOffset    = stringDataOffset + stringDataSize;
		var payloadLength    = (uint)payloadBytes.Length;

		using var stream = new MemoryStream((int)(payloadOffset + payloadLength));
		using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

		WriteHeader(writer, includesOffset, stringDataOffset, payloadOffset, payloadLength);
		WriteIncludes(writer, includeList, stringTable);
		WriteStringData(writer, stringTable.Bytes);
		writer.Write(payloadBytes);

		writer.Flush();
		return stream.ToArray();
	}

	/// <summary>
	/// Parses <paramref name="textIr"/> into a <see cref="ModuleNode"/> so the
	/// caller can run <c>AstLowering.Lower</c> + <c>ModuleBinaryWriter.Write</c>
	/// before calling <see cref="CompileFromPayload"/>.
	/// </summary>
	public static ModuleNode ParseTextIr(string textIr) =>
		TextIrParser.ParseModule(textIr);

	/// <summary>
	/// Parses the TextIR file at <paramref name="inputPath"/> and returns the
	/// <see cref="ModuleNode"/> — the caller lowers it and passes the binary
	/// payload to <see cref="CompileFromPayload"/>.
	/// </summary>
	public static ModuleNode ParseTextIrFile(string inputPath) =>
		TextIrParser.ParseModule(File.ReadAllText(inputPath, Encoding.UTF8));

	// ── Write helpers ─────────────────────────────────────────────────────────

	/// <remarks>
	/// V3 header layout (24 bytes):
	/// <code>
	///   6 bytes  – ASCII "FOB/IR"
	///   2 bytes  – ushort version (= 3)
	///   4 bytes  – uint includesOffset
	///   4 bytes  – uint stringDataOffset
	///   4 bytes  – uint payloadOffset
	///   4 bytes  – uint payloadLength
	/// </code>
	/// </remarks>
	private static void WriteHeader(
		BinaryWriter writer,
		uint includesOffset,
		uint stringDataOffset,
		uint payloadOffset,
		uint payloadLength)
	{
		writer.Write(Encoding.ASCII.GetBytes(Magic));
		writer.Write(FobFormatVersion.Current);   // ushort = 2
		writer.Write(includesOffset);
		writer.Write(stringDataOffset);
		writer.Write(payloadOffset);
		writer.Write(payloadLength);
	}

	private static void WriteIncludes(
		BinaryWriter writer,
		List<string> includes,
		StringTable stringTable)
	{
		writer.Write((uint)includes.Count);
		foreach (var inc in includes)
			writer.Write(stringTable.GetOffset(inc));
	}

	private static void WriteStringData(BinaryWriter writer, byte[] data)
	{
		writer.Write((uint)data.Length);
		writer.Write(data);
	}

	// ── Collect includes from AST (helper for callers) ────────────────────────

	/// <summary>
	/// Walks a <see cref="ModuleNode"/> and returns all referenced external type
	/// names — i.e. types used in calls / newobj that are not defined in the module.
	/// Pass the result as the <c>includes</c> argument to <see cref="CompileFromPayload"/>.
	/// </summary>
	public static IReadOnlyList<string> CollectIncludes(ModuleNode module)
	{
		var definedTypes = new HashSet<string>(
			module.Classes.Select(c => c.Name)
			      .Concat(module.Interfaces.Select(i => i.Name)));
		var includes = new HashSet<string>(StringComparer.Ordinal);

		foreach (var cls in module.Classes)
		{
			foreach (var ctor in cls.Constructors)
				CollectFromBody(ctor.Body, definedTypes, includes);
			foreach (var method in cls.Methods)
				CollectFromBody(method.Body, definedTypes, includes);
		}

		return includes.OrderBy(x => x, StringComparer.Ordinal).ToList();
	}

	private static void CollectFromBody(
		BlockStatement body,
		HashSet<string> definedTypes,
		HashSet<string> includes)
	{
		foreach (var stmt in body.Statements)
		{
			switch (stmt)
			{
				case InstructionStatement inst:
					CollectFromInstruction(inst.Instruction, definedTypes, includes);
					break;
				case IfStatement ifStmt:
					CollectFromBody(ifStmt.Then, definedTypes, includes);
					if (ifStmt.Else is not null)
						CollectFromBody(ifStmt.Else, definedTypes, includes);
					break;
				case WhileStatement whileStmt:
					CollectFromBody(whileStmt.Body, definedTypes, includes);
					break;
			}
		}
	}

	private static void CollectFromInstruction(
		ObjectIR.AST.Instruction instruction,
		HashSet<string> definedTypes,
		HashSet<string> includes)
	{
		switch (instruction as object)
		{
			case CallInstruction call:
				TryAdd(call.Target.DeclaringType.Name, definedTypes, includes);
				TryAdd(call.ReturnType.Name, definedTypes, includes);
				foreach (var arg in call.Arguments)
					TryAdd(arg.Name, definedTypes, includes);
				break;
			case NewObjInstruction newObj:
				TryAdd(newObj.Type.Name, definedTypes, includes);
				if (newObj.Constructor is not null)
					TryAdd(newObj.Constructor.DeclaringType.Name, definedTypes, includes);
				foreach (var arg in newObj.Arguments)
					TryAdd(arg.Name, definedTypes, includes);
				break;
		}
	}

	private static void TryAdd(
		string typeName,
		HashSet<string> definedTypes,
		HashSet<string> includes)
	{
		if (!string.IsNullOrWhiteSpace(typeName) && !definedTypes.Contains(typeName))
			includes.Add(typeName);
	}

	// ── Private types ─────────────────────────────────────────────────────────

	private sealed class StringTable
	{
		private readonly Dictionary<string, uint> _offsets;
		public byte[] Bytes { get; }

		public StringTable(Dictionary<string, uint> offsets, byte[] bytes)
		{
			_offsets = offsets;
			Bytes    = bytes;
		}

		public uint GetOffset(string value) => _offsets.TryGetValue(value, out var o) ? o : 0;
	}

	private static StringTable BuildStringTable(IEnumerable<string> strings)
	{
		var unique  = new HashSet<string>(strings.Where(s => !string.IsNullOrWhiteSpace(s)), StringComparer.Ordinal);
		var offsets = new Dictionary<string, uint>(StringComparer.Ordinal);
		using var ms = new MemoryStream();

		foreach (var value in unique.OrderBy(s => s, StringComparer.Ordinal))
		{
			offsets[value] = (uint)ms.Length;
			var encoded = Encoding.UTF8.GetBytes(value);
			ms.Write(encoded, 0, encoded.Length);
			ms.WriteByte(0); // null-terminator
		}

		return new StringTable(offsets, ms.ToArray());
	}
}
