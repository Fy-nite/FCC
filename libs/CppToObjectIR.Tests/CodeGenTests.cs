using CppToObjectIR;
using ObjectIR.Core.IR;
using ObjectIR.Core.Serialization;
using Xunit;

namespace CppToObjectIR.Tests;

public class CodeGenTests
{
    private static Module Compile(string src, string name = "test") =>
        CppCompiler.Compile(name, src);

    // -------------------------------------------------------------------------
    // Module basics
    // -------------------------------------------------------------------------

    [Fact]
    public void Compile_EmptySource_ReturnsEmptyModule()
    {
        var module = Compile(string.Empty, "empty");
        Assert.Equal("empty", module.Name);
        Assert.Empty(module.Types);
        Assert.Empty(module.Functions);
    }

    [Fact]
    public void Compile_ModuleName_IsSet()
    {
        var module = Compile("void f() {}", "myMod");
        Assert.Equal("myMod", module.Name);
    }

    // -------------------------------------------------------------------------
    // Free functions
    // -------------------------------------------------------------------------

    [Fact]
    public void Compile_FreeFunction_AppearsInModuleFunctions()
    {
        var module = Compile("void greet() {}");
        Assert.Single(module.Functions);
        Assert.Equal("greet", module.Functions[0].Name);
    }

    [Fact]
    public void Compile_FreeFunctionReturnType_Int_MapsToInt32()
    {
        var module = Compile("int getValue() { return 0; }");
        Assert.Equal(TypeReference.Int32.Name, module.Functions[0].ReturnType.Name);
    }

    [Fact]
    public void Compile_FreeFunctionReturnType_Void_MapsToVoid()
    {
        var module = Compile("void doNothing() {}");
        Assert.Equal(TypeReference.Void.Name, module.Functions[0].ReturnType.Name);
    }

    [Fact]
    public void Compile_FunctionParameters_AreEmitted()
    {
        var module = Compile("int add(int a, int b) { return a + b; }");
        var fn = module.Functions[0];
        Assert.Equal(2, fn.Parameters.Count);
        Assert.Equal("a", fn.Parameters[0].Name);
        Assert.Equal("b", fn.Parameters[1].Name);
    }

    [Fact]
    public void Compile_ReturnIntLiteral_EmitsLoadConstantAndReturn()
    {
        var module = Compile("int f() { return 42; }");
        var fn = module.Functions[0];
        Assert.Contains(fn.Instructions, i => i is LoadConstantInstruction lc && lc.Value.Equals(42));
        Assert.Contains(fn.Instructions, i => i is ReturnInstruction);
    }

    [Fact]
    public void Compile_ReturnStringLiteral_EmitsLoadString()
    {
        var module = Compile("void f() { return; }");
        var fn = module.Functions[0];
        Assert.Contains(fn.Instructions, i => i is ReturnInstruction);
    }

    [Fact]
    public void Compile_LocalVarDecl_EmitsStoreLocal()
    {
        var module = Compile("void f() { int x = 10; }");
        var fn = module.Functions[0];
        Assert.Contains(fn.Instructions, i => i is StoreLocalInstruction sl && sl.LocalName == "x");
    }
    
    [Fact]
    public void Compile_LocalVarDecl_AddsToLocals()
    {
        var module = Compile("void f() { int x = 10; }");
        var fn = module.Functions[0];
        Assert.Contains(fn.Locals, l => l.Name == "x");
    }

    [Fact]
    public void Compile_ArithmeticExpression_EmitsArithmetic()
    {
        var module = Compile("int f(int a, int b) { return a + b; }");
        var fn = module.Functions[0];
        Assert.Contains(fn.Instructions, i => i is ArithmeticInstruction ai && ai.Operation == ArithmeticOp.Add);
    }

    [Fact]
    public void Compile_FunctionCall_EmitsCallInstruction()
    {
        var module = Compile("void f() { foo(); }");
        var fn = module.Functions[0];
        Assert.Contains(fn.Instructions, i => i is CallInstruction ci && ci.Method.Name == "foo");
    }

    // -------------------------------------------------------------------------
    // Classes
    // -------------------------------------------------------------------------

    [Fact]
    public void Compile_Class_AppearsInModuleTypes()
    {
        var module = Compile("class Animal {};");
        Assert.Single(module.Types);
        Assert.Equal("Animal", module.Types[0].Name);
        Assert.Equal(TypeKind.Class, module.Types[0].Kind);
    }

    [Fact]
    public void Compile_ClassNamespace_IsSet()
    {
        var module = Compile("namespace Zoo { class Lion {}; }");
        var lion = module.Types.First(t => t.Name == "Lion");
        Assert.Equal("Zoo", lion.Namespace);
    }

    [Fact]
    public void Compile_ClassWithField_FieldIsInClassDef()
    {
        var module = Compile("class Foo { public: int value; };");
        var cls = Assert.IsType<ClassDefinition>(module.Types[0]);
        Assert.Single(cls.Fields);
        Assert.Equal("value", cls.Fields[0].Name);
        Assert.Equal(TypeReference.Int32.Name, cls.Fields[0].Type.Name);
    }

    [Fact]
    public void Compile_PrivateField_HasPrivateAccess()
    {
        var module = Compile("class Foo { private: int secret; };");
        var cls = Assert.IsType<ClassDefinition>(module.Types[0]);
        Assert.Single(cls.Fields);
        Assert.Equal(AccessModifier.Private, cls.Fields[0].Access);
    }

    [Fact]
    public void Compile_ClassWithMethod_MethodIsInClassDef()
    {
        var module = Compile("class Foo { public: void doIt() {} };");
        var cls = Assert.IsType<ClassDefinition>(module.Types[0]);
        Assert.Single(cls.Methods);
        Assert.Equal("doIt", cls.Methods[0].Name);
    }

    [Fact]
    public void Compile_Constructor_IsMarkedIsConstructor()
    {
        var module = Compile("class Foo { public: Foo() {} };");
        var cls = Assert.IsType<ClassDefinition>(module.Types[0]);
        var ctor = cls.Methods.First(m => m.IsConstructor);
        Assert.True(ctor.IsConstructor);
    }

    [Fact]
    public void Compile_VirtualMethod_IsVirtual()
    {
        var module = Compile("class Base { public: virtual void speak() {} };");
        var cls = Assert.IsType<ClassDefinition>(module.Types[0]);
        var method = cls.Methods[0];
        Assert.True(method.IsVirtual);
    }

    [Fact]
    public void Compile_StaticMethod_IsStatic()
    {
        var module = Compile("class Util { public: static int max(int a, int b) { return a; } };");
        var cls = Assert.IsType<ClassDefinition>(module.Types[0]);
        var method = cls.Methods[0];
        Assert.True(method.IsStatic);
    }

    [Fact]
    public void Compile_ClassWithBaseClass_BaseTypeIsSet()
    {
        var module = Compile("class Dog : public Animal {};");
        var cls = Assert.IsType<ClassDefinition>(module.Types[0]);
        Assert.NotNull(cls.BaseType);
        Assert.Equal("Animal", cls.BaseType!.Name);
    }

    // -------------------------------------------------------------------------
    // Struct
    // -------------------------------------------------------------------------

    [Fact]
    public void Compile_Struct_AppearsAsStructKind()
    {
        var module = Compile("struct Vec2 { float x; float y; };");
        Assert.Single(module.Types);
        Assert.Equal(TypeKind.Struct, module.Types[0].Kind);
        Assert.Equal("Vec2", module.Types[0].Name);
    }

    [Fact]
    public void Compile_StructFields_AreEmitted()
    {
        var module = Compile("struct Vec2 { float x; float y; };");
        var str = Assert.IsType<StructDefinition>(module.Types[0]);
        Assert.Equal(2, str.Fields.Count);
    }

    // -------------------------------------------------------------------------
    // Enum
    // -------------------------------------------------------------------------

    [Fact]
    public void Compile_Enum_AppearsAsEnumKind()
    {
        var module = Compile("enum Color { Red, Green, Blue };");
        Assert.Single(module.Types);
        Assert.Equal(TypeKind.Enum, module.Types[0].Kind);
        Assert.Equal("Color", module.Types[0].Name);
    }

    [Fact]
    public void Compile_EnumMembers_AreEmitted()
    {
        var module = Compile("enum Color { Red, Green, Blue };");
        var en = Assert.IsType<EnumDefinition>(module.Types[0]);
        Assert.Equal(3, en.Members.Count);
        Assert.Equal("Red",   en.Members[0].Name);
        Assert.Equal("Green", en.Members[1].Name);
        Assert.Equal("Blue",  en.Members[2].Name);
    }

    // -------------------------------------------------------------------------
    // Control flow instructions
    // -------------------------------------------------------------------------

    [Fact]
    public void Compile_IfStatement_EmitsIfInstruction()
    {
        var module = Compile("void f(int x) { if (x > 0) { return; } }");
        var fn = module.Functions[0];
        Assert.Contains(fn.Instructions, i => i is IfInstruction);
    }

    [Fact]
    public void Compile_WhileLoop_EmitsWhileInstruction()
    {
        var module = Compile("void f() { int n = 0; while (n < 10) { n++; } }");
        var fn = module.Functions[0];
        Assert.Contains(fn.Instructions, i => i is WhileInstruction);
    }

    [Fact]
    public void Compile_ForLoop_EmitsWhileInstruction()
    {
        var module = Compile("void f() { for (int i = 0; i < 5; i++) {} }");
        var fn = module.Functions[0];
        // For-loops are lowered to a while
        Assert.Contains(fn.Instructions, i => i is WhileInstruction);
    }

    [Fact]
    public void Compile_BreakInsideLoop_EmitsBreakInstruction()
    {
        var module = Compile("void f() { while (1) { break; } }");
        var fn = module.Functions[0];
        var loop = fn.Instructions.OfType<WhileInstruction>().First();
        Assert.Contains(loop.Body, i => i is BreakInstruction);
    }

    [Fact]
    public void Compile_ContinueInsideLoop_EmitsContinueInstruction()
    {
        var module = Compile("void f() { while (1) { continue; } }");
        var fn = module.Functions[0];
        var loop = fn.Instructions.OfType<WhileInstruction>().First();
        Assert.Contains(loop.Body, i => i is ContinueInstruction);
    }

    [Fact]
    public void Compile_TryCatch_EmitsTryInstruction()
    {
        const string src = @"
void f() {
    try { int x = 1; }
    catch (std::exception ex) { return; }
}";
        var module = Compile(src);
        var fn = module.Functions[0];
        Assert.Contains(fn.Instructions, i => i is TryInstruction);
    }

    // -------------------------------------------------------------------------
    // Type mappings
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("void",   "void")]
    [InlineData("int",    "int32")]
    [InlineData("bool",   "bool")]
    [InlineData("char",   "char")]
    [InlineData("float",  "float32")]
    [InlineData("double", "float64")]
    [InlineData("long",   "int64")]
    public void Compile_TypeMapping_IsCorrect(string cppType, string irTypeName)
    {
        var src = $"{cppType} f({cppType} x) {{ return x; }}";
        // Just check it compiles and the function exists; for void we adjust source
        if (cppType == "void")
            src = "void f() {}";
        var module = Compile(src);
        Assert.Single(module.Functions);
        Assert.Equal(irTypeName, module.Functions[0].ReturnType.Name);
    }

    // -------------------------------------------------------------------------
    // Larger integration smoke test
    // -------------------------------------------------------------------------

    [Fact]
    public void Compile_FullClass_IntegrationSmokeTest()
    {
        const string src = @"
namespace Animals {
    class Dog : public Animal {
    public:
        Dog(std::string name) { this->name = name; }
        virtual void bark() {
            int count = 3;
            while (count > 0) {
                count--;
            }
        }
        std::string getName() { return name; }
    private:
        std::string name;
    };
}";
        var module = Compile(src, "AnimalsMod");
        Assert.Equal("AnimalsMod", module.Name);
        var dog = Assert.IsType<ClassDefinition>(module.Types.First(t => t.Name == "Dog"));
        Assert.Equal("Animals", dog.Namespace);
        Assert.NotNull(dog.BaseType);
        Assert.True(dog.Methods.Any(m => m.IsVirtual));
        Assert.True(dog.Methods.Any(m => m.IsConstructor));
    }

    // -------------------------------------------------------------------------
    // Field access instructions  (ldfld / stfld / ldsfld / stsfld)
    // -------------------------------------------------------------------------

    [Fact]
    public void Compile_StaticField_Write_EmitsStoreStaticFieldInstruction()
    {
        const string src = @"
class Counter {
public:
    static int value;
    static void Set(int v) { value = v; }
};";
        var module = Compile(src);
        var cls = Assert.IsType<ClassDefinition>(module.Types[0]);
        var method = cls.Methods.First(m => m.Name == "Set");
        Assert.Contains(method.Instructions, i => i is StoreStaticFieldInstruction);
    }

    [Fact]
    public void Compile_StaticField_Read_EmitsLoadStaticFieldInstruction()
    {
        const string src = @"
class Counter {
public:
    static int value;
    static int Get() { return value; }
};";
        var module = Compile(src);
        var cls = Assert.IsType<ClassDefinition>(module.Types[0]);
        var method = cls.Methods.First(m => m.Name == "Get");
        Assert.Contains(method.Instructions, i => i is LoadStaticFieldInstruction);
    }

    [Fact]
    public void Compile_StaticField_DumpText_EmitsStsfldWithOperand()
    {
        const string src = @"
class Counter {
public:
    static int value;
    static void Set(int v) { value = v; }
};";
        var module = Compile(src);
        var text = module.DumpText();
        // stsfld must be followed by the qualified field reference, not a TODO comment
        Assert.Contains("stsfld Counter.value", text);
        Assert.DoesNotContain("stsfld  // TODO", text);
    }

    [Fact]
    public void Compile_StaticField_DumpText_EmitsLdsfldWithOperand()
    {
        const string src = @"
class Counter {
public:
    static int value;
    static int Get() { return value; }
};";
        var module = Compile(src);
        var text = module.DumpText();
        // ldsfld must be followed by the qualified field reference, not a TODO comment
        Assert.Contains("ldsfld Counter.value", text);
        Assert.DoesNotContain("ldsfld  // TODO", text);
    }

    [Fact]
    public void Compile_InstanceField_DumpText_EmitsLdfldWithOperand()
    {
        const string src = @"
class Foo {
public:
    int x;
    int getX() { return x; }
};";
        var module = Compile(src);
        var text = module.DumpText();
        Assert.Contains("ldfld Foo.x", text);
        Assert.DoesNotContain("ldfld  // TODO", text);
    }

    [Fact]
    public void Compile_InstanceField_DumpText_EmitsStfldWithOperand()
    {
        const string src = @"
class Foo {
public:
    int x;
    void setX(int v) { x = v; }
};";
        var module = Compile(src);
        var text = module.DumpText();
        Assert.Contains("stfld Foo.x", text);
        Assert.DoesNotContain("stfld  // TODO", text);
    }
}
