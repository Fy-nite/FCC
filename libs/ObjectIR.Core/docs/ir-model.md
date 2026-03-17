# IR Model

This page describes the core data model in the `ObjectIR.Core.IR` namespace.

---

## Module

`Module` is the top-level container. Everything lives inside a module.

```csharp
var module = new Module("MyApp");           // name only
var module = new Module("MyApp", "2.0.0");  // name + semver version
```

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Module identifier |
| `Version` | `Version` | Semver version, default `1.0.0` |
| `Types` | `List<TypeDefinition>` | All type definitions |
| `Functions` | `List<FunctionDefinition>` | Top-level (non-member) functions |
| `Metadata` | `Dictionary<string, object>` | Arbitrary key/value metadata |

**Helper methods**

```csharp
module.DefineClass("Foo");       // adds ClassDefinition, returns it
module.DefineInterface("IFoo");  // adds InterfaceDefinition, returns it
module.DefineStruct("Point");    // adds StructDefinition, returns it
module.FindType("Ns.Foo");       // look up by qualified name, returns null if missing
```

---

## Type definitions

All type definitions inherit from `TypeDefinition`.

| Class | `Kind` | Description |
|-------|--------|-------------|
| `ClassDefinition` | `TypeKind.Class` | Reference type; supports inheritance and interfaces |
| `InterfaceDefinition` | `TypeKind.Interface` | Contract with method/property signatures |
| `StructDefinition` | `TypeKind.Struct` | Value type; supports interfaces |
| `EnumDefinition` | `TypeKind.Enum` | Named integer constants |

### Common TypeDefinition properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Simple (unqualified) type name |
| `Namespace` | `string?` | Namespace prefix |
| `Access` | `AccessModifier` | `Public` / `Private` / `Protected` / `Internal` |
| `GenericParameters` | `List<GenericParameter>` | Type parameters for generic types |
| `Metadata` | `Dictionary<string, object>` | Arbitrary annotations |

`GetQualifiedName()` returns `Namespace.Name` (or just `Name` when namespace is empty), plus generic parameters if present — e.g. `"System.Collections.List<int32>"`.

### ClassDefinition extras

```csharp
var cls = module.DefineClass("Animal");
cls.BaseType = TypeReference.FromName("System.Object");
cls.Interfaces.Add(TypeReference.FromName("IDisposable"));
cls.IsAbstract = true;
cls.IsSealed   = false;

var field  = cls.DefineField("name", TypeReference.String);
var method = cls.DefineMethod("Speak", TypeReference.String);
var ctor   = cls.DefineConstructor();   // creates ".ctor" method
```

### InterfaceDefinition extras

```csharp
var iface = module.DefineInterface("IAnimal");
iface.BaseInterfaces.Add(TypeReference.FromName("IDisposable"));
iface.DefineMethod("Speak", TypeReference.String);  // IsAbstract = true automatically
```

### StructDefinition extras

Same field/method helpers as `ClassDefinition` but no inheritance — only interfaces.

### EnumDefinition extras

```csharp
var e = new EnumDefinition("Color");
e.UnderlyingType = TypeReference.Int32;  // default
e.DefineMember("Red",   0);
e.DefineMember("Green", 1);
e.DefineMember("Blue",  2);
```

---

## Members

### FieldDefinition

```csharp
var field = cls.DefineField("count", TypeReference.Int32);
field.Access      = AccessModifier.Private;
field.IsStatic    = false;
field.IsReadOnly  = true;
field.InitialValue = 0;
```

### PropertyDefinition

```csharp
var prop = new PropertyDefinition("Name", TypeReference.String);
prop.Getter = cls.DefineMethod("get_Name", TypeReference.String);
cls.Properties.Add(prop);
```

### MethodDefinition

```csharp
var method = cls.DefineMethod("Add", TypeReference.Int32);
method.Access      = AccessModifier.Public;
method.IsVirtual   = true;
method.IsOverride  = false;
method.IsAbstract  = false;
method.IsConstructor = false;

method.DefineParameter("a", TypeReference.Int32);
method.DefineParameter("b", TypeReference.Int32);
method.DefineLocal("result", TypeReference.Int32);

// Instructions are in method.Instructions (InstructionList)
```

### FunctionDefinition

Top-level functions (not inside any class) live in `Module.Functions`. Same shape as `MethodDefinition` but no access modifier or virtual flags.

### GenericParameter / TypeConstraint

```csharp
var param = new GenericParameter("T");
param.Constraints.Add(TypeConstraint.Class());                    // class constraint
param.Constraints.Add(TypeConstraint.BaseType(TypeReference.FromName("IDisposable")));
cls.GenericParameters.Add(param);
```

---

## TypeReference

`TypeReference` is immutable. Use the built-in static members or the factory helpers:

```csharp
// Primitives
TypeReference.Void, .Bool, .Char
TypeReference.Int8,  .Int16,  .Int32,  .Int64
TypeReference.UInt8, .UInt16, .UInt32, .UInt64
TypeReference.Float32, .Float64
TypeReference.String

// Standard collections
TypeReference.List(TypeReference.Int32)          // List<int32>
TypeReference.Dict(TypeReference.String, TypeReference.Int32)
TypeReference.Set(TypeReference.Int32)
TypeReference.Optional(TypeReference.String)

// Custom types
TypeReference.FromName("MyNamespace.MyClass")

// Modifiers
var arrRef = TypeReference.Int32.MakeArrayType();           // int32[]
var genRef = TypeReference.FromName("Box").MakeGenericType(TypeReference.String);  // Box<string>
```

---

## Instructions

Every instruction inherits from `Instruction` and lives in a `MethodDefinition.Instructions` (`InstructionList`).

### Emitting via InstructionList

```csharp
var instructions = method.Instructions;

instructions.EmitLoadArg(0);
instructions.EmitLoadField(fieldRef);
instructions.EmitAdd();
instructions.EmitReturn();
```

### OpCode categories

| Category | OpCodes |
|----------|---------|
| Load | `Ldarg`, `Ldloc`, `Ldfld`, `Ldsfld`, `LdcI4`, `LdcR4`, `Ldstr`, `Ldnull`, `Ldelem`, `Ldlen` |
| Store | `Starg`, `Stloc`, `Stfld`, `Stsfld`, `Stelem` |
| Arithmetic | `Add`, `Sub`, `Mul`, `Div`, `Rem`, `Neg`, `And`, `Or`, `Xor`, `Not`, `Shl`, `Shr` |
| Comparison | `Ceq`, `Cgt`, `Clt` |
| Calls | `Call`, `Callvirt`, `Calli`, `Newobj` |
| Objects | `Newarr`, `Castclass`, `Isinst`, `Box`, `Unbox` |
| Stack | `Dup`, `Pop` |
| Conversions | `ConvI4`, `ConvI8`, `ConvR4`, `ConvR8`, `ConvU4`, `ConvU8` |
| Control flow | `Ret`, `Br`, `Brtrue`, `Brfalse`, `Beq`, `Bne`, `Bgt`, `Blt` |
| Structured | `If`, `While`, `For`, `Switch`, `Try`, `Break`, `Continue`, `Throw` |

### Structured control flow

High-level instructions hold nested `InstructionList` blocks:

```csharp
var ifInst = new IfInstruction(condition);
ifInst.ThenBlock.EmitLoadArg(0);
ifInst.ThenBlock.EmitReturn();

ifInst.ElseBlock = new InstructionList();
ifInst.ElseBlock.EmitLoadNull();
ifInst.ElseBlock.EmitReturn();

instructions.Add(ifInst);
```

### Visitor pattern

Implement `IInstructionVisitor` to walk an instruction stream:

```csharp
public class MyPass : IInstructionVisitor
{
    public void Visit(LoadArgInstruction i) { /* ... */ }
    public void Visit(CallInstruction i)    { /* ... */ }
    // ... implement all methods
}

var pass = new MyPass();
foreach (var inst in method.Instructions)
    inst.Accept(pass);
```
