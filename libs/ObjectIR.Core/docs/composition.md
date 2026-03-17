# Composition

`ModuleComposer` (in `ObjectIR.Core.Composition`) lets you merge multiple independent modules into a single unified module while catching conflicts and preserving dependency order.

---

## Basic usage

```csharp
using ObjectIR.Core.Composition;

var composer = new ModuleComposer();
composer.AddModule(modelsModule);
composer.AddModule(servicesModule);
composer.AddModule(controllersModule);

// Validate before composing (optional but recommended)
CompositionValidation result = composer.Validate();
if (result.HasErrors)
{
    foreach (var error in result.Errors)
        Console.Error.WriteLine(error);
    return;
}

foreach (var warning in result.Warnings)
    Console.WriteLine($"WARN: {warning}");

// Merge into one module
Module composite = composer.Compose("MyApp", "1.0.0");
```

`AddModule` returns the `ModuleComposer` itself so you can chain calls:

```csharp
Module composite = new ModuleComposer()
    .AddModule(modelsModule)
    .AddModule(servicesModule)
    .Compose("MyApp");
```

---

## Validation

`Validate()` checks for:

- **Type name conflicts** — the same unqualified type name defined in more than one module raises an error.
- **Unresolved dependencies** — a base class, implemented interface, or field type that isn't found in any registered module raises a warning (not an error, because external/built-in types are allowed).

```csharp
CompositionValidation v = composer.Validate();
v.HasErrors    // true if composition would fail
v.HasWarnings  // true if there are non-fatal issues
v.Errors       // IReadOnlyList<string>
v.Warnings     // IReadOnlyList<string>
Console.WriteLine(v.ToString());  // formatted summary
```

---

## Compose

`Compose(name, version?)` performs the actual merge:

1. Runs `Validate()` — throws `InvalidOperationException` if there are errors.
2. Topologically sorts all types across all input modules (base classes before derived classes).
3. Copies all types and top-level functions into the composite module.
4. Merges metadata from each module, namespaced by source module name (`"models.Author"` → value).
5. Adds `"ComposedFrom"` and `"CompositionTime"` entries to the composite metadata.

---

## Dependency graph

You can inspect the dependency graph without composing:

```csharp
Dictionary<string, List<string>> graph = composer.GetDependencyGraph();

foreach (var (type, deps) in graph)
    Console.WriteLine($"{type} → [{string.Join(", ", deps)}]");
```

---

## Namespace aliasing

If two modules export types with the same name under different namespaces, you can register a link to inform the composer how to route lookups:

```csharp
composer.LinkNamespace(
    sourceNamespace: "MyApp.Models",
    targetModule:    "CommonLib",
    targetNamespace: "Common.Models"
);
```

> **Note**: namespace aliasing is informational — it stores the mapping for use during composition but does not rename types in the output.

---

## DependencyResolver

`DependencyResolver` is the lower-level engine that `ModuleComposer` delegates to. You can use it directly if you only need dependency analysis without a full compose step.

```csharp
using ObjectIR.Core.Composition;

var resolver = new DependencyResolver();
resolver.RegisterModule(moduleA);
resolver.RegisterModule(moduleB);

// Resolve a type reference to its definition
TypeDefinition? def = resolver.ResolveType(TypeReference.FromName("Animal"));

// Get all types a given type depends on
IEnumerable<TypeDefinition> deps = resolver.GetDependencies(animalDef);

// Get all types that depend on a given type
IEnumerable<TypeDefinition> dependents = resolver.GetDependents(animalDef);

// Topological order across all registered modules
IEnumerable<TypeDefinition> ordered = resolver.TopologicalSort();

// Detect circular dependencies
IEnumerable<List<TypeDefinition>> cycles = resolver.FindCircularDependencies();
foreach (var cycle in cycles)
    Console.WriteLine($"Cycle: {string.Join(" → ", cycle.Select(t => t.Name))}");
```

---

## Generating a report

`ModuleComposer.GenerateReport()` returns a human-readable summary:

```csharp
Console.WriteLine(composer.GenerateReport());
// === Module Composition Report ===
// Total modules: 3
// Total types: 12
// ...
```
