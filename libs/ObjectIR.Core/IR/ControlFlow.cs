namespace ObjectIR.Core.IR;

// ============================================================================
// Structured Control Flow Instructions (High-Level)
// ============================================================================

/// <summary>
/// Represents an if-else statement with condition and blocks
/// </summary>
public sealed class IfInstruction : Instruction
{
    public Condition Condition { get; set; }
    public InstructionList ThenBlock { get; } = new();
    public InstructionList? ElseBlock { get; set; }

    public IfInstruction(Condition condition) : base(OpCode.If)
    {
        Condition = condition;
    }

    public override void Accept(IInstructionVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Represents a while loop with condition and body
/// </summary>
public sealed class WhileInstruction : Instruction
{
    public Condition Condition { get; set; }
    public InstructionList Body { get; } = new();

    public WhileInstruction(Condition condition) : base(OpCode.While)
    {
        Condition = condition;
    }

    public override void Accept(IInstructionVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Represents a try-catch-finally block
/// </summary>
public sealed class TryInstruction : Instruction
{
    public InstructionList TryBlock { get; } = new();
    public List<CatchClause> CatchClauses { get; } = new();
    public InstructionList? FinallyBlock { get; set; }

    public TryInstruction() : base(OpCode.Try) { }

    public override void Accept(IInstructionVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Represents a catch clause in a try-catch block
/// </summary>
public sealed class CatchClause
{
    public TypeReference ExceptionType { get; set; }
    public string VariableName { get; set; }
    public InstructionList Body { get; } = new();

    public CatchClause(TypeReference exceptionType, string variableName)
    {
        ExceptionType = exceptionType;
        VariableName = variableName;
    }
}

/// <summary>
/// Represents a throw instruction
/// </summary>
public sealed class ThrowInstruction : Instruction
{
    public ThrowInstruction() : base(OpCode.Throw) { }

    public override void Accept(IInstructionVisitor visitor) => visitor.Visit(this);
}

// ============================================================================
// Condition Types
// ============================================================================

/// <summary>
/// Base class for conditions in control flow
/// </summary>
public abstract class Condition
{
    public static Condition Stack() => new StackCondition();
    public static Condition Binary(ComparisonOp op) => new BinaryCondition(op);
    public static Condition Expression(Instruction expr) => new ExpressionCondition(expr);
    public static Condition Or(Condition left, Condition right) => new LogicalCondition(LogicalOp.Or, left, right);
    public static Condition And(Condition left, Condition right) => new LogicalCondition(LogicalOp.And, left, right);
    public static Condition Not(Condition inner) => new LogicalCondition(LogicalOp.Not, inner, null);
}

/// <summary>
/// Condition that uses the top of the stack
/// </summary>
public sealed class StackCondition : Condition
{
    public override string ToString() => "stack";
}

/// <summary>
/// Condition that compares two stack values
/// </summary>
public sealed class BinaryCondition : Condition
{
    public ComparisonOp Operation { get; set; }

    public BinaryCondition(ComparisonOp operation)
    {
        Operation = operation;
    }

    public override string ToString() => Operation.ToString().ToLower();
}

/// <summary>
/// Condition based on an expression
/// </summary>
public sealed class ExpressionCondition : Condition
{
    public Instruction Expression { get; set; }

    public ExpressionCondition(Instruction expression)
    {
        Expression = expression;
    }

    public override string ToString() => "expression";
}

/// <summary>
/// Logical combination of conditions (and/or/not)
/// </summary>
public enum LogicalOp { And, Or, Not }

public sealed class LogicalCondition : Condition
{
    public LogicalOp Operation { get; set; }
    public Condition Left { get; set; }
    public Condition? Right { get; set; }

    public LogicalCondition(LogicalOp op, Condition left, Condition? right)
    {
        Operation = op;
        Left = left;
        Right = right;
    }

    public override string ToString()
    {
        return Operation switch
        {
            LogicalOp.And => $"({Left} and {Right})",
            LogicalOp.Or => $"({Left} or {Right})",
            LogicalOp.Not => $"(not {Left})",
            _ => base.ToString()
        };
    }
}

/// <summary>
/// Represents a foreach loop
/// </summary>
public sealed class ForEachInstruction : Instruction
{
    public string ItemName { get; set; }
    public string CollectionName { get; set; }
    public InstructionList Body { get; } = new();

    public ForEachInstruction(string itemName, string collectionName) : base(OpCode.For)
    {
        ItemName = itemName;
        CollectionName = collectionName;
    }

    public override void Accept(IInstructionVisitor visitor) => visitor.Visit(this);
}
