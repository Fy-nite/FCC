using System;
using System.Collections.Generic;

namespace ObjectIR
{
    /// <summary>
    /// Enum of supported instruction types in the IR
    /// </summary>
    public enum OpCode
    {
        // Stack operations
        Nop = 0,
        Dup = 1,
        Pop = 2,

        // Load operations
        LdArg = 3,
        LdLoc = 4,
        LdFld = 5,
        LdCon = 6,
        LdStr = 7,
        LdI4 = 8,
        LdI8 = 9,
        LdR4 = 10,
        LdR8 = 11,
        LdTrue = 12,
        LdFalse = 13,
        LdNull = 14,

        // Store operations
        StLoc = 15,
        StFld = 16,
        StArg = 17,

        // Arithmetic operations
        Add = 18,
        Sub = 19,
        Mul = 20,
        Div = 21,
        Rem = 22,
        Neg = 23,

        // Comparison operations
        Ceq = 24,
        Cne = 25,
        Clt = 26,
        Cle = 27,
        Cgt = 28,
        Cge = 29,

        // Control flow
        Ret = 30,
        Br = 31,
        BrTrue = 32,
        BrFalse = 33,
        If = 34,

        // Object operations
        NewObj = 35,
        Call = 36,
        CallVirt = 37,
        CastClass = 38,
        IsInst = 39,

        // Array operations
        NewArr = 40,
        LdElem = 41,
        StElem = 42,
        LdLen = 43,

        // Misc
        Break = 44,
        Continue = 45,
        Throw = 46,
        While = 47,
    }

    /// <summary>
    /// Represents the kind of structured condition used by high-level control flow
    /// </summary>
    public enum ConditionKind
    {
        None,
        Stack,
        Binary,
        Expression,
    }

    /// <summary>
    /// Data describing a method call target in the IR
    /// </summary>
    public class CallTarget
    {
        public string DeclaringType { get; set; }
        public string Name { get; set; }
        public List<string> ParameterTypes { get; set; }
        public string ReturnType { get; set; }

        public CallTarget()
        {
            ParameterTypes = new List<string>();
        }
    }

    /// <summary>
    /// Data describing a field reference in an instruction
    /// </summary>
    public class FieldTarget
    {
        public string DeclaringType { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
    }

    /// <summary>
    /// Represents a parsed instruction and its operands
    /// </summary>
    public class Instruction
    {
        private OpCode opCode = OpCode.Nop;
        private string operandString = string.Empty;
        private int operandInt = 0;
        private double operandDouble = 0.0;
        private bool hasConstant = false;
        private string constantType = string.Empty;
        private string constantRawValue = string.Empty;
        private bool constantBool = false;
        private bool constantIsNull = false;
        private string identifier = string.Empty;
        private CallTarget callTarget;
        private FieldTarget fieldTarget;
        private ConditionData condition;
        private WhileData whileData;
        private IfData ifData;

        public OpCode OpCode
        {
            get { return opCode; }
            set { opCode = value; }
        }

        public string OperandString
        {
            get { return operandString; }
            set { operandString = value ?? string.Empty; }
        }

        public int OperandInt
        {
            get { return operandInt; }
            set { operandInt = value; }
        }

        public double OperandDouble
        {
            get { return operandDouble; }
            set { operandDouble = value; }
        }

        public bool HasConstant
        {
            get { return hasConstant; }
            set { hasConstant = value; }
        }

        public string ConstantType
        {
            get { return constantType; }
            set { constantType = value ?? string.Empty; }
        }

        public string ConstantRawValue
        {
            get { return constantRawValue; }
            set { constantRawValue = value ?? string.Empty; }
        }

        public bool ConstantBool
        {
            get { return constantBool; }
            set { constantBool = value; }
        }

        public bool ConstantIsNull
        {
            get { return constantIsNull; }
            set { constantIsNull = value; }
        }

        public string Identifier
        {
            get { return identifier; }
            set { identifier = value ?? string.Empty; }
        }

        public CallTarget CallTarget
        {
            get { return callTarget; }
            set { callTarget = value; }
        }

        public FieldTarget FieldTarget
        {
            get { return fieldTarget; }
            set { fieldTarget = value; }
        }

        public ConditionData Condition
        {
            get { return condition ?? (condition = new ConditionData()); }
            set { condition = value; }
        }

        public WhileData While
        {
            get { return whileData; }
            set { whileData = value; }
        }

        public IfData If
        {
            get { return ifData; }
            set { ifData = value; }
        }

        public Instruction()
        {
        }

        public Instruction(Instruction other)
        {
            if (other == null)
                return;

            OpCode = other.OpCode;
            OperandString = other.OperandString;
            OperandInt = other.OperandInt;
            OperandDouble = other.OperandDouble;
            HasConstant = other.HasConstant;
            ConstantType = other.ConstantType;
            ConstantRawValue = other.ConstantRawValue;
            ConstantBool = other.ConstantBool;
            ConstantIsNull = other.ConstantIsNull;
            Identifier = other.Identifier;
            CallTarget = other.CallTarget;
            FieldTarget = other.FieldTarget;
            Condition = other.Condition != null ? new ConditionData(other.Condition) : new ConditionData();
            While = other.While != null ? new WhileData(other.While) : null;
            If = other.If != null ? new IfData(other.If) : null;
        }

        /// <summary>
        /// Condition data for structured control flow
        /// </summary>
        public class ConditionData
        {
            private ConditionKind kind = ConditionKind.None;
            private OpCode comparisonOp = OpCode.Nop;
            private List<Instruction> setupInstructions;
            private List<Instruction> expressionInstructions;

            public ConditionKind Kind
            {
                get { return kind; }
                set { kind = value; }
            }

            public OpCode ComparisonOp
            {
                get { return comparisonOp; }
                set { comparisonOp = value; }
            }

            public List<Instruction> SetupInstructions
            {
                get { return setupInstructions ?? (setupInstructions = new List<Instruction>()); }
                set { setupInstructions = value; }
            }

            public List<Instruction> ExpressionInstructions
            {
                get { return expressionInstructions ?? (expressionInstructions = new List<Instruction>()); }
                set { expressionInstructions = value; }
            }

            public ConditionData()
            {
            }

            public ConditionData(ConditionData other)
            {
                if (other == null)
                    return;

                Kind = other.Kind;
                ComparisonOp = other.ComparisonOp;
                SetupInstructions = new List<Instruction>(other.SetupInstructions);
                ExpressionInstructions = new List<Instruction>(other.ExpressionInstructions);
            }
        }

        /// <summary>
        /// While loop data containing condition and body
        /// </summary>
        public class WhileData
        {
            private ConditionData condition;
            private List<Instruction> body;

            public ConditionData Condition
            {
                get { return condition ?? (condition = new ConditionData()); }
                set { condition = value; }
            }

            public List<Instruction> Body
            {
                get { return body ?? (body = new List<Instruction>()); }
                set { body = value; }
            }

            public WhileData()
            {
            }

            public WhileData(WhileData other)
            {
                if (other == null)
                    return;

                Condition = other.Condition != null ? new ConditionData(other.Condition) : new ConditionData();
                Body = new List<Instruction>(other.Body);
            }
        }

        /// <summary>
        /// If statement data containing then and else blocks
        /// </summary>
        public class IfData
        {
            private List<Instruction> thenBlock;
            private List<Instruction> elseBlock;

            public List<Instruction> ThenBlock
            {
                get { return thenBlock ?? (thenBlock = new List<Instruction>()); }
                set { thenBlock = value; }
            }

            public List<Instruction> ElseBlock
            {
                get { return elseBlock ?? (elseBlock = new List<Instruction>()); }
                set { elseBlock = value; }
            }

            public IfData()
            {
            }

            public IfData(IfData other)
            {
                if (other == null)
                    return;

                ThenBlock = new List<Instruction>(other.ThenBlock);
                ElseBlock = new List<Instruction>(other.ElseBlock);
            }
        }
    }
}
