# Serialization

ObjectIR.Core can load and save modules in three formats:

| Format | Class | Notes |
|--------|-------|-------|
| Text (human-readable) | `ModuleLoader` | Good for hand-authoring and debugging |
| JSON | `ModuleSerializer` | Interoperable; also validates against a schema |
| BSON | `BsonSerializer` | Compact binary; requires MongoDB.Bson |

---

## Text format

### Grammar overview

```
module <Name> [<version>]

class <Name> [extends <Type>] [implements <Type>, ...] {
    field <name>: <Type>
    method <name>(<param>: <Type>, ...) -> <ReturnType> {
        <instructions>
    }
}

interface <Name> {
    method <name>(<param>: <Type>, ...) -> <ReturnType>
}

struct <Name> {
    field <name>: <Type>
}

enum <Name> {
    <Member> = <value>
}
```

Comments start with `//` and are stripped before parsing.

### Example

```
module CalculatorApp

class Calculator {
    field history: List<int32>

    method Add(a: int32, b: int32) -> int32 {
        ldarg a
        ldarg b
        add
        ret
    }

    method Sub(a: int32, b: int32) -> int32 {
        ldarg a
        ldarg b
        sub
        ret
    }
}

interface IOperation {
    method Execute(a: int32, b: int32) -> int32
}
```

### Loading from a string

```csharp
using ObjectIR.Core.Serialization;

var loader = new ModuleLoader();
Module module = loader.LoadFromText(text);
```

### Loading from a file

```csharp
string text = File.ReadAllText("calculator.ir");
var module = new ModuleLoader().LoadFromText(text);
```

### Caching

`ModuleLoader` keeps an internal cache keyed by module name. If you call `LoadFromText` twice with different text that produces the same module name, the second call may return the cached result. Call `new ModuleLoader()` for a fresh context.

---

## JSON format

`ModuleSerializer` serializes a `Module` to a JSON string or deserializes one back:

```csharp
using ObjectIR.Core.Serialization;

// Serialize
string json = ModuleSerializer.Serialize(module);

// Deserialize
Module loaded = ModuleSerializer.Deserialize(json);
```

A companion `JsonValidator` class can validate a JSON string against the ObjectIR schema before deserializing:

```csharp
var errors = JsonValidator.Validate(json);
if (errors.Count == 0)
    Module loaded = ModuleSerializer.Deserialize(json);
```

### Extension methods

`ModuleSerializationExtensions` adds convenience methods directly on `Module`:

```csharp
string json = module.ToJson();
module.SaveToFile("output.ir.json");

Module loaded = Module.FromJson(json);
Module loaded = Module.LoadFromFile("output.ir.json");
```

---

## BSON format

Use `BsonSerializer` when you need a compact binary representation that is still schema-flexible (unlike the FOB format which is optimized for minimal size):

```csharp
using ObjectIR.Core.Serialization;

byte[] bytes = BsonSerializer.Serialize(module);
Module loaded = BsonSerializer.Deserialize(bytes);
```

> **Dependency**: BSON serialization requires the `MongoDB.Bson` NuGet package, which is already included as a dependency of ObjectIR.Core.

---

## Advanced formats

`AdvancedModuleFormats` provides additional round-trip formats (e.g. XML, MessagePack wrappers). See the source file for the available methods — these are experimental and the API may change.

---

## Choosing a format

| Scenario | Recommended format |
|----------|--------------------|
| Human-authored IR, config files | Text |
| REST APIs, debug tooling | JSON |
| Database storage, network transfer | BSON |
| Compiler output, production distribution | [FOB binary](fob-format.md) |
