using System.Text;

namespace ObjectIR.Core.IR;

/// <summary>
/// Serializes a <see cref="Module"/> into the FOB/IR v3 binary payload
/// consumed by the C++ runtime <c>fob_loader.cpp</c> <c>ParsePayload()</c>.
/// </summary>
/// <remarks>
/// <para><b>Payload layout (little-endian):</b></para>
/// <code>
/// [StringPool]
///   uint32  count
///   for each: uint32 byteLen + UTF-8 bytes (no null terminator)
///
/// [ModuleHeader]
///   uint32  moduleNameIndex
///   uint16  versionMajor
///   uint16  versionMinor
///   uint16  versionPatch
///   uint32  entryPoint  ((typeIdx &lt;&lt; 16) | methodIdx, or 0xFFFFFFFF)
///
/// [Types]
///   uint32  typeCount
///   for each:
///     uint8   kind        (TypeKind ordinal: Class=0, Interface=1, Struct=2, Enum=3)
///     uint8   access      (AccessModifier ordinal: Public=0, Private=1, Protected=2, Internal=3)
///     uint32  nameIndex
///     uint32  namespaceIndex  (0xFFFFFFFF = none)
///     uint16  typeFlags       (bit0=abstract, bit1=sealed)
///     uint32  baseTypeIndex   (0xFFFFFFFF = none)
///     uint32  interfaceCount, interfaceCount x uint32 nameIndex
///     uint32  fieldCount,  for each: uint32 nameIdx, uint32 typeNameIdx, uint8 access, uint8 flags
///     uint32  methodCount, for each: &lt;method layout&gt;
///
/// [Functions]
///   uint32  funcCount
///   for each: &lt;method layout&gt;
///
/// Method layout:
///   uint32  nameIndex
///   uint32  returnTypeNameIndex
///   uint8   access
///   uint8   flags  (bit0=static, bit1=virtual, bit2=abstract, bit3=override, bit4=constructor)
///   uint32  paramCount,  for each: uint32 nameIdx, uint32 typeNameIdx
///   uint32  localCount,  for each: uint32 nameIdx, uint32 typeNameIdx
///   uint32  instrCount,  for each: &lt;instruction layout&gt;
///
/// Instruction layout:
///   uint8 opcode  (ObjectIR.Core.IR.OpCode ordinal == fob_loader SerializedOpCode ordinal)
///   operands — see OpCode-specific details in fob_loader.hpp
/// </code>
/// </remarks>
public static class ModuleBinaryWriter
{
    private const uint NullIdx      = 0xFFFF_FFFFu;
    private const byte CondStack      = 0;
    private const byte CondBinary     = 1;
    private const byte CondExpression = 2;

    // ── Entry point ───────────────────────────────────────────────────────────

    /// <summary>Serializes <paramref name="module"/> to a FOB/IR v3 payload byte array.</summary>
    public static byte[] Write(Module module)
    {
        var st = new StringTable();
        CollectModuleStrings(st, module);

        using var ms = new MemoryStream();
        using var w  = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

        WriteStringPool(w, st);
        WriteModuleHeader(w, st, module);
        WriteTypes(w, st, module);
        WriteFunctions(w, st, module);

        w.Flush();
        return ms.ToArray();
    }

    // ── String collection (pass 1) ────────────────────────────────────────────

    private static void CollectModuleStrings(StringTable st, Module m)
    {
        st.Intern(m.Name);
        foreach (var t in m.Types)     CollectTypeStrings(st, t);
        foreach (var f in m.Functions) CollectMethodStrings(st, f.Name, f.ReturnType,
                                           f.Parameters, f.Locals, f.Instructions);
    }

    private static void CollectTypeStrings(StringTable st, TypeDefinition t)
    {
        st.Intern(t.Name);
        if (!string.IsNullOrEmpty(t.Namespace)) st.Intern(t.Namespace);

        if (GetBaseType(t) is { } bt) st.Intern(bt.GetQualifiedName());
        foreach (var i in GetInterfaces(t)) st.Intern(i.GetQualifiedName());
        foreach (var f in GetFields(t))
        {
            st.Intern(f.Name);
            st.Intern(f.Type.GetQualifiedName());
        }
        foreach (var m in GetMethods(t))
            CollectMethodStrings(st, m.Name, m.ReturnType, m.Parameters, m.Locals, m.Instructions);
    }

    private static void CollectMethodStrings(StringTable st, string name, TypeReference returnType,
        IEnumerable<Parameter> parameters, IEnumerable<LocalVariable> locals,
        InstructionList instructions)
    {
        st.Intern(name);
        st.Intern(returnType.GetQualifiedName());
        foreach (var p in parameters) { st.Intern(p.Name); st.Intern(p.Type.GetQualifiedName()); }
        foreach (var l in locals)     { st.Intern(l.Name); st.Intern(l.Type.GetQualifiedName()); }
        CollectInstructionListStrings(st, instructions);
    }

    private static void CollectInstructionListStrings(StringTable st, IEnumerable<Instruction> instrs)
    {
        foreach (var instr in instrs) CollectInstrStrings(st, instr);
    }

    private static void CollectInstrStrings(StringTable st, Instruction instr)
    {
        switch (instr)
        {
            case LoadLocalInstruction i:       st.Intern(i.LocalName); break;
            case StoreArgInstruction i:        st.Intern(i.ArgumentName); break;
            case StoreLocalInstruction i:      st.Intern(i.LocalName); break;
            case LoadConstantInstruction i when i.OpCode == OpCode.Ldstr:
                st.Intern(i.Value?.ToString()); break;
            case LoadFieldInstruction i:
                st.Intern(i.Field.DeclaringType.GetQualifiedName());
                st.Intern(i.Field.Name);
                st.Intern(i.Field.FieldType.GetQualifiedName()); break;
            case LoadStaticFieldInstruction i:
                st.Intern(i.Field.DeclaringType.GetQualifiedName());
                st.Intern(i.Field.Name);
                st.Intern(i.Field.FieldType.GetQualifiedName()); break;
            case StoreFieldInstruction i:
                st.Intern(i.Field.DeclaringType.GetQualifiedName());
                st.Intern(i.Field.Name);
                st.Intern(i.Field.FieldType.GetQualifiedName()); break;
            case StoreStaticFieldInstruction i:
                st.Intern(i.Field.DeclaringType.GetQualifiedName());
                st.Intern(i.Field.Name);
                st.Intern(i.Field.FieldType.GetQualifiedName()); break;
            case CallInstruction i:
                CollectMethodRefStrings(st, i.Method); break;
            case CallVirtualInstruction i:
                CollectMethodRefStrings(st, i.Method); break;
            case NewObjectInstruction i:
                st.Intern(i.Type.GetQualifiedName());
                foreach (var p in i.ParameterTypes) st.Intern(p.GetQualifiedName()); break;
            case NewArrayInstruction i:  st.Intern(i.ElementType.GetQualifiedName()); break;
            case CastInstruction i:      st.Intern(i.TargetType.GetQualifiedName()); break;
            case IsInstanceInstruction i: st.Intern(i.TargetType.GetQualifiedName()); break;
            case ConversionInstruction i: st.Intern(i.TargetType.GetQualifiedName()); break;
            case ReturnInstruction i when i.Value != null:
                CollectInstrStrings(st, i.Value); break;
            case IfInstruction i:
                CollectConditionStrings(st, i.Condition);
                CollectInstructionListStrings(st, i.ThenBlock);
                if (i.ElseBlock != null) CollectInstructionListStrings(st, i.ElseBlock); break;
            case WhileInstruction i:
                CollectConditionStrings(st, i.Condition);
                CollectInstructionListStrings(st, i.Body); break;
            case TryInstruction i:
                CollectInstructionListStrings(st, i.TryBlock);
                foreach (var cc in i.CatchClauses)
                {
                    st.Intern(cc.ExceptionType.GetQualifiedName());
                    st.Intern(cc.VariableName);
                    CollectInstructionListStrings(st, cc.Body);
                }
                if (i.FinallyBlock != null) CollectInstructionListStrings(st, i.FinallyBlock);
                break;
            case ForEachInstruction i:
                st.Intern(i.ItemName);
                st.Intern(i.CollectionName);
                CollectInstructionListStrings(st, i.Body); break;
        }
        // Zero-operand instructions need no string collection.
    }

    private static void CollectMethodRefStrings(StringTable st, MethodReference m)
    {
        st.Intern(m.DeclaringType.GetQualifiedName());
        st.Intern(m.Name);
        st.Intern(m.ReturnType.GetQualifiedName());
        foreach (var p in m.ParameterTypes) st.Intern(p.GetQualifiedName());
    }

    private static void CollectConditionStrings(StringTable st, Condition cond)
    {
        if (cond is ExpressionCondition ec) CollectInstrStrings(st, ec.Expression);
        else if (cond is LogicalCondition lc)
        {
            CollectConditionStrings(st, lc.Left);
            if (lc.Right != null) CollectConditionStrings(st, lc.Right);
        }
    }

    // ── Binary writing (pass 2) ───────────────────────────────────────────────

    private static void WriteStringPool(BinaryWriter w, StringTable st)
    {
        var strings = st.GetAll();
        w.Write((uint)strings.Count);
        foreach (var s in strings)
        {
            var bytes = Encoding.UTF8.GetBytes(s);
            w.Write((uint)bytes.Length);
            w.Write(bytes);
        }
    }

    private static void WriteModuleHeader(BinaryWriter w, StringTable st, Module m)
    {
        w.Write(st.Intern(m.Name));
        w.Write((ushort)m.Version.Major);
        w.Write((ushort)m.Version.Minor);
        w.Write((ushort)(m.Version.Build >= 0 ? m.Version.Build : 0));
        w.Write(ComputeEntryPoint(m, st));
    }

    private static uint ComputeEntryPoint(Module m, StringTable st)
    {
        // Prefer metadata hint: "TypeName.MethodName"
        if (m.Metadata.TryGetValue("EntryPoint", out var epObj) && epObj is string epStr)
        {
            int dot = epStr.LastIndexOf('.');
            if (dot > 0)
            {
                var className  = epStr[..dot];
                var methodName = epStr[(dot + 1)..];
                for (int ti = 0; ti < m.Types.Count; ti++)
                {
                    if (m.Types[ti].GetQualifiedName() == className)
                    {
                        var methods = GetMethods(m.Types[ti]);
                        for (int mi = 0; mi < methods.Count; mi++)
                            if (methods[mi].Name == methodName)
                                return (uint)((ti << 16) | mi);
                    }
                }
            }
        }

        // Fallback: first method named "Main" or "main" in any type
        for (int ti = 0; ti < m.Types.Count; ti++)
        {
            var methods = GetMethods(m.Types[ti]);
            for (int mi = 0; mi < methods.Count; mi++)
                if (methods[mi].Name is "Main" or "main")
                    return (uint)((ti << 16) | mi);
        }

        return NullIdx;
    }

    private static void WriteTypes(BinaryWriter w, StringTable st, Module m)
    {
        w.Write((uint)m.Types.Count);
        foreach (var t in m.Types) WriteType(w, st, t);
    }

    private static void WriteType(BinaryWriter w, StringTable st, TypeDefinition t)
    {
        w.Write((byte)t.Kind);
        w.Write((byte)t.Access);
        w.Write(st.Intern(t.Name));
        w.Write(string.IsNullOrEmpty(t.Namespace) ? NullIdx : st.Intern(t.Namespace));

        ushort typeFlags = 0;
        if (t is ClassDefinition cls)
        {
            if (cls.IsAbstract) typeFlags |= 1;
            if (cls.IsSealed)   typeFlags |= 2;
        }
        w.Write(typeFlags);

        var baseType = GetBaseType(t);
        w.Write(baseType != null ? st.Intern(baseType.GetQualifiedName()) : NullIdx);

        var ifaces = GetInterfaces(t);
        w.Write((uint)ifaces.Count);
        foreach (var i in ifaces) w.Write(st.Intern(i.GetQualifiedName()));

        var fields = GetFields(t);
        w.Write((uint)fields.Count);
        foreach (var f in fields) WriteField(w, st, f);

        var methods = GetMethods(t);
        w.Write((uint)methods.Count);
        foreach (var m in methods) WriteMethod(w, st, m);
    }

    private static void WriteField(BinaryWriter w, StringTable st, FieldDefinition f)
    {
        w.Write(st.Intern(f.Name));
        w.Write(st.Intern(f.Type.GetQualifiedName()));
        w.Write((byte)f.Access);
        byte flags = 0;
        if (f.IsStatic)   flags |= 1;
        if (f.IsReadOnly) flags |= 2;
        w.Write(flags);
    }

    private static void WriteMethod(BinaryWriter w, StringTable st, MethodDefinition m)
    {
        w.Write(st.Intern(m.Name));
        w.Write(st.Intern(m.ReturnType.GetQualifiedName()));
        w.Write((byte)m.Access);
        byte flags = 0;
        if (m.IsStatic)      flags |= 0x01;
        if (m.IsVirtual)     flags |= 0x02;
        if (m.IsAbstract)    flags |= 0x04;
        if (m.IsOverride)    flags |= 0x08;
        if (m.IsConstructor) flags |= 0x10;
        w.Write(flags);

        w.Write((uint)m.Parameters.Count);
        foreach (var p in m.Parameters)
        {
            w.Write(st.Intern(p.Name));
            w.Write(st.Intern(p.Type.GetQualifiedName()));
        }

        w.Write((uint)m.Locals.Count);
        foreach (var l in m.Locals)
        {
            w.Write(st.Intern(l.Name));
            w.Write(st.Intern(l.Type.GetQualifiedName()));
        }

        WriteInstructionBlock(w, st, m.Instructions);
    }

    private static void WriteFunctions(BinaryWriter w, StringTable st, Module m)
    {
        w.Write((uint)m.Functions.Count);
        foreach (var f in m.Functions)
        {
            w.Write(st.Intern(f.Name));
            w.Write(st.Intern(f.ReturnType.GetQualifiedName()));
            w.Write((byte)AccessModifier.Public); // functions are always public
            w.Write((byte)0x01);                  // always static

            w.Write((uint)f.Parameters.Count);
            foreach (var p in f.Parameters)
            {
                w.Write(st.Intern(p.Name));
                w.Write(st.Intern(p.Type.GetQualifiedName()));
            }

            w.Write((uint)f.Locals.Count);
            foreach (var l in f.Locals)
            {
                w.Write(st.Intern(l.Name));
                w.Write(st.Intern(l.Type.GetQualifiedName()));
            }

            WriteInstructionBlock(w, st, f.Instructions);
        }
    }

    // ── Instruction block ─────────────────────────────────────────────────────

    /// <summary>
    /// Writes a uint32 count followed by each flattened instruction.
    /// ReturnInstruction.Value is expanded inline before its Ret opcode.
    /// </summary>
    private static void WriteInstructionBlock(BinaryWriter w, StringTable st, IEnumerable<Instruction> instrs)
    {
        var flat = Flatten(instrs).ToList();
        w.Write((uint)flat.Count);
        foreach (var instr in flat) WriteInstruction(w, st, instr);
    }

    /// <summary>
    /// Expands ReturnInstruction(Value) into [Value_instruction, ReturnInstruction(null)]
    /// so the binary representation only ever has zero-operand Ret.
    /// </summary>
    private static IEnumerable<Instruction> Flatten(IEnumerable<Instruction> instrs)
    {
        foreach (var instr in instrs)
        {
            if (instr is ReturnInstruction { Value: { } val })
            {
                foreach (var vi in Flatten(new[] { val })) yield return vi;
                yield return new ReturnInstruction(null);
            }
            else
            {
                yield return instr;
            }
        }
    }

    // ── Single instruction ────────────────────────────────────────────────────

    private static void WriteInstruction(BinaryWriter w, StringTable st, Instruction instr)
    {
        switch (instr)
        {
            // ── Load argument ─────────────────────────────────────────────────
            case LoadArgInstruction i:
                w.Write((byte)OpCode.Ldarg);
                w.Write(i.Index >= 0 ? i.Index : 0); // int32
                break;

            // ── Load/store local ──────────────────────────────────────────────
            case LoadLocalInstruction i:
                w.Write((byte)OpCode.Ldloc);
                w.Write(st.Intern(i.LocalName)); // uint32 string idx
                break;

            case StoreArgInstruction i:
                w.Write((byte)OpCode.Starg);
                w.Write(st.Intern(i.ArgumentName));
                break;

            case StoreLocalInstruction i:
                w.Write((byte)OpCode.Stloc);
                w.Write(st.Intern(i.LocalName));
                break;

            // ── Field access ──────────────────────────────────────────────────
            case LoadFieldInstruction i:
                w.Write((byte)OpCode.Ldfld);
                WriteFieldRef(w, st, i.Field);
                break;

            case LoadStaticFieldInstruction i:
                w.Write((byte)OpCode.Ldsfld);
                WriteFieldRef(w, st, i.Field);
                break;

            case StoreFieldInstruction i:
                w.Write((byte)OpCode.Stfld);
                WriteFieldRef(w, st, i.Field);
                break;

            case StoreStaticFieldInstruction i:
                w.Write((byte)OpCode.Stsfld);
                WriteFieldRef(w, st, i.Field);
                break;

            // ── Constants ─────────────────────────────────────────────────────
            case LoadConstantInstruction i:
                WriteLoadConstant(w, st, i);
                break;

            case LoadNullInstruction _:
                w.Write((byte)OpCode.Ldnull);
                break;

            // ── Arithmetic ────────────────────────────────────────────────────
            case ArithmeticInstruction i:
                w.Write((byte)i.OpCode); // ordinal matches SerializedOpCode directly
                break;

            case UnaryNegateInstruction _:
                w.Write((byte)OpCode.Neg);
                break;

            case UnaryNotInstruction _:
                w.Write((byte)OpCode.Not);
                break;

            // ── Comparison ────────────────────────────────────────────────────
            case ComparisonInstruction i:
                w.Write((byte)MapComparisonOp(i.Operation));
                break;

            // ── Calls ─────────────────────────────────────────────────────────
            case CallInstruction i:
                w.Write((byte)OpCode.Call);
                WriteMethodRef(w, st, i.Method);
                break;

            case CallVirtualInstruction i:
                w.Write((byte)OpCode.Callvirt);
                WriteMethodRef(w, st, i.Method);
                break;

            // ── Object / array creation ───────────────────────────────────────
            case NewObjectInstruction i:
                w.Write((byte)OpCode.Newobj);
                w.Write(st.Intern(i.Type.GetQualifiedName()));
                break;

            case NewArrayInstruction i:
                w.Write((byte)OpCode.Newarr);
                w.Write(st.Intern(i.ElementType.GetQualifiedName()));
                break;

            // ── Type checks / casts ───────────────────────────────────────────
            case CastInstruction i:
                w.Write((byte)OpCode.Castclass);
                w.Write(st.Intern(i.TargetType.GetQualifiedName()));
                break;

            case IsInstanceInstruction i:
                w.Write((byte)OpCode.Isinst);
                w.Write(st.Intern(i.TargetType.GetQualifiedName()));
                break;

            // ── Return ────────────────────────────────────────────────────────
            // Value already emitted by Flatten(); only no-value Ret reaches here.
            case ReturnInstruction _:
                w.Write((byte)OpCode.Ret);
                break;

            // ── Stack manipulation ────────────────────────────────────────────
            case DupInstruction _:
                w.Write((byte)OpCode.Dup);
                break;

            case PopInstruction _:
                w.Write((byte)OpCode.Pop);
                break;

            // ── Conversion ────────────────────────────────────────────────────
            case ConversionInstruction i:
                w.Write((byte)MapConversionType(i.TargetType));
                break;

            // ── Array element ─────────────────────────────────────────────────
            case LoadElementInstruction _:
                w.Write((byte)OpCode.Ldelem);
                break;

            case StoreElementInstruction _:
                w.Write((byte)OpCode.Stelem);
                break;

            // ── Structured control flow ───────────────────────────────────────
            case IfInstruction i:
                w.Write((byte)OpCode.If);
                WriteCondition(w, st, i.Condition);
                WriteInstructionBlock(w, st, i.ThenBlock);
                bool hasElse = i.ElseBlock is { Count: > 0 };
                w.Write((byte)(hasElse ? 1 : 0));
                if (hasElse) WriteInstructionBlock(w, st, i.ElseBlock!);
                break;

            case WhileInstruction i:
                w.Write((byte)OpCode.While);
                WriteCondition(w, st, i.Condition);
                WriteInstructionBlock(w, st, i.Body);
                break;

            // ── Other zero-operand ────────────────────────────────────────────
            case BreakInstruction _:
                w.Write((byte)OpCode.Break);
                break;

            case ContinueInstruction _:
                w.Write((byte)OpCode.Continue);
                break;

            case ThrowInstruction _:
                w.Write((byte)OpCode.Throw);
                break;

            // ── Unsupported / future (Try, ForEach, etc.) ────────────────────
            default:
                throw new NotSupportedException(
                    $"Instruction '{instr.GetType().Name}' (opcode {instr.OpCode}) " +
                    "is not yet supported by the FOB/IR v3 serializer. " +
                    "The C++ runtime has no operand layout for this instruction.");
        }
    }

    // ── Instruction helpers ───────────────────────────────────────────────────

    private static void WriteLoadConstant(BinaryWriter w, StringTable st, LoadConstantInstruction i)
    {
        switch (i.OpCode)
        {
            case OpCode.LdcI4:
                w.Write((byte)OpCode.LdcI4);
                w.Write(Convert.ToInt32(i.Value));
                break;
            case OpCode.LdcI8:
                w.Write((byte)OpCode.LdcI8);
                w.Write(Convert.ToInt64(i.Value));
                break;
            case OpCode.LdcR4:
                w.Write((byte)OpCode.LdcR4);
                w.Write(Convert.ToSingle(i.Value));
                break;
            case OpCode.LdcR8:
                w.Write((byte)OpCode.LdcR8);
                w.Write(Convert.ToDouble(i.Value));
                break;
            case OpCode.Ldstr:
                w.Write((byte)OpCode.Ldstr);
                w.Write(st.Intern(i.Value?.ToString()));
                break;
            default:
                // Fallback: treat as int32 constant
                w.Write((byte)OpCode.LdcI4);
                w.Write(Convert.ToInt32(i.Value));
                break;
        }
    }

    private static void WriteFieldRef(BinaryWriter w, StringTable st, FieldReference f)
    {
        w.Write(st.Intern(f.DeclaringType.GetQualifiedName()));
        w.Write(st.Intern(f.Name));
        w.Write(st.Intern(f.FieldType.GetQualifiedName()));
    }

    private static void WriteMethodRef(BinaryWriter w, StringTable st, MethodReference m)
    {
        w.Write(st.Intern(m.DeclaringType.GetQualifiedName()));
        w.Write(st.Intern(m.Name));
        w.Write(st.Intern(m.ReturnType.GetQualifiedName()));
        w.Write((uint)m.ParameterTypes.Count);
        foreach (var p in m.ParameterTypes) w.Write(st.Intern(p.GetQualifiedName()));
    }

    private static void WriteCondition(BinaryWriter w, StringTable st, Condition cond)
    {
        switch (cond)
        {
            case BinaryCondition bc:
                w.Write(CondBinary);
                w.Write((byte)bc.Operation); // ComparisonOp ordinal matches C++ compOp table
                break;

            case ExpressionCondition ec:
                w.Write(CondExpression);
                // C++ ParseCondition reads a full instruction block for expressions
                WriteInstructionBlock(w, st, new[] { ec.Expression });
                break;

            default:
                // StackCondition or LogicalCondition (no direct C++ encoding for logical)
                w.Write(CondStack);
                break;
        }
    }

    // ── Opcode mapping helpers ────────────────────────────────────────────────

    private static OpCode MapComparisonOp(ComparisonOp op) => op switch
    {
        ComparisonOp.Equal          => OpCode.Ceq,
        ComparisonOp.Greater        => OpCode.Cgt,
        ComparisonOp.Less           => OpCode.Clt,
        // NotEqual/GreaterOrEqual/LessOrEqual have no standalone opcode in v3;
        // use Ceq as closest fallback — callers should prefer BinaryCondition in IfInstruction.
        ComparisonOp.NotEqual       => OpCode.Ceq,
        ComparisonOp.GreaterOrEqual => OpCode.Cgt,
        ComparisonOp.LessOrEqual    => OpCode.Clt,
        _                           => OpCode.Ceq,
    };

    private static OpCode MapConversionType(TypeReference t)
    {
        var n = t.GetQualifiedName();
        return n switch
        {
            "int"    or "int32"   or "System.Int32"   => OpCode.ConvI4,
            "long"   or "int64"   or "System.Int64"   => OpCode.ConvI8,
            "float"  or "float32" or "System.Single"  => OpCode.ConvR4,
            "double" or "float64" or "System.Double"  => OpCode.ConvR8,
            "uint"   or "uint32"  or "System.UInt32"  => OpCode.ConvU4,
            "ulong"  or "uint64"  or "System.UInt64"  => OpCode.ConvU8,
            _                                          => OpCode.ConvI4,
        };
    }

    // ── TypeDefinition helpers ────────────────────────────────────────────────

    private static TypeReference? GetBaseType(TypeDefinition t) => t switch
    {
        ClassDefinition c => c.BaseType,
        _                 => null,
    };

    private static IReadOnlyList<TypeReference> GetInterfaces(TypeDefinition t) => t switch
    {
        ClassDefinition c     => c.Interfaces,
        StructDefinition s    => s.Interfaces,
        InterfaceDefinition i => i.BaseInterfaces,
        _                     => Array.Empty<TypeReference>(),
    };

    private static IReadOnlyList<FieldDefinition> GetFields(TypeDefinition t) => t switch
    {
        ClassDefinition c  => c.Fields,
        StructDefinition s => s.Fields,
        _                  => Array.Empty<FieldDefinition>(),
    };

    private static IReadOnlyList<MethodDefinition> GetMethods(TypeDefinition t) => t switch
    {
        ClassDefinition c     => c.Methods,
        StructDefinition s    => s.Methods,
        InterfaceDefinition i => i.Methods,
        _                     => Array.Empty<MethodDefinition>(),
    };

    // ── StringTable ───────────────────────────────────────────────────────────

    private sealed class StringTable
    {
        private readonly Dictionary<string, uint> _map  = new(StringComparer.Ordinal) { { "", 0 } };
        private readonly List<string>             _list = [""];

        /// <summary>Interns <paramref name="s"/> and returns its pool index (0 = empty/null).</summary>
        public uint Intern(string? s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            if (_map.TryGetValue(s, out uint idx)) return idx;
            uint newIdx = (uint)_list.Count;
            _list.Add(s);
            _map[s] = newIdx;
            return newIdx;
        }

        public IReadOnlyList<string> GetAll() => _list;
    }
}
