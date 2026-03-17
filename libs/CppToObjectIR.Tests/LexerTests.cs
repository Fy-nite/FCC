using CppToObjectIR.Lexing;
using Xunit;

namespace CppToObjectIR.Tests;

public class LexerTests
{
    private static List<Token> Lex(string src) => new Lexer(src).Tokenize();

    [Fact]
    public void Lex_EmptySource_ReturnsOnlyEof()
    {
        var tokens = Lex(string.Empty);
        Assert.Single(tokens);
        Assert.Equal(TokenKind.Eof, tokens[0].Kind);
    }

    [Fact]
    public void Lex_LineComment_IsSkipped()
    {
        var tokens = Lex("// this is a comment\nint");
        Assert.Equal(2, tokens.Count); // KwInt + Eof
        Assert.Equal(TokenKind.KwInt, tokens[0].Kind);
    }

    [Fact]
    public void Lex_BlockComment_IsSkipped()
    {
        var tokens = Lex("/* block */ int");
        Assert.Equal(2, tokens.Count);
        Assert.Equal(TokenKind.KwInt, tokens[0].Kind);
    }

    [Fact]
    public void Lex_PreprocessorDirective_IsSkipped()
    {
        var tokens = Lex("#include <iostream>\nint");
        Assert.Equal(2, tokens.Count);
        Assert.Equal(TokenKind.KwInt, tokens[0].Kind);
    }

    [Fact]
    public void Lex_Keywords_AreRecognized()
    {
        var src = "class struct enum namespace public private protected " +
                  "if else while for return break continue " +
                  "static virtual override const void int bool float double char";
        var tokens = Lex(src);
        // Remove EOF
        tokens.RemoveAt(tokens.Count - 1);

        var expected = new[]
        {
            TokenKind.KwClass, TokenKind.KwStruct, TokenKind.KwEnum, TokenKind.KwNamespace,
            TokenKind.KwPublic, TokenKind.KwPrivate, TokenKind.KwProtected,
            TokenKind.KwIf, TokenKind.KwElse, TokenKind.KwWhile, TokenKind.KwFor,
            TokenKind.KwReturn, TokenKind.KwBreak, TokenKind.KwContinue,
            TokenKind.KwStatic, TokenKind.KwVirtual, TokenKind.KwOverride, TokenKind.KwConst,
            TokenKind.KwVoid, TokenKind.KwInt, TokenKind.KwBool, TokenKind.KwFloat,
            TokenKind.KwDouble, TokenKind.KwChar
        };

        Assert.Equal(expected.Length, tokens.Count);
        for (int i = 0; i < expected.Length; i++)
            Assert.Equal(expected[i], tokens[i].Kind);
    }

    [Fact]
    public void Lex_Identifier_IsRecognized()
    {
        var tokens = Lex("myVariable");
        Assert.Equal(TokenKind.Identifier, tokens[0].Kind);
        Assert.Equal("myVariable", tokens[0].Text);
    }

    [Theory]
    [InlineData("42",      TokenKind.IntLiteral)]
    [InlineData("0xFF",    TokenKind.IntLiteral)]
    [InlineData("3.14f",   TokenKind.FloatLiteral)]
    [InlineData("2.0",     TokenKind.FloatLiteral)]
    [InlineData("100L",    TokenKind.IntLiteral)]
    public void Lex_NumericLiterals_AreRecognized(string src, TokenKind expected)
    {
        var tokens = Lex(src);
        Assert.Equal(expected, tokens[0].Kind);
    }

    [Fact]
    public void Lex_StringLiteral_ExtractsText()
    {
        var tokens = Lex("\"hello world\"");
        Assert.Equal(TokenKind.StringLiteral, tokens[0].Kind);
        Assert.Equal("hello world", tokens[0].Text);
    }

    [Fact]
    public void Lex_CharLiteral_ExtractsChar()
    {
        var tokens = Lex("'A'");
        Assert.Equal(TokenKind.CharLiteral, tokens[0].Kind);
        Assert.Equal("A", tokens[0].Text);
    }

    [Fact]
    public void Lex_BoolLiterals_AreRecognized()
    {
        var tokens = Lex("true false");
        Assert.Equal(TokenKind.BoolLiteral, tokens[0].Kind);
        Assert.Equal("true", tokens[0].Text);
        Assert.Equal(TokenKind.BoolLiteral, tokens[1].Kind);
        Assert.Equal("false", tokens[1].Text);
    }

    [Fact]
    public void Lex_Operators_AreRecognized()
    {
        var tokens = Lex("++ -- += -= == != <= >= && ||");
        tokens.RemoveAt(tokens.Count - 1);
        var expected = new[]
        {
            TokenKind.PlusPlus, TokenKind.MinusMinus,
            TokenKind.PlusAssign, TokenKind.MinusAssign,
            TokenKind.EqualEqual, TokenKind.BangEqual,
            TokenKind.LessEqual, TokenKind.GreaterEqual,
            TokenKind.AmpAmp, TokenKind.PipePipe
        };
        Assert.Equal(expected.Length, tokens.Count);
        for (int i = 0; i < expected.Length; i++)
            Assert.Equal(expected[i], tokens[i].Kind);
    }

    [Fact]
    public void Lex_Arrow_IsRecognized()
    {
        var tokens = Lex("->");
        Assert.Equal(TokenKind.Arrow, tokens[0].Kind);
    }

    [Fact]
    public void Lex_DoubleColon_IsRecognized()
    {
        var tokens = Lex("::");
        Assert.Equal(TokenKind.DoubleColon, tokens[0].Kind);
    }

    [Fact]
    public void Lex_LineNumbers_AreTracked()
    {
        var tokens = Lex("int\nfloat");
        Assert.Equal(1, tokens[0].Line);
        Assert.Equal(2, tokens[1].Line);
    }
}
