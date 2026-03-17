using CppToObjectIR.Lexing;
using CppToObjectIR.Ast;

namespace CppToObjectIR.Parsing;

/// <summary>
/// Recursive-descent parser for a practical subset of C++.
/// Supports: namespaces, class/struct/enum, free functions, member functions,
/// constructors, fields, common statements and expressions.
/// </summary>
public sealed class Parser
{
    private readonly List<Token> _tokens;
    private int _pos;
    private string? _currentNamespace;

    public Parser(List<Token> tokens)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
    }

    // =====================================================================
    // Token helpers
    // =====================================================================

    private Token Current => _pos < _tokens.Count ? _tokens[_pos] : _tokens[^1];
    private Token Peek(int offset = 1)
    {
        int idx = _pos + offset;
        return idx < _tokens.Count ? _tokens[idx] : _tokens[^1];
    }

    private Token Advance() => _tokens[_pos < _tokens.Count - 1 ? _pos++ : _pos];

    private Token Expect(TokenKind kind)
    {
        if (Current.Kind != kind)
            throw new CppParseException($"Expected {kind} but got {Current.Kind} ('{Current.Text}') at {Current.Line}:{Current.Column}");
        return Advance();
    }

    private bool Check(TokenKind kind) => Current.Kind == kind;
    private bool Match(TokenKind kind) { if (Check(kind)) { Advance(); return true; } return false; }

    private void SkipSemicolon() => Match(TokenKind.Semicolon);

    // =====================================================================
    // Top-level
    // =====================================================================

    public TranslationUnitNode Parse()
    {
        var unit = new TranslationUnitNode { Line = 1, Column = 1 };
        while (!Check(TokenKind.Eof))
        {
            var decl = ParseTopLevelDeclaration();
            if (decl != null)
                unit.Declarations.Add(decl);
        }
        return unit;
    }

    private AstNode? ParseTopLevelDeclaration()
    {
        if (Check(TokenKind.KwNamespace))   return ParseNamespace();
        if (Check(TokenKind.KwClass))       return ParseClassDecl(null);
        if (Check(TokenKind.KwStruct))      return ParseStructDecl(null);
        if (Check(TokenKind.KwEnum))        return ParseEnumDecl(null);
        if (IsTypeStart())                  return ParseFunctionOrFieldDecl(null);

        // Unknown token — skip to recover
        Advance();
        return null;
    }

    // =====================================================================
    // Namespace
    // =====================================================================

    private NamespaceNode ParseNamespace()
    {
        var tok = Expect(TokenKind.KwNamespace);
        var name = Expect(TokenKind.Identifier).Text;
        var node = new NamespaceNode { Name = name, Line = tok.Line, Column = tok.Column };

        var prev = _currentNamespace;
        _currentNamespace = _currentNamespace == null ? name : $"{_currentNamespace}.{name}";

        Expect(TokenKind.LBrace);
        while (!Check(TokenKind.RBrace) && !Check(TokenKind.Eof))
        {
            if (Check(TokenKind.KwNamespace))
                node.Members.Add(ParseNamespace());
            else if (Check(TokenKind.KwClass))
                node.Members.Add(ParseClassDecl(name));
            else if (Check(TokenKind.KwStruct))
                node.Members.Add(ParseStructDecl(name));
            else if (Check(TokenKind.KwEnum))
                node.Members.Add(ParseEnumDecl(name));
            else if (IsTypeStart())
                node.Members.Add(ParseFunctionOrFieldDecl(name));
            else
                Advance(); // skip
        }
        Expect(TokenKind.RBrace);
        Match(TokenKind.Semicolon);

        _currentNamespace = prev;
        return node;
    }

    // =====================================================================
    // Class / Struct
    // =====================================================================

    private ClassDeclNode ParseClassDecl(string? ns)
    {
        var tok = Expect(TokenKind.KwClass);
        var name = Expect(TokenKind.Identifier).Text;
        var node = new ClassDeclNode
        {
            Name = name, Namespace = ns ?? _currentNamespace,
            DefaultAccess = AccessSpecifier.Private,
            Line = tok.Line, Column = tok.Column
        };

        // : public Base
        if (Match(TokenKind.Colon))
        {
            do
            {
                SkipAccessSpecifier(out _);
                var baseName = ParseQualifiedName();
                if (node.BaseClass == null) node.BaseClass = baseName;
                else node.BaseInterfaces.Add(baseName);
            } while (Match(TokenKind.Comma));
        }

        if (Match(TokenKind.Semicolon)) return node; // forward declaration

        ParseClassBody(node);
        Match(TokenKind.Semicolon);
        return node;
    }

    private StructDeclNode ParseStructDecl(string? ns)
    {
        var tok = Expect(TokenKind.KwStruct);
        var name = Expect(TokenKind.Identifier).Text;
        var node = new StructDeclNode
        {
            Name = name, Namespace = ns ?? _currentNamespace,
            DefaultAccess = AccessSpecifier.Public,
            Line = tok.Line, Column = tok.Column
        };

        if (Match(TokenKind.Colon))
        {
            do { SkipAccessSpecifier(out _); ParseQualifiedName(); } while (Match(TokenKind.Comma));
        }

        if (Match(TokenKind.Semicolon)) return node;

        ParseClassBody(node);
        Match(TokenKind.Semicolon);
        return node;
    }

    private void ParseClassBody(TypeDeclNode node)
    {
        Expect(TokenKind.LBrace);

        var currentAccess = node.DefaultAccess;
        var section = new MemberSection { Access = currentAccess };
        node.Sections.Add(section);

        while (!Check(TokenKind.RBrace) && !Check(TokenKind.Eof))
        {
            if (Check(TokenKind.KwPublic) || Check(TokenKind.KwPrivate) || Check(TokenKind.KwProtected))
            {
                SkipAccessSpecifier(out currentAccess);
                Expect(TokenKind.Colon);
                section = new MemberSection { Access = currentAccess };
                node.Sections.Add(section);
                continue;
            }

            if (Check(TokenKind.KwClass) || Check(TokenKind.KwStruct))
            {
                section.Members.Add(Check(TokenKind.KwClass)
                    ? ParseClassDecl(node.Namespace)
                    : ParseStructDecl(node.Namespace));
                continue;
            }

            if (Check(TokenKind.KwEnum))
            {
                section.Members.Add(ParseEnumDecl(node.Namespace));
                continue;
            }

            if (IsTypeStart() || IsModifier())
            {
                var member = ParseMemberDecl(node.Name);
                if (member != null) section.Members.Add(member);
                continue;
            }

            // Constructor / destructor by name
            if (Check(TokenKind.Identifier) && Current.Text == node.Name)
            {
                section.Members.Add(ParseConstructorDecl(node.Name));
                continue;
            }

            if (Check(TokenKind.Tilde))
            {
                // destructor — parse and discard body
                ParseDestructorDecl(node.Name);
                continue;
            }

            Advance(); // skip unknown
        }
        Expect(TokenKind.RBrace);
    }

    // =====================================================================
    // Enum
    // =====================================================================

    private EnumDeclNode ParseEnumDecl(string? ns)
    {
        var tok = Expect(TokenKind.KwEnum);
        bool isClass = Match(TokenKind.KwClass);
        var name = Expect(TokenKind.Identifier).Text;
        var node = new EnumDeclNode { Name = name, Namespace = ns ?? _currentNamespace, IsClass = isClass, Line = tok.Line, Column = tok.Column };

        if (Match(TokenKind.Colon))
            node.UnderlyingType = ParseTypeName().BaseName;

        if (Match(TokenKind.Semicolon)) return node;

        Expect(TokenKind.LBrace);
        long autoVal = 0;
        while (!Check(TokenKind.RBrace) && !Check(TokenKind.Eof))
        {
            var memberName = Expect(TokenKind.Identifier).Text;
            var member = new EnumMemberNode { Name = memberName };
            if (Match(TokenKind.Assign))
            {
                member.Value = ParseExpression();
                if (member.Value is IntLiteralNode il) autoVal = il.Value + 1;
                else autoVal++;
            }
            else
            {
                member.Value = new IntLiteralNode { Value = autoVal++ };
            }
            node.Members.Add(member);
            Match(TokenKind.Comma);
        }
        Expect(TokenKind.RBrace);
        Match(TokenKind.Semicolon);
        return node;
    }

    // =====================================================================
    // Member declarations
    // =====================================================================

    private AstNode? ParseMemberDecl(string typeName)
    {
        bool isStatic = false, isVirtual = false, isConst = false, isInline = false;
        while (IsModifier())
        {
            if (Match(TokenKind.KwStatic))  isStatic = true;
            else if (Match(TokenKind.KwVirtual)) isVirtual = true;
            else if (Match(TokenKind.KwConst)) isConst = true;
            else if (Match(TokenKind.KwInline)) isInline = true;
            else if (Match(TokenKind.KwExplicit)) { /* ignore */ }
            else Advance();
        }

        var returnType = ParseTypeName();
        var name = Expect(TokenKind.Identifier).Text;

        if (Check(TokenKind.LParen))
        {
            var method = new MethodDeclNode
            {
                ReturnType = returnType, Name = name,
                IsStatic = isStatic, IsVirtual = isVirtual,
                Line = Current.Line, Column = Current.Column
            };
            ParseParameterList(method.Parameters);

            if (Match(TokenKind.KwConst)) method.IsConst = true;
            if (Match(TokenKind.KwOverride)) method.IsOverride = true;

            // pure virtual: = 0
            if (Check(TokenKind.Assign) && Peek().Text == "0")
            {
                Advance(); Advance();
                method.IsPureVirtual = true;
            }

            if (Check(TokenKind.LBrace)) method.Body = ParseBlock();
            else Match(TokenKind.Semicolon);

            return method;
        }
        else
        {
            // field
            var field = new FieldDeclNode
            {
                Type = returnType, Name = name,
                IsStatic = isStatic, IsConst = isConst,
                Line = Current.Line, Column = Current.Column
            };
            if (Match(TokenKind.Assign)) field.Initializer = ParseExpression();
            Expect(TokenKind.Semicolon);
            return field;
        }
    }

    private MethodDeclNode ParseConstructorDecl(string typeName)
    {
        var tok = Advance(); // consume class name as ctor name
        var ctor = new MethodDeclNode
        {
            ReturnType = new CppTypeNode { BaseName = "void" },
            Name = typeName,
            IsConstructor = true,
            Line = tok.Line, Column = tok.Column
        };
        ParseParameterList(ctor.Parameters);

        // Skip initializer list: : base(args), member(val)
        if (Match(TokenKind.Colon))
        {
            while (!Check(TokenKind.LBrace) && !Check(TokenKind.Semicolon) && !Check(TokenKind.Eof))
                Advance();
        }

        if (Check(TokenKind.LBrace)) ctor.Body = ParseBlock();
        else Match(TokenKind.Semicolon);
        return ctor;
    }

    private void ParseDestructorDecl(string typeName)
    {
        Advance(); // ~
        Advance(); // class name
        Expect(TokenKind.LParen); Expect(TokenKind.RParen);
        if (Check(TokenKind.LBrace)) ParseBlock();
        else Match(TokenKind.Semicolon);
    }

    // =====================================================================
    // Free function / field
    // =====================================================================

    private AstNode ParseFunctionOrFieldDecl(string? ns)
    {
        bool isStatic = false, isInline = false;
        while (IsModifier())
        {
            if (Match(TokenKind.KwStatic)) isStatic = true;
            else if (Match(TokenKind.KwInline)) isInline = true;
            else Advance();
        }

        var returnType = ParseTypeName();
        var name = ParseQualifiedName();

        if (Check(TokenKind.LParen))
        {
            var func = new FunctionDeclNode
            {
                ReturnType = returnType, Name = name,
                Namespace = ns ?? _currentNamespace,
                IsStatic = isStatic, IsInline = isInline,
                Line = Current.Line, Column = Current.Column
            };
            ParseParameterList(func.Parameters);
            if (Check(TokenKind.LBrace)) func.Body = ParseBlock();
            else Match(TokenKind.Semicolon);
            return func;
        }
        else
        {
            // top-level variable — treat as global field (less common, just skip)
            if (Match(TokenKind.Assign)) ParseExpression();
            Match(TokenKind.Semicolon);
            return new FieldDeclNode { Type = returnType, Name = name };
        }
    }

    // =====================================================================
    // Parameters
    // =====================================================================

    private void ParseParameterList(List<ParameterNode> parameters)
    {
        Expect(TokenKind.LParen);
        while (!Check(TokenKind.RParen) && !Check(TokenKind.Eof))
        {
            if (Check(TokenKind.Dot) && Peek().Kind == TokenKind.Dot) { Advance(); Advance(); Advance(); break; } // ...
            var type = ParseTypeName();
            string paramName = string.Empty;
            if (Check(TokenKind.Identifier)) paramName = Advance().Text;

            var param = new ParameterNode { Type = type, Name = paramName };
            if (Match(TokenKind.Assign)) param.DefaultValue = ParseExpression();
            parameters.Add(param);
            Match(TokenKind.Comma);
        }
        Expect(TokenKind.RParen);
    }

    // =====================================================================
    // Type parsing
    // =====================================================================

    private CppTypeNode ParseTypeName()
    {
        bool isConst = false, isUnsigned = false;
        while (Check(TokenKind.KwConst)) { Advance(); isConst = true; }
        if (Check(TokenKind.KwUnsigned)) { Advance(); isUnsigned = true; }
        else if (Check(TokenKind.KwSigned)) Advance();

        var baseName = ParseQualifiedName();
        var node = new CppTypeNode { BaseName = baseName, IsConst = isConst, IsUnsigned = isUnsigned };

        // Template args: Foo<Bar, Baz>
        if (Check(TokenKind.Less))
        {
            Advance();
            while (!Check(TokenKind.Greater) && !Check(TokenKind.Eof))
            {
                node.TemplateArgs.Add(ParseTypeName());
                Match(TokenKind.Comma);
            }
            Expect(TokenKind.Greater);
        }

        while (Check(TokenKind.Star))  { Advance(); node.IsPointer = true; }
        while (Check(TokenKind.Ampersand)) { Advance(); node.IsReference = true; }

        return node;
    }

    private string ParseQualifiedName()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(ParseBaseName());
        while (Check(TokenKind.DoubleColon))
        {
            Advance();
            sb.Append("::"); sb.Append(ParseBaseName());
        }
        return sb.ToString();
    }

    private string ParseBaseName()
    {
        if (IsBuiltinType()) return Advance().Text;
        if (Check(TokenKind.Identifier)) return Advance().Text;
        return string.Empty;
    }

    // =====================================================================
    // Statements
    // =====================================================================

    private BlockStatementNode ParseBlock()
    {
        var tok = Expect(TokenKind.LBrace);
        var block = new BlockStatementNode { Line = tok.Line, Column = tok.Column };
        while (!Check(TokenKind.RBrace) && !Check(TokenKind.Eof))
        {
            var stmt = ParseStatement();
            if (stmt != null) block.Statements.Add(stmt);
        }
        Expect(TokenKind.RBrace);
        return block;
    }

    private StatementNode? ParseStatement()
    {
        if (Check(TokenKind.LBrace))   return ParseBlock();
        if (Check(TokenKind.KwReturn)) return ParseReturn();
        if (Check(TokenKind.KwIf))     return ParseIf();
        if (Check(TokenKind.KwWhile))  return ParseWhile();
        if (Check(TokenKind.KwFor))    return ParseFor();
        if (Check(TokenKind.KwDo))     return ParseDoWhile();
        if (Check(TokenKind.KwBreak))  { Advance(); Expect(TokenKind.Semicolon); return new BreakStatementNode(); }
        if (Check(TokenKind.KwContinue)) { Advance(); Expect(TokenKind.Semicolon); return new ContinueStatementNode(); }
        if (Check(TokenKind.KwTry))    return ParseTry();
        if (Check(TokenKind.KwThrow))  return ParseThrow();

        // Variable declaration: type name = ...; (ambiguous with expression)
        if (IsTypeStart() && LooksLikeVarDecl())
            return ParseVarDecl();

        // Expression statement
        var expr = ParseExpression();
        Expect(TokenKind.Semicolon);
        return new ExpressionStatementNode { Expression = expr };
    }

    private ReturnStatementNode ParseReturn()
    {
        var tok = Expect(TokenKind.KwReturn);
        var node = new ReturnStatementNode { Line = tok.Line, Column = tok.Column };
        if (!Check(TokenKind.Semicolon)) node.Value = ParseExpression();
        Expect(TokenKind.Semicolon);
        return node;
    }

    private IfStatementNode ParseIf()
    {
        var tok = Expect(TokenKind.KwIf);
        Expect(TokenKind.LParen);
        var cond = ParseExpression();
        Expect(TokenKind.RParen);
        var then = ParseStatement()!;
        StatementNode? els = null;
        if (Match(TokenKind.KwElse)) els = ParseStatement();
        return new IfStatementNode { Condition = cond, Then = then, Else = els, Line = tok.Line, Column = tok.Column };
    }

    private WhileStatementNode ParseWhile()
    {
        var tok = Expect(TokenKind.KwWhile);
        Expect(TokenKind.LParen);
        var cond = ParseExpression();
        Expect(TokenKind.RParen);
        var body = ParseStatement()!;
        return new WhileStatementNode { Condition = cond, Body = body, Line = tok.Line, Column = tok.Column };
    }

    private ForStatementNode ParseFor()
    {
        var tok = Expect(TokenKind.KwFor);
        Expect(TokenKind.LParen);

        StatementNode? init = null;
        if (!Check(TokenKind.Semicolon))
        {
            if (IsTypeStart() && LooksLikeVarDecl())
                init = ParseVarDecl();
            else
            {
                var e = ParseExpression();
                Expect(TokenKind.Semicolon);
                init = new ExpressionStatementNode { Expression = e };
            }
        }
        else Advance();

        ExpressionNode? cond = null;
        if (!Check(TokenKind.Semicolon)) cond = ParseExpression();
        Expect(TokenKind.Semicolon);

        ExpressionNode? update = null;
        if (!Check(TokenKind.RParen)) update = ParseExpression();
        Expect(TokenKind.RParen);

        var body = ParseStatement()!;
        return new ForStatementNode { Init = init, Condition = cond, Update = update, Body = body, Line = tok.Line, Column = tok.Column };
    }

    private DoWhileStatementNode ParseDoWhile()
    {
        var tok = Expect(TokenKind.KwDo);
        var body = ParseStatement()!;
        Expect(TokenKind.KwWhile);
        Expect(TokenKind.LParen);
        var cond = ParseExpression();
        Expect(TokenKind.RParen);
        Expect(TokenKind.Semicolon);
        return new DoWhileStatementNode { Body = body, Condition = cond, Line = tok.Line, Column = tok.Column };
    }

    private TryStatementNode ParseTry()
    {
        var tok = Expect(TokenKind.KwTry);
        var node = new TryStatementNode { TryBlock = ParseBlock(), Line = tok.Line, Column = tok.Column };
        while (Check(TokenKind.KwCatch))
        {
            Advance();
            Expect(TokenKind.LParen);
            // catch(...) or catch(Type varName)
            if (Check(TokenKind.Dot)) { Advance(); Advance(); Advance(); Expect(TokenKind.RParen); }
            else
            {
                var exType = ParseTypeName();
                var varName = Check(TokenKind.Identifier) ? Advance().Text : "_ex";
                Expect(TokenKind.RParen);
                var clause = new CatchClauseNode { ExceptionType = exType, VariableName = varName, Body = ParseBlock() };
                node.CatchClauses.Add(clause);
            }
        }
        return node;
    }

    private ThrowStatementNode ParseThrow()
    {
        var tok = Expect(TokenKind.KwThrow);
        var node = new ThrowStatementNode { Line = tok.Line, Column = tok.Column };
        if (!Check(TokenKind.Semicolon)) node.Value = ParseExpression();
        Expect(TokenKind.Semicolon);
        return node;
    }

    private VarDeclStatementNode ParseVarDecl()
    {
        var type = ParseTypeName();
        var name = Expect(TokenKind.Identifier).Text;
        var node = new VarDeclStatementNode { Type = type, Name = name };
        if (Match(TokenKind.Assign)) node.Initializer = ParseExpression();
        Expect(TokenKind.Semicolon);
        return node;
    }

    // =====================================================================
    // Expressions — Pratt/precedence climbing
    // =====================================================================

    private ExpressionNode ParseExpression() => ParseAssignment();

    private ExpressionNode ParseAssignment()
    {
        var left = ParseTernary();

        AssignOp? op = Current.Kind switch
        {
            TokenKind.Assign        => AssignOp.Assign,
            TokenKind.PlusAssign    => AssignOp.AddAssign,
            TokenKind.MinusAssign   => AssignOp.SubAssign,
            TokenKind.StarAssign    => AssignOp.MulAssign,
            TokenKind.SlashAssign   => AssignOp.DivAssign,
            TokenKind.PercentAssign => AssignOp.RemAssign,
            TokenKind.AmpAssign     => AssignOp.AndAssign,
            TokenKind.PipeAssign    => AssignOp.OrAssign,
            TokenKind.CaretAssign   => AssignOp.XorAssign,
            TokenKind.LessLessAssign => AssignOp.ShlAssign,
            TokenKind.GreaterGreaterAssign => AssignOp.ShrAssign,
            _ => null
        };

        if (op.HasValue)
        {
            Advance();
            var right = ParseAssignment();
            return new AssignmentExpressionNode { Operator = op.Value, Target = left, Value = right };
        }
        return left;
    }

    private ExpressionNode ParseTernary()
    {
        var cond = ParseLogicalOr();
        if (!Match(TokenKind.Question)) return cond;
        var then = ParseExpression();
        Expect(TokenKind.Colon);
        var els = ParseTernary();
        return new TernaryExpressionNode { Condition = cond, Then = then, Else = els };
    }

    private ExpressionNode ParseLogicalOr()
    {
        var left = ParseLogicalAnd();
        while (Check(TokenKind.PipePipe))
        {
            Advance();
            left = new BinaryExpressionNode { Operator = BinaryOp.LogicalOr, Left = left, Right = ParseLogicalAnd() };
        }
        return left;
    }

    private ExpressionNode ParseLogicalAnd()
    {
        var left = ParseBitOr();
        while (Check(TokenKind.AmpAmp))
        {
            Advance();
            left = new BinaryExpressionNode { Operator = BinaryOp.LogicalAnd, Left = left, Right = ParseBitOr() };
        }
        return left;
    }

    private ExpressionNode ParseBitOr()
    {
        var left = ParseBitXor();
        while (Check(TokenKind.Pipe))
        {
            Advance();
            left = new BinaryExpressionNode { Operator = BinaryOp.BitOr, Left = left, Right = ParseBitXor() };
        }
        return left;
    }

    private ExpressionNode ParseBitXor()
    {
        var left = ParseBitAnd();
        while (Check(TokenKind.Caret))
        {
            Advance();
            left = new BinaryExpressionNode { Operator = BinaryOp.BitXor, Left = left, Right = ParseBitAnd() };
        }
        return left;
    }

    private ExpressionNode ParseBitAnd()
    {
        var left = ParseEquality();
        while (Check(TokenKind.Ampersand))
        {
            Advance();
            left = new BinaryExpressionNode { Operator = BinaryOp.BitAnd, Left = left, Right = ParseEquality() };
        }
        return left;
    }

    private ExpressionNode ParseEquality()
    {
        var left = ParseRelational();
        while (Check(TokenKind.EqualEqual) || Check(TokenKind.BangEqual))
        {
            var op = Current.Kind == TokenKind.EqualEqual ? BinaryOp.Equal : BinaryOp.NotEqual;
            Advance();
            left = new BinaryExpressionNode { Operator = op, Left = left, Right = ParseRelational() };
        }
        return left;
    }

    private ExpressionNode ParseRelational()
    {
        var left = ParseShift();
        while (Check(TokenKind.Less) || Check(TokenKind.LessEqual) ||
               Check(TokenKind.Greater) || Check(TokenKind.GreaterEqual))
        {
            var op = Current.Kind switch
            {
                TokenKind.Less         => BinaryOp.Less,
                TokenKind.LessEqual    => BinaryOp.LessEqual,
                TokenKind.Greater      => BinaryOp.Greater,
                _                      => BinaryOp.GreaterEqual,
            };
            Advance();
            left = new BinaryExpressionNode { Operator = op, Left = left, Right = ParseShift() };
        }
        return left;
    }

    private ExpressionNode ParseShift()
    {
        var left = ParseAddSub();
        while (Check(TokenKind.LessLess) || Check(TokenKind.GreaterGreater))
        {
            var op = Current.Kind == TokenKind.LessLess ? BinaryOp.Shl : BinaryOp.Shr;
            Advance();
            left = new BinaryExpressionNode { Operator = op, Left = left, Right = ParseAddSub() };
        }
        return left;
    }

    private ExpressionNode ParseAddSub()
    {
        var left = ParseMulDiv();
        while (Check(TokenKind.Plus) || Check(TokenKind.Minus))
        {
            var op = Current.Kind == TokenKind.Plus ? BinaryOp.Add : BinaryOp.Sub;
            Advance();
            left = new BinaryExpressionNode { Operator = op, Left = left, Right = ParseMulDiv() };
        }
        return left;
    }

    private ExpressionNode ParseMulDiv()
    {
        var left = ParseCast();
        while (Check(TokenKind.Star) || Check(TokenKind.Slash) || Check(TokenKind.Percent))
        {
            var op = Current.Kind switch
            {
                TokenKind.Star    => BinaryOp.Mul,
                TokenKind.Slash   => BinaryOp.Div,
                _                 => BinaryOp.Rem,
            };
            Advance();
            left = new BinaryExpressionNode { Operator = op, Left = left, Right = ParseCast() };
        }
        return left;
    }

    private ExpressionNode ParseCast()
    {
        // C-style cast: (type) expr
        if (Check(TokenKind.LParen) && IsTypeCast())
        {
            Advance();
            var targetType = ParseTypeName();
            Expect(TokenKind.RParen);
            return new CastExpressionNode { TargetType = targetType, Operand = ParseUnary() };
        }
        return ParseUnary();
    }

    private ExpressionNode ParseUnary()
    {
        if (Check(TokenKind.Minus))
        {
            Advance();
            return new UnaryExpressionNode { Operator = UnaryOp.Negate, Operand = ParseUnary() };
        }
        if (Check(TokenKind.Bang))
        {
            Advance();
            return new UnaryExpressionNode { Operator = UnaryOp.LogicalNot, Operand = ParseUnary() };
        }
        if (Check(TokenKind.Tilde))
        {
            Advance();
            return new UnaryExpressionNode { Operator = UnaryOp.BitwiseNot, Operand = ParseUnary() };
        }
        if (Check(TokenKind.PlusPlus))
        {
            Advance();
            return new UnaryExpressionNode { Operator = UnaryOp.PreIncrement, Operand = ParseUnary() };
        }
        if (Check(TokenKind.MinusMinus))
        {
            Advance();
            return new UnaryExpressionNode { Operator = UnaryOp.PreDecrement, Operand = ParseUnary() };
        }
        if (Check(TokenKind.Star)) // dereference
        {
            Advance();
            return new UnaryExpressionNode { Operator = UnaryOp.Negate, Operand = ParseUnary() }; // simplified
        }
        if (Check(TokenKind.Ampersand)) // address-of — skip
        {
            Advance();
            return ParseUnary();
        }
        return ParsePostfix();
    }

    private ExpressionNode ParsePostfix()
    {
        var expr = ParsePrimary();
        while (true)
        {
            if (Check(TokenKind.Dot) || Check(TokenKind.Arrow))
            {
                bool isArrow = Current.Kind == TokenKind.Arrow;
                Advance();
                var member = Expect(TokenKind.Identifier).Text;
                expr = new MemberAccessNode { Object = expr, Member = member, IsArrow = isArrow };
            }
            else if (Check(TokenKind.LParen))
            {
                var args = new List<ExpressionNode>();
                Advance();
                while (!Check(TokenKind.RParen) && !Check(TokenKind.Eof))
                {
                    args.Add(ParseExpression());
                    Match(TokenKind.Comma);
                }
                Expect(TokenKind.RParen);
                expr = new CallExpressionNode { Callee = expr, Arguments = { } };
                ((CallExpressionNode)expr).Arguments.AddRange(args);
            }
            else if (Check(TokenKind.LBracket))
            {
                Advance();
                var idx = ParseExpression();
                Expect(TokenKind.RBracket);
                expr = new IndexExpressionNode { Array = expr, Index = idx };
            }
            else if (Check(TokenKind.PlusPlus))
            {
                Advance();
                expr = new UnaryExpressionNode { Operator = UnaryOp.PostIncrement, Operand = expr, IsPostfix = true };
            }
            else if (Check(TokenKind.MinusMinus))
            {
                Advance();
                expr = new UnaryExpressionNode { Operator = UnaryOp.PostDecrement, Operand = expr, IsPostfix = true };
            }
            else break;
        }
        return expr;
    }

    private ExpressionNode ParsePrimary()
    {
        var tok = Current;

        if (tok.Kind == TokenKind.IntLiteral)
        {
            Advance();
            long val = ParseIntText(tok.Text);
            return new IntLiteralNode { Value = val, Line = tok.Line, Column = tok.Column };
        }
        if (tok.Kind == TokenKind.FloatLiteral)
        {
            Advance();
            string numText = tok.Text.TrimEnd('f', 'F', 'l', 'L');
            double val = double.TryParse(numText, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0;
            return new FloatLiteralNode { Value = val, Line = tok.Line, Column = tok.Column };
        }
        if (tok.Kind == TokenKind.StringLiteral)
        {
            Advance();
            return new StringLiteralNode { Value = tok.Text, Line = tok.Line, Column = tok.Column };
        }
        if (tok.Kind == TokenKind.CharLiteral)
        {
            Advance();
            char c = tok.Text.Length > 0 ? tok.Text[0] : '\0';
            return new CharLiteralNode { Value = c, Line = tok.Line, Column = tok.Column };
        }
        if (tok.Kind == TokenKind.BoolLiteral)
        {
            Advance();
            return new BoolLiteralNode { Value = tok.Text == "true", Line = tok.Line, Column = tok.Column };
        }
        if (tok.Kind == TokenKind.NullptrLiteral)
        {
            Advance();
            return new NullptrLiteralNode { Line = tok.Line, Column = tok.Column };
        }
        if (tok.Kind == TokenKind.KwThis)
        {
            Advance();
            return new ThisExpressionNode { Line = tok.Line, Column = tok.Column };
        }
        if (tok.Kind == TokenKind.KwNew)
        {
            return ParseNewExpression();
        }
        if (tok.Kind == TokenKind.LParen)
        {
            Advance();
            var inner = ParseExpression();
            Expect(TokenKind.RParen);
            return inner;
        }
        if (tok.Kind == TokenKind.Identifier || IsBuiltinType())
        {
            Advance();
            var name = tok.Text;
            // qualified name
            while (Check(TokenKind.DoubleColon))
            {
                Advance();
                name += "::" + Expect(TokenKind.Identifier).Text;
            }
            return new IdentifierNode { Name = name, Line = tok.Line, Column = tok.Column };
        }

        // fallback
        Advance();
        return new IdentifierNode { Name = tok.Text, Line = tok.Line, Column = tok.Column };
    }

    private NewExpressionNode ParseNewExpression()
    {
        var tok = Expect(TokenKind.KwNew);
        var type = ParseTypeName();
        var node = new NewExpressionNode { Type = type, Line = tok.Line, Column = tok.Column };

        // new Foo[size]
        if (Match(TokenKind.LBracket))
        {
            node.ArraySize = ParseExpression();
            Expect(TokenKind.RBracket);
        }
        // new Foo(args)
        else if (Check(TokenKind.LParen))
        {
            Advance();
            while (!Check(TokenKind.RParen) && !Check(TokenKind.Eof))
            {
                node.Arguments.Add(ParseExpression());
                Match(TokenKind.Comma);
            }
            Expect(TokenKind.RParen);
        }
        return node;
    }

    // =====================================================================
    // Lookahead helpers
    // =====================================================================

    private bool IsTypeStart()
        => IsBuiltinType() || Check(TokenKind.KwConst) || Check(TokenKind.KwUnsigned) || Check(TokenKind.KwSigned)
           || (Check(TokenKind.Identifier) && LooksLikeTypeName());

    private bool LooksLikeTypeName()
    {
        // heuristic: identifier followed by another identifier, *, &, or <
        int save = _pos;
        Advance(); // consume identifier
        // skip :: Foo parts
        while (Check(TokenKind.DoubleColon)) { Advance(); Advance(); }
        // skip template args
        if (Check(TokenKind.Less)) { SkipAngleBrackets(); }
        bool couldBeType = Check(TokenKind.Identifier) || Check(TokenKind.Star) || Check(TokenKind.Ampersand);
        _pos = save;
        return couldBeType;
    }

    private void SkipAngleBrackets()
    {
        if (!Check(TokenKind.Less)) return;
        int depth = 0;
        while (!Check(TokenKind.Eof))
        {
            if (Check(TokenKind.Less)) depth++;
            else if (Check(TokenKind.Greater)) { depth--; if (depth == 0) { Advance(); return; } }
            Advance();
        }
    }

    private bool LooksLikeVarDecl()
    {
        // Save position, skip type, check for identifier then = or ;
        int save = _pos;
        try
        {
            ParseTypeName(); // may advance
            bool result = Check(TokenKind.Identifier);
            return result;
        }
        catch { return false; }
        finally { _pos = save; }
    }

    private bool IsBuiltinType()
        => Current.Kind is TokenKind.KwVoid or TokenKind.KwInt or TokenKind.KwLong or TokenKind.KwShort
           or TokenKind.KwChar or TokenKind.KwBool or TokenKind.KwFloat or TokenKind.KwDouble or TokenKind.KwAuto;

    private bool IsModifier()
        => Current.Kind is TokenKind.KwStatic or TokenKind.KwVirtual or TokenKind.KwConst
           or TokenKind.KwInline or TokenKind.KwExplicit;

    private bool IsTypeCast()
    {
        // Check if (token) looks like a type cast: (type) expr
        int save = _pos + 1; // after LParen
        int tempPos = save;
        int depth = 1;
        while (tempPos < _tokens.Count && depth > 0)
        {
            if (_tokens[tempPos].Kind == TokenKind.LParen) depth++;
            else if (_tokens[tempPos].Kind == TokenKind.RParen) depth--;
            tempPos++;
        }
        if (tempPos >= _tokens.Count) return false;
        var afterParen = _tokens[tempPos];
        // after ')' there should be an operand start, not a binary op
        return afterParen.Kind is TokenKind.Identifier or TokenKind.IntLiteral or TokenKind.FloatLiteral
            or TokenKind.Minus or TokenKind.Bang or TokenKind.KwThis;
    }

    private void SkipAccessSpecifier(out AccessSpecifier access)
    {
        access = Current.Kind switch
        {
            TokenKind.KwPublic    => AccessSpecifier.Public,
            TokenKind.KwPrivate   => AccessSpecifier.Private,
            TokenKind.KwProtected => AccessSpecifier.Protected,
            _                     => AccessSpecifier.Public
        };
        if (Current.Kind is TokenKind.KwPublic or TokenKind.KwPrivate or TokenKind.KwProtected)
            Advance();
    }

    private static long ParseIntText(string text)
    {
        text = text.ToLowerInvariant().TrimEnd('u', 'l');
        if (text.StartsWith("0x"))
            return Convert.ToInt64(text, 16);
        return long.TryParse(text, out var v) ? v : 0;
    }
}
