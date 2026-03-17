using CppToObjectIR.Ast;
using ObjectIR.Core.IR;

namespace CppToObjectIR.CodeGen;

/// <summary>
/// Walks a C++ AST and emits an ObjectIR <see cref="Module"/>.
/// </summary>
public sealed class IrCodeGenerator
{
    private readonly Module _module;

    // Tracks the class we are currently emitting members for (used for 'this').
    private TypeDefinition? _currentType;

    // Maps local variable names to their IR type within the current method body.
    private readonly Dictionary<string, TypeReference> _locals = new();

    // Maps parameter names to their index (for Ldarg).
    private readonly Dictionary<string, int> _paramIndices = new();

    // Maps parameter names to their IR type within the current method body.
    private readonly Dictionary<string, TypeReference> _paramTypes = new();

    // Return type of the method/function currently being compiled.
    private TypeReference? _currentReturnType;

    // Registry of "TypeName.MemberName" -> TypeReference for field/method return type inference.
    // Populated from pre-parsed headers and types defined in the compiled source.
    private readonly Dictionary<string, TypeReference> _memberTypeRegistry;

    public IrCodeGenerator(string moduleName, Dictionary<string, TypeReference>? memberTypeRegistry = null)
    {
        _module = new Module(moduleName);
        _memberTypeRegistry = memberTypeRegistry ?? new Dictionary<string, TypeReference>(StringComparer.Ordinal);
    }

    // =====================================================================
    // Entry point
    // =====================================================================

    public Module Generate(TranslationUnitNode unit)
    {
        foreach (var decl in unit.Declarations)
            EmitTopLevelDecl(decl, null);
        return _module;
    }

    // =====================================================================
    // Top-level
    // =====================================================================

    private void EmitTopLevelDecl(AstNode decl, string? ns)
    {
        switch (decl)
        {
            case NamespaceNode nsn:
                EmitNamespace(nsn);
                break;
            case ClassDeclNode cls:
                EmitClass(cls);
                break;
            case StructDeclNode str:
                EmitStruct(str);
                break;
            case EnumDeclNode en:
                EmitEnum(en);
                break;
            case FunctionDeclNode fn:
                EmitFreeFunction(fn);
                break;
        }
    }

    // =====================================================================
    // Namespace  (emitted as a static class)
    // =====================================================================

    private void EmitNamespace(NamespaceNode nsn)
    {
        // Free functions inside a namespace become static methods of a class
        // named after the namespace.  Classes/structs/enums are emitted normally.
        var freeFunctions = nsn.Members.OfType<FunctionDeclNode>().ToList();

        if (freeFunctions.Count > 0)
        {
            var classDef = _module.DefineClass(nsn.Name);
            var prev = _currentType;
            _currentType = classDef;

            foreach (var fn in freeFunctions)
            {
                var method = classDef.DefineMethod(fn.Name, MapType(fn.ReturnType));
                method.IsStatic = true;
                method.Access = AccessModifier.Public;

                _locals.Clear();
                _paramIndices.Clear();

                int idx = 0;
                foreach (var p in fn.Parameters)
                {
                    method.DefineParameter(p.Name, MapType(p.Type));
                    _paramIndices[p.Name] = idx++;
                }

                if (fn.Body != null)
                    EmitBlock(fn.Body, method.Instructions, method.Locals);
            }

            _currentType = prev;
        }

        // Handle non-function members (classes, structs, enums, nested namespaces)
        foreach (var member in nsn.Members)
        {
            switch (member)
            {
                case FunctionDeclNode: break; // already handled above
                case ClassDeclNode cls:    EmitClass(cls); break;
                case StructDeclNode str:   EmitStruct(str); break;
                case EnumDeclNode en:      EmitEnum(en); break;
                case NamespaceNode nested: EmitNamespace(nested); break;
            }
        }
    }

    // =====================================================================
    // Class
    // =====================================================================

    private void EmitClass(ClassDeclNode cls)
    {
        var classDef = _module.DefineClass(cls.Name);
        classDef.Namespace = cls.Namespace;
        classDef.IsAbstract = cls.IsAbstract;
        if (cls.BaseClass != null)
            classDef.BaseType = TypeReference.FromName(cls.BaseClass);
        foreach (var iface in cls.BaseInterfaces)
            classDef.Interfaces.Add(TypeReference.FromName(iface));

        var prev = _currentType;
        _currentType = classDef;

        foreach (var section in cls.Sections)
        {
            var access = MapAccess(section.Access);
            foreach (var member in section.Members)
                EmitMember(member, classDef, access);
        }

        _currentType = prev;
    }

    // =====================================================================
    // Struct
    // =====================================================================

    private void EmitStruct(StructDeclNode str)
    {
        var structDef = _module.DefineStruct(str.Name);
        structDef.Namespace = str.Namespace;

        var prev = _currentType;
        _currentType = structDef;

        foreach (var section in str.Sections)
        {
            foreach (var member in section.Members)
            {
                switch (member)
                {
                    case FieldDeclNode f:
                        var field = structDef.DefineField(f.Name, MapType(f.Type));
                        field.IsStatic = f.IsStatic;
                        field.IsReadOnly = f.IsConst;
                        break;
                    case MethodDeclNode m:
                        EmitMethodOnStruct(m, structDef);
                        break;
                }
            }
        }

        _currentType = prev;
    }

    // =====================================================================
    // Enum
    // =====================================================================

    private void EmitEnum(EnumDeclNode en)
    {
        var enumDef = new EnumDefinition(en.Name);
        enumDef.Namespace = en.Namespace;
        if (en.UnderlyingType != null)
            enumDef.UnderlyingType = MapBuiltinType(en.UnderlyingType);

        foreach (var m in en.Members)
        {
            long val = m.Value is IntLiteralNode il ? il.Value : 0;
            enumDef.DefineMember(m.Name, val);
        }
        _module.Types.Add(enumDef);
    }

    // =====================================================================
    // Members
    // =====================================================================

    private void EmitMember(AstNode member, ClassDefinition classDef, AccessModifier access)
    {
        switch (member)
        {
            case FieldDeclNode f:
                var field = classDef.DefineField(f.Name, MapType(f.Type));
                field.Access = access;
                field.IsStatic = f.IsStatic;
                field.IsReadOnly = f.IsConst;
                if (f.Initializer is IntLiteralNode il) field.InitialValue = il.Value;
                else if (f.Initializer is FloatLiteralNode fl) field.InitialValue = fl.Value;
                else if (f.Initializer is StringLiteralNode sl) field.InitialValue = sl.Value;
                else if (f.Initializer is BoolLiteralNode bl) field.InitialValue = bl.Value;
                break;
            case MethodDeclNode m:
                EmitMethodOnClass(m, classDef, access);
                break;
            case ClassDeclNode nestedCls:
                EmitClass(nestedCls);
                break;
            case StructDeclNode nestedStr:
                EmitStruct(nestedStr);
                break;
            case EnumDeclNode nestedEnum:
                EmitEnum(nestedEnum);
                break;
        }
    }

    private void EmitMethodOnClass(MethodDeclNode m, ClassDefinition classDef, AccessModifier access)
    {
        MethodDefinition method;
        if (m.IsConstructor)
            method = classDef.DefineConstructor();
        else
            method = classDef.DefineMethod(m.Name, MapType(m.ReturnType));

        method.Access = access;
        method.IsStatic = m.IsStatic;
        method.IsVirtual = m.IsVirtual;
        method.IsOverride = m.IsOverride;
        method.IsAbstract = m.IsPureVirtual;

        PopulateMethodBody(method, m);
    }

    private void EmitMethodOnStruct(MethodDeclNode m, StructDefinition structDef)
    {
        var method = structDef.DefineMethod(m.Name, MapType(m.ReturnType));
        method.IsStatic = m.IsStatic;
        PopulateMethodBody(method, m);
    }

    // =====================================================================
    // Free functions
    // =====================================================================

    private void EmitFreeFunction(FunctionDeclNode fn)
    {
        var func = new FunctionDefinition(fn.Name, MapType(fn.ReturnType));
        _module.Functions.Add(func);

        _locals.Clear();
        _paramIndices.Clear();
        _paramTypes.Clear();
        _currentReturnType = func.ReturnType;

        int idx = 0;
        foreach (var p in fn.Parameters)
        {
            var pType = MapType(p.Type);
            func.DefineParameter(p.Name, pType);
            _paramIndices[p.Name] = idx++;
            _paramTypes[p.Name] = pType;
        }

        if (fn.Body != null)
            EmitBlock(fn.Body, func.Instructions, func.Locals);
    }

    // =====================================================================
    // Method body helpers
    // =====================================================================

    private void PopulateMethodBody(MethodDefinition method, MethodDeclNode m)
    {
        _locals.Clear();
        _paramIndices.Clear();
        _paramTypes.Clear();
        _currentReturnType = method.ReturnType;

        // 'this' is arg 0 for instance methods
        int idx = m.IsStatic ? 0 : 1;
        foreach (var p in m.Parameters)
        {
            var pType = MapType(p.Type);
            method.DefineParameter(p.Name, pType);
            _paramIndices[p.Name] = idx++;
            _paramTypes[p.Name] = pType;
        }

        if (m.Body != null)
            EmitBlock(m.Body, method.Instructions, method.Locals);
    }

    // =====================================================================
    // Statement emission
    // =====================================================================

    private void EmitBlock(BlockStatementNode block, InstructionList il, List<LocalVariable> locals)
    {
        foreach (var stmt in block.Statements)
            EmitStatement(stmt, il, locals);
    }

    private void EmitStatement(StatementNode stmt, InstructionList il, List<LocalVariable> locals)
    {
        switch (stmt)
        {
            case BlockStatementNode blk:
                EmitBlock(blk, il, locals);
                break;

            case ReturnStatementNode ret:
                if (ret.Value != null)
                    EmitExpression(ret.Value, il, _currentReturnType);
                il.EmitReturn();
                break;

            case ExpressionStatementNode exprStmt:
                EmitExpression(exprStmt.Expression, il);
                // Pop result for assignment/unary expressions (they push a value).
                // Calls are emitted with void return type, so no pop is needed.
                if (exprStmt.Expression is AssignmentExpressionNode or UnaryExpressionNode)
                    il.EmitPop();
                break;

            case VarDeclStatementNode varDecl:
                var irType = MapType(varDecl.Type);
                var local = new LocalVariable(varDecl.Name, irType);
                locals.Add(local);
                _locals[varDecl.Name] = irType;

                if (varDecl.Initializer != null)
                {
                    EmitExpression(varDecl.Initializer, il, irType);
                    il.EmitStoreLocal(varDecl.Name);
                }
                break;

            case IfStatementNode ifStmt:
                EmitIf(ifStmt, il, locals);
                break;

            case WhileStatementNode wh:
                EmitWhile(wh, il, locals);
                break;

            case ForStatementNode forStmt:
                EmitFor(forStmt, il, locals);
                break;

            case DoWhileStatementNode doWhile:
                // Emit as while with an extra flag, simplified to while
                EmitDoWhile(doWhile, il, locals);
                break;

            case BreakStatementNode:
                il.Emit(new BreakInstruction());
                break;

            case ContinueStatementNode:
                il.Emit(new ContinueInstruction());
                break;

            case TryStatementNode tryStmt:
                EmitTry(tryStmt, il, locals);
                break;

            case ThrowStatementNode throwStmt:
                if (throwStmt.Value != null) EmitExpression(throwStmt.Value, il);
                il.Emit(new ThrowInstruction());
                break;
        }
    }

    private void EmitIf(IfStatementNode ifStmt, InstructionList il, List<LocalVariable> locals)
    {
        EmitExpression(ifStmt.Condition, il);
        var ifInstr = new IfInstruction(Condition.Stack());
        il.Emit(ifInstr);

        EmitStatementIntoList(ifStmt.Then, ifInstr.ThenBlock, locals);

        if (ifStmt.Else != null)
        {
            ifInstr.ElseBlock = new InstructionList();
            EmitStatementIntoList(ifStmt.Else, ifInstr.ElseBlock, locals);
        }
    }

    private void EmitWhile(WhileStatementNode wh, InstructionList il, List<LocalVariable> locals)
    {
        EmitExpression(wh.Condition, il);
        var whileInstr = new WhileInstruction(Condition.Stack());
        il.Emit(whileInstr);
        EmitStatementIntoList(wh.Body, whileInstr.Body, locals);
    }

    private void EmitFor(ForStatementNode forStmt, InstructionList il, List<LocalVariable> locals)
    {
        // Init
        if (forStmt.Init != null)
            EmitStatement(forStmt.Init, il, locals);

        // Condition goes into while
        if (forStmt.Condition != null)
            EmitExpression(forStmt.Condition, il);
        else
            il.EmitLoadConstant(1, TypeReference.Int32); // infinite loop

        var whileInstr = new WhileInstruction(Condition.Stack());
        il.Emit(whileInstr);

        EmitStatementIntoList(forStmt.Body, whileInstr.Body, locals);

        // Update at end of body
        if (forStmt.Update != null)
        {
            EmitExpression(forStmt.Update, whileInstr.Body);
            whileInstr.Body.EmitPop();
        }
    }

    private void EmitDoWhile(DoWhileStatementNode doWhile, InstructionList il, List<LocalVariable> locals)
    {
        // Simplified: emit body once unconditionally then as while
        EmitStatementIntoList(doWhile.Body, il, locals);
        EmitExpression(doWhile.Condition, il);
        var whileInstr = new WhileInstruction(Condition.Stack());
        il.Emit(whileInstr);
        EmitStatementIntoList(doWhile.Body, whileInstr.Body, locals);
    }

    private void EmitTry(TryStatementNode tryStmt, InstructionList il, List<LocalVariable> locals)
    {
        var tryInstr = new TryInstruction();
        il.Emit(tryInstr);
        EmitBlock(tryStmt.TryBlock, tryInstr.TryBlock, locals);

        foreach (var clause in tryStmt.CatchClauses)
        {
            var catchClause = new CatchClause(MapType(clause.ExceptionType), clause.VariableName);
            tryInstr.CatchClauses.Add(catchClause);
            EmitBlock(clause.Body, catchClause.Body, locals);
        }
    }

    private void EmitStatementIntoList(StatementNode stmt, InstructionList il, List<LocalVariable> locals)
    {
        if (stmt is BlockStatementNode blk)
            EmitBlock(blk, il, locals);
        else
            EmitStatement(stmt, il, locals);
    }

    // =====================================================================
    // Expression emission
    // =====================================================================

    private void EmitExpression(ExpressionNode expr, InstructionList il, TypeReference? hintType = null)
    {
        switch (expr)
        {
            case IntLiteralNode n:
                il.EmitLoadConstant((int)n.Value, TypeReference.Int32);
                break;

            case FloatLiteralNode f:
                if (hintType == TypeReference.Float32)
                    il.EmitLoadConstant((float)f.Value, TypeReference.Float32);
                else
                    il.EmitLoadConstant(f.Value, TypeReference.Float64);
                break;

            case StringLiteralNode s:
                il.EmitLoadConstant(s.Value, TypeReference.String);
                break;

            case CharLiteralNode c:
                il.EmitLoadConstant((int)c.Value, TypeReference.Int32);
                break;

            case BoolLiteralNode b:
                il.EmitLoadConstant(b.Value ? 1 : 0, TypeReference.Int32);
                break;

            case NullptrLiteralNode:
                il.EmitLoadNull();
                break;

            case ThisExpressionNode:
                il.EmitLoadArg(0);
                break;

            case IdentifierNode id:
                EmitLoad(id.Name, il);
                break;

            case BinaryExpressionNode bin when bin.Operator == BinaryOp.Shl && IsCoutChain(bin):
                EmitCoutChain(bin, il);
                break;

            case BinaryExpressionNode bin:
                EmitBinary(bin, il);
                break;

            case UnaryExpressionNode un:
                EmitUnary(un, il);
                break;

            case AssignmentExpressionNode assign:
                EmitAssignment(assign, il, hintType);
                break;

            case CallExpressionNode call:
                EmitCall(call, il, hintType);
                break;

            case MemberAccessNode member:
                EmitMemberAccess(member, il);
                break;

            case IndexExpressionNode idx:
                EmitExpression(idx.Array, il);
                EmitExpression(idx.Index, il);
                il.Emit(new LoadElementInstruction());
                break;

            case NewExpressionNode newExpr:
                EmitNew(newExpr, il);
                break;

            case CastExpressionNode cast:
                EmitExpression(cast.Operand, il);
                il.Emit(new ConversionInstruction(MapType(cast.TargetType)));
                break;

            case TernaryExpressionNode tern:
                EmitExpression(tern.Condition, il);
                var ifI = new IfInstruction(Condition.Stack());
                il.Emit(ifI);
                EmitExpression(tern.Then, ifI.ThenBlock);
                ifI.ElseBlock = new InstructionList();
                EmitExpression(tern.Else, ifI.ElseBlock);
                break;
        }
    }

    private void EmitLoad(string name, InstructionList il)
    {
        if (_paramIndices.TryGetValue(name, out int idx))
        {
            il.EmitLoadArg(idx);
        }
        else if (_locals.ContainsKey(name))
        {
            il.EmitLoadLocal(name);
        }
        else
        {
            // If this looks like a field on the current type, emit ldfld this.field
            if (_currentType is ClassDefinition classDef)
            {
                var field = classDef.Fields.FirstOrDefault(f => f.Name == name);
                if (field != null)
                {
                    var declaring = TypeReference.FromName(classDef.GetQualifiedName());
                    var fieldRef = new FieldReference(declaring, field.Name, field.Type);
                    if (field.IsStatic)
                    {
                        il.EmitLoadStaticField(fieldRef);
                    }
                    else
                    {
                        // push 'this' then ldfld
                        il.EmitLoadArg(0);
                        il.EmitLoadField(fieldRef);
                    }
                    return;
                }
            }
            else if (_currentType is StructDefinition structDef)
            {
                var field = structDef.Fields.FirstOrDefault(f => f.Name == name);
                if (field != null)
                {
                    var declaring = TypeReference.FromName(structDef.GetQualifiedName());
                    var fieldRef = new FieldReference(declaring, field.Name, field.Type);
                    if (field.IsStatic)
                    {
                        il.EmitLoadStaticField(fieldRef);
                    }
                    else
                    {
                        il.EmitLoadArg(0);
                        il.EmitLoadField(fieldRef);
                    }
                    return;
                }
            }

            // Static field or unknown — emit as load local with name
            il.EmitLoadLocal(name);
        }
    }

    private void EmitBinary(BinaryExpressionNode bin, InstructionList il)
    {
        EmitExpression(bin.Left, il);
        EmitExpression(bin.Right, il);

        switch (bin.Operator)
        {
            case BinaryOp.Add: il.EmitAdd(); break;
            case BinaryOp.Sub: il.EmitSub(); break;
            case BinaryOp.Mul: il.EmitMul(); break;
            case BinaryOp.Div: il.EmitDiv(); break;
            case BinaryOp.Rem: il.EmitRem(); break;
            case BinaryOp.BitAnd: il.Emit(new ArithmeticInstruction(ArithmeticOp.And)); break;
            case BinaryOp.BitOr:  il.Emit(new ArithmeticInstruction(ArithmeticOp.Or)); break;
            case BinaryOp.BitXor: il.Emit(new ArithmeticInstruction(ArithmeticOp.Xor)); break;
            case BinaryOp.Shl: il.Emit(new ArithmeticInstruction(ArithmeticOp.Shl)); break;
            case BinaryOp.Shr: il.Emit(new ArithmeticInstruction(ArithmeticOp.Shr)); break;
            case BinaryOp.LogicalAnd: il.Emit(new ArithmeticInstruction(ArithmeticOp.And)); break;
            case BinaryOp.LogicalOr:  il.Emit(new ArithmeticInstruction(ArithmeticOp.Or)); break;
            case BinaryOp.Equal:        il.EmitCompareEqual(); break;
            case BinaryOp.NotEqual:     il.Emit(new ComparisonInstruction(ComparisonOp.NotEqual)); break;
            case BinaryOp.Less:         il.EmitCompareLess(); break;
            case BinaryOp.LessEqual:    il.Emit(new ComparisonInstruction(ComparisonOp.LessOrEqual)); break;
            case BinaryOp.Greater:      il.EmitCompareGreater(); break;
            case BinaryOp.GreaterEqual: il.Emit(new ComparisonInstruction(ComparisonOp.GreaterOrEqual)); break;
        }
    }

    private void EmitUnary(UnaryExpressionNode un, InstructionList il)
    {
        switch (un.Operator)
        {
            case UnaryOp.Negate:
                EmitExpression(un.Operand, il);
                il.Emit(new UnaryNegateInstruction());
                break;
            case UnaryOp.BitwiseNot:
                EmitExpression(un.Operand, il);
                il.Emit(new UnaryNotInstruction());
                break;
            case UnaryOp.LogicalNot:
                EmitExpression(un.Operand, il);
                il.Emit(new UnaryNotInstruction());
                break;
            case UnaryOp.PreIncrement:
                EmitExpression(un.Operand, il);
                il.EmitLoadConstant(1, TypeReference.Int32);
                il.EmitAdd();
                EmitStore(un.Operand, il);
                EmitExpression(un.Operand, il); // push new value
                break;
            case UnaryOp.PreDecrement:
                EmitExpression(un.Operand, il);
                il.EmitLoadConstant(1, TypeReference.Int32);
                il.EmitSub();
                EmitStore(un.Operand, il);
                EmitExpression(un.Operand, il);
                break;
            case UnaryOp.PostIncrement:
                EmitExpression(un.Operand, il);
                il.EmitDup();
                il.EmitLoadConstant(1, TypeReference.Int32);
                il.EmitAdd();
                EmitStore(un.Operand, il);
                break;
            case UnaryOp.PostDecrement:
                EmitExpression(un.Operand, il);
                il.EmitDup();
                il.EmitLoadConstant(1, TypeReference.Int32);
                il.EmitSub();
                EmitStore(un.Operand, il);
                break;
        }
    }

    private void EmitStore(ExpressionNode target, InstructionList il)
    {
        switch (target)
        {
            case IdentifierNode id:
                if (_locals.ContainsKey(id.Name))
                    il.EmitStoreLocal(id.Name);
                else if (_paramIndices.ContainsKey(id.Name))
                    il.EmitStoreArg(id.Name);
                else if (_currentType is ClassDefinition classDef)
                {
                    var field = classDef.Fields.FirstOrDefault(f => f.Name == id.Name);
                    if (field != null)
                    {
                        var declaring = TypeReference.FromName(classDef.GetQualifiedName());
                        var fieldRef2 = new FieldReference(declaring, field.Name, field.Type);
                        if (field.IsStatic)
                        {
                            il.EmitStoreStaticField(fieldRef2);
                        }
                        else
                        {
                            // For instance field store: push 'this' then emit stfld. The value is already on the stack.
                            il.EmitLoadArg(0);
                            il.EmitStoreField(fieldRef2);
                        }
                        break;
                    }
                }
                else if (_currentType is StructDefinition structDef)
                {
                    var field = structDef.Fields.FirstOrDefault(f => f.Name == id.Name);
                    if (field != null)
                    {
                        var declaring = TypeReference.FromName(structDef.GetQualifiedName());
                        var fieldRef2 = new FieldReference(declaring, field.Name, field.Type);
                        if (field.IsStatic)
                        {
                            il.EmitStoreStaticField(fieldRef2);
                        }
                        else
                        {
                            il.EmitLoadArg(0);
                            il.EmitStoreField(fieldRef2);
                        }
                        break;
                    }
                }
                break;
            case MemberAccessNode mem:
                // Push object
                EmitExpression(mem.Object, il);
                var fieldRef = new FieldReference(TypeReference.FromName("__unknown"), mem.Member, TypeReference.Int32);
                il.EmitStoreField(fieldRef);
                break;
        }
    }

    private void EmitAssignment(AssignmentExpressionNode assign, InstructionList il, TypeReference? hintType = null)
    {
        // Derive value type hint from the assignment target's known local type
        TypeReference? valueHint = hintType;
        if (valueHint == null && assign.Target is IdentifierNode targetId && _locals.TryGetValue(targetId.Name, out var targetType))
            valueHint = targetType;
        // Special-case instance field assignments to ensure correct stack order
        if (assign.Target is IdentifierNode id && (_currentType is ClassDefinition classDef || _currentType is StructDefinition))
        {
            // Find a field with this name
            FieldDefinition? field = null;
            bool isStatic = false;
            TypeReference? declaringType = null;
            if (_currentType is ClassDefinition cdef)
            {
                field = cdef.Fields.FirstOrDefault(f => f.Name == id.Name);
                if (field != null)
                {
                    isStatic = field.IsStatic;
                    declaringType = TypeReference.FromName(cdef.GetQualifiedName());
                }
            }
            else if (_currentType is StructDefinition sdef)
            {
                field = sdef.Fields.FirstOrDefault(f => f.Name == id.Name);
                if (field != null)
                {
                    isStatic = field.IsStatic;
                    declaringType = TypeReference.FromName(sdef.GetQualifiedName());
                }
            }

            if (field != null && !isStatic)
            {
                var fieldRef = new FieldReference(declaringType ?? TypeReference.FromName("__unknown"), field.Name, field.Type);

                if (assign.Operator != AssignOp.Assign)
                {
                    // Compound assignment on instance field:
                    // Emit: ldarg.0; dup; ldfld <field>; <rhs>; <op>; stfld <field>
                    il.EmitLoadArg(0);
                    il.EmitDup();
                    il.EmitLoadField(fieldRef);
                    EmitExpression(assign.Value, il, valueHint ?? field.Type);
                    var arithOp = assign.Operator switch
                    {
                        AssignOp.AddAssign => ArithmeticOp.Add,
                        AssignOp.SubAssign => ArithmeticOp.Sub,
                        AssignOp.MulAssign => ArithmeticOp.Mul,
                        AssignOp.DivAssign => ArithmeticOp.Div,
                        AssignOp.RemAssign => ArithmeticOp.Rem,
                        AssignOp.AndAssign => ArithmeticOp.And,
                        AssignOp.OrAssign  => ArithmeticOp.Or,
                        AssignOp.XorAssign => ArithmeticOp.Xor,
                        AssignOp.ShlAssign => ArithmeticOp.Shl,
                        AssignOp.ShrAssign => ArithmeticOp.Shr,
                        _ => ArithmeticOp.Add
                    };
                    il.Emit(new ArithmeticInstruction(arithOp));
                    il.EmitStoreField(fieldRef);
                }
                else
                {
                    // Simple assignment on instance field: ldarg.0; <value>; stfld
                    il.EmitLoadArg(0);
                    EmitExpression(assign.Value, il, valueHint ?? field.Type);
                    il.EmitStoreField(fieldRef);
                }

                // push stored value (assignment is an expression)
                EmitExpression(assign.Target, il);
                return;
            }
        }

        // Fallback: generic handling for locals/params/static fields/others
        if (assign.Operator != AssignOp.Assign)
        {
            // Compound assignment: load, operate, store
            EmitExpression(assign.Target, il);
            EmitExpression(assign.Value, il, valueHint);
            var arithOp = assign.Operator switch
            {
                AssignOp.AddAssign => ArithmeticOp.Add,
                AssignOp.SubAssign => ArithmeticOp.Sub,
                AssignOp.MulAssign => ArithmeticOp.Mul,
                AssignOp.DivAssign => ArithmeticOp.Div,
                AssignOp.RemAssign => ArithmeticOp.Rem,
                AssignOp.AndAssign => ArithmeticOp.And,
                AssignOp.OrAssign  => ArithmeticOp.Or,
                AssignOp.XorAssign => ArithmeticOp.Xor,
                AssignOp.ShlAssign => ArithmeticOp.Shl,
                AssignOp.ShrAssign => ArithmeticOp.Shr,
                _ => ArithmeticOp.Add
            };
            il.Emit(new ArithmeticInstruction(arithOp));
        }
        else
        {
            EmitExpression(assign.Value, il, valueHint);
        }

        EmitStore(assign.Target, il);
        EmitExpression(assign.Target, il); // push stored value (assignment is an expression)
    }

    private void EmitCall(CallExpressionNode call, InstructionList il, TypeReference? returnTypeHint = null)
    {
        // Determine method reference
        string methodName;
        TypeReference? declaringType = null;

        if (call.Callee is MemberAccessNode mem)
        {
            // obj.method() / obj->method()
            EmitExpression(mem.Object, il);
            methodName = mem.Member;
            // Resolve declaring type via full type inference (handles chained access like cat.transform.Rotate)
            var receiverType = InferExprType(mem.Object);
            declaringType = receiverType.Name.StartsWith("__")
                ? TypeReference.FromName("__dynamic")
                : receiverType;
        }
        else if (call.Callee is IdentifierNode id)
        {
            if (id.Name.Contains("::"))
            {
                // Qualified call like Console::WriteLine or Ns::Sub::Method
                var sep = id.Name.LastIndexOf("::", StringComparison.Ordinal);
                var classPath = id.Name[..sep].Replace("::", ".");
                methodName = id.Name[(sep + 2)..];
                declaringType = TypeReference.FromName(classPath);
            }
            else
            {
                methodName = id.Name;
                declaringType = TypeReference.FromName("__global");
            }
        }
        else
        {
            // Complex callee — emit it and use calli
            EmitExpression(call.Callee, il);
            foreach (var arg in call.Arguments) EmitExpression(arg, il);
            var callMethod = new MethodReference(TypeReference.Void, "__indirect", TypeReference.Void, new());
            il.EmitCall(callMethod);
            return;
        }

        foreach (var arg in call.Arguments)
            EmitExpression(arg, il);

        var paramTypes = call.Arguments.Select(InferExprType).ToList();
        var methodRef = new MethodReference(declaringType, methodName, returnTypeHint ?? TypeReference.Void, paramTypes);

        if (call.Callee is MemberAccessNode)
            il.EmitCallVirtual(methodRef);
        else
            il.EmitCall(methodRef);
    }

    private void EmitMemberAccess(MemberAccessNode member, InstructionList il)
    {
        EmitExpression(member.Object, il);
        // Resolve declaring type from known locals/params/chained member access
        var ownerType = InferExprType(member.Object);
        TypeReference declaringType = ownerType.Name.StartsWith("__")
            ? TypeReference.FromName("__dynamic")
            : ownerType;
        // Look up the field's own type in the registry so it can be used for further inference
        var fieldTypeKey = $"{ownerType.GetQualifiedName()}.{member.Member}";
        TypeReference fieldType = _memberTypeRegistry.TryGetValue(fieldTypeKey, out var ft)
            ? ft
            : TypeReference.FromName("__unknown");
        var fieldRef = new FieldReference(declaringType, member.Member, fieldType);
        il.EmitLoadField(fieldRef);
    }

    private void EmitNew(NewExpressionNode newExpr, InstructionList il)
    {
        var type = MapType(newExpr.Type);

        if (newExpr.ArraySize != null)
        {
            EmitExpression(newExpr.ArraySize, il);
            il.EmitNewArray(type);
            return;
        }

        foreach (var arg in newExpr.Arguments)
            EmitExpression(arg, il);

        var paramTypes = newExpr.Arguments.Select(InferExprType).ToList();
        il.EmitNewObject(type, paramTypes);
    }

    // =====================================================================
    // iostream helpers  (std::cout << ... << std::endl)
    // =====================================================================

    /// <summary>Returns true when the leftmost operand of a &lt;&lt; chain is std::cout.</summary>
    private static bool IsCoutChain(BinaryExpressionNode bin)
    {
        if (bin.Operator != BinaryOp.Shl) return false;
        if (IsStdCout(bin.Left)) return true;
        return bin.Left is BinaryExpressionNode left && IsCoutChain(left);
    }

    private static bool IsStdCout(ExpressionNode expr)
        => expr is IdentifierNode id && id.Name is "std::cout" or "cout";

    private static bool IsStdEndl(ExpressionNode expr)
        => expr is IdentifierNode id && id.Name is "std::endl" or "endl" or "std::flush" or "flush";

    private static bool HasTrailingEndl(ExpressionNode expr)
    {
        if (expr is BinaryExpressionNode bin && bin.Operator == BinaryOp.Shl)
            return IsStdEndl(bin.Right) || HasTrailingEndl(bin.Right);
        return IsStdEndl(expr);
    }

    private List<ExpressionNode> CollectCoutArgs(ExpressionNode expr)
    {
        var args = new List<ExpressionNode>();
        CollectCoutArgsInner(expr, args);
        return args;
    }

    private void CollectCoutArgsInner(ExpressionNode expr, List<ExpressionNode> args)
    {
        if (expr is BinaryExpressionNode bin && bin.Operator == BinaryOp.Shl)
        {
            CollectCoutArgsInner(bin.Left, args);
            if (!IsStdCout(bin.Right) && !IsStdEndl(bin.Right))
                args.Add(bin.Right);
        }
        else if (!IsStdCout(expr) && !IsStdEndl(expr))
        {
            args.Add(expr);
        }
    }

    private void EmitCoutChain(BinaryExpressionNode bin, InstructionList il)
    {
        bool hasEndl = HasTrailingEndl(bin);
        var args = CollectCoutArgs(bin);

        if (args.Count == 0)
        {
            if (hasEndl)
            {
                // Just emit Console.WriteLine() with no argument (empty line)
                var mr = new MethodReference(
                    TypeReference.FromName("System.Console"), "WriteLine",
                    TypeReference.Void, new List<TypeReference>());
                il.EmitCall(mr);
            }
            return;
        }

        for (int i = 0; i < args.Count; i++)
        {
            EmitExpression(args[i], il);
            bool isLast = i == args.Count - 1;
            var name = isLast && hasEndl ? "WriteLine" : "Write";
            var mr = new MethodReference(
                TypeReference.FromName("System.Console"), name,
                TypeReference.Void, new List<TypeReference> { TypeReference.String });
            il.EmitCall(mr);
        }
    }

    /// <summary>Best-effort type inference for a single expression node.</summary>
    private TypeReference InferExprType(ExpressionNode expr) => expr switch
    {
        StringLiteralNode _                                               => TypeReference.String,
        IntLiteralNode _                                                  => TypeReference.Int32,
        FloatLiteralNode _                                                => TypeReference.Float64,
        BoolLiteralNode _                                                 => TypeReference.Bool,
        CharLiteralNode _                                                 => TypeReference.Char,
        IdentifierNode id when _locals.TryGetValue(id.Name, out var lt)  => lt,
        IdentifierNode id when _paramTypes.TryGetValue(id.Name, out var pt) => pt,
        MemberAccessNode mem                                              => InferMemberAccessType(mem),
        BinaryExpressionNode bin                                           => InferBinaryExpressionType(bin),
        CallExpressionNode call                                             => InferCallReturnType(call),
        _                                                                 => TypeReference.Int32  // fallback
    };

    private TypeReference InferBinaryExpressionType(BinaryExpressionNode bin)
    {
        var left = InferExprType(bin.Left);
        var right = InferExprType(bin.Right);
        // if either side is a float32 prefer float32, else float64 if either is float64
        if (left == TypeReference.Float32 || right == TypeReference.Float32)
            return TypeReference.Float32;
        if (left == TypeReference.Float64 || right == TypeReference.Float64)
            return TypeReference.Float64;
        // otherwise integer
        return TypeReference.Int32;
    }

    private TypeReference InferCallReturnType(CallExpressionNode call)
    {
        // Try to resolve via callee information and member registry
        if (call.Callee is MemberAccessNode mem)
        {
            var ownerType = InferExprType(mem.Object);
            if (!ownerType.Name.StartsWith("__"))
            {
                var key = $"{ownerType.GetQualifiedName()}.{mem.Member}";
                if (_memberTypeRegistry.TryGetValue(key, out var t)) return t;
            }
        }
        else if (call.Callee is IdentifierNode id)
        {
            // Qualified free function like Ns::Func or plain global
            var name = id.Name.Replace("::", ".");
            // Try to split into type.method
            var sep = name.LastIndexOf('.');
            if (sep > 0)
            {
                var decl = name[..sep];
                var mname = name[(sep + 1)..];
                var key = $"{decl}.{mname}";
                if (_memberTypeRegistry.TryGetValue(key, out var t)) return t;
            }
        }
        return TypeReference.Int32;
    }

    /// <summary>
    /// Looks up the return/field type of <c>obj.member</c> using the member type registry.
    /// Returns <c>__unknown</c> when the type cannot be resolved.
    /// </summary>
    private TypeReference InferMemberAccessType(MemberAccessNode mem)
    {
        var ownerType = InferExprType(mem.Object);
        // Only resolve if we have a real type (not a sentinel like __dynamic / __unknown)
        if (ownerType.Name.StartsWith("__")) return TypeReference.FromName("__unknown");
        var key = $"{ownerType.GetQualifiedName()}.{mem.Member}";
        return _memberTypeRegistry.TryGetValue(key, out var t) ? t : TypeReference.FromName("__unknown");
    }

    // =====================================================================
    // Type mapping
    // =====================================================================

    private TypeReference MapType(CppTypeNode t)
    {
        var baseName = CppCompiler.NormalizeName(t.BaseName);
        var baseRef = MapBuiltinType(baseName);

        if (t.TemplateArgs.Count > 0)
            baseRef = baseRef.MakeGenericType(t.TemplateArgs.Select(MapType).ToArray());

        if (t.IsPointer || t.IsReference)
            return baseRef; // ObjectIR doesn't have a separate pointer layer; treat as the same type

        return baseRef;
    }

    private static TypeReference MapBuiltinType(string name) => name switch
    {
        "void"              => TypeReference.Void,
        "bool"              => TypeReference.Bool,
        "char"              => TypeReference.Char,
        "int"               => TypeReference.Int32,
        "short"             => TypeReference.Int16,
        "long"              => TypeReference.Int64,
        "long long"         => TypeReference.Int64,
        "unsigned int"      => TypeReference.UInt32,
        "unsigned short"    => TypeReference.UInt16,
        "unsigned long"     => TypeReference.UInt64,
        "unsigned char"     => TypeReference.UInt8,
        "float"             => TypeReference.Float32,
        "double"            => TypeReference.Float64,
        "auto"              => TypeReference.Int32, // best-effort
        "string"            => TypeReference.String,
        "std::string"       => TypeReference.String,
        "std.string"        => TypeReference.String,
        _                   => TypeReference.FromName(name)
    };

    private static AccessModifier MapAccess(AccessSpecifier access) => access switch
    {
        AccessSpecifier.Public    => AccessModifier.Public,
        AccessSpecifier.Private   => AccessModifier.Private,
        AccessSpecifier.Protected => AccessModifier.Protected,
        _                         => AccessModifier.Public
    };
}
