# Architecture

## Overview

ObjectIR.Core is structured in layers. Each layer depends only on layers below it.

```
┌─────────────────────────────────────────────────────┐
│  Compilers  (Construct front-end → IRBuilder)        │
├─────────────────────────────────────────────────────┤
│  Composition  (ModuleComposer, DependencyResolver)  │
├─────────────────────────────────────────────────────┤
│  Serialization  (text, JSON, BSON, ModuleLoader)    │
├─────────────────────────────────────────────────────┤
│  Builder  (fluent IRBuilder API)                    │
├─────────────────────────────────────────────────────┤
│  IR  (core data model — the source of truth)        │
└─────────────────────────────────────────────────────┘
```

---

## Namespaces at a glance

| Namespace | What lives here |
|-----------|-----------------|
| `ObjectIR.Core.IR` | The data model: `Module`, `TypeDefinition`, `MethodDefinition`, `Instruction`, etc. |
| `ObjectIR.Core.Builder` | Fluent `IRBuilder` and sub-builders (`ClassBuilder`, `MethodBuilder`, …). Convenience wrapper around the IR model. |
| `ObjectIR.Core.Serialization` | `ModuleSerializer` (JSON/BSON), `ModuleLoader` (text format), `BsonSerializer`, `InstructionSerializer` |
| `ObjectIR.Core.Composition` | `ModuleComposer` — merges modules; `DependencyResolver` — tracks and resolves cross-module type/method references |
| `ObjectIR.Core.Compilers` | `ConstructCompiler` — converts a Construct-language AST into an `ObjectIR.Core.IR.Module` |
| `ObjectIR.AST` | AST node types (`ModuleNode`, `ClassNode`, …) and `TextIrParser` |
| `ObjectIR.FobCompiler` | `FobIrBinary` record and format-version constants for the FOB binary format |

---

## Core data flow

```
Source text / AST
       │
       ▼
  TextIrParser / ConstructCompiler
       │  produces
       ▼
    Module  ◄──── IRBuilder (fluent)
       │
       ├──► ModuleSerializer   → JSON / BSON / text on disk
       │
       ├──► ModuleComposer     → merged Module
       │         │
       │         └──► DependencyResolver  (topological sort, cycle detection)
       │
       └──► FobIrCompiler      → .fob binary file
```

---

## Design decisions

**Mutable model, immutable references**  
`Module`, `ClassDefinition`, etc. are mutable so you can build up a module incrementally. `TypeReference` and `MethodReference` are value-like and immutable; their constructors are private and you obtain instances through factory properties (`TypeReference.Int32`) or static helpers (`TypeReference.FromName`).

**Visitor pattern for instructions**  
Every `Instruction` subclass implements `Accept(IInstructionVisitor)`. If you need to do a pass over the instruction stream (e.g. for code generation or analysis), implement `IInstructionVisitor` rather than pattern-matching on concrete types.

**High-level structured control flow**  
The IR includes `IfInstruction`, `WhileInstruction`, `ForEachInstruction`, and `TryInstruction` as first-class instructions rather than raw branch targets. This makes it easier to work with high-level language constructs without needing to lower them to `brtrue`/`brfalse` yourself.

**Fluent builder keeps the IR model clean**  
The `IRBuilder` family is a thin convenience layer; it never holds state that isn't already reflected in the `Module` it wraps. You can mix direct model manipulation with builder calls freely.
