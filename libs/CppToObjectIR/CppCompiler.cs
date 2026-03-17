using CppToObjectIR.Ast;
using CppToObjectIR.CodeGen;
using CppToObjectIR.Lexing;
using CppToObjectIR.Parsing;
using ObjectIR.Core.IR;

namespace CppToObjectIR;

/// <summary>
/// Main entry-point for the C++ → ObjectIR compiler.
/// </summary>
public static class CppCompiler
{
    /// <summary>
    /// Compiles a C++ source string into an ObjectIR <see cref="Module"/>.
    /// An optional pre-built member type registry (populated from headers) improves type inference.
    /// </summary>
    public static Module Compile(string moduleName, string cppSource,
        Dictionary<string, TypeReference>? memberTypeRegistry = null)
    {
        var tokens = Lex(cppSource);
        var ast    = Parse(tokens);
        return CodeGen(moduleName, ast, memberTypeRegistry);
    }

    /// <summary>Tokenizes C++ source into a token list.</summary>
    public static List<Token> Lex(string cppSource)
    {
        var lexer = new Lexer(cppSource);
        return lexer.Tokenize();
    }

    /// <summary>Parses a token list into a <see cref="TranslationUnitNode"/>.</summary>
    public static TranslationUnitNode Parse(List<Token> tokens)
    {
        var parser = new Parser(tokens);
        return parser.Parse();
    }

    /// <summary>Converts an AST into an ObjectIR <see cref="Module"/>.</summary>
    public static Module CodeGen(string moduleName, TranslationUnitNode ast,
        Dictionary<string, TypeReference>? memberTypeRegistry = null)
    {
        var gen = new IrCodeGenerator(moduleName, memberTypeRegistry);
        return gen.Generate(ast);
    }

    // =========================================================================
    // Type registry
    // =========================================================================

    /// <summary>
    /// Builds a "TypeName.MemberName → TypeReference" registry from a parsed AST.
    /// Pass registries from all headers + the main source to <see cref="Compile"/> for
    /// full type inference on chained member access.
    /// </summary>
    public static Dictionary<string, TypeReference> BuildMemberTypeRegistry(TranslationUnitNode ast)
    {
        var registry = new Dictionary<string, TypeReference>(StringComparer.Ordinal);
        PopulateRegistry(ast.Declarations, null, registry);
        return registry;
    }

    /// <summary>Returns every filename named in a <c>#include</c> directive in <paramref name="source"/>.</summary>
    public static IEnumerable<string> GetIncludeFilenames(string source)
    {
        foreach (var rawLine in source.Split('\n'))
        {
            var line = rawLine.TrimStart();
            if (!line.StartsWith("#include", StringComparison.Ordinal)) continue;
            var rest = line.Substring(8).TrimStart();
            if (rest.Length < 2) continue;
            char close = rest[0] == '"' ? '"' : '>';
            if (rest[0] != '"' && rest[0] != '<') continue;
            var end = rest.IndexOf(close, 1);
            if (end > 1) yield return rest[1..end];
        }
    }

    // ── Registry helpers ─────────────────────────────────────────────────────

    private static void PopulateRegistry(IEnumerable<AstNode> decls, string? ns,
        Dictionary<string, TypeReference> registry)
    {
        foreach (var decl in decls)
        {
            switch (decl)
            {
                case NamespaceNode nsn:
                    var fullNs = ns == null ? nsn.Name : $"{ns}.{nsn.Name}";
                    PopulateRegistry(nsn.Members, fullNs, registry);
                    break;
                case ClassDeclNode cls:
                    RegisterTypeMembers(cls.Name, ns, cls.Sections, registry);
                    break;
                case StructDeclNode str:
                    RegisterTypeMembers(str.Name, ns, str.Sections, registry);
                    break;
            }
        }
    }

    private static void RegisterTypeMembers(string typeName, string? ns,
        IEnumerable<MemberSection> sections, Dictionary<string, TypeReference> registry)
    {
        var qualName = ns == null ? typeName : $"{ns}.{typeName}";
        foreach (var section in sections)
        {
            foreach (var member in section.Members)
            {
                switch (member)
                {
                    case FieldDeclNode f:
                        registry[$"{qualName}.{f.Name}"] = RegistryTypeRef(f.Type, ns);
                        break;
                    case MethodDeclNode m when !m.IsConstructor:
                        // First overload wins (for return-type inference, all overloads share the same return type anyway)
                        registry.TryAdd($"{qualName}.{m.Name}", RegistryTypeRef(m.ReturnType, ns));
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Converts a parsed C++ type node to a TypeReference, normalising <c>::</c> → <c>.</c>
    /// and qualifying unqualified names with the surrounding namespace context.
    /// </summary>
    internal static TypeReference RegistryTypeRef(CppTypeNode t, string? ns = null)
    {
        var baseName = NormalizeName(t.BaseName);
        // If the name has no namespace qualifier and is not a builtin, qualify it
        // with the enclosing namespace so "Transform" → "UnityEngine.Transform".
        if (ns != null && !baseName.Contains('.') && !IsBuiltinName(baseName))
            baseName = $"{ns}.{baseName}";
        return MapBuiltinTypeStatic(baseName);
    }

    /// <summary>Replaces C++ <c>::</c> scope resolution with <c>.</c>.</summary>
    internal static string NormalizeName(string cppName) => cppName.Replace("::", ".");

    private static bool IsBuiltinName(string name) => name is
        "void" or "bool" or "char" or "int" or "short" or "long" or "long long" or
        "unsigned int" or "unsigned short" or "unsigned long" or "unsigned char" or
        "float" or "double" or "auto" or "string" or "std.string";

    internal static TypeReference MapBuiltinTypeStatic(string name) => name switch
    {
        "void"           => TypeReference.Void,
        "bool"           => TypeReference.Bool,
        "char"           => TypeReference.Char,
        "int"            => TypeReference.Int32,
        "short"          => TypeReference.Int16,
        "long"           => TypeReference.Int64,
        "long long"      => TypeReference.Int64,
        "unsigned int"   => TypeReference.UInt32,
        "unsigned short" => TypeReference.UInt16,
        "unsigned long"  => TypeReference.UInt64,
        "unsigned char"  => TypeReference.UInt8,
        "float"          => TypeReference.Float32,
        "double"         => TypeReference.Float64,
        "auto"           => TypeReference.Int32,
        "string"         => TypeReference.String,
        "std.string"     => TypeReference.String,
        _                => TypeReference.FromName(name)
    };
}
