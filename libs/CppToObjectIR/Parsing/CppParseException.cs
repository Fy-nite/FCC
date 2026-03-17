namespace CppToObjectIR.Parsing;

public sealed class CppParseException : Exception
{
    public CppParseException(string message) : base(message) { }
}
