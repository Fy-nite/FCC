using System.Text;
using ObjectIR.AST;

namespace ObjectIR.FobCompiler;

/// <summary>
/// Provides human-readable output from a FOB/IR v3 binary for diagnostic purposes.
/// </summary>
/// <remarks>
/// V3 binaries store compact binary bytecode and cannot be round-tripped back to
/// TextIR source without the original source file.  The decompiler emits a
/// structured comment header describing the binary's contents.
/// </remarks>
public sealed class FobIrDecompiler
{
// \u2500\u2500 Overloads: raw bytes \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500

/// <summary>Decompiles a FOB/IR byte array to a diagnostic text summary.</summary>
public string DecompileToText(byte[] bytes, string? moduleName = null) =>
DecompileToText(FobIrReader.ReadFromBytes(bytes), moduleName);

// \u2500\u2500 Overloads: file paths \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500

/// <summary>
/// Reads the FOB/IR binary at <paramref name="inputPath"/>, generates a
/// diagnostic summary, and writes it to <paramref name="outputPath"/>
/// (defaults to the same base name with a <c>.textir</c> extension).
/// </summary>
public void DecompileFile(string inputPath, string? outputPath = null)
{
outputPath ??= Path.ChangeExtension(inputPath, ".textir");
var binary     = FobIrReader.ReadFromFile(inputPath);
var moduleName = Path.GetFileNameWithoutExtension(inputPath);
var text       = DecompileToText(binary, moduleName);
File.WriteAllText(outputPath, text, Encoding.UTF8);
}

/// <summary>Reads and decompiles a FOB/IR file to a diagnostic text string.</summary>
public string DecompileFileToText(string filePath, string? moduleName = null) =>
DecompileToText(
FobIrReader.ReadFromFile(filePath),
moduleName ?? Path.GetFileNameWithoutExtension(filePath));

// \u2500\u2500 Core \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500

/// <summary>
/// Produces a diagnostic text summary of the binary's header, includes,
/// and payload size.
/// </summary>
public string DecompileToText(FobIrBinary binary, string? moduleName = null)
{
moduleName ??= "Decompiled";

var sb = new StringBuilder();
sb.AppendLine($"// FOB/IR v{binary.FormatVersion} binary");
sb.AppendLine($"// Module:  {moduleName}");
sb.AppendLine($"// Payload: {binary.Payload.Length:N0} bytes of binary bytecode");

if (binary.Includes.Count > 0)
{
sb.AppendLine($"// Dependencies ({binary.Includes.Count}):");
foreach (var inc in binary.Includes)
sb.AppendLine($"//   {inc}");
}

sb.AppendLine($"//");
sb.AppendLine($"// Use 'lattice run <file.fobir>' to execute.");
return sb.ToString();
}
}
