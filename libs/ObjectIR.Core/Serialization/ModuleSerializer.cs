namespace ObjectIR.Core.Serialization;

using ObjectIR.Core.IR;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

#pragma warning disable CS1591

/// <summary>
/// Provides serialization and dumping capabilities for IR modules
/// </summary>
public sealed class ModuleSerializer
{
    private readonly Module _module;
    // When dumping instructions we keep track of the method currently being emitted
    // so we can pretty-print `ldarg 0` as `ldarg this` for instance methods.
    private MethodDefinition? _currentMethodBeingDumped;

  public ModuleSerializer(Module module)
 {
        _module = module ?? throw new ArgumentNullException(nameof(module));
    }

 /// <summary>
    /// Dumps the module as a structured object representation
    /// </summary>
    public ModuleData Dump() => DumpModule();

    /// <summary>
    /// Dumps the module as JSON string
    /// </summary>
    public string DumpToJson(bool indented = true)
    {
        var data = DumpModule();
        var options = new JsonSerializerOptions
        {
            WriteIndented = indented,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        return JsonSerializer.Serialize(data, options);
    }

    /// <summary>
    /// Dumps the module as human-readable text
    /// </summary>
    public string DumpToText()
    {
 var sb = new StringBuilder();
   
        sb.AppendLine($"Module: {_module.Name}");
        sb.AppendLine($"Version: {_module.Version}");
        
 if (_module.Metadata.Count > 0)
        {
    sb.AppendLine("\nMetadata:");
 foreach (var (key, value) in _module.Metadata)
            {
       sb.AppendLine($"  {key}: {value}");
        }
   }

        if (_module.Types.Count > 0)
        {
            sb.AppendLine($"\nTypes ({_module.Types.Count}):");
            foreach (var type in _module.Types)
    {
   DumpType(sb, type, indent: 1);
  }
  }

  if (_module.Functions.Count > 0)
        {
            sb.AppendLine($"\nFunctions ({_module.Functions.Count}):");
 foreach (var func in _module.Functions)
            {
     DumpFunction(sb, func, indent: 1);
 }
        }

        return sb.ToString();
  }

    /// <summary>
    /// Dumps the module as IR code (similar to assembly language)
    /// </summary>
    public string DumpToIRCode()
    {
        var sb = new StringBuilder();

        // Module header
        sb.AppendLine($"module {_module.Name} version {_module.Version}");

        // Types
        foreach (var type in _module.Types)
        {
            DumpTypeAsIRCode(sb, type);
            sb.AppendLine();
        }

        // Functions
        foreach (var func in _module.Functions)
        {
            DumpFunctionAsIRCode(sb, func);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Dumps the module to an array of type descriptions
    /// </summary>
    public TypeData[] DumpTypes() => _module.Types.Select(t => DumpTypeData(t)).ToArray();

    /// <summary>
    /// Dumps the module to an array of function descriptions
    /// </summary>
    public FunctionData[] DumpFunctions() => _module.Functions.Select(f => DumpFunctionData(f)).ToArray();

    // ============================================================================
    // Private Methods
    // ============================================================================

    private ModuleData DumpModule()
    {
        return new ModuleData
    {
            Name = _module.Name,
       Version = _module.Version.ToString(),
 Metadata = _module.Metadata.ToDictionary(x => x.Key, x => x.Value?.ToString() ?? "null"),
          Types = _module.Types.Select(DumpTypeData).ToArray(),
            Functions = _module.Functions.Select(DumpFunctionData).ToArray()
        };
    }

    private TypeData DumpTypeData(TypeDefinition type)
    {
        return type switch
      {
       ClassDefinition classDef => new TypeData
            {
    Kind = "Class",
          Name = classDef.Name,
       Namespace = classDef.Namespace,
                Access = classDef.Access.ToString(),
   IsAbstract = classDef.IsAbstract,
              IsSealed = classDef.IsSealed,
         BaseType = classDef.BaseType?.GetQualifiedName(),
     Interfaces = classDef.Interfaces.Select(i => i.GetQualifiedName()).ToArray(),
   GenericParameters = classDef.GenericParameters.Select(g => g.Name).ToArray(),
              Fields = classDef.Fields.Select(DumpFieldData).ToArray(),
  Methods = classDef.Methods.Select(DumpMethodData).ToArray(),
     Properties = classDef.Properties.Select(DumpPropertyData).ToArray()
 },
            InterfaceDefinition interfaceDef => new TypeData
  {
     Kind = "Interface",
      Name = interfaceDef.Name,
                Namespace = interfaceDef.Namespace,
    Access = interfaceDef.Access.ToString(),
    BaseInterfaces = interfaceDef.BaseInterfaces.Select(i => i.GetQualifiedName()).ToArray(),
    Methods = interfaceDef.Methods.Select(DumpMethodData).ToArray(),
Properties = interfaceDef.Properties.Select(DumpPropertyData).ToArray()
  },
            StructDefinition structDef => new TypeData
    {
          Kind = "Struct",
    Name = structDef.Name,
      Namespace = structDef.Namespace,
                Access = structDef.Access.ToString(),
         Interfaces = structDef.Interfaces.Select(i => i.GetQualifiedName()).ToArray(),
          GenericParameters = structDef.GenericParameters.Select(g => g.Name).ToArray(),
                Fields = structDef.Fields.Select(DumpFieldData).ToArray(),
      Methods = structDef.Methods.Select(DumpMethodData).ToArray()
 },
            EnumDefinition enumDef => new TypeData
            {
                Kind = "Enum",
                Name = enumDef.Name,
                Namespace = enumDef.Namespace,
                Access = enumDef.Access.ToString(),
                UnderlyingType = enumDef.UnderlyingType.GetQualifiedName()
            },
 _ => new TypeData { Kind = "Unknown", Name = type.Name }
        };
    }

    private FieldData DumpFieldData(FieldDefinition field)
 {
        return new FieldData
        {
         Name = field.Name,
         Type = field.Type.GetQualifiedName(),
    Access = field.Access.ToString(),
            IsStatic = field.IsStatic,
 IsReadOnly = field.IsReadOnly,
        InitialValue = field.InitialValue?.ToString()
      };
    }

    private PropertyData DumpPropertyData(PropertyDefinition property)
    {
        return new PropertyData
        {
Name = property.Name,
            Type = property.Type.GetQualifiedName(),
            Access = property.Access.ToString(),
         HasGetter = property.Getter != null,
            HasSetter = property.Setter != null,
        GetterAccess = property.Getter?.Access.ToString(),
            SetterAccess = property.Setter?.Access.ToString()
        };
  }

    private MethodData DumpMethodData(MethodDefinition method)
    {
        var instructionsNode = JsonNode.Parse(InstructionSerializer.SerializeInstructions(method.Instructions).GetRawText()) ?? new JsonArray();

        return new MethodData
        {
            Name = method.Name,
            ReturnType = method.ReturnType.GetQualifiedName(),
            Access = method.Access.ToString(),
            IsStatic = method.IsStatic,
            IsVirtual = method.IsVirtual,
            IsOverride = method.IsOverride,
            IsAbstract = method.IsAbstract,
            IsConstructor = method.IsConstructor,
            Parameters = method.Parameters.Select(p => new ParameterData
            {
                Name = p.Name,
                Type = p.Type.GetQualifiedName()
            }).ToArray(),
            LocalVariables = method.Locals.Select(l => new LocalVariableData
            {
                Name = l.Name,
                Type = l.Type.GetQualifiedName()
            }).ToArray(),
            InstructionCount = method.Instructions.Count,
            Instructions = instructionsNode
        };
    }

    private FunctionData DumpFunctionData(FunctionDefinition function)
    {
        var instructionsNode = JsonNode.Parse(InstructionSerializer.SerializeInstructions(function.Instructions).GetRawText()) ?? new JsonArray();

        return new FunctionData
        {
            Name = function.Name,
            ReturnType = function.ReturnType.GetQualifiedName(),
            Parameters = function.Parameters.Select(p => new ParameterData
            {
                Name = p.Name,
                Type = p.Type.GetQualifiedName()
            }).ToArray(),
            LocalVariables = function.Locals.Select(l => new LocalVariableData
            {
                Name = l.Name,
                Type = l.Type.GetQualifiedName()
            }).ToArray(),
            InstructionCount = function.Instructions.Count,
            Instructions = instructionsNode
        };
    }

    private void DumpType(StringBuilder sb, TypeDefinition type, int indent = 0)
    {
        var ind = new string(' ', indent * 2);
        
        if (type is ClassDefinition classDef)
        {
 var modifiers = new List<string>();
 if (classDef.IsAbstract) modifiers.Add("abstract");
        if (classDef.IsSealed) modifiers.Add("sealed");
            
            var modifierStr = modifiers.Count > 0 ? string.Join(" ", modifiers) + " " : "";
 sb.AppendLine($"{ind}{modifierStr}class {classDef.GetQualifiedName()}");
 
            if (classDef.BaseType != null)
           sb.AppendLine($"{ind}  : {classDef.BaseType.GetQualifiedName()}");
    
          if (classDef.Interfaces.Count > 0)
        {
         sb.AppendLine($"{ind}  implements: {string.Join(", ", classDef.Interfaces.Select(i => i.GetQualifiedName()))}");
   }
            
   if (classDef.Fields.Count > 0)
   {
     sb.AppendLine($"{ind}  Fields:");
 foreach (var field in classDef.Fields)
           {
      sb.AppendLine($"{ind}    {field.Access} {(field.IsStatic ? "static " : "")}{(field.IsReadOnly ? "readonly " : "")}{field.Type.GetQualifiedName()} {field.Name}");
           }
  }
            
            if (classDef.Methods.Count > 0)
       {
    sb.AppendLine($"{ind}  Methods:");
         foreach (var method in classDef.Methods)
    {
   var methodModifiers = new List<string>();
         if (method.IsStatic) methodModifiers.Add("static");
           if (method.IsVirtual) methodModifiers.Add("virtual");
         if (method.IsOverride) methodModifiers.Add("override");
    if (method.IsAbstract) methodModifiers.Add("abstract");
          
    var methodModStr = methodModifiers.Count > 0 ? string.Join(" ", methodModifiers) + " " : "";
      var methodName = method.IsConstructor ? ".ctor" : method.Name;
        var returnType = method.ReturnType.GetQualifiedName();
          var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type.GetQualifiedName()} {p.Name}"));
            
 sb.AppendLine($"{ind}    {method.Access} {methodModStr}{returnType} {methodName}({parameters}) [{method.Instructions.Count} instructions]");
         }
          }
        }
        else if (type is InterfaceDefinition interfaceDef)
      {
  sb.AppendLine($"{ind}interface {interfaceDef.GetQualifiedName()}");
          
    if (interfaceDef.Methods.Count > 0)
            {
       sb.AppendLine($"{ind}  Methods:");
    foreach (var method in interfaceDef.Methods)
                {
         var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type.GetQualifiedName()} {p.Name}"));
   sb.AppendLine($"{ind}    {method.ReturnType.GetQualifiedName()} {method.Name}({parameters})");
            }
    }
        }
  else if (type is StructDefinition structDef)
     {
            sb.AppendLine($"{ind}struct {structDef.GetQualifiedName()}");
         
     if (structDef.Fields.Count > 0)
            {
      sb.AppendLine($"{ind}  Fields:");
       foreach (var field in structDef.Fields)
         {
         sb.AppendLine($"{ind}    {field.Access} {field.Type.GetQualifiedName()} {field.Name}");
                }
   }
        }
    }

    private void DumpFunction(StringBuilder sb, FunctionDefinition function, int indent = 0)
    {
        var ind = new string(' ', indent * 2);
  var parameters = string.Join(", ", function.Parameters.Select(p => $"{p.Type.GetQualifiedName()} {p.Name}"));
        sb.AppendLine($"{ind}{function.ReturnType.GetQualifiedName()} {function.Name}({parameters}) [{function.Instructions.Count} instructions]");
    }

    /// <summary>
    /// Loads a module from JSON string
    /// </summary>
    public static Module LoadFromJson(string json)
    {
        // Validate JSON against schema before deserializing
        var validationResult = JsonValidator.Validate(json);
        if (!validationResult.IsValid)
        {
            throw new JsonValidationException($"Invalid ObjectIR module JSON: {validationResult.ErrorMessage}");
        }

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        var data = JsonSerializer.Deserialize<ModuleData>(json, options) ?? throw new InvalidOperationException("Failed to deserialize module data");
        return LoadModule(data);
    }

    /// <summary>
    /// Loads a module from ModuleData
    /// </summary>
    public static Module LoadModule(ModuleData data)
    {
        var module = new Module(data.Name)
        {
            Version = Version.Parse(data.Version)
        };

        foreach (var kvp in data.Metadata)
        {
            module.Metadata[kvp.Key] = kvp.Value;
        }

        // Load types
        foreach (var typeData in data.Types)
        {
            var typeDef = LoadType(typeData);
            module.Types.Add(typeDef);
        }

        // Load functions
        foreach (var funcData in data.Functions)
        {
            var funcDef = LoadFunction(funcData);
            module.Functions.Add(funcDef);
        }

        return module;
    }

    private static TypeDefinition LoadType(TypeData typeData)
    {
        TypeDefinition typeDef = typeData.Kind switch
        {
            "Class" => LoadClass(typeData),
            "Interface" => LoadInterface(typeData),
            "Struct" => LoadStruct(typeData),
            "Enum" => LoadEnum(typeData),
            _ => throw new NotSupportedException($"Unknown type kind: {typeData.Kind}")
        };

        // Load generic parameters
        foreach (var gp in typeData.GenericParameters)
        {
            var param = new GenericParameter(gp);
            typeDef.GenericParameters.Add(param);
        }

        return typeDef;
    }

    private static ClassDefinition LoadClass(TypeData typeData)
    {
        var classDef = new ClassDefinition(typeData.Name);
        classDef.Namespace = typeData.Namespace;
        classDef.Access = Enum.Parse<AccessModifier>(typeData.Access);
        classDef.IsAbstract = typeData.IsAbstract;
        classDef.IsSealed = typeData.IsSealed;
        if (typeData.BaseType != null)
        {
            classDef.BaseType = TypeReference.FromName(typeData.BaseType);
        }
        foreach (var iface in typeData.Interfaces)
        {
            classDef.Interfaces.Add(TypeReference.FromName(iface));
        }

        // Load fields
        foreach (var fieldData in typeData.Fields)
        {
            var field = classDef.DefineField(fieldData.Name, TypeReference.FromName(fieldData.Type));
            field.Access = Enum.Parse<AccessModifier>(fieldData.Access);
            field.IsStatic = fieldData.IsStatic;
            field.IsReadOnly = fieldData.IsReadOnly;
            if (fieldData.InitialValue != null)
            {
                field.InitialValue = fieldData.InitialValue;
            }
        }

        // Load properties
        foreach (var propData in typeData.Properties)
        {
            var prop = new PropertyDefinition(propData.Name, TypeReference.FromName(propData.Type));
            prop.Access = Enum.Parse<AccessModifier>(propData.Access);
            classDef.Properties.Add(prop);
        }

        // Load methods
        foreach (var methodData in typeData.Methods)
        {
            var method = classDef.DefineMethod(methodData.Name, TypeReference.FromName(methodData.ReturnType));
            LoadMethodData(method, methodData);
        }

        return classDef;
    }

    private static InterfaceDefinition LoadInterface(TypeData typeData)
    {
        var interfaceDef = new InterfaceDefinition(typeData.Name);
        interfaceDef.Namespace = typeData.Namespace;
        interfaceDef.Access = Enum.Parse<AccessModifier>(typeData.Access);

        foreach (var iface in typeData.BaseInterfaces)
        {
            interfaceDef.BaseInterfaces.Add(TypeReference.FromName(iface));
        }

        // Load properties
        foreach (var propData in typeData.Properties)
        {
            var prop = new PropertyDefinition(propData.Name, TypeReference.FromName(propData.Type));
            prop.Access = Enum.Parse<AccessModifier>(propData.Access);
            interfaceDef.Properties.Add(prop);
        }

        // Load methods
        foreach (var methodData in typeData.Methods)
        {
            var method = interfaceDef.DefineMethod(methodData.Name, TypeReference.FromName(methodData.ReturnType));
            LoadMethodData(method, methodData);
        }

        return interfaceDef;
    }

    private static StructDefinition LoadStruct(TypeData typeData)
    {
        var structDef = new StructDefinition(typeData.Name);
        structDef.Namespace = typeData.Namespace;
        structDef.Access = Enum.Parse<AccessModifier>(typeData.Access);

        foreach (var iface in typeData.Interfaces)
        {
            structDef.Interfaces.Add(TypeReference.FromName(iface));
        }

        // Load fields
        foreach (var fieldData in typeData.Fields)
        {
            var field = structDef.DefineField(fieldData.Name, TypeReference.FromName(fieldData.Type));
            field.Access = Enum.Parse<AccessModifier>(fieldData.Access);
            field.IsStatic = fieldData.IsStatic;
            field.IsReadOnly = fieldData.IsReadOnly;
            if (fieldData.InitialValue != null)
            {
                field.InitialValue = fieldData.InitialValue;
            }
        }

        // Load methods
        foreach (var methodData in typeData.Methods)
        {
            var method = structDef.DefineMethod(methodData.Name, TypeReference.FromName(methodData.ReturnType));
            LoadMethodData(method, methodData);
        }

        return structDef;
    }

    private static EnumDefinition LoadEnum(TypeData typeData)
    {
        var enumDef = new EnumDefinition(typeData.Name);
        enumDef.Namespace = typeData.Namespace;
        enumDef.Access = Enum.Parse<AccessModifier>(typeData.Access);
        enumDef.UnderlyingType = TypeReference.FromName(typeData.UnderlyingType ?? "int32");
        // Note: Members are not serialized
        return enumDef;
    }

    private static FunctionDefinition LoadFunction(FunctionData funcData)
    {
        var func = new FunctionDefinition(funcData.Name, TypeReference.FromName(funcData.ReturnType));

        // Load parameters
        foreach (var paramData in funcData.Parameters)
        {
            func.DefineParameter(paramData.Name, TypeReference.FromName(paramData.Type));
        }

        // Load locals
        foreach (var localData in funcData.LocalVariables)
        {
            func.DefineLocal(localData.Name, TypeReference.FromName(localData.Type));
        }

        if (funcData.Instructions is JsonArray instructionsNode)
        {
            using var instructionsDoc = JsonDocument.Parse(instructionsNode.ToJsonString());
            var instructions = InstructionSerializer.DeserializeInstructions(instructionsDoc.RootElement);
            foreach (var instruction in instructions)
            {
                func.Instructions.Add(instruction);
            }
        }

        // Note: Instructions are not loaded if missing
        return func;
    }

    private static void LoadMethodData(MethodDefinition method, MethodData methodData)
    {
        method.Access = Enum.Parse<AccessModifier>(methodData.Access);
        method.IsStatic = methodData.IsStatic;
        method.IsVirtual = methodData.IsVirtual;
        method.IsOverride = methodData.IsOverride;
        method.IsAbstract = methodData.IsAbstract;
        method.IsConstructor = methodData.IsConstructor;

        // Load parameters
        foreach (var paramData in methodData.Parameters)
        {
            method.DefineParameter(paramData.Name, TypeReference.FromName(paramData.Type));
        }

        // Load locals
        foreach (var localData in methodData.LocalVariables)
        {
            method.DefineLocal(localData.Name, TypeReference.FromName(localData.Type));
        }

        if (methodData.Instructions is JsonArray instructionsNode)
        {
            using var instructionsDoc = JsonDocument.Parse(instructionsNode.ToJsonString());
            var instructions = InstructionSerializer.DeserializeInstructions(instructionsDoc.RootElement);
            foreach (var instruction in instructions)
            {
                method.Instructions.Add(instruction);
            }
        }

        // Instructions remain empty if missing for compatibility
    }

    /// <summary>
    /// Dumps the module as FOB (Finite Open Bytecode) binary format
    /// </summary>
    public byte[] DumpToFOB()
    {
        // Serialize the module IR to a FOB/IR v3 payload, then wrap it with the
        // standard "FOB/IR" file header using FobIrCompiler.
        var payload  = ObjectIR.Core.IR.ModuleBinaryWriter.Write(_module);
        var compiler = new ObjectIR.FobCompiler.FobIrCompiler();
        return compiler.CompileFromPayload(payload);
    }

    private long WriteFOBHeader(BinaryWriter writer, List<string> strings, TypeData[] types)
    {
        // Magic: "FOB"
        writer.Write((byte)'F');
        writer.Write((byte)'O');
        writer.Write((byte)'B');

        // Fork name length and name: "OBJECTIR,FOB"
        string forkName = "OBJECTIR,FOB";
        writer.Write((byte)forkName.Length);
        writer.Write(Encoding.UTF8.GetBytes(forkName));

        // File size placeholder (will be updated at end)
        long fileSizePos = writer.BaseStream.Position;
        writer.Write(0u); // fileSize

        // Entry point - try to find from metadata, otherwise use -1
        uint entryPoint = CalculateEntryPoint(_module, types, strings);
        writer.Write(entryPoint);

        return fileSizePos;
    }

    private uint CalculateEntryPoint(Module module, TypeData[] types, List<string> strings)
    {
        // Try to get entry point from metadata
        if (module.Metadata.TryGetValue("EntryPoint", out var entryMetadata) && entryMetadata is string entryString)
        {
            Console.WriteLine($"DEBUG: EntryPoint metadata: {entryString}");
            // If metadata contains a dot, treat as Type.Method qualified name
            int dotIndex = entryString.LastIndexOf('.');
            if (dotIndex > 0)
            {
                string className = entryString[..dotIndex];
                string methodName = entryString[(dotIndex + 1)..];
                Console.WriteLine($"DEBUG: Looking for class '{className}', method '{methodName}'");

                // Find type index
                for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
                {
                    var type = types[typeIndex];
                    string qualifiedName = string.IsNullOrEmpty(type.Namespace) ? type.Name : $"{type.Namespace}.{type.Name}";
                    Console.WriteLine($"DEBUG: Checking type {typeIndex}: qualifiedName='{qualifiedName}', Name='{type.Name}', Namespace='{type.Namespace}'");
                    if (qualifiedName == className)
                    {
                        Console.WriteLine($"DEBUG: Found matching type at index {typeIndex}");
                        // Find method index within this type
                        if (type.Methods != null)
                        {
                            for (int methodIndex = 0; methodIndex < type.Methods.Length; methodIndex++)
                            {
                                var method = type.Methods[methodIndex];
                                Console.WriteLine($"DEBUG: Checking method {methodIndex}: Name='{method.Name}'");
                                if (method.Name == methodName)
                                {
                                    Console.WriteLine($"DEBUG: Found matching method at index {methodIndex}");
                                    // Entry point is encoded as (type_index << 16) | method_index
                                    return (uint)((typeIndex << 16) | methodIndex);
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                // Unqualified name — try to find a top-level function first
                var functions = module.Functions;
                for (int fi = 0; fi < functions.Count; fi++)
                {
                    if (functions[fi].Name == entryString)
                    {
                        Console.WriteLine($"DEBUG: Found free function entry point '{entryString}' at index {fi}");
                        // Encode function entry as 0xFFFF << 16 | function_index
                        return (uint)((0xFFFF << 16) | (uint)fi);
                    }
                }

                // If not found as free function, try unqualified method name across types
                for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
                {
                    var type = types[typeIndex];
                    if (type.Methods == null) continue;
                    for (int methodIndex = 0; methodIndex < type.Methods.Length; methodIndex++)
                    {
                        if (type.Methods[methodIndex].Name == entryString)
                        {
                            Console.WriteLine($"DEBUG: Found method entry point {entryString} at type {typeIndex} method {methodIndex}");
                            return (uint)((typeIndex << 16) | methodIndex);
                        }
                    }
                }
            }
        }

        // If metadata wasn't set or lookup failed, try sensible defaults: free function `main` or method `Main`/`main`.
        // Look for top-level `main`
        for (int fi = 0; fi < module.Functions.Count; fi++)
        {
            if (module.Functions[fi].Name == "main")
            {
                Console.WriteLine("DEBUG: Defaulting entry point to free function 'main'");
                return (uint)((0xFFFF << 16) | (uint)fi);
            }
        }

        // Look for methods named 'main' or 'Main'
        for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
        {
            var type = types[typeIndex];
            if (type.Methods == null) continue;
            for (int methodIndex = 0; methodIndex < type.Methods.Length; methodIndex++)
            {
                var name = type.Methods[methodIndex].Name;
                if (name == "main" || name == "Main")
                {
                    Console.WriteLine($"DEBUG: Defaulting entry point to method {name} on type index {typeIndex}");
                    return (uint)((typeIndex << 16) | methodIndex);
                }
            }
        }

        Console.WriteLine("DEBUG: No valid entry point found");
        return 0xFFFFFFFFu;
    }

    private void WriteStringsSection(BinaryWriter writer, List<string> strings)
    {
        // Section name and null terminator
        writer.Write(Encoding.UTF8.GetBytes(".strings"));
        writer.Write((byte)0);

        // Size placeholder position
        long sizePos = writer.BaseStream.Position;
        writer.Write((uint)0); // Placeholder

        long sectionStart = writer.BaseStream.Position;

        // String count
        writer.Write(strings.Count);

        // Strings
        foreach (var str in strings)
        {
            var bytes = Encoding.UTF8.GetBytes(str);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        // Update section size
        long sectionEnd = writer.BaseStream.Position;
        UpdateSectionSize(writer, sizePos, (uint)(sectionEnd - sectionStart));
    }

    private void WriteTypesSection(BinaryWriter writer, TypeData[] types, Dictionary<string, int> stringIndices, Dictionary<TypeData, int> typeIndices)
    {
        // Section name and null terminator
        writer.Write(Encoding.UTF8.GetBytes(".types"));
        writer.Write((byte)0);

        // Size placeholder position
        long sizePos = writer.BaseStream.Position;
        writer.Write((uint)0); // Placeholder

        long sectionStart = writer.BaseStream.Position;

        // Type count
        writer.Write(types.Length);

        foreach (var type in types)
        {
            WriteTypeDefinition(writer, type, stringIndices, typeIndices);
        }

        // Update section size
        long sectionEnd = writer.BaseStream.Position;
        UpdateSectionSize(writer, sizePos, (uint)(sectionEnd - sectionStart));
    }

    private void WriteTypeDefinition(BinaryWriter writer, TypeData type, Dictionary<string, int> stringIndices, Dictionary<TypeData, int> typeIndices)
    {
        // Kind (0x01 = Class, 0x02 = Interface)
        writer.Write(type.Kind == "class" ? (byte)0x01 : (byte)0x02);

        // Name and namespace indices
        writer.Write(stringIndices[type.Name ?? ""]);
        writer.Write(stringIndices[type.Namespace ?? ""]);

        // Access (default to public)
        writer.Write((byte)0x01);

        // Flags
        byte flags = 0;
        if (type.IsAbstract) flags |= 0x01;
        if (type.IsSealed) flags |= 0x02;
        writer.Write(flags);

        // Base type index (0xFFFFFFFF for none)
        writer.Write(0xFFFFFFFFu);

        // Interface count (0 for now)
        writer.Write(0);

        // Field count and fields
        var fields = type.Fields ?? Array.Empty<FieldData>();
        writer.Write(fields.Length);
        foreach (var field in fields)
        {
            writer.Write(stringIndices[field.Name ?? ""]);
            writer.Write(0u); // type index (placeholder)
            writer.Write((byte)0x01); // access
            writer.Write((byte)0); // flags
        }

        // Method count and methods
        var methods = type.Methods ?? Array.Empty<MethodData>();
        writer.Write(methods.Length);
        foreach (var method in methods)
        {
            WriteMethodDefinition(writer, method, stringIndices);
        }
    }

    private void WriteMethodDefinition(BinaryWriter writer, MethodData method, Dictionary<string, int> stringIndices)
    {
        writer.Write(stringIndices[method.Name ?? ""]);
        writer.Write(0u); // return type index (placeholder)
        writer.Write((byte)0x01); // access
        writer.Write((byte)0); // flags

        // Parameters
        var parameters = method.Parameters ?? Array.Empty<ParameterData>();
        writer.Write(parameters.Length);
        foreach (var param in parameters)
        {
            writer.Write(stringIndices[param.Name ?? ""]);
            writer.Write(0u); // type index (placeholder)
        }

        // Locals (empty for now)
        writer.Write(0);

        // Instructions (store offset and count)
        var instructionCount = method.InstructionCount;
        writer.Write(instructionCount);
        for (int i = 0; i < instructionCount; i++)
        {
            writer.Write(0u); // instruction offset (placeholder)
        }
    }

    private void WriteCodeSection(BinaryWriter writer, ModuleData moduleData)
    {
        // Section name and null terminator
        writer.Write(Encoding.UTF8.GetBytes(".code"));
        writer.Write((byte)0);

        // Size placeholder position
        long sizePos = writer.BaseStream.Position;
        writer.Write((uint)0); // Placeholder

        long sectionStart = writer.BaseStream.Position;

        // Collect all instructions from methods and functions
        var allInstructions = new List<Instruction>();
        CollectAllInstructions(moduleData, allInstructions);

        // Instruction count
        writer.Write(allInstructions.Count);

        // Serialize instructions
        foreach (var instruction in allInstructions)
        {
            WriteInstruction(writer, instruction);
        }

        // Update section size
        long sectionEnd = writer.BaseStream.Position;
        UpdateSectionSize(writer, sizePos, (uint)(sectionEnd - sectionStart));
    }

    private void CollectAllInstructions(ModuleData moduleData, List<Instruction> instructions)
    {
        // Collect from types
        foreach (var type in moduleData.Types ?? Array.Empty<TypeData>())
        {
            foreach (var method in type.Methods ?? Array.Empty<MethodData>())
            {
                if (method.Instructions != null)
                {
                    var methodInstructions = DeserializeInstructions(method.Instructions);
                    instructions.AddRange(methodInstructions);
                }
            }
        }

        // Collect from functions
        foreach (var function in moduleData.Functions ?? Array.Empty<FunctionData>())
        {
            if (function.Instructions != null)
            {
                var functionInstructions = DeserializeInstructions(function.Instructions);
                instructions.AddRange(functionInstructions);
            }
        }
    }

    private List<Instruction> DeserializeInstructions(JsonNode instructionsNode)
    {
        if (instructionsNode == null)
            return new List<Instruction>();

        try
        {
            // Convert JsonNode to JsonElement for InstructionSerializer
            using var doc = JsonDocument.Parse(instructionsNode.ToJsonString());
            var instructions = InstructionSerializer.DeserializeInstructions(doc.RootElement);
            return new List<Instruction>(instructions);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Failed to deserialize instructions: {ex.Message}");
            return new List<Instruction>();
        }
    }

    private void WriteInstruction(BinaryWriter writer, Instruction instruction)
    {
        // Write opcode (simplified - need proper opcode mapping)
        writer.Write((byte)MapOpCodeToByte(instruction.OpCode));

        // Write operand count (simplified)
        writer.Write((byte)GetOperandCount(instruction));

        // Write operands (simplified)
        WriteOperands(writer, instruction);
    }

    private byte MapOpCodeToByte(OpCode opCode)
    {
        // Simplified mapping - need proper implementation
        return opCode switch
        {
            OpCode.Ldarg => 0x01,
            OpCode.Ldloc => 0x02,
            OpCode.Ldfld => 0x03,
            OpCode.Ldsfld => 0x04,
            OpCode.LdcI4 => 0x05,
            OpCode.LdcI8 => 0x06,
            OpCode.LdcR4 => 0x07,
            OpCode.LdcR8 => 0x08,
            OpCode.Ldstr => 0x09,
            OpCode.Ldnull => 0x0A,
            OpCode.Starg => 0x0B,
            OpCode.Stloc => 0x0C,
            OpCode.Stfld => 0x0D,
            OpCode.Stsfld => 0x0E,
            OpCode.Add => 0x0F,
            OpCode.Sub => 0x10,
            OpCode.Mul => 0x11,
            OpCode.Div => 0x12,
            OpCode.Rem => 0x13,
            OpCode.Ceq => 0x14,
            OpCode.Cgt => 0x15,
            OpCode.Clt => 0x16,
            OpCode.Call => 0x17,
            OpCode.Callvirt => 0x18,
            OpCode.Newobj => 0x19,
            OpCode.Newarr => 0x1A,
            OpCode.Ret => 0x1B,
            OpCode.Dup => 0x1C,
            OpCode.Pop => 0x1D,
            _ => 0x00 // Unknown
        };
    }

    private byte GetOperandCount(Instruction instruction)
    {
        // Simplified - need proper implementation based on instruction type
        return instruction switch
        {
            LoadArgInstruction => 1,
            LoadLocalInstruction => 1,
            LoadConstantInstruction => 1,
            LoadNullInstruction => 0,
            StoreArgInstruction => 1,
            StoreLocalInstruction => 1,
            ArithmeticInstruction => 0,
            ComparisonInstruction => 0,
            ReturnInstruction => 0,
            DupInstruction => 0,
            PopInstruction => 0,
            _ => 0
        };
    }

    private void WriteOperands(BinaryWriter writer, Instruction instruction)
    {
        // Simplified operand writing - need proper implementation
        switch (instruction)
        {
            case LoadArgInstruction lai:
                writer.Write((byte)0x01); // String index type
                writer.Write(0u); // Placeholder string index
                break;
            case LoadLocalInstruction lli:
                writer.Write((byte)0x01); // String index type
                writer.Write(0u); // Placeholder string index
                break;
            case LoadConstantInstruction lci:
                WriteConstantOperand(writer, lci.Value, lci.Type);
                break;
            // Add other cases as needed
        }
    }

    private void WriteConstantOperand(BinaryWriter writer, object value, TypeReference type)
    {
        if (type == TypeReference.String)
        {
            writer.Write((byte)0x01); // String index type
            writer.Write(0u); // Placeholder string index
        }
        else if (type == TypeReference.Int32)
        {
            writer.Write((byte)0x02); // Integer type
            writer.Write((int)value);
        }
        else if (type == TypeReference.Int64)
        {
            writer.Write((byte)0x02); // Integer type
            writer.Write((long)value);
        }
        else if (type == TypeReference.Float32)
        {
            writer.Write((byte)0x03); // Double type
            writer.Write((double)(float)value);
        }
        else if (type == TypeReference.Float64)
        {
            writer.Write((byte)0x03); // Double type
            writer.Write((double)value);
        }
        else if (type == TypeReference.Bool)
        {
            writer.Write((byte)0x06); // Bool type
            writer.Write((bool)value);
        }
    }

    private void WriteConstantsSection(BinaryWriter writer, ModuleData moduleData)
    {
        // Section name and null terminator
        writer.Write(Encoding.UTF8.GetBytes(".constants"));
        writer.Write((byte)0);

        // Size placeholder position
        long sizePos = writer.BaseStream.Position;
        writer.Write((uint)0); // Placeholder

        long sectionStart = writer.BaseStream.Position;

        // Collect all constants from instructions
        var constants = new List<ConstantData>();
        CollectConstants(moduleData, constants);

        // Constant count
        writer.Write(constants.Count);

        // Serialize constants
        foreach (var constant in constants)
        {
            WriteConstant(writer, constant);
        }

        // Update section size
        long sectionEnd = writer.BaseStream.Position;
        UpdateSectionSize(writer, sizePos, (uint)(sectionEnd - sectionStart));
    }

    private void CollectConstants(ModuleData moduleData, List<ConstantData> constants)
    {
        // Collect from types
        foreach (var type in moduleData.Types ?? Array.Empty<TypeData>())
        {
            foreach (var method in type.Methods ?? Array.Empty<MethodData>())
            {
                if (method.Instructions != null)
                {
                    CollectConstantsFromInstructions(method.Instructions, constants);
                }
            }
        }

        // Collect from functions
        foreach (var function in moduleData.Functions ?? Array.Empty<FunctionData>())
        {
            if (function.Instructions != null)
            {
                CollectConstantsFromInstructions(function.Instructions, constants);
            }
        }
    }

    private void CollectConstantsFromInstructions(JsonNode instructionsNode, List<ConstantData> constants)
    {
        if (instructionsNode is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is not null)
                {
                    CollectConstantsFromJsonInstruction(item, constants);
                }
            }
        }
    }

    private void CollectConstantsFromJsonInstruction(JsonNode instrNode, List<ConstantData> constants)
    {
        if (instrNode is not JsonObject obj)
            return;

        var opCode = obj["opCode"]?.GetValue<string>();
        
        // Collect constants from ldc instructions
        if (opCode == "ldc" && obj["operand"] is JsonObject operand)
        {
            var value = operand["value"]?.GetValue<string>();
            var type = operand["type"]?.GetValue<string>();
            if (value != null && type != null)
            {
                constants.Add(new ConstantData
                {
                    Value = value,
                    Type = type
                });
            }
        }
        
        // Recursively collect from nested instructions (e.g., while body)
        if (obj["operand"] is JsonObject nestedOp && nestedOp["body"] is JsonArray bodyInstructions)
        {
            foreach (var bodyInstr in bodyInstructions)
            {
                if (bodyInstr is not null)
                {
                    CollectConstantsFromJsonInstruction(bodyInstr, constants);
                }
            }
        }
    }

    private void WriteConstant(BinaryWriter writer, ConstantData constant)
    {
        // Normalize type name for comparison
        var typeName = constant.Type.ToLowerInvariant();
        
        // Write type
        var typeByte = typeName switch
        {
            "system.string" or "string" => (byte)0x05, // String
            "system.int32" or "int32" => (byte)0x01, // Int32
            "system.int64" or "int64" => (byte)0x02, // Int64
            "system.single" or "float32" or "float" => (byte)0x03, // Float
            "system.double" or "float64" or "double" => (byte)0x04, // Double
            "system.boolean" or "bool" => (byte)0x06, // Bool
            _ => (byte)0x07 // Null
        };
        
        writer.Write(typeByte);

        // Write value based on type
        switch (typeByte)
        {
            case 0x01: // Int32
                if (int.TryParse(constant.Value, out var intValue))
                    writer.Write(intValue);
                else
                    writer.Write(0);
                break;
            case 0x02: // Int64
                if (long.TryParse(constant.Value, out var longValue))
                    writer.Write(longValue);
                else
                    writer.Write(0L);
                break;
            case 0x03: // Float
                if (float.TryParse(constant.Value, out var floatValue))
                    writer.Write(floatValue);
                else
                    writer.Write(0.0f);
                break;
            case 0x04: // Double
                if (double.TryParse(constant.Value, out var doubleValue))
                    writer.Write(doubleValue);
                else
                    writer.Write(0.0);
                break;
            case 0x05: // String
                var bytes = Encoding.UTF8.GetBytes(constant.Value);
                writer.Write(bytes.Length);
                writer.Write(bytes);
                break;
            case 0x06: // Bool
                if (bool.TryParse(constant.Value, out var boolValue))
                    writer.Write(boolValue);
                else
                    writer.Write(false);
                break;
            case 0x07: // Null
                // No value to write
                break;
        }
    }

    private sealed class ConstantData
    {
        public string Value { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }

    private void WriteSectionHeader(BinaryWriter writer, string name, uint size)
    {
        // Section name (null-terminated)
        writer.Write(Encoding.UTF8.GetBytes(name));
        writer.Write((byte)0);

        // Size
        writer.Write(size);
    }

    private void UpdateSectionSize(BinaryWriter writer, long sizePos, uint size)
    {
        long currentPos = writer.BaseStream.Position;
        writer.BaseStream.Position = sizePos;
        writer.Write(size);
        writer.BaseStream.Position = currentPos;
    }

    private List<string> BuildStringTable(ModuleData moduleData)
    {
        var strings = new HashSet<string>();

        // Ensure empty string is present so lookups using "" don't fail
        strings.Add(string.Empty);

        // Add module name
        if (!string.IsNullOrEmpty(moduleData.Name))
            strings.Add(moduleData.Name);

        // Add type names and namespaces
        foreach (var type in moduleData.Types ?? Array.Empty<TypeData>())
        {
            if (!string.IsNullOrEmpty(type.Name)) strings.Add(type.Name);
            if (!string.IsNullOrEmpty(type.Namespace)) strings.Add(type.Namespace);

            // Add field names
            foreach (var field in type.Fields ?? Array.Empty<FieldData>())
            {
                if (!string.IsNullOrEmpty(field.Name)) strings.Add(field.Name);
            }

            // Add method names and parameters
            foreach (var method in type.Methods ?? Array.Empty<MethodData>())
            {
                if (!string.IsNullOrEmpty(method.Name)) strings.Add(method.Name);

                foreach (var param in method.Parameters ?? Array.Empty<ParameterData>())
                {
                    if (!string.IsNullOrEmpty(param.Name)) strings.Add(param.Name);
                }
            }
        }

        return strings.ToList();
    }

    /// <summary>
    /// Dumps the module as BSON (binary JSON) for smaller file sizes
    /// </summary>
    public byte[] DumpToBson()
    {
        var data = DumpModule();
        var bsonDoc = BsonSerializer.ModuleDataToBson(data);
        return bsonDoc.ToBson();
    }

    /// <summary>
    /// Loads a module from BSON data
    /// </summary>
    public static Module LoadFromBson(byte[] bsonData)
    {
        using var stream = new MemoryStream(bsonData);
        using var reader = new BsonBinaryReader(stream);
        var context = BsonDeserializationContext.CreateRoot(reader);
        var bsonDoc = BsonDocumentSerializer.Instance.Deserialize(context);
        var moduleData = BsonSerializer.BsonToModuleData(bsonDoc);
        return LoadModule(moduleData);
    }

    public static string ToJson(Module module)
    {
        var serializer = new ModuleSerializer(module);
        return serializer.DumpToJson();
    }

    /// <summary>
    /// Converts a module to BSON format
    /// </summary>
    public static byte[] ToBson(Module module)
    {
        var serializer = new ModuleSerializer(module);
        return serializer.DumpToBson();
    }

    // ============================================================================
    // IR Code Dumping Methods
    // ============================================================================

    private void DumpTypeAsIRCode(StringBuilder sb, TypeDefinition type)
    {
        if (type is InterfaceDefinition interfaceDef)
        {
            DumpInterfaceAsIRCode(sb, interfaceDef);
        }
        else if (type is ClassDefinition classDef)
        {
            DumpClassAsIRCode(sb, classDef);
        }
    }

    private void DumpInterfaceAsIRCode(StringBuilder sb, InterfaceDefinition interfaceDef)
    {
        sb.AppendLine($"// {interfaceDef.Name} interface");
        sb.AppendLine($"interface {interfaceDef.Name} {{");

        foreach (var method in interfaceDef.Methods)
        {
            var returnType = method.ReturnType.GetQualifiedName();
            var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Name}: {p.Type.GetQualifiedName()}"));
            sb.AppendLine($"    method {method.Name}({parameters}) -> {returnType}");
        }

        sb.AppendLine("}");
    }

    private void DumpClassAsIRCode(StringBuilder sb, ClassDefinition classDef)
    {
        sb.AppendLine($"// {classDef.Name} class");
        var inheritance = new List<string>();
        if (classDef.BaseType != null)
        {
            inheritance.Add(classDef.BaseType.GetQualifiedName());
        }
        inheritance.AddRange(classDef.Interfaces.Select(i => i.GetQualifiedName()));

        var inheritanceStr = inheritance.Count > 0 ? $" : {string.Join(", ", inheritance)}" : "";
        sb.AppendLine($"class {classDef.Name}{inheritanceStr} {{");

        // Fields
        foreach (var field in classDef.Fields)
        {
            var access = field.Access.ToString().ToLower();
            sb.AppendLine($"    {access} field {field.Name}: {field.Type.GetQualifiedName()}");
        }

        if (classDef.Fields.Count > 0 && classDef.Methods.Count > 0)
        {
            sb.AppendLine();
        }

        // Methods
        foreach (var method in classDef.Methods)
        {
            DumpMethodAsIRCode(sb, method, classDef);
            if (method != classDef.Methods.Last())
            {
                sb.AppendLine();
            }
        }

        sb.AppendLine("}");
    }

    private void DumpMethodAsIRCode(StringBuilder sb, MethodDefinition method, ClassDefinition declaringClass)
    {
        var methodName = method.IsConstructor ? "constructor" : "method";
        var returnType = method.ReturnType.GetQualifiedName();
        var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Name}: {p.Type.GetQualifiedName()}"));

        var implements = "";
        if (method.ImplementsInterface != null)
        {
            implements = $" implements {method.ImplementsInterface.DeclaringType.GetQualifiedName()}.{method.ImplementsInterface.Name}";
        }

        sb.AppendLine($"    {methodName} {method.Name}({parameters}) -> {returnType}{implements} {{");

        // Set current method context for pretty-printing inside DumpInstructionAsIRCode
        _currentMethodBeingDumped = method;

        // Locals
        foreach (var local in method.Locals)
        {
            sb.AppendLine($"        local {local.Name}: {local.Type.GetQualifiedName()}");
        }

        if (method.Locals.Count > 0 && method.Instructions.Count > 0)
        {
            sb.AppendLine();
        }

        // Instructions
        for (int i = 0; i < method.Instructions.Count; i++)
        {
            // Look ahead for the common pattern: <setup instr> <setup instr> while <cond>
            if (i + 2 < method.Instructions.Count && method.Instructions[i + 2] is WhileInstruction whileLookahead)
            {
                var first = method.Instructions[i];
                var second = method.Instructions[i + 1];

                if (IsSimpleLoad(first) && IsSimpleLoad(second))
                {
                    // Render a compact condition with expanded body: show operand values (e.g. "while (y < 200) { ... }")
                    var leftText = DumpInstructionAsIRCode(first);
                    var rightText = DumpInstructionAsIRCode(second);
                    string leftOp = RenderOperandFromLoadText(leftText);
                    string rightOp = RenderOperandFromLoadText(rightText);
                    var cond = FormatCondition(whileLookahead.Condition);

                    sb.AppendLine($"        while ({leftOp} {cond} {rightOp}) {{");
                    for (int biIndex = 0; biIndex < whileLookahead.Body.Count; biIndex++)
                    {
                        var bi = whileLookahead.Body[biIndex];
                        // nested lookahead inside the while body
                        if (biIndex + 2 < whileLookahead.Body.Count && whileLookahead.Body[biIndex + 2] is WhileInstruction nestedLookahead)
                        {
                            var firstN = whileLookahead.Body[biIndex];
                            var secondN = whileLookahead.Body[biIndex + 1];
                            if (IsSimpleLoad(firstN) && IsSimpleLoad(secondN))
                            {
                                var leftTextN = DumpInstructionAsIRCode(firstN);
                                var rightTextN = DumpInstructionAsIRCode(secondN);
                                string leftOpN = RenderOperandFromLoadText(leftTextN);
                                string rightOpN = RenderOperandFromLoadText(rightTextN);
                                var condN = FormatCondition(nestedLookahead.Condition);
                                sb.AppendLine($"            while ({leftOpN} {condN} {rightOpN}) {{");
                                for (int inner = 0; inner < nestedLookahead.Body.Count; inner++)
                                {
                                    DumpInstructionBlock(sb, nestedLookahead.Body[inner], indentLevel: 4);
                                }
                                sb.AppendLine("            }");
                                biIndex += 2;
                                continue;
                            }
                        }

                        DumpInstructionBlock(sb, bi, indentLevel: 3);
                    }
                    sb.AppendLine("        }");

                    i += 2; // skip the two we've consumed
                    continue;
                }
            }

            // Route all structured control-flow instructions through DumpInstructionBlock
            if (method.Instructions[i] is WhileInstruction or IfInstruction or ForEachInstruction)
            {
                DumpInstructionBlock(sb, method.Instructions[i], indentLevel: 2); // method body indentation
                continue;
            }

            sb.AppendLine($"        {DumpInstructionAsIRCode(method.Instructions[i])}");
        }

        sb.AppendLine("    }");

        _currentMethodBeingDumped = null;
    }

    private void DumpFunctionAsIRCode(StringBuilder sb, FunctionDefinition func)
    {
        var returnType = func.ReturnType.GetQualifiedName();
        var parameters = string.Join(", ", func.Parameters.Select(p => $"{p.Name}: {p.Type.GetQualifiedName()}"));

        sb.AppendLine($"// {func.Name} function");
        sb.AppendLine($"function {func.Name}({parameters}) -> {returnType} {{");

        // Locals
        foreach (var local in func.Locals)
        {
            sb.AppendLine($"    local {local.Name}: {local.Type.GetQualifiedName()}");
        }

        if (func.Locals.Count > 0 && func.Instructions.Count > 0)
        {
            sb.AppendLine();
        }

        // Instructions
        for (int i = 0; i < func.Instructions.Count; i++)
        {
            // Look ahead for the common pattern: <setup instr> <setup instr> while <cond>
            if (i + 2 < func.Instructions.Count && func.Instructions[i + 2] is WhileInstruction whileLookahead)
            {
                var first = func.Instructions[i];
                var second = func.Instructions[i + 1];

                if (IsSimpleLoad(first) && IsSimpleLoad(second))
                {
                    // Render a compact condition with expanded body
                    var leftText = DumpInstructionAsIRCode(first);
                    var rightText = DumpInstructionAsIRCode(second);
                    string leftOp = RenderOperandFromLoadText(leftText);
                    string rightOp = RenderOperandFromLoadText(rightText);
                    var cond = FormatCondition(whileLookahead.Condition);

                    sb.AppendLine($"    while ({leftOp} {cond} {rightOp}) {{");
                    for (int biIndex = 0; biIndex < whileLookahead.Body.Count; biIndex++)
                    {
                        var bi = whileLookahead.Body[biIndex];
                        if (biIndex + 2 < whileLookahead.Body.Count && whileLookahead.Body[biIndex + 2] is WhileInstruction nestedLookahead)
                        {
                            var firstN = whileLookahead.Body[biIndex];
                            var secondN = whileLookahead.Body[biIndex + 1];
                            if (IsSimpleLoad(firstN) && IsSimpleLoad(secondN))
                            {
                                var leftTextN = DumpInstructionAsIRCode(firstN);
                                var rightTextN = DumpInstructionAsIRCode(secondN);
                                string leftOpN = RenderOperandFromLoadText(leftTextN);
                                string rightOpN = RenderOperandFromLoadText(rightTextN);
                                var condN = FormatCondition(nestedLookahead.Condition);
                                sb.AppendLine($"        while ({leftOpN} {condN} {rightOpN}) {{");
                                for (int inner = 0; inner < nestedLookahead.Body.Count; inner++)
                                {
                                    DumpInstructionBlock(sb, nestedLookahead.Body[inner], indentLevel: 3);
                                }
                                sb.AppendLine("        }");
                                biIndex += 2;
                                continue;
                            }
                        }

                        DumpInstructionBlock(sb, bi, indentLevel: 2);
                    }
                    sb.AppendLine("    }");

                    i += 2; // skip the two we've consumed
                    continue;
                }
            }

            // Route all structured control-flow instructions through DumpInstructionBlock
            if (func.Instructions[i] is WhileInstruction or IfInstruction or ForEachInstruction)
            {
                DumpInstructionBlock(sb, func.Instructions[i], indentLevel: 1); // function indentation
                continue;
            }

            sb.AppendLine($"    {DumpInstructionAsIRCode(func.Instructions[i])}");
        }

        sb.AppendLine("}");
    }

    private string DumpInstructionAsIRCode(Instruction instruction)
    {
        // This is a simplified version - in practice, you'd need to handle all instruction types
        // For now, we'll use the OpCode name and basic operand formatting
        var opCode = instruction.OpCode.ToString().ToLower();

        // Handle some common instructions with special formatting
        switch (instruction.OpCode)
        {
            case OpCode.Ldarg:
                if (instruction is LoadArgInstruction loadArg)
                {
                    if (loadArg.Name != null)
                        return $"ldarg {loadArg.Name}";
                    // Pretty-print `ldarg 0` as `ldarg this` when dumping an instance method
                    if (loadArg.Index == 0 && _currentMethodBeingDumped != null && !_currentMethodBeingDumped.IsStatic)
                        return "ldarg this";
                    return $"ldarg {loadArg.Index}";
                }
                break;

            case OpCode.Ldloc:
                if (instruction is LoadLocalInstruction loadLocal)
                {
                    return $"ldloc {loadLocal.LocalName}";
                }
                break;

            case OpCode.Stloc:
                if (instruction is StoreLocalInstruction storeLocal)
                {
                    return $"stloc {storeLocal.LocalName}";
                }
                break;

            case OpCode.Ldfld:
                if (instruction is LoadFieldInstruction loadField)
                {
                    return $"ldfld {loadField.Field.DeclaringType.GetQualifiedName()}.{loadField.Field.Name}";
                }
                break;

            case OpCode.Stfld:
                if (instruction is StoreFieldInstruction storeField)
                {
                    return $"stfld {storeField.Field.DeclaringType.GetQualifiedName()}.{storeField.Field.Name}";
                }
                break;

            case OpCode.Ldsfld:
                if (instruction is LoadStaticFieldInstruction loadStaticField)
                {
                    return $"ldsfld {loadStaticField.Field.DeclaringType.GetQualifiedName()}.{loadStaticField.Field.Name}";
                }
                break;

            case OpCode.Stsfld:
                if (instruction is StoreStaticFieldInstruction storeStaticField)
                {
                    return $"stsfld {storeStaticField.Field.DeclaringType.GetQualifiedName()}.{storeStaticField.Field.Name}";
                }
                break;

            case OpCode.LdcI4:
            case OpCode.LdcI8:
            case OpCode.LdcR4:
            case OpCode.LdcR8:
            case OpCode.Ldstr:
                if (instruction is LoadConstantInstruction loadConst)
                {
                    if (loadConst.Value is string str)
                        return $"ldstr \"{str}\"";
                    else if (loadConst.Value is int i32)
                        return $"ldc.i4 {i32}";
                    else if (loadConst.Value is long i64)
                        return $"ldc.i8 {i64}";
                    else if (loadConst.Value is float f32)
                        return $"ldc.r4 {f32}";
                    else if (loadConst.Value is double f64)
                        return $"ldc.r8 {f64}";
                    else if (loadConst.Value is bool b)
                        return $"ldc.i4 {(b ? 1 : 0)}  // {b}";
                }
                break;

            case OpCode.If:
                // IfInstruction is normally rendered via DumpInstructionBlock; this is a safety fallback.
                if (instruction is IfInstruction ifInstr)
                {
                    var cond = FormatCondition(ifInstr.Condition);
                    return $"if ({cond}) {{ /* {ifInstr.ThenBlock.Count} then / {ifInstr.ElseBlock?.Count ?? 0} else instrs */ }}";
                }
                break;

            case OpCode.Ldnull:
                return "ldnull";

            case OpCode.Call:
                if (instruction is CallInstruction call)
                {
                    var methodRef = call.Method;
                    var args = string.Join(", ", methodRef.ParameterTypes.Select(t => t.GetQualifiedName()));
                    return $"call {methodRef.DeclaringType.GetQualifiedName()}.{methodRef.Name}({args}) -> {methodRef.ReturnType.GetQualifiedName()}";
                }
                break;

            case OpCode.Callvirt:
                if (instruction is CallVirtualInstruction callVirt)
                {
                    var methodRef = callVirt.Method;
                    var args = string.Join(", ", methodRef.ParameterTypes.Select(t => t.GetQualifiedName()));
                    return $"callvirt {methodRef.DeclaringType.GetQualifiedName()}.{methodRef.Name}({args}) -> {methodRef.ReturnType.GetQualifiedName()}";
                }
                break;

            case OpCode.Newobj:
                if (instruction is NewObjectInstruction newObj)
                {
                    var ctorParams = string.Join(", ", newObj.ParameterTypes.Select(t => t.GetQualifiedName()));
                    return $"newobj {newObj.Type.GetQualifiedName()}.constructor({ctorParams})";
                }
                break;

            case OpCode.Ret:
                return "ret";

            case OpCode.Dup:
                return "dup";

            case OpCode.Pop:
                return "pop";

            case OpCode.Add:
                return "add";

            case OpCode.Sub:
                return "sub";

            case OpCode.Mul:
                return "mul";

            case OpCode.Div:
                return "div";

            case OpCode.Rem:
                return "rem";

            case OpCode.Neg:
                return "neg";

            case OpCode.Ceq:
                return "ceq";

            case OpCode.Cgt:
                return "cgt";

            case OpCode.Clt:
                return "clt";

            case OpCode.While:
                // WhileInstruction is normally rendered via DumpInstructionBlock; this is a safety fallback.
                if (instruction is WhileInstruction whileInstr)
                {
                    var cond = FormatCondition(whileInstr.Condition);
                    return $"while ({cond}) {{ /* {whileInstr.Body.Count} body instrs */ }}";
                }
                break;
            case OpCode.For:
                // ForEachInstruction is normally rendered via DumpInstructionBlock; this is a safety fallback.
                if (instruction is ForEachInstruction feInstr)
                {
                    return $"foreach ({feInstr.ItemName} in {feInstr.CollectionName}) {{ /* {feInstr.Body.Count} body instrs */ }}";
                }
                break;

            case OpCode.Br:
                // Branch instructions would need target labels
                return "br /* target */";

            case OpCode.Brtrue:
                return "brtrue /* target */";

            case OpCode.Brfalse:
                return "brfalse /* target */";
        }

        // Fallback for unhandled instructions
        return $"{opCode}  // TODO: Implement proper formatting";
    }

    private string FormatCondition(Condition condition)
    {
        switch (condition)
        {
            case StackCondition:
                return "stack";
            case BinaryCondition bc:
                return bc.Operation switch
                {
                    ComparisonOp.Equal => "==",
                    ComparisonOp.NotEqual => "!=",
                    ComparisonOp.Less => "<",
                    ComparisonOp.LessOrEqual => "<=",
                    ComparisonOp.Greater => ">",
                    ComparisonOp.GreaterOrEqual => ">=",
                    _ => bc.Operation.ToString()
                };
            case ExpressionCondition:
                return "expression";
            case LogicalCondition lc:
                return lc.Operation switch
                {
                    LogicalOp.And => $"({FormatCondition(lc.Left)} and {FormatCondition(lc.Right!)})",
                    LogicalOp.Or => $"({FormatCondition(lc.Left)} or {FormatCondition(lc.Right!)})",
                    LogicalOp.Not => $"(not {FormatCondition(lc.Left)})",
                    _ => lc.ToString()
                };
            default:
                return condition.ToString();
        }
    }

    // Dump an instruction possibly as multiple lines (handles nested while/if bodies)
    private void DumpInstructionBlock(StringBuilder sb, Instruction instruction, int indentLevel)
    {
        var ind = new string(' ', indentLevel * 4);

        if (instruction is WhileInstruction whileInst)
        {
            var cond = FormatCondition(whileInst.Condition);

            // If condition is a binary operator (e.g. "<") and the two
            // preceding lines we have already emitted are simple loads,
            // remove those two lines and render a combined while header
            // like: "while (x < 200) {". This handles nested cases where
            // the loads were printed before we reached the while.
            if (cond is "==" or "!=" or "<" or "<=" or ">" or ">=")
            {
                // Try to peek last two non-empty lines
                string? last = null;
                string? secondLast = null;
                // Read backwards from the StringBuilder
                int pos = sb.Length - 1;
                // Skip trailing newlines/spaces
                while (pos >= 0 && (sb[pos] == '\n' || sb[pos] == '\r' || sb[pos] == ' ' || sb[pos] == '\t')) pos--;
                if (pos >= 0)
                {
                    // Get last line
                    int lineEnd = pos;
                    int lineStart = sb.ToString().LastIndexOf('\n', pos);
                    lineStart = lineStart >= 0 ? lineStart + 1 : 0;
                    last = sb.ToString(lineStart, lineEnd - lineStart + 1).Trim();

                    // Find second last
                    int prevPos = lineStart - 2; // go before the found newline
                    while (prevPos >= 0 && (sb[prevPos] == '\n' || sb[prevPos] == '\r' || sb[prevPos] == ' ' || sb[prevPos] == '\t')) prevPos--;
                    if (prevPos >= 0)
                    {
                        int lineEnd2 = prevPos;
                        int lineStart2 = sb.ToString().LastIndexOf('\n', prevPos);
                        lineStart2 = lineStart2 >= 0 ? lineStart2 + 1 : 0;
                        secondLast = sb.ToString(lineStart2, lineEnd2 - lineStart2 + 1).Trim();
                    }
                }

                if (!string.IsNullOrEmpty(secondLast) && !string.IsNullOrEmpty(last))
                {
                    // Check both are simple loads
                    if ((secondLast.StartsWith("ldc.i4 ") || secondLast.StartsWith("ldc.i8 ") || secondLast.StartsWith("ldloc ") || secondLast.StartsWith("ldarg ") || secondLast.StartsWith("ldstr "))
                        && (last.StartsWith("ldc.i4 ") || last.StartsWith("ldc.i8 ") || last.StartsWith("ldloc ") || last.StartsWith("ldarg ") || last.StartsWith("ldstr ")))
                    {
                        var leftOp = RenderOperandFromLoadText(secondLast);
                        var rightOp = RenderOperandFromLoadText(last);

                        // Remove the two last lines from sb
                        int removePos = sb.ToString().LastIndexOf('\n');
                        if (removePos >= 0)
                        {
                            // remove last line
                            sb.Remove(removePos + 1, sb.Length - (removePos + 1));
                            // remove second last line
                            removePos = sb.ToString().LastIndexOf('\n');
                            if (removePos >= 0)
                            {
                                sb.Remove(removePos + 1, sb.Length - (removePos + 1));
                            }
                            else
                            {
                                // no previous newline, clear all
                                sb.Clear();
                            }
                        }

                        sb.AppendLine($"{ind}while ({leftOp} {cond} {rightOp}) {{");
                    }
                    else
                    {
                        sb.AppendLine($"{ind}while ({cond}) {{");
                    }
                }
                else
                {
                    sb.AppendLine($"{ind}while ({cond}) {{");
                }
            }
            else
            {
                sb.AppendLine($"{ind}while ({cond}) {{");
            }
            for (int biIndex = 0; biIndex < whileInst.Body.Count; biIndex++)
            {
                var inner = whileInst.Body[biIndex];
                if (biIndex + 2 < whileInst.Body.Count && whileInst.Body[biIndex + 2] is WhileInstruction nestedLookahead)
                {
                    var firstN = whileInst.Body[biIndex];
                    var secondN = whileInst.Body[biIndex + 1];
                    if (IsSimpleLoad(firstN) && IsSimpleLoad(secondN))
                    {
                        var leftTextN = DumpInstructionAsIRCode(firstN);
                        var rightTextN = DumpInstructionAsIRCode(secondN);
                        string leftOpN = RenderOperandFromLoadText(leftTextN);
                        string rightOpN = RenderOperandFromLoadText(rightTextN);
                        var condN = FormatCondition(nestedLookahead.Condition);
                        sb.AppendLine(ind + "    while (" + leftOpN + " " + condN + " " + rightOpN + ") {");
                        for (int innerIdx = 0; innerIdx < nestedLookahead.Body.Count; innerIdx++)
                        {
                            DumpInstructionBlock(sb, nestedLookahead.Body[innerIdx], indentLevel + 2);
                        }
                        sb.AppendLine(ind + "    }");
                        biIndex += 2;
                        continue;
                    }
                }

                // Normal processing of inner instruction
                DumpInstructionBlock(sb, inner, indentLevel + 1);
            }
            sb.AppendLine(ind + "}");
            return;
        }

        if (instruction is ForEachInstruction fe)
        {
            sb.AppendLine($"{ind}foreach ({fe.ItemName} in {fe.CollectionName}) {{");
            foreach (var inner in fe.Body)
            {
                DumpInstructionBlock(sb, inner, indentLevel + 1);
            }
            sb.AppendLine($"{ind}}}");
            return;
        }

        if (instruction is IfInstruction ifInst)
        {
            var cond = FormatCondition(ifInst.Condition);
            sb.AppendLine($"{ind}if ({cond}) {{");
            foreach (var thenInstr in ifInst.ThenBlock)
            {
                DumpInstructionBlock(sb, thenInstr, indentLevel + 1);
            }
            sb.AppendLine($"{ind}}}");
            if (ifInst.ElseBlock != null && ifInst.ElseBlock.Count > 0)
            {
                sb.AppendLine($"{ind}else {{");
                foreach (var elseInstr in ifInst.ElseBlock)
                {
                    DumpInstructionBlock(sb, elseInstr, indentLevel + 1);
                }
                sb.AppendLine($"{ind}}}");
            }
            return;
        }

        // Default: single-line instruction
        sb.AppendLine($"{ind}{DumpInstructionAsIRCode(instruction)}");
    }

    private static bool IsSimpleLoad(Instruction instr)
    {
        return instr.OpCode == OpCode.Ldloc
            || instr.OpCode == OpCode.LdcI4
            || instr.OpCode == OpCode.LdcI8
            || instr.OpCode == OpCode.Ldstr
            || instr.OpCode == OpCode.Ldarg;
    }

    private static string RenderOperandFromLoadText(string loadText)
    {
        if (string.IsNullOrEmpty(loadText))
            return "?";

        // common forms: "ldc.i4 200", "ldloc y", "ldarg 0", "ldstr "foo""
        if (loadText.StartsWith("ldc.i4 "))
            return loadText.Substring("ldc.i4 ".Length);
        if (loadText.StartsWith("ldc.i8 "))
            return loadText.Substring("ldc.i8 ".Length);
        if (loadText.StartsWith("ldloc "))
            return loadText.Substring("ldloc ".Length);
        if (loadText.StartsWith("ldarg "))
            return loadText.Substring("ldarg ".Length);
        if (loadText.StartsWith("ldstr "))
            return loadText.Substring("ldstr ".Length);

        return loadText;
    }
}

// ============================================================================
// Data Transfer Objects
// ============================================================================

/// <summary>
/// Represents serialized module data
/// </summary>
public sealed class ModuleData
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
    public TypeData[] Types { get; set; } = Array.Empty<TypeData>();
    public FunctionData[] Functions { get; set; } = Array.Empty<FunctionData>();
}

/// <summary>
/// Represents serialized type data
/// </summary>
public sealed class TypeData
{
    public string Kind { get; set; } = string.Empty; // "Class", "Interface", "Struct", "Enum"
    public string Name { get; set; } = string.Empty;
    public string? Namespace { get; set; }
    public string Access { get; set; } = string.Empty;
    public bool IsAbstract { get; set; }
    public bool IsSealed { get; set; }
    public string? BaseType { get; set; }
    public string[] Interfaces { get; set; } = Array.Empty<string>();
    public string[] BaseInterfaces { get; set; } = Array.Empty<string>(); // For interfaces
    public string? UnderlyingType { get; set; } // For enums
    public string[] GenericParameters { get; set; } = Array.Empty<string>();
    public FieldData[] Fields { get; set; } = Array.Empty<FieldData>();
    public MethodData[] Methods { get; set; } = Array.Empty<MethodData>();
    public PropertyData[] Properties { get; set; } = Array.Empty<PropertyData>();
}

/// <summary>
/// Represents serialized field data
/// </summary>
public sealed class FieldData
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Access { get; set; } = string.Empty;
    public bool IsStatic { get; set; }
  public bool IsReadOnly { get; set; }
    public string? InitialValue { get; set; }
}

/// <summary>
/// Represents serialized property data
/// </summary>
public sealed class PropertyData
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Access { get; set; } = string.Empty;
    public bool HasGetter { get; set; }
    public bool HasSetter { get; set; }
    public string? GetterAccess { get; set; }
    public string? SetterAccess { get; set; }
}

/// <summary>
/// Represents serialized method data
/// </summary>
public sealed class MethodData
{
  public string Name { get; set; } = string.Empty;
    public string ReturnType { get; set; } = string.Empty;
    public string Access { get; set; } = string.Empty;
    public bool IsStatic { get; set; }
    public bool IsVirtual { get; set; }
    public bool IsOverride { get; set; }
    public bool IsAbstract { get; set; }
    public bool IsConstructor { get; set; }
    public ParameterData[] Parameters { get; set; } = Array.Empty<ParameterData>();
    public LocalVariableData[] LocalVariables { get; set; } = Array.Empty<LocalVariableData>();
    public int InstructionCount { get; set; }
        public JsonNode? Instructions { get; set; }
}

/// <summary>
/// Represents serialized function data
/// </summary>
public sealed class FunctionData
{
    public string Name { get; set; } = string.Empty;
    public string ReturnType { get; set; } = string.Empty;
    public ParameterData[] Parameters { get; set; } = Array.Empty<ParameterData>();
    public LocalVariableData[] LocalVariables { get; set; } = Array.Empty<LocalVariableData>();
    public int InstructionCount { get; set; }
    public JsonNode? Instructions { get; set; }
}

/// <summary>
/// Represents serialized parameter data
/// </summary>
public sealed class ParameterData
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// Represents serialized local variable data
/// </summary>
public sealed class LocalVariableData
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

#pragma warning restore CS1591
