namespace CppToObjectIR.Ast;

// ============================================================================
// Base
// ============================================================================

public abstract class AstNode
{
    public int Line { get; set; }
    public int Column { get; set; }
}

// ============================================================================
// Top-level
// ============================================================================

/// <summary>Represents a whole translation unit (file)</summary>
public sealed class TranslationUnitNode : AstNode
{
    public List<AstNode> Declarations { get; } = new();
}

/// <summary>namespace Foo { ... }</summary>
public sealed class NamespaceNode : AstNode
{
    public string Name { get; set; } = string.Empty;
    public List<AstNode> Members { get; } = new();
}

// ============================================================================
// Type declarations
// ============================================================================

public abstract class TypeDeclNode : AstNode
{
    public string Name { get; set; } = string.Empty;
    public string? Namespace { get; set; }
    public AccessSpecifier DefaultAccess { get; set; }
    public List<MemberSection> Sections { get; } = new();
}

public sealed class ClassDeclNode : TypeDeclNode
{
    public string? BaseClass { get; set; }
    public List<string> BaseInterfaces { get; } = new();
    public bool IsAbstract { get; set; }
}

public sealed class StructDeclNode : TypeDeclNode { }

public sealed class EnumDeclNode : AstNode
{
    public string Name { get; set; } = string.Empty;
    public string? Namespace { get; set; }
    public bool IsClass { get; set; }
    public string? UnderlyingType { get; set; }
    public List<EnumMemberNode> Members { get; } = new();
}

public sealed class EnumMemberNode : AstNode
{
    public string Name { get; set; } = string.Empty;
    public ExpressionNode? Value { get; set; }
}

/// <summary>Groups members within a class under an access specifier</summary>
public sealed class MemberSection
{
    public AccessSpecifier Access { get; set; }
    public List<AstNode> Members { get; } = new();
}

public enum AccessSpecifier { Public, Private, Protected }

// ============================================================================
// Members
// ============================================================================

public sealed class FieldDeclNode : AstNode
{
    public CppTypeNode Type { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public bool IsStatic { get; set; }
    public bool IsConst { get; set; }
    public ExpressionNode? Initializer { get; set; }
}

public sealed class MethodDeclNode : AstNode
{
    public CppTypeNode ReturnType { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public List<ParameterNode> Parameters { get; } = new();
    public bool IsStatic { get; set; }
    public bool IsVirtual { get; set; }
    public bool IsOverride { get; set; }
    public bool IsConst { get; set; }
    public bool IsPureVirtual { get; set; }
    public bool IsConstructor { get; set; }
    public bool IsDestructor { get; set; }
    public BlockStatementNode? Body { get; set; }
}

public sealed class ParameterNode : AstNode
{
    public CppTypeNode Type { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public ExpressionNode? DefaultValue { get; set; }
}

// ============================================================================
// Types
// ============================================================================

public sealed class CppTypeNode : AstNode
{
    public string BaseName { get; set; } = string.Empty;
    public bool IsConst { get; set; }
    public bool IsPointer { get; set; }
    public bool IsReference { get; set; }
    public bool IsUnsigned { get; set; }
    public List<CppTypeNode> TemplateArgs { get; } = new();

    public override string ToString()
    {
        var s = IsConst ? "const " : "";
        s += BaseName;
        if (TemplateArgs.Count > 0)
            s += $"<{string.Join(", ", TemplateArgs)}>";
        if (IsPointer) s += "*";
        if (IsReference) s += "&";
        return s;
    }
}

// ============================================================================
// Statements
// ============================================================================

public abstract class StatementNode : AstNode { }

public sealed class BlockStatementNode : StatementNode
{
    public List<StatementNode> Statements { get; } = new();
}

public sealed class ReturnStatementNode : StatementNode
{
    public ExpressionNode? Value { get; set; }
}

public sealed class ExpressionStatementNode : StatementNode
{
    public ExpressionNode Expression { get; set; } = null!;
}

public sealed class VarDeclStatementNode : StatementNode
{
    public CppTypeNode Type { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public ExpressionNode? Initializer { get; set; }
}

public sealed class IfStatementNode : StatementNode
{
    public ExpressionNode Condition { get; set; } = null!;
    public StatementNode Then { get; set; } = null!;
    public StatementNode? Else { get; set; }
}

public sealed class WhileStatementNode : StatementNode
{
    public ExpressionNode Condition { get; set; } = null!;
    public StatementNode Body { get; set; } = null!;
}

public sealed class ForStatementNode : StatementNode
{
    /// <summary>Either VarDeclStatementNode or ExpressionStatementNode</summary>
    public StatementNode? Init { get; set; }
    public ExpressionNode? Condition { get; set; }
    public ExpressionNode? Update { get; set; }
    public StatementNode Body { get; set; } = null!;
}

public sealed class DoWhileStatementNode : StatementNode
{
    public StatementNode Body { get; set; } = null!;
    public ExpressionNode Condition { get; set; } = null!;
}

public sealed class BreakStatementNode : StatementNode { }
public sealed class ContinueStatementNode : StatementNode { }

public sealed class TryStatementNode : StatementNode
{
    public BlockStatementNode TryBlock { get; set; } = null!;
    public List<CatchClauseNode> CatchClauses { get; } = new();
    public BlockStatementNode? FinallyBlock { get; set; }
}

public sealed class CatchClauseNode : AstNode
{
    public CppTypeNode ExceptionType { get; set; } = null!;
    public string VariableName { get; set; } = string.Empty;
    public BlockStatementNode Body { get; set; } = null!;
}

public sealed class ThrowStatementNode : StatementNode
{
    public ExpressionNode? Value { get; set; }
}

// ============================================================================
// Expressions
// ============================================================================

public abstract class ExpressionNode : AstNode { }

public sealed class IntLiteralNode : ExpressionNode
{
    public long Value { get; set; }
}

public sealed class FloatLiteralNode : ExpressionNode
{
    public double Value { get; set; }
}

public sealed class StringLiteralNode : ExpressionNode
{
    public string Value { get; set; } = string.Empty;
}

public sealed class CharLiteralNode : ExpressionNode
{
    public char Value { get; set; }
}

public sealed class BoolLiteralNode : ExpressionNode
{
    public bool Value { get; set; }
}

public sealed class NullptrLiteralNode : ExpressionNode { }

public sealed class IdentifierNode : ExpressionNode
{
    public string Name { get; set; } = string.Empty;
}

public sealed class BinaryExpressionNode : ExpressionNode
{
    public BinaryOp Operator { get; set; }
    public ExpressionNode Left { get; set; } = null!;
    public ExpressionNode Right { get; set; } = null!;
}

public sealed class UnaryExpressionNode : ExpressionNode
{
    public UnaryOp Operator { get; set; }
    public ExpressionNode Operand { get; set; } = null!;
    public bool IsPostfix { get; set; }
}

public sealed class AssignmentExpressionNode : ExpressionNode
{
    public AssignOp Operator { get; set; }
    public ExpressionNode Target { get; set; } = null!;
    public ExpressionNode Value { get; set; } = null!;
}

public sealed class CallExpressionNode : ExpressionNode
{
    public ExpressionNode Callee { get; set; } = null!;
    public List<ExpressionNode> Arguments { get; } = new();
}

public sealed class MemberAccessNode : ExpressionNode
{
    public ExpressionNode Object { get; set; } = null!;
    public string Member { get; set; } = string.Empty;
    public bool IsArrow { get; set; }
}

public sealed class IndexExpressionNode : ExpressionNode
{
    public ExpressionNode Array { get; set; } = null!;
    public ExpressionNode Index { get; set; } = null!;
}

public sealed class NewExpressionNode : ExpressionNode
{
    public CppTypeNode Type { get; set; } = null!;
    public List<ExpressionNode> Arguments { get; } = new();
    public ExpressionNode? ArraySize { get; set; }
}

public sealed class CastExpressionNode : ExpressionNode
{
    public CppTypeNode TargetType { get; set; } = null!;
    public ExpressionNode Operand { get; set; } = null!;
}

public sealed class TernaryExpressionNode : ExpressionNode
{
    public ExpressionNode Condition { get; set; } = null!;
    public ExpressionNode Then { get; set; } = null!;
    public ExpressionNode Else { get; set; } = null!;
}

public sealed class ThisExpressionNode : ExpressionNode { }

// ============================================================================
// Operators
// ============================================================================

public enum BinaryOp
{
    Add, Sub, Mul, Div, Rem,
    BitAnd, BitOr, BitXor, Shl, Shr,
    LogicalAnd, LogicalOr,
    Equal, NotEqual, Less, LessEqual, Greater, GreaterEqual
}

public enum UnaryOp
{
    Negate, BitwiseNot, LogicalNot,
    PreIncrement, PreDecrement,
    PostIncrement, PostDecrement
}

public enum AssignOp
{
    Assign,
    AddAssign, SubAssign, MulAssign, DivAssign, RemAssign,
    AndAssign, OrAssign, XorAssign, ShlAssign, ShrAssign
}

// ============================================================================
// Free functions (top-level, not in a class)
// ============================================================================

public sealed class FunctionDeclNode : AstNode
{
    public CppTypeNode ReturnType { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Namespace { get; set; }
    public List<ParameterNode> Parameters { get; } = new();
    public bool IsStatic { get; set; }
    public bool IsInline { get; set; }
    public BlockStatementNode? Body { get; set; }
}
