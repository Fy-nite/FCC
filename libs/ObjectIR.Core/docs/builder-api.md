# Builder API

The `IRBuilder` family (in `ObjectIR.Core.Builder`) provides a fluent interface for constructing modules without touching the underlying IR model directly. Every builder method returns either `this` (for chaining) or a child builder, and each child builder has an `EndXxx()` method that returns the parent.

---

## IRBuilder

Top-level entry point.

```csharp
var builder = new IRBuilder("ModuleName");

Module module = builder.Build();
```

| Method | Returns | Description |
|--------|---------|-------------|
| `Class(name)` | `ClassBuilder` | Define a new class |
| `Interface(name)` | `InterfaceBuilder` | Define a new interface |
| `Struct(name)` | `StructBuilder` | Define a new struct |
| `Build()` | `Module` | Return the finished module |

---

## ClassBuilder

Obtained from `IRBuilder.Class(name)`.

```csharp
builder
    .Class("Animal")
        .Namespace("MyApp.Models")
        .Access(AccessModifier.Public)
        .Abstract()
        .Generic("T")
        .Extends(TypeReference.FromName("System.Object"))
        .Implements(TypeReference.FromName("IDisposable"))
        // ... add fields / methods
    .EndClass()
```

| Method | Description |
|--------|-------------|
| `.Namespace(ns)` | Set the namespace |
| `.Access(modifier)` | Set access modifier |
| `.Abstract()` | Mark as abstract |
| `.Sealed()` | Mark as sealed |
| `.Generic(params)` | Add generic type parameters |
| `.Extends(typeRef)` | Set base class |
| `.Implements(typeRefs)` | Add implemented interfaces |
| `.Field(name, type)` | → `FieldBuilder` |
| `.Method(name, returnType)` | → `MethodBuilder` |
| `.Constructor()` | → `MethodBuilder` for `.ctor` |
| `.EndClass()` | → `IRBuilder` |

---

## InterfaceBuilder

Obtained from `IRBuilder.Interface(name)`.

```csharp
builder
    .Interface("IAnimal")
        .Namespace("MyApp.Models")
        .Access(AccessModifier.Public)
        .Method("Speak", TypeReference.String)
    .EndInterface()
```

| Method | Description |
|--------|-------------|
| `.Namespace(ns)` | Set the namespace |
| `.Access(modifier)` | Set access modifier |
| `.Method(name, returnType)` | Add an abstract method signature |
| `.EndInterface()` | → `IRBuilder` |

---

## StructBuilder

Obtained from `IRBuilder.Struct(name)`.

```csharp
builder
    .Struct("Point")
        .Access(AccessModifier.Public)
        .Field("X", TypeReference.Float32)
        .Field("Y", TypeReference.Float32)
    .EndStruct()
```

| Method | Description |
|--------|-------------|
| `.Access(modifier)` | Set access modifier |
| `.Field(name, type)` | Add a field (returns `StructBuilder` for chaining) |
| `.EndStruct()` | → `IRBuilder` |

---

## FieldBuilder

Obtained from `ClassBuilder.Field(name, type)`.

```csharp
classBuilder
    .Field("_count", TypeReference.Int32)
        .Access(AccessModifier.Private)
        .Static()
        .ReadOnly()
        .InitialValue(0)
    .EndField()
```

| Method | Description |
|--------|-------------|
| `.Access(modifier)` | Set access modifier (default: `Private`) |
| `.Static()` | Mark as static |
| `.ReadOnly()` | Mark as read-only |
| `.InitialValue(value)` | Set the initial value |
| `.EndField()` | → `ClassBuilder` |

---

## MethodBuilder

Obtained from `ClassBuilder.Method(name, returnType)` or `.Constructor()`.

```csharp
classBuilder
    .Method("Add", TypeReference.Int32)
        .Access(AccessModifier.Public)
        .Virtual()
        .Parameter("a", TypeReference.Int32)
        .Parameter("b", TypeReference.Int32)
        .Local("result", TypeReference.Int32)
        .Body()
            .Ldarg(1)
            .Ldarg(2)
            .Add()
            .Ret()
        .EndBody()
    .EndMethod()
```

| Method | Description |
|--------|-------------|
| `.Access(modifier)` | Set access modifier (default: `Public`) |
| `.Static()` | Mark as static |
| `.Virtual()` | Mark as virtual |
| `.Override()` | Mark as override |
| `.Abstract()` | Mark as abstract |
| `.Parameter(name, type)` | Add a parameter (in order) |
| `.Local(name, type)` | Declare a local variable |
| `.Body()` | → `InstructionBuilder` |
| `.EndMethod()` | → `ClassBuilder?` |

---

## InstructionBuilder

Obtained from `MethodBuilder.Body()`. All methods return `this` for chaining.

### Load / store

| Method | IL equivalent | Description |
|--------|---------------|-------------|
| `Ldarg(index)` | `ldarg` | Load argument by index |
| `Ldloc(name)` | `ldloc` | Load local variable |
| `Ldfld(field)` | `ldfld` | Load instance field |
| `Ldsfld(field)` | `ldsfld` | Load static field |
| `LdcI4(value)` | `ldc.i4` | Load `int32` constant |
| `LdcR4(value)` | `ldc.r4` | Load `float32` constant |
| `Ldstr(value)` | `ldstr` | Load string constant |
| `Ldnull()` | `ldnull` | Load null |
| `Stloc(name)` | `stloc` | Store to local variable |
| `Stfld(field)` | `stfld` | Store to instance field |

### Arithmetic

| Method | Description |
|--------|-------------|
| `Add()` | Pop two values, push sum |
| `Sub()` | Pop two values, push difference |
| `Mul()` | Pop two values, push product |
| `Div()` | Pop two values, push quotient |

### Comparison

| Method | Description |
|--------|-------------|
| `Ceq()` | Push 1 if top two values are equal |
| `Cgt()` | Push 1 if second > top |
| `Clt()` | Push 1 if second < top |

### Calls

| Method | Description |
|--------|-------------|
| `Call(methodRef)` | Static / non-virtual call |
| `Callvirt(methodRef)` | Virtual call |
| `Newobj(typeRef)` | Allocate and call constructor |
| `Newarr(elementType)` | Allocate array |

### Stack

| Method | Description |
|--------|-------------|
| `Dup()` | Duplicate top of stack |
| `Pop()` | Discard top of stack |
| `Ret()` | Return (value on stack if non-void) |

### Structured control flow

```csharp
// if / else
.If(
    new Condition(ComparisonOp.Greater, ...),
    then => then.Ldarg(0).Ret(),
    @else => @else.Ldnull().Ret()
)

// while loop
.While(
    new Condition(ComparisonOp.Less, ...),
    body => body.Ldloc("i").LdcI4(1).Add().Stloc("i")
)

// foreach
.Foreach("item", "collection", body => body.Call(printRef))
```

`EndBody()` returns the parent `MethodBuilder`.

---

## Full example

```csharp
using ObjectIR.Core.Builder;
using ObjectIR.Core.IR;

var printRef = new MethodReference(
    TypeReference.FromName("System.Console"),
    "WriteLine",
    TypeReference.Void,
    new List<TypeReference> { TypeReference.String }
);

var module = new IRBuilder("Demo")
    .Interface("IShape")
        .Method("Area", TypeReference.Float64)
    .EndInterface()
    .Class("Circle")
        .Implements(TypeReference.FromName("IShape"))
        .Field("_radius", TypeReference.Float64)
            .Access(AccessModifier.Private)
            .EndField()
        .Constructor()
            .Parameter("radius", TypeReference.Float64)
            .Body()
                .Ldarg(0)
                .Ldarg(1)
                .Stfld(new FieldReference(
                    TypeReference.FromName("Circle"),
                    "_radius",
                    TypeReference.Float64))
                .Ret()
            .EndBody()
        .EndMethod()
        .Method("Area", TypeReference.Float64)
            .Override()
            .Body()
                .Ldarg(0)
                .Ldfld(new FieldReference(
                    TypeReference.FromName("Circle"),
                    "_radius",
                    TypeReference.Float64))
                .Dup()
                .Mul()
                // multiply by π would need a constant load
                .Ret()
            .EndBody()
        .EndMethod()
    .EndClass()
    .Build();
```
