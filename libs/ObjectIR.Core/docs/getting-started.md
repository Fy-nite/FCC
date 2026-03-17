# Getting Started

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)

## Installation

```bash
dotnet add package ObjectIR.Core
```

Or add to your `.csproj` manually:

```xml
<PackageReference Include="ObjectIR.Core" Version="0.1.0" />
```

---

## Your first module

The quickest way to build a module is with the fluent `IRBuilder`:

```csharp
using ObjectIR.Core.Builder;
using ObjectIR.Core.IR;

// 1. Create a builder scoped to a module name
var builder = new IRBuilder("HelloWorld");

// 2. Define a class with a field and a method
builder
    .Class("Greeter")
        .Field("message", TypeReference.String)
            .Access(AccessModifier.Private)
            .EndField()
        .Method("Greet", TypeReference.Void)
            .Access(AccessModifier.Public)
            .Body()
                .Ldarg(0)                         // load 'this'
                .Ldfld(new FieldReference(
                    TypeReference.FromName("Greeter"),
                    "message",
                    TypeReference.String))        // load field
                .Ret()
            .EndBody()
        .EndMethod()
    .EndClass();

// 3. Get the finished module
Module module = builder.Build();

Console.WriteLine(module.Name);             // HelloWorld
Console.WriteLine(module.Types[0].Name);   // Greeter
```

---

## Loading from text

If you prefer a text-based format over the fluent API, `ModuleLoader` can parse it for you:

```csharp
using ObjectIR.Core.Serialization;

var text = """
    module HelloWorld
    class Greeter {
        field message: string
        method Greet() -> void {
            ldarg 0
            ldfld Greeter.message
            ret
        }
    }
    """;

var loader = new ModuleLoader();
Module module = loader.LoadFromText(text);
```

See [Serialization](serialization.md) for the full text format reference.

---

## What to read next

| Goal | Page |
|------|------|
| Understand how everything fits together | [Architecture](architecture.md) |
| Learn all the builder methods | [Builder API](builder-api.md) |
| Explore the data model | [IR Model](ir-model.md) |
| Save/load modules to disk | [Serialization](serialization.md) |
| Merge multiple modules | [Composition](composition.md) |
