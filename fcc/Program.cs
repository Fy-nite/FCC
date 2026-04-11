using System;
using System.IO;
using CppToObjectIR;
using CppToObjectIR.Ast;
using ObjectIR.Core.Serialization;

if (args.Length == 0)
{
	Console.WriteLine("Usage: fcc <input.cpp> [output.oir]");
	Environment.ExitCode = 1;
}
else
{
	var input = args[0];
	var output = args.Length > 1 ? args[1] : Path.ChangeExtension(input, ".oir");
	try
	{
			var source = File.ReadAllText(input);
			// Default module name is the input filename; prefer a top-level namespace if present.
			var moduleName = Path.GetFileNameWithoutExtension(input);
		var sourceDir = Path.GetDirectoryName(Path.GetFullPath(input))!;

			// Build a member type registry from the main source…
			var mainAst = CppCompiler.Parse(CppCompiler.Lex(source));
			// If the source declares a top-level namespace, use it as the module name.
			var topNs = mainAst.Declarations.OfType<NamespaceNode>().FirstOrDefault()?.Name;
			if (!string.IsNullOrEmpty(topNs)) moduleName = topNs;
		var registry = CppCompiler.BuildMemberTypeRegistry(mainAst);

		// …then merge type info from every resolvable #include'd header.
		foreach (var includeName in CppCompiler.GetIncludeFilenames(source))
		{
			var headerPath = Path.Combine(sourceDir, includeName);
			if (!File.Exists(headerPath)) continue;
			try
			{
				var headerAst = CppCompiler.Parse(CppCompiler.Lex(File.ReadAllText(headerPath)));
				foreach (var (k, v) in CppCompiler.BuildMemberTypeRegistry(headerAst))
					registry.TryAdd(k, v);
			}
			catch { /* skip headers that fail to parse */ }
		}

		var module = CppCompiler.Compile(moduleName, source, registry);
		var serializer = new ModuleSerializer(module);
		var ir = serializer.DumpToFOB();
		File.WriteAllBytes(output, ir);
		Environment.ExitCode = 0;
	}
	catch (Exception ex)
	{
		Console.Error.WriteLine($"Error: {ex.Message}");
		Environment.ExitCode = 2;
	}
}
