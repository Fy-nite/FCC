using CppToObjectIR.Ast;
using CppToObjectIR.Lexing;
using CppToObjectIR.Parsing;
using Xunit;

namespace CppToObjectIR.Tests;

public class ParserTests
{
    private static TranslationUnitNode Parse(string src)
    {
        var tokens = new Lexer(src).Tokenize();
        return new Parser(tokens).Parse();
    }

    // -------------------------------------------------------------------------
    // Free functions
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_EmptyVoidFunction_Produces_FunctionDeclNode()
    {
        var unit = Parse("void foo() {}");
        Assert.Single(unit.Declarations);
        var fn = Assert.IsType<FunctionDeclNode>(unit.Declarations[0]);
        Assert.Equal("foo", fn.Name);
        Assert.Equal("void", fn.ReturnType.BaseName);
        Assert.Empty(fn.Parameters);
        Assert.NotNull(fn.Body);
        Assert.Empty(fn.Body!.Statements);
    }

    [Fact]
    public void Parse_FunctionWithParameters_Produces_CorrectParameters()
    {
        var unit = Parse("int add(int a, int b) { return a + b; }");
        var fn = Assert.IsType<FunctionDeclNode>(unit.Declarations[0]);
        Assert.Equal(2, fn.Parameters.Count);
        Assert.Equal("a", fn.Parameters[0].Name);
        Assert.Equal("int", fn.Parameters[0].Type.BaseName);
        Assert.Equal("b", fn.Parameters[1].Name);
    }

    [Fact]
    public void Parse_FunctionReturnStatement_HasCorrectValue()
    {
        var unit = Parse("int getVal() { return 42; }");
        var fn = Assert.IsType<FunctionDeclNode>(unit.Declarations[0]);
        var ret = Assert.IsType<ReturnStatementNode>(fn.Body!.Statements[0]);
        var lit = Assert.IsType<IntLiteralNode>(ret.Value);
        Assert.Equal(42, lit.Value);
    }

    [Fact]
    public void Parse_FunctionDeclarationOnly_HasNullBody()
    {
        var unit = Parse("int foo(int x);");
        var fn = Assert.IsType<FunctionDeclNode>(unit.Declarations[0]);
        Assert.Null(fn.Body);
    }

    // -------------------------------------------------------------------------
    // Class declarations
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_EmptyClass_Produces_ClassDeclNode()
    {
        var unit = Parse("class MyClass {};");
        var cls = Assert.IsType<ClassDeclNode>(unit.Declarations[0]);
        Assert.Equal("MyClass", cls.Name);
    }

    [Fact]
    public void Parse_ClassWithBaseClass_SetsBaseClass()
    {
        var unit = Parse("class Dog : public Animal {};");
        var cls = Assert.IsType<ClassDeclNode>(unit.Declarations[0]);
        Assert.Equal("Animal", cls.BaseClass);
    }

    [Fact]
    public void Parse_ClassWithPublicField_ProducesField()
    {
        var unit = Parse("class Foo { public: int x; };");
        var cls = Assert.IsType<ClassDeclNode>(unit.Declarations[0]);
        var section = cls.Sections.First(s => s.Access == AccessSpecifier.Public);
        var field = Assert.IsType<FieldDeclNode>(section.Members[0]);
        Assert.Equal("x", field.Name);
        Assert.Equal("int", field.Type.BaseName);
    }

    [Fact]
    public void Parse_ClassWithMethod_ProducesMethodDecl()
    {
        var unit = Parse("class Foo { public: void greet() {} };");
        var cls = Assert.IsType<ClassDeclNode>(unit.Declarations[0]);
        var section = cls.Sections.First(s => s.Access == AccessSpecifier.Public);
        var method = Assert.IsType<MethodDeclNode>(section.Members[0]);
        Assert.Equal("greet", method.Name);
    }

    [Fact]
    public void Parse_ClassConstructor_IsMarkedAsConstructor()
    {
        var unit = Parse("class Foo { public: Foo() {} };");
        var cls = Assert.IsType<ClassDeclNode>(unit.Declarations[0]);
        var section = cls.Sections.First(s => s.Access == AccessSpecifier.Public);
        var ctor = Assert.IsType<MethodDeclNode>(section.Members[0]);
        Assert.True(ctor.IsConstructor);
    }

    [Fact]
    public void Parse_VirtualMethod_IsMarkedVirtual()
    {
        var unit = Parse("class Base { public: virtual void speak() {} };");
        var cls = Assert.IsType<ClassDeclNode>(unit.Declarations[0]);
        var section = cls.Sections.First(s => s.Access == AccessSpecifier.Public);
        var method = Assert.IsType<MethodDeclNode>(section.Members[0]);
        Assert.True(method.IsVirtual);
    }

    [Fact]
    public void Parse_StaticMethod_IsMarkedStatic()
    {
        var unit = Parse("class Util { public: static int compute(int x); };");
        var cls = Assert.IsType<ClassDeclNode>(unit.Declarations[0]);
        var section = cls.Sections.First(s => s.Access == AccessSpecifier.Public);
        var method = Assert.IsType<MethodDeclNode>(section.Members[0]);
        Assert.True(method.IsStatic);
    }

    // -------------------------------------------------------------------------
    // Struct
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_Struct_Produces_StructDeclNode()
    {
        var unit = Parse("struct Point { int x; int y; };");
        var str = Assert.IsType<StructDeclNode>(unit.Declarations[0]);
        Assert.Equal("Point", str.Name);
        Assert.Single(str.Sections);
        Assert.Equal(2, str.Sections[0].Members.Count);
    }

    // -------------------------------------------------------------------------
    // Enum
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_Enum_Produces_EnumDeclNode()
    {
        var unit = Parse("enum Color { Red, Green, Blue };");
        var en = Assert.IsType<EnumDeclNode>(unit.Declarations[0]);
        Assert.Equal("Color", en.Name);
        Assert.Equal(3, en.Members.Count);
        Assert.Equal("Red",   en.Members[0].Name);
        Assert.Equal("Green", en.Members[1].Name);
        Assert.Equal("Blue",  en.Members[2].Name);
    }

    [Fact]
    public void Parse_EnumWithExplicitValues_RecordsValues()
    {
        var unit = Parse("enum Status { Ok = 0, Fail = 1 };");
        var en = Assert.IsType<EnumDeclNode>(unit.Declarations[0]);
        var okVal  = Assert.IsType<IntLiteralNode>(en.Members[0].Value);
        var failVal = Assert.IsType<IntLiteralNode>(en.Members[1].Value);
        Assert.Equal(0, okVal.Value);
        Assert.Equal(1, failVal.Value);
    }

    // -------------------------------------------------------------------------
    // Namespace
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_Namespace_Produces_NamespaceNode()
    {
        var unit = Parse("namespace MyNs { void foo() {} }");
        var ns = Assert.IsType<NamespaceNode>(unit.Declarations[0]);
        Assert.Equal("MyNs", ns.Name);
        Assert.Single(ns.Members);
    }

    // -------------------------------------------------------------------------
    // Statements
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_IfStatement_ProducesCorrectAST()
    {
        var unit = Parse("void f() { if (x > 0) { return; } }");
        var fn = Assert.IsType<FunctionDeclNode>(unit.Declarations[0]);
        var ifStmt = Assert.IsType<IfStatementNode>(fn.Body!.Statements[0]);
        Assert.NotNull(ifStmt.Condition);
        Assert.NotNull(ifStmt.Then);
        Assert.Null(ifStmt.Else);
    }

    [Fact]
    public void Parse_IfElseStatement_HasElseBranch()
    {
        var unit = Parse("void f() { if (x) { return; } else { return; } }");
        var fn = Assert.IsType<FunctionDeclNode>(unit.Declarations[0]);
        var ifStmt = Assert.IsType<IfStatementNode>(fn.Body!.Statements[0]);
        Assert.NotNull(ifStmt.Else);
    }

    [Fact]
    public void Parse_WhileLoop_ProducesWhileStatementNode()
    {
        var unit = Parse("void f() { while (n > 0) { n--; } }");
        var fn = Assert.IsType<FunctionDeclNode>(unit.Declarations[0]);
        Assert.IsType<WhileStatementNode>(fn.Body!.Statements[0]);
    }

    [Fact]
    public void Parse_ForLoop_ProducesForStatementNode()
    {
        var unit = Parse("void f() { for (int i = 0; i < 10; i++) {} }");
        var fn = Assert.IsType<FunctionDeclNode>(unit.Declarations[0]);
        var forStmt = Assert.IsType<ForStatementNode>(fn.Body!.Statements[0]);
        Assert.NotNull(forStmt.Init);
        Assert.NotNull(forStmt.Condition);
        Assert.NotNull(forStmt.Update);
    }

    [Fact]
    public void Parse_VarDecl_ProducesVarDeclStatement()
    {
        var unit = Parse("void f() { int x = 5; }");
        var fn = Assert.IsType<FunctionDeclNode>(unit.Declarations[0]);
        var varDecl = Assert.IsType<VarDeclStatementNode>(fn.Body!.Statements[0]);
        Assert.Equal("x", varDecl.Name);
        Assert.Equal("int", varDecl.Type.BaseName);
        var init = Assert.IsType<IntLiteralNode>(varDecl.Initializer);
        Assert.Equal(5, init.Value);
    }

    [Fact]
    public void Parse_BreakContinue_ProduceCorrectNodes()
    {
        var unit = Parse("void f() { while (1) { break; continue; } }");
        var fn = Assert.IsType<FunctionDeclNode>(unit.Declarations[0]);
        var loop = Assert.IsType<WhileStatementNode>(fn.Body!.Statements[0]);
        var block = Assert.IsType<BlockStatementNode>(loop.Body);
        Assert.IsType<BreakStatementNode>(block.Statements[0]);
        Assert.IsType<ContinueStatementNode>(block.Statements[1]);
    }

    // -------------------------------------------------------------------------
    // Expressions
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_BinaryArithmetic_ProducesBinaryExpression()
    {
        var unit = Parse("int f() { return a + b * c; }");
        var fn = Assert.IsType<FunctionDeclNode>(unit.Declarations[0]);
        var ret = Assert.IsType<ReturnStatementNode>(fn.Body!.Statements[0]);
        var add = Assert.IsType<BinaryExpressionNode>(ret.Value);
        Assert.Equal(BinaryOp.Add, add.Operator);
        var mul = Assert.IsType<BinaryExpressionNode>(add.Right);
        Assert.Equal(BinaryOp.Mul, mul.Operator);
    }

    [Fact]
    public void Parse_Assignment_ProducesAssignmentNode()
    {
        var unit = Parse("void f() { x = 10; }");
        var fn = Assert.IsType<FunctionDeclNode>(unit.Declarations[0]);
        var stmt = Assert.IsType<ExpressionStatementNode>(fn.Body!.Statements[0]);
        var assign = Assert.IsType<AssignmentExpressionNode>(stmt.Expression);
        Assert.Equal(AssignOp.Assign, assign.Operator);
    }

    [Fact]
    public void Parse_FunctionCall_ProducesCallNode()
    {
        var unit = Parse("void f() { foo(1, 2); }");
        var fn = Assert.IsType<FunctionDeclNode>(unit.Declarations[0]);
        var stmt = Assert.IsType<ExpressionStatementNode>(fn.Body!.Statements[0]);
        var call = Assert.IsType<CallExpressionNode>(stmt.Expression);
        var callee = Assert.IsType<IdentifierNode>(call.Callee);
        Assert.Equal("foo", callee.Name);
        Assert.Equal(2, call.Arguments.Count);
    }

    [Fact]
    public void Parse_MemberAccess_ProducesMemberAccessNode()
    {
        var unit = Parse("void f() { obj.value; }");
        var fn = Assert.IsType<FunctionDeclNode>(unit.Declarations[0]);
        var stmt = Assert.IsType<ExpressionStatementNode>(fn.Body!.Statements[0]);
        var mem = Assert.IsType<MemberAccessNode>(stmt.Expression);
        Assert.Equal("value", mem.Member);
        Assert.False(mem.IsArrow);
    }

    [Fact]
    public void Parse_ArrowAccess_IsArrow()
    {
        var unit = Parse("void f() { ptr->value; }");
        var fn = Assert.IsType<FunctionDeclNode>(unit.Declarations[0]);
        var stmt = Assert.IsType<ExpressionStatementNode>(fn.Body!.Statements[0]);
        var mem = Assert.IsType<MemberAccessNode>(stmt.Expression);
        Assert.True(mem.IsArrow);
    }

    [Fact]
    public void Parse_NewExpression_ProducesNewNode()
    {
        var unit = Parse("void f() { new MyClass(1); }");
        var fn = Assert.IsType<FunctionDeclNode>(unit.Declarations[0]);
        var stmt = Assert.IsType<ExpressionStatementNode>(fn.Body!.Statements[0]);
        var newExpr = Assert.IsType<NewExpressionNode>(stmt.Expression);
        Assert.Equal("MyClass", newExpr.Type.BaseName);
        Assert.Single(newExpr.Arguments);
    }

    [Fact]
    public void Parse_TryCatch_ProducesTryStatement()
    {
        const string src = @"
void f() {
    try { int x = 1; }
    catch (std::exception e) { return; }
}";
        var unit = Parse(src);
        var fn = Assert.IsType<FunctionDeclNode>(unit.Declarations[0]);
        var tryStmt = Assert.IsType<TryStatementNode>(fn.Body!.Statements[0]);
        Assert.Single(tryStmt.CatchClauses);
        Assert.Equal("e", tryStmt.CatchClauses[0].VariableName);
    }
}
