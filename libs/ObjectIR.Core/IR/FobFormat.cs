namespace ObjectIR.FobCompiler;

/// <summary>
/// FOB/IR format versions.
/// </summary>
public static class FobFormatVersion
{
	/// <summary>
	/// Version 3 — the module is stored as compact binary bytecode
	/// (<c>ModuleBinaryWriter</c> payload).  Loading is a single binary
	/// deserialisation; no JSON parsing or AST lowering is required.
	/// </summary>
	public const ushort V3 = 3;

	/// <summary>The version written by the current compiler.</summary>
	public const ushort Current = V3;
}

/// <summary>
/// The decoded contents of a FOB/IR binary.
/// </summary>
/// <remarks>
/// <b>File layout (24-byte header + three sections):</b>
/// <code>
/// [Header]
///   6 bytes  – ASCII magic "FOB/IR"
///   2 bytes  – ushort format version (= 3)
///   4 bytes  – uint includesOffset   (absolute file position)
///   4 bytes  – uint stringDataOffset (absolute file position)
///   4 bytes  – uint payloadOffset    (absolute file position)
///   4 bytes  – uint payloadLength    (byte count of binary bytecode payload)
///
/// [Includes]   @ includesOffset   – count + null-terminated name offsets
/// [StringData] @ stringDataOffset – length + null-terminated packed UTF-8 strings
/// [Payload]    @ payloadOffset    – ModuleBinaryWriter binary bytecode
/// </code>
/// </remarks>
public sealed class FobIrBinary
{
	/// <summary>Format version read from the file header.</summary>
	public ushort FormatVersion { get; }

	/// <summary>
	/// The raw binary bytecode payload produced by <c>ModuleBinaryWriter.Write</c>.
	/// Pass to <c>ModuleBinaryReader.Read</c> to obtain a <c>ModuleDto</c>.
	/// </summary>
	public byte[] Payload { get; }

	/// <summary>External type names this binary depends on at runtime.</summary>
	public IReadOnlyList<string> Includes { get; }

	/// <summary>Constructs a decoded FOB/IR binary result.</summary>
	public FobIrBinary(ushort formatVersion, byte[] payload, IReadOnlyList<string> includes)
	{
		FormatVersion = formatVersion;
		Payload       = payload;
		Includes      = includes;
	}
}
