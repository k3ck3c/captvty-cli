using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

class Program
{
    static TypeDefinition FindType(ModuleDefinition module, string name)
    {
        foreach (var t in module.Types)
        {
            var r = FindTypeRecursive(t, name);
            if (r != null) return r;
        }
        throw new Exception("Type introuvable : " + name);
    }

    static TypeDefinition FindTypeRecursive(TypeDefinition t, string name)
    {
        if (t.Name == name) return t;
        foreach (var n in t.NestedTypes)
        {
            var r = FindTypeRecursive(n, name);
            if (r != null) return r;
        }
        return null;
    }

    static MethodDefinition FindMethod(TypeDefinition t, string name, int argc = -1)
    {
        var q = t.Methods.Where(m => m.Name == name);
        if (argc >= 0) q = q.Where(m => m.Parameters.Count == argc);
        var a = q.ToArray();
        if (a.Length != 1)
            throw new Exception(t.Name + "::" + name + " : " + a.Length + " candidats");
        return a[0];
    }

    static void MakeRet(MethodDefinition m)
    {
        m.Body = new MethodBody(m);
        m.Body.GetILProcessor().Append(Instruction.Create(OpCodes.Ret));
    }

    static void PatchCallSetDll(ModuleDefinition mod)
    {
        var type = FindType(mod, "_uqA");
        var method = FindMethod(type, "_3DA");
        var il = method.Body.GetILProcessor();
        Instruction call = null;

        foreach (var ins in method.Body.Instructions)
        {
            if (ins.OpCode != OpCodes.Call) continue;
            var mr = ins.Operand as MethodReference;
            if (mr != null && mr.Name == "_Le" && mr.DeclaringType.Name == "_otB" &&
                mr.ReturnType.FullName == "System.Boolean" && mr.Parameters.Count == 1 &&
                mr.Parameters[0].ParameterType.FullName == "System.String")
            {
                call = ins;
                break;
            }
        }
        if (call == null) throw new Exception("Appel _otB::_Le(string) introuvable");
        if (call.Next == null || call.Next.OpCode != OpCodes.Pop)
            throw new Exception("_otB::_Le n'est pas suivi du pop attendu");

        call.OpCode = OpCodes.Pop;
        call.Operand = null;
        il.InsertAfter(call, il.Create(OpCodes.Ldc_I4_1));
    }

    static void PatchShowInitException(ModuleDefinition mod)
    {
        var method = FindMethod(FindType(mod, "_uqA"), "_3DA");
        var catches = method.Body.ExceptionHandlers.Where(h =>
            h.HandlerType == ExceptionHandlerType.Catch && h.HandlerStart != null).ToList();
        var handler = catches.FirstOrDefault(h => h.HandlerStart.Offset > 0x80);
        if (handler == null) throw new Exception("Catch d'initialisation introuvable");
        if (handler.HandlerStart.OpCode != OpCodes.Pop)
            throw new Exception("Le catch d'initialisation ne commence pas par pop");

        var writeLine = mod.ImportReference(typeof(Console).GetMethod("WriteLine", new Type[] { typeof(object) }));
        handler.HandlerStart.OpCode = OpCodes.Call;
        handler.HandlerStart.Operand = writeLine;
    }

    static void PatchSkipGetTypes(ModuleDefinition mod)
    {
        var method = FindMethod(FindType(mod, "_uqA"), "_3DA");
        Instruction getAsm = null, getTypes = null, mNb = null;
        foreach (var i in method.Body.Instructions)
        {
            var mr = i.Operand as MethodReference;
            if (mr == null) continue;
            if (mr.Name == "GetExecutingAssembly" && mr.DeclaringType.FullName == "System.Reflection.Assembly") getAsm = i;
            if (mr.Name == "GetTypes" && mr.DeclaringType.FullName == "System.Reflection.Assembly") getTypes = i;
            if (mr.Name == "_mNb" && mr.DeclaringType.Name == "_ozb") mNb = i;
        }
        if (getAsm == null || getTypes == null || mNb == null)
            throw new Exception("Bloc GetExecutingAssembly/GetTypes/_mNb introuvable");
        foreach (var i in new[] { getAsm, getTypes, mNb }) { i.OpCode = OpCodes.Nop; i.Operand = null; }
    }

    static void PatchUxTheme(ModuleDefinition mod)
    {
        var ctor = FindMethod(FindType(mod, "_d4b"), ".ctor");
        Instruction call = null;
        foreach (var ins in ctor.Body.Instructions)
        {
            if (ins.OpCode != OpCodes.Call) continue;
            var mr = ins.Operand as MethodReference;
            if (mr != null && mr.Name == "_ouA" && mr.DeclaringType.Name == "_d4b" &&
                mr.ReturnType.FullName == "System.Boolean") { call = ins; break; }
        }
        if (call == null) throw new Exception("_d4b::_ouA introuvable");
        call.OpCode = OpCodes.Ldc_I4_0;
        call.Operand = null;
    }

    static void PatchSkipCurlCleanup(ModuleDefinition mod)
    {
        var method = FindMethod(FindType(mod, "_uqA"), "_9Hb");
        bool found = false;
        foreach (var ins in method.Body.Instructions)
        {
            var mr = ins.Operand as MethodReference;
            if ((ins.OpCode == OpCodes.Call || ins.OpCode == OpCodes.Callvirt) && mr != null &&
                mr.DeclaringType.Name == "_gDb" && mr.Name == "_eyb")
            {
                ins.OpCode = OpCodes.Nop; ins.Operand = null; found = true;
            }
        }
        if (!found) throw new Exception("_gDb::_eyb introuvable");
    }

    static void PatchConsoleException(ModuleDefinition mod)
    {
        var method = FindMethod(FindType(mod, "_uqA"), "_pQb", 1);
        method.Body = new MethodBody(method);
        var il = method.Body.GetILProcessor();
        var ret = il.Create(OpCodes.Ret);
        var writeLine = mod.ImportReference(typeof(Console).GetMethod("WriteLine", new Type[] { typeof(object) }));
        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Brfalse_S, ret));
        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Call, writeLine));
        il.Append(ret);
    }

    static void PatchWvCtor(ModuleDefinition mod)
    {
        var type = FindType(mod, "_wV");
        var ctor = FindMethod(type, ".ctor", 0);
        ctor.Body = new MethodBody(ctor);
        var il = ctor.Body.GetILProcessor();
        var panelType = mod.ImportReference(typeof(System.Windows.Forms.Panel)).Resolve();
        var panelCtorDef = panelType.Methods.First(m => m.Name == ".ctor" && m.Parameters.Count == 0);
        var panelCtor = mod.ImportReference(panelCtorDef);
        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Call, panelCtor));
        il.Append(il.Create(OpCodes.Ret));
    }

    static void PatchMono24(ModuleDefinition mod)
    {
        var type = FindType(mod, "_ryA");
        MakeRet(FindMethod(type, ".cctor", 0));
        MakeRet(FindMethod(type, "_IpB", 0));
        var pza = FindMethod(type, "_PzA", 1);
        pza.Body = new MethodBody(pza);
        var il = pza.Body.GetILProcessor();
        var comboType = mod.ImportReference(typeof(System.Windows.Forms.ComboBox)).Resolve();
        var wndProcDef = comboType.Methods.First(m => m.Name == "WndProc" && m.Parameters.Count == 1);
        var wndProc = mod.ImportReference(wndProcDef);
        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Ldarg_1));
        il.Append(il.Create(OpCodes.Call, wndProc));
        il.Append(il.Create(OpCodes.Ret));
    }

    static void PatchMono26(ModuleDefinition mod)
    {
        var type = FindType(mod, "_71B");
        MakeRet(FindMethod(type, ".cctor", 0));
        var hk = FindMethod(type, "_hk", 1);
        var buttonType = mod.ImportReference(typeof(System.Windows.Forms.Button)).Resolve();
        var wndProcDef = buttonType.Methods.First(m => m.Name == "WndProc" && m.Parameters.Count == 1);
        var wndProc = mod.ImportReference(wndProcDef);
        hk.Body = new MethodBody(hk);
        var il = hk.Body.GetILProcessor();
        il.Append(il.Create(OpCodes.Ldarg_0)); il.Append(il.Create(OpCodes.Ldarg_1));
        il.Append(il.Create(OpCodes.Call, wndProc)); il.Append(il.Create(OpCodes.Ret));
    }

    static void PatchMono27(ModuleDefinition mod)
    {
        var vk = FindMethod(FindType(mod, "_Nc"), "_vk", 2);
        vk.Body = new MethodBody(vk); vk.Body.InitLocals = true;
        var nullableColor = mod.ImportReference(typeof(Nullable<System.Drawing.Color>));
        vk.Body.Variables.Add(new VariableDefinition(nullableColor));
        var il = vk.Body.GetILProcessor();
        var hasValue = mod.ImportReference(typeof(Nullable<System.Drawing.Color>).GetProperty("HasValue").GetGetMethod());
        var getValue = mod.ImportReference(typeof(Nullable<System.Drawing.Color>).GetMethod("GetValueOrDefault", Type.EmptyTypes));
        var setBackColor = mod.ImportReference(typeof(System.Windows.Forms.Control).GetProperty("BackColor").GetSetMethod());
        var setForeColor = mod.ImportReference(typeof(System.Windows.Forms.Control).GetProperty("ForeColor").GetSetMethod());
        var getWindow = mod.ImportReference(typeof(System.Drawing.SystemColors).GetProperty("Window").GetGetMethod());
        var getWindowText = mod.ImportReference(typeof(System.Drawing.SystemColors).GetProperty("WindowText").GetGetMethod());
        var useDefault = Instruction.Create(OpCodes.Nop);
        var setBack = Instruction.Create(OpCodes.Nop);
        il.Append(il.Create(OpCodes.Ldarg_1)); il.Append(il.Create(OpCodes.Stloc_0));
        il.Append(il.Create(OpCodes.Ldloca_S, vk.Body.Variables[0])); il.Append(il.Create(OpCodes.Call, hasValue));
        il.Append(il.Create(OpCodes.Brfalse_S, useDefault));
        il.Append(il.Create(OpCodes.Ldarg_0)); il.Append(il.Create(OpCodes.Ldloca_S, vk.Body.Variables[0]));
        il.Append(il.Create(OpCodes.Call, getValue)); il.Append(il.Create(OpCodes.Br_S, setBack));
        il.Append(useDefault); il.Append(il.Create(OpCodes.Ldarg_0)); il.Append(il.Create(OpCodes.Call, getWindow));
        il.Append(setBack); il.Append(il.Create(OpCodes.Callvirt, setBackColor));
        il.Append(il.Create(OpCodes.Ldarg_0)); il.Append(il.Create(OpCodes.Call, getWindowText));
        il.Append(il.Create(OpCodes.Callvirt, setForeColor)); il.Append(il.Create(OpCodes.Ret));
    }

    static void PatchMono28(ModuleDefinition mod)
    {
        var t = FindType(mod, "_2PB");
        var toString = mod.ImportReference(typeof(long).GetMethod("ToString", Type.EmptyTypes));
        foreach (var name in new[] { "_12B", "_yRb" })
        {
            var m = FindMethod(t, name, 1); m.Body = new MethodBody(m); var il = m.Body.GetILProcessor();
            il.Append(il.Create(OpCodes.Ldarga_S, m.Parameters[0]));
            il.Append(il.Create(OpCodes.Call, toString)); il.Append(il.Create(OpCodes.Ret));
        }
    }

    static void PatchMono29(ModuleDefinition mod)
    {
        var t = FindType(mod, "_1QB"); var ctor = FindMethod(t, ".ctor", 0); var gyb = FindMethod(t, "_gYB", 0);
        var dgvCtor = mod.ImportReference(typeof(System.Windows.Forms.DataGridView).GetConstructor(Type.EmptyTypes));
        ctor.Body = new MethodBody(ctor); ctor.Body.InitLocals = false; var il = ctor.Body.GetILProcessor();
        il.Append(il.Create(OpCodes.Ldarg_0)); il.Append(il.Create(OpCodes.Call, dgvCtor));
        il.Append(il.Create(OpCodes.Ldarg_0)); il.Append(il.Create(OpCodes.Call, gyb)); il.Append(il.Create(OpCodes.Ret));
    }

    static void PatchMono30(ModuleDefinition mod)
    {
        var t = FindType(mod, "_3yA"); var cctor = FindMethod(t, ".cctor", 0);
        var t3=t.Fields.First(f=>f.Name=="_T3"); var uab=t.Fields.First(f=>f.Name=="_uAB"); var v4=t.Fields.First(f=>f.Name=="_V4");
        var ola=FindType(mod,"_olA"); var olaCtor=FindMethod(ola,".ctor",0);
        cctor.Body=new MethodBody(cctor); cctor.Body.InitLocals=false; var il=cctor.Body.GetILProcessor();
        il.Append(il.Create(OpCodes.Newobj,olaCtor)); il.Append(il.Create(OpCodes.Stsfld,t3));
        il.Append(il.Create(OpCodes.Ldnull)); il.Append(il.Create(OpCodes.Stsfld,uab));
        il.Append(il.Create(OpCodes.Ldnull)); il.Append(il.Create(OpCodes.Stsfld,v4)); il.Append(il.Create(OpCodes.Ret));
    }

    static int PatchRecursive(TypeDefinition type, Func<MethodReference,bool> match, Action<ILProcessor,Instruction> action)
    {
        int count=0;
        foreach(var method in type.Methods)
        {
            if(!method.HasBody) continue;
            var il=method.Body.GetILProcessor();
            for(int i=0;i<method.Body.Instructions.Count;i++)
            {
                var ins=method.Body.Instructions[i];
                if(ins.OpCode!=OpCodes.Call && ins.OpCode!=OpCodes.Callvirt) continue;
                var mr=ins.Operand as MethodReference;
                if(mr==null || !match(mr)) continue;
                action(il,ins); count++;
            }
        }
        foreach(var n in type.NestedTypes) count+=PatchRecursive(n,match,action);
        return count;
    }

    static void PatchMono31(ModuleDefinition mod)
    {
        foreach(var root in mod.Types)
            PatchRecursive(root, mr => mr.Name=="_bi" && mr.DeclaringType!=null && mr.DeclaringType.Name=="_OWA" &&
                mr.Parameters.Count==2 && mr.ReturnType.FullName=="System.Boolean",
                (il,ins)=> { il.InsertBefore(ins,il.Create(OpCodes.Pop)); il.InsertBefore(ins,il.Create(OpCodes.Pop));
                             ins.OpCode=OpCodes.Ldc_I4_1; ins.Operand=null; });
    }

    static void PatchMono32(ModuleDefinition mod)
    {
        foreach(var root in mod.Types)
            PatchRecursive(root, mr => mr.Name=="_ns" && mr.DeclaringType!=null && mr.DeclaringType.Name=="_Ji" &&
                mr.Parameters.Count==3 && mr.ReturnType.FullName=="System.IntPtr",
                (il,ins)=> { il.InsertBefore(ins,il.Create(OpCodes.Pop)); il.InsertBefore(ins,il.Create(OpCodes.Pop));
                             il.InsertBefore(ins,il.Create(OpCodes.Pop)); ins.OpCode=OpCodes.Ldc_I4_0; ins.Operand=null;
                             il.InsertAfter(ins,il.Create(OpCodes.Conv_I)); });
    }

    static void PatchMono33(ModuleDefinition mod)
    {
        var type=FindType(mod,"_iF"); var field=type.Fields.First(f=>f.Name=="_elA"); var ctor=FindMethod(type,".ctor",0);
        var il=ctor.Body.GetILProcessor(); var ret=ctor.Body.Instructions.First(i=>i.OpCode==OpCodes.Ret);
        il.InsertBefore(ret,il.Create(OpCodes.Ldarg_0)); il.InsertBefore(ret,il.Create(OpCodes.Ldstr,""));
        il.InsertBefore(ret,il.Create(OpCodes.Stfld,field));
    }

    static int NeutralizeOneArgCall(MethodDefinition method, string declaringType, string methodName)
    {
        int count=0; var il=method.Body.GetILProcessor();
        for(int i=0;i<method.Body.Instructions.Count;i++)
        {
            var ins=method.Body.Instructions[i];
            if(ins.OpCode!=OpCodes.Call && ins.OpCode!=OpCodes.Callvirt) continue;
            var mr=ins.Operand as MethodReference;
            if(mr==null || mr.DeclaringType.Name!=declaringType || mr.Name!=methodName) continue;
            il.InsertBefore(ins,il.Create(OpCodes.Pop)); ins.OpCode=OpCodes.Ldc_I4_0; ins.Operand=null; count++;
        }
        return count;
    }

    static void PatchMono34(ModuleDefinition mod)
    {
        var t71=FindType(mod,"_71B"); var spa=FindMethod(t71,"_sPA");
        spa.Body.ExceptionHandlers.Clear(); spa.Body.Variables.Clear(); spa.Body.Instructions.Clear();
        spa.Body.GetILProcessor().Append(Instruction.Create(OpCodes.Ret));
        NeutralizeOneArgCall(FindMethod(FindType(mod,"_Gyb"),"_ATB"),"_Ji","_Ey");
        NeutralizeOneArgCall(FindMethod(FindType(mod,"_ryA"),"_VU"),"_eZb","_fG");
    }

    static void PatchMono35(ModuleDefinition mod)
    {
        var method=FindMethod(FindType(mod,"_etb"),"_MNb"); var il=method.Body.GetILProcessor(); int count=0;
        for(int i=0;i<method.Body.Instructions.Count;i++)
        {
            var ins=method.Body.Instructions[i];
            if(ins.OpCode!=OpCodes.Call && ins.OpCode!=OpCodes.Callvirt) continue;
            var mr=ins.Operand as MethodReference;
            if(mr==null || mr.DeclaringType.FullName!="System.Drawing.Bitmap" || mr.Name!="SetResolution") continue;
            il.InsertBefore(ins,il.Create(OpCodes.Pop)); il.InsertBefore(ins,il.Create(OpCodes.Pop));
            ins.OpCode=OpCodes.Pop; ins.Operand=null; count++;
        }
        if(count!=1) throw new Exception("Nombre inattendu de SetResolution : "+count);
    }

    static void PatchMono37(ModuleDefinition mod)
    {
        foreach(var root in mod.Types)
            PatchRecursive(root, mr => mr.DeclaringType!=null && mr.DeclaringType.Name=="_Ji" && mr.Name=="_Ey",
                (il,ins)=> { ins.OpCode=OpCodes.Pop; ins.Operand=null; il.InsertAfter(ins,il.Create(OpCodes.Ldc_I4_0)); });
    }

    static void Main(string[] args)
    {
        if(args.Length!=2) { Console.WriteLine("Usage: patch-mono-compat.exe input.exe output.exe"); return; }
        var asm=AssemblyDefinition.ReadAssembly(args[0]); var mod=asm.MainModule;

        // mono-10..13 : les quatre modifications de _uqA::_3DA.
        PatchCallSetDll(mod);
        PatchShowInitException(mod);
        PatchSkipGetTypes(mod);

        // mono-14..22.
        PatchUxTheme(mod);
        MakeRet(FindMethod(FindType(mod,"_d4b"),"_bYA"));
        MakeRet(FindMethod(FindType(mod,"_OWA"),"_vab",0));
        MakeRet(FindMethod(FindType(mod,"_uqA"),"_z5b",0));
        PatchSkipCurlCleanup(mod);
        PatchConsoleException(mod);
        MakeRet(FindMethod(FindType(mod,"_MXB"),"_ST",0));
        PatchWvCtor(mod);
        MakeRet(FindMethod(FindType(mod,"_wV"),"_plB",1));

        // mono-23..35.
        var ux=FindType(mod,"_uXb"); MakeRet(FindMethod(ux,".cctor",0)); MakeRet(FindMethod(ux,"_qX",1));
        PatchMono24(mod);
        MakeRet(FindMethod(FindType(mod,"_Nc"),"_b3A",0));
        PatchMono26(mod);
        PatchMono27(mod);
        PatchMono28(mod);
        PatchMono29(mod);
        PatchMono30(mod);
        PatchMono31(mod);
        PatchMono32(mod);
        PatchMono33(mod);
        PatchMono34(mod);
        PatchMono35(mod);

        // IMPORTANT : mono-37 est appliqué directement à l'état mono-35.
        // On n'applique PAS mono-36, qui avait le bug de cible de branche.
        PatchMono37(mod);

        asm.Write(args[1]);
        Console.WriteLine("Créé : " + args[1]);
    }
}
