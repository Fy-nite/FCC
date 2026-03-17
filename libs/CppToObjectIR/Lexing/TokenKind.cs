namespace CppToObjectIR.Lexing;

public enum TokenKind
{
    // Literals
    IntLiteral,
    FloatLiteral,
    StringLiteral,
    CharLiteral,
    BoolLiteral,     // true / false
    NullptrLiteral,  // nullptr

    // Identifiers and keywords
    Identifier,

    // Type keywords
    KwVoid,
    KwInt,
    KwLong,
    KwShort,
    KwChar,
    KwBool,
    KwFloat,
    KwDouble,
    KwUnsigned,
    KwSigned,
    KwAuto,

    // Class / struct / enum
    KwClass,
    KwStruct,
    KwEnum,
    KwNamespace,
    KwPublic,
    KwPrivate,
    KwProtected,

    // Modifiers
    KwStatic,
    KwConst,
    KwVirtual,
    KwOverride,
    KwExplicit,
    KwInline,

    // Control flow
    KwIf,
    KwElse,
    KwWhile,
    KwFor,
    KwDo,
    KwBreak,
    KwContinue,
    KwReturn,
    KwSwitch,
    KwCase,
    KwDefault,

    // Exception handling
    KwTry,
    KwCatch,
    KwThrow,

    // OOP
    KwNew,
    KwDelete,
    KwThis,

    // Operators
    Plus,           // +
    Minus,          // -
    Star,           // *
    Slash,          // /
    Percent,        // %
    Ampersand,      // &
    Pipe,           // |
    Caret,          // ^
    Tilde,          // ~
    Bang,           // !
    LessLess,       // <<
    GreaterGreater, // >>

    // Assignment
    Assign,         // =
    PlusAssign,     // +=
    MinusAssign,    // -=
    StarAssign,     // *=
    SlashAssign,    // /=
    PercentAssign,  // %=
    AmpAssign,      // &=
    PipeAssign,     // |=
    CaretAssign,    // ^=
    LessLessAssign, // <<=
    GreaterGreaterAssign, // >>=
    PlusPlus,       // ++
    MinusMinus,     // --

    // Comparison
    EqualEqual,     // ==
    BangEqual,      // !=
    Less,           // <
    LessEqual,      // <=
    Greater,        // >
    GreaterEqual,   // >=

    // Logical
    AmpAmp,         // &&
    PipePipe,       // ||

    // Member access
    Dot,            // .
    Arrow,          // ->
    DoubleColon,    // ::

    // Punctuation
    Semicolon,      // ;
    Colon,          // :
    Comma,          // ,
    Question,       // ?
    LParen,         // (
    RParen,         // )
    LBrace,         // {
    RBrace,         // }
    LBracket,       // [
    RBracket,       // ]
    LAngle,         // < (also Less, resolved by context)
    RAngle,         // > (also Greater, resolved by context)

    // Preprocessor (minimal, skipped by lexer)
    HashInclude,
    HashDefine,
    HashIfdef,
    HashEndif,
    HashPragma,

    // Special
    Eof,
    Unknown
}
