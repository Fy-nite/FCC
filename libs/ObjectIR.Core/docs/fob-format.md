# FOB Format

The FOB (Flat Object Binary) format is the compact binary distribution format for ObjectIR modules. It is used by the `FobIrCompiler` to produce `.fob` files and by `FobIrReader` to load them back.

---

## File layout

A `.fob` file is a fixed 24-byte header followed by three variable-length sections:

```
Offset  Size   Field
──────  ─────  ──────────────────────────────────────────────────────────
0       6      Magic bytes — ASCII "FOB/IR"
6       2      Format version (ushort, little-endian) — currently 3
8       4      includesOffset   — absolute byte position of the Includes section
12      4      stringDataOffset — absolute byte position of the StringData section
16      4      payloadOffset    — absolute byte position of the Payload section
20      4      payloadLength    — byte count of the binary bytecode payload
──────  ─────  ──────────────────────────────────────────────────────────

[Includes]    @ includesOffset
  4 bytes     count of include entries (uint32)
  count×N     null-terminated UTF-8 string offsets (relative to stringData)

[StringData]  @ stringDataOffset
  4 bytes     total byte length of the packed string pool (uint32)
  N bytes     null-terminated packed UTF-8 strings

[Payload]     @ payloadOffset
  payloadLength bytes  — ModuleBinaryWriter binary bytecode
```

---

## Format versions

| Version | Description |
|---------|-------------|
| 3 (current) | Module stored as compact binary bytecode (`ModuleBinaryWriter` payload). Loading is a single binary deserialisation — no JSON parsing or AST lowering required. |

`FobFormatVersion.Current` always reflects the version written by the current compiler.

---

## FobIrBinary

`FobIrBinary` is the decoded in-memory representation of a `.fob` file:

```csharp
// After reading a .fob file with FobIrReader:
FobIrBinary binary = FobIrReader.Read(fileBytes);

ushort version              = binary.FormatVersion;   // 3
byte[] payload              = binary.Payload;         // raw bytecode
IReadOnlyList<string> deps  = binary.Includes;        // external type names
```

---

## Reading a .fob file

```csharp
using ObjectIR.FobCompiler;

byte[] fileBytes = File.ReadAllBytes("output.fob");
FobIrBinary binary = FobIrReader.Read(fileBytes);

// Deserialize the payload into a Module
Module module = FobIrDecompiler.Decompile(binary);
```

---

## Writing a .fob file

```csharp
using ObjectIR.FobCompiler;

byte[] fileBytes = FobIrCompiler.Compile(module);
File.WriteAllBytes("output.fob", fileBytes);
```

---

## When to use FOB

The FOB format is best suited for:

- **Compiler output** — fast to write, minimal file size
- **Production distribution** — no runtime parsing overhead
- **Caching** — serialize once, reload instantly

For human-readable or interoperable formats see [Serialization](serialization.md).
