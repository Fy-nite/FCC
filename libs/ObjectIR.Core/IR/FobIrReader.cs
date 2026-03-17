using System.Text;

namespace ObjectIR.FobCompiler;

/// <summary>
/// Reads a FOB/IR v3 binary into a <see cref="FobIrBinary"/>.
/// </summary>
/// <remarks>
/// <b>File layout:</b>
/// <code>
/// [Header]            24 bytes
///   6  – ASCII magic "FOB/IR"
///   2  – ushort version (= 3)
///   4  – uint includesOffset
///   4  – uint stringDataOffset
///   4  – uint payloadOffset
///   4  – uint payloadLength
///
/// [Includes]          @ includesOffset
///   4  – uint count
///   count × 4  – uint offset into StringData blob
///
/// [StringData]        @ stringDataOffset
///   4  – uint dataLength
///   dataLength bytes – null-terminated UTF-8 strings
///
/// [Payload]           @ payloadOffset
///   payloadLength bytes – ModuleBinaryWriter binary bytecode
/// </code>
/// </remarks>
public static class FobIrReader
{
private const string Magic = "FOB/IR";

/// <summary>Reads a FOB/IR binary from a byte array.</summary>
public static FobIrBinary ReadFromBytes(byte[] bytes)
{
using var ms = new MemoryStream(bytes);
return ReadFromStream(ms);
}

/// <summary>Reads a FOB/IR binary file from disk.</summary>
public static FobIrBinary ReadFromFile(string path)
{
using var fs = File.OpenRead(path);
return ReadFromStream(fs);
}

/// <summary>Reads a FOB/IR binary from an arbitrary <see cref="Stream"/>.</summary>
public static FobIrBinary ReadFromStream(Stream stream)
{
using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

var magic = Encoding.ASCII.GetString(reader.ReadBytes(Magic.Length));
if (magic != Magic)
throw new InvalidDataException($"Not a FOB/IR file \u2014 unexpected magic '{magic}'.");

var version = reader.ReadUInt16();
if (version != FobFormatVersion.V3)
throw new NotSupportedException(
$"FOB/IR format version {version} is not supported. Expected v{FobFormatVersion.V3}.");

var includesOffset   = reader.ReadUInt32();
var stringDataOffset = reader.ReadUInt32();
var payloadOffset    = reader.ReadUInt32();
var payloadLength    = reader.ReadUInt32();

// \u2500\u2500 StringData \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500
stream.Position = stringDataOffset;
var dataLength  = reader.ReadUInt32();
var dataBytes   = reader.ReadBytes((int)dataLength);

string ReadString(uint offset)
{
var start = (int)offset;
var end   = start;
while (end < dataBytes.Length && dataBytes[end] != 0) end++;
return Encoding.UTF8.GetString(dataBytes, start, end - start);
}

// \u2500\u2500 Includes \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500
stream.Position = includesOffset;
var includeCount = reader.ReadUInt32();
var includes     = new List<string>((int)includeCount);
for (var i = 0; i < includeCount; i++)
includes.Add(ReadString(reader.ReadUInt32()));

// \u2500\u2500 Payload \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500
stream.Position = payloadOffset;
var payload = reader.ReadBytes((int)payloadLength);

return new FobIrBinary(FobFormatVersion.V3, payload, includes);
}
}
