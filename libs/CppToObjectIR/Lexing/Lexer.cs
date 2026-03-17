namespace CppToObjectIR.Lexing;

/// <summary>
/// Tokenizes a subset of C++ source code.
/// Handles: comments, preprocessor directives (skips them), all operators,
/// keywords, identifiers, and numeric/string/char literals.
/// </summary>
public sealed class Lexer
{
    private static readonly Dictionary<string, TokenKind> Keywords = new()
    {
        ["void"]      = TokenKind.KwVoid,
        ["int"]       = TokenKind.KwInt,
        ["long"]      = TokenKind.KwLong,
        ["short"]     = TokenKind.KwShort,
        ["char"]      = TokenKind.KwChar,
        ["bool"]      = TokenKind.KwBool,
        ["float"]     = TokenKind.KwFloat,
        ["double"]    = TokenKind.KwDouble,
        ["unsigned"]  = TokenKind.KwUnsigned,
        ["signed"]    = TokenKind.KwSigned,
        ["auto"]      = TokenKind.KwAuto,
        ["class"]     = TokenKind.KwClass,
        ["struct"]    = TokenKind.KwStruct,
        ["enum"]      = TokenKind.KwEnum,
        ["namespace"] = TokenKind.KwNamespace,
        ["public"]    = TokenKind.KwPublic,
        ["private"]   = TokenKind.KwPrivate,
        ["protected"] = TokenKind.KwProtected,
        ["static"]    = TokenKind.KwStatic,
        ["const"]     = TokenKind.KwConst,
        ["virtual"]   = TokenKind.KwVirtual,
        ["override"]  = TokenKind.KwOverride,
        ["explicit"]  = TokenKind.KwExplicit,
        ["inline"]    = TokenKind.KwInline,
        ["if"]        = TokenKind.KwIf,
        ["else"]      = TokenKind.KwElse,
        ["while"]     = TokenKind.KwWhile,
        ["for"]       = TokenKind.KwFor,
        ["do"]        = TokenKind.KwDo,
        ["break"]     = TokenKind.KwBreak,
        ["continue"]  = TokenKind.KwContinue,
        ["return"]    = TokenKind.KwReturn,
        ["switch"]    = TokenKind.KwSwitch,
        ["case"]      = TokenKind.KwCase,
        ["default"]   = TokenKind.KwDefault,
        ["try"]       = TokenKind.KwTry,
        ["catch"]     = TokenKind.KwCatch,
        ["throw"]     = TokenKind.KwThrow,
        ["new"]       = TokenKind.KwNew,
        ["delete"]    = TokenKind.KwDelete,
        ["this"]      = TokenKind.KwThis,
        ["true"]      = TokenKind.BoolLiteral,
        ["false"]     = TokenKind.BoolLiteral,
        ["nullptr"]   = TokenKind.NullptrLiteral,
    };

    private readonly string _src;
    private int _pos;
    private int _line;
    private int _col;

    public Lexer(string source)
    {
        _src = source ?? throw new ArgumentNullException(nameof(source));
        _pos = 0;
        _line = 1;
        _col = 1;
    }

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();
        while (true)
        {
            var tok = NextToken();
            tokens.Add(tok);
            if (tok.Kind == TokenKind.Eof)
                break;
        }
        return tokens;
    }

    private char Current => _pos < _src.Length ? _src[_pos] : '\0';
    private char Peek(int offset = 1) => (_pos + offset) < _src.Length ? _src[_pos + offset] : '\0';

    private char Advance()
    {
        var c = Current;
        _pos++;
        if (c == '\n') { _line++; _col = 1; }
        else { _col++; }
        return c;
    }

    private Token MakeToken(TokenKind kind, string text, int line, int col)
        => new Token(kind, text, line, col);

    private Token NextToken()
    {
        // Skip whitespace and handle preprocessor lines
        while (_pos < _src.Length)
        {
            char c = Current;

            // Whitespace
            if (char.IsWhiteSpace(c)) { Advance(); continue; }

            // Line comment
            if (c == '/' && Peek() == '/')
            {
                while (_pos < _src.Length && Current != '\n') Advance();
                continue;
            }

            // Block comment
            if (c == '/' && Peek() == '*')
            {
                Advance(); Advance(); // skip /*
                while (_pos < _src.Length && !(Current == '*' && Peek() == '/'))
                    Advance();
                if (_pos < _src.Length) { Advance(); Advance(); } // skip */
                continue;
            }

            // Preprocessor directive — skip whole line
            if (c == '#')
            {
                while (_pos < _src.Length && Current != '\n') Advance();
                continue;
            }

            break;
        }

        if (_pos >= _src.Length)
            return MakeToken(TokenKind.Eof, string.Empty, _line, _col);

        int startLine = _line;
        int startCol = _col;
        char ch = Current;

        // Identifiers / keywords
        if (char.IsLetter(ch) || ch == '_')
        {
            var sb = new System.Text.StringBuilder();
            while (_pos < _src.Length && (char.IsLetterOrDigit(Current) || Current == '_'))
                sb.Append(Advance());
            var text = sb.ToString();
            var kind = Keywords.TryGetValue(text, out var kw) ? kw : TokenKind.Identifier;
            return MakeToken(kind, text, startLine, startCol);
        }

        // Numeric literals
        if (char.IsDigit(ch) || (ch == '.' && char.IsDigit(Peek())))
        {
            return ReadNumber(startLine, startCol);
        }

        // String literal
        if (ch == '"')
        {
            Advance();
            var sb = new System.Text.StringBuilder();
            while (_pos < _src.Length && Current != '"')
            {
                if (Current == '\\') { Advance(); sb.Append(Advance()); }
                else sb.Append(Advance());
            }
            if (_pos < _src.Length) Advance(); // closing "
            return MakeToken(TokenKind.StringLiteral, sb.ToString(), startLine, startCol);
        }

        // Char literal
        if (ch == '\'')
        {
            Advance();
            var sb = new System.Text.StringBuilder();
            while (_pos < _src.Length && Current != '\'')
            {
                if (Current == '\\') { Advance(); sb.Append(Advance()); }
                else sb.Append(Advance());
            }
            if (_pos < _src.Length) Advance(); // closing '
            return MakeToken(TokenKind.CharLiteral, sb.ToString(), startLine, startCol);
        }

        // Operators and punctuation
        return ReadOperator(startLine, startCol);
    }

    private Token ReadNumber(int line, int col)
    {
        var sb = new System.Text.StringBuilder();
        bool isFloat = false;

        // Hex
        if (Current == '0' && (Peek() == 'x' || Peek() == 'X'))
        {
            sb.Append(Advance()); sb.Append(Advance());
            while (_pos < _src.Length && IsHexDigit(Current)) sb.Append(Advance());
            return MakeToken(TokenKind.IntLiteral, sb.ToString(), line, col);
        }

        while (_pos < _src.Length && char.IsDigit(Current)) sb.Append(Advance());

        if (_pos < _src.Length && Current == '.' && char.IsDigit(Peek()))
        {
            isFloat = true;
            sb.Append(Advance());
            while (_pos < _src.Length && char.IsDigit(Current)) sb.Append(Advance());
        }

        if (_pos < _src.Length && (Current == 'e' || Current == 'E'))
        {
            isFloat = true;
            sb.Append(Advance());
            if (_pos < _src.Length && (Current == '+' || Current == '-')) sb.Append(Advance());
            while (_pos < _src.Length && char.IsDigit(Current)) sb.Append(Advance());
        }

        // Optional suffix (f, u, l, ul, ll, etc.)
        while (_pos < _src.Length && (Current == 'f' || Current == 'F' || Current == 'u' ||
                                       Current == 'U' || Current == 'l' || Current == 'L'))
        {
            char suf = Current;
            if (suf == 'f' || suf == 'F') isFloat = true;
            sb.Append(Advance());
        }

        return MakeToken(isFloat ? TokenKind.FloatLiteral : TokenKind.IntLiteral, sb.ToString(), line, col);
    }

    private static bool IsHexDigit(char c)
        => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

    private Token ReadOperator(int line, int col)
    {
        char c = Advance();

        switch (c)
        {
            case '+':
                if (Current == '+') { Advance(); return MakeToken(TokenKind.PlusPlus, "++", line, col); }
                if (Current == '=') { Advance(); return MakeToken(TokenKind.PlusAssign, "+=", line, col); }
                return MakeToken(TokenKind.Plus, "+", line, col);
            case '-':
                if (Current == '-') { Advance(); return MakeToken(TokenKind.MinusMinus, "--", line, col); }
                if (Current == '=') { Advance(); return MakeToken(TokenKind.MinusAssign, "-=", line, col); }
                if (Current == '>') { Advance(); return MakeToken(TokenKind.Arrow, "->", line, col); }
                return MakeToken(TokenKind.Minus, "-", line, col);
            case '*':
                if (Current == '=') { Advance(); return MakeToken(TokenKind.StarAssign, "*=", line, col); }
                return MakeToken(TokenKind.Star, "*", line, col);
            case '/':
                if (Current == '=') { Advance(); return MakeToken(TokenKind.SlashAssign, "/=", line, col); }
                return MakeToken(TokenKind.Slash, "/", line, col);
            case '%':
                if (Current == '=') { Advance(); return MakeToken(TokenKind.PercentAssign, "%=", line, col); }
                return MakeToken(TokenKind.Percent, "%", line, col);
            case '&':
                if (Current == '&') { Advance(); return MakeToken(TokenKind.AmpAmp, "&&", line, col); }
                if (Current == '=') { Advance(); return MakeToken(TokenKind.AmpAssign, "&=", line, col); }
                return MakeToken(TokenKind.Ampersand, "&", line, col);
            case '|':
                if (Current == '|') { Advance(); return MakeToken(TokenKind.PipePipe, "||", line, col); }
                if (Current == '=') { Advance(); return MakeToken(TokenKind.PipeAssign, "|=", line, col); }
                return MakeToken(TokenKind.Pipe, "|", line, col);
            case '^':
                if (Current == '=') { Advance(); return MakeToken(TokenKind.CaretAssign, "^=", line, col); }
                return MakeToken(TokenKind.Caret, "^", line, col);
            case '~':
                return MakeToken(TokenKind.Tilde, "~", line, col);
            case '!':
                if (Current == '=') { Advance(); return MakeToken(TokenKind.BangEqual, "!=", line, col); }
                return MakeToken(TokenKind.Bang, "!", line, col);
            case '<':
                if (Current == '<')
                {
                    Advance();
                    if (Current == '=') { Advance(); return MakeToken(TokenKind.LessLessAssign, "<<=", line, col); }
                    return MakeToken(TokenKind.LessLess, "<<", line, col);
                }
                if (Current == '=') { Advance(); return MakeToken(TokenKind.LessEqual, "<=", line, col); }
                return MakeToken(TokenKind.Less, "<", line, col);
            case '>':
                if (Current == '>')
                {
                    Advance();
                    if (Current == '=') { Advance(); return MakeToken(TokenKind.GreaterGreaterAssign, ">>=", line, col); }
                    return MakeToken(TokenKind.GreaterGreater, ">>", line, col);
                }
                if (Current == '=') { Advance(); return MakeToken(TokenKind.GreaterEqual, ">=", line, col); }
                return MakeToken(TokenKind.Greater, ">", line, col);
            case '=':
                if (Current == '=') { Advance(); return MakeToken(TokenKind.EqualEqual, "==", line, col); }
                return MakeToken(TokenKind.Assign, "=", line, col);
            case '.':
                return MakeToken(TokenKind.Dot, ".", line, col);
            case ':':
                if (Current == ':') { Advance(); return MakeToken(TokenKind.DoubleColon, "::", line, col); }
                return MakeToken(TokenKind.Colon, ":", line, col);
            case ';': return MakeToken(TokenKind.Semicolon, ";", line, col);
            case ',': return MakeToken(TokenKind.Comma, ",", line, col);
            case '?': return MakeToken(TokenKind.Question, "?", line, col);
            case '(': return MakeToken(TokenKind.LParen, "(", line, col);
            case ')': return MakeToken(TokenKind.RParen, ")", line, col);
            case '{': return MakeToken(TokenKind.LBrace, "{", line, col);
            case '}': return MakeToken(TokenKind.RBrace, "}", line, col);
            case '[': return MakeToken(TokenKind.LBracket, "[", line, col);
            case ']': return MakeToken(TokenKind.RBracket, "]", line, col);
            default:
                return MakeToken(TokenKind.Unknown, c.ToString(), line, col);
        }
    }
}
