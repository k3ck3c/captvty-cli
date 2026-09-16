using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using Mono.Cecil;
using Mono.Cecil.Cil;

class P
{
    static void Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine(
                "usage: patch-bfmtv-hashset.exe input.exe output.exe");
            Environment.Exit(2);
        }

        var asm = AssemblyDefinition.ReadAssembly(args[0]);
        var mod = asm.MainModule;

        var mbb = mod.GetTypes()
            .FirstOrDefault(t => t.FullName == "_CmA/_MBb");

        var dib = mod.GetTypes()
            .FirstOrDefault(t => t.FullName == "_CmA/_MBb/_Dib");

        if (mbb == null || dib == null)
        {
            Console.Error.WriteLine("ERROR: _MBb/_Dib not found");
            Environment.Exit(3);
        }

        /*
         * Add:
         *
         * static bool _LockedAdd(HashSet<string> h, string s)
         * {
         *     Monitor.Enter(h);
         *     try {
         *         return h.Add(s);
         *     }
         *     finally {
         *         Monitor.Exit(h);
         *     }
         * }
         */

        var boolType = mod.TypeSystem.Boolean;
        var stringType = mod.TypeSystem.String;

        var hashOpen = mod.ImportReference(
            typeof(HashSet<>));

        var hashString = new GenericInstanceType(hashOpen);
        hashString.GenericArguments.Add(stringType);

        var helper = new MethodDefinition(
            "_LockedAdd",
            MethodAttributes.Private |
            MethodAttributes.Static |
            MethodAttributes.HideBySig,
            boolType);

        helper.Parameters.Add(
            new ParameterDefinition("h",
                ParameterAttributes.None,
                hashString));

        helper.Parameters.Add(
            new ParameterDefinition("s",
                ParameterAttributes.None,
                stringType));

        helper.Body.InitLocals = true;

        var result = new VariableDefinition(boolType);
        helper.Body.Variables.Add(result);

        var il = helper.Body.GetILProcessor();

        var monitorEnter = mod.ImportReference(
            typeof(Monitor).GetMethod(
                "Enter",
                new Type[] { typeof(object) }));

        var monitorExit = mod.ImportReference(
            typeof(Monitor).GetMethod(
                "Exit",
                new Type[] { typeof(object) }));

        var hashAdd = mod.ImportReference(
            typeof(HashSet<string>).GetMethod(
                "Add",
                new Type[] { typeof(string) }));

        var tryStart = Instruction.Create(OpCodes.Ldarg_0);
        var finallyStart = Instruction.Create(OpCodes.Ldarg_0);
        var afterFinally = Instruction.Create(OpCodes.Ldloc, result);

        il.Append(Instruction.Create(OpCodes.Ldarg_0));
        il.Append(Instruction.Create(OpCodes.Call, monitorEnter));

        il.Append(tryStart);
        il.Append(Instruction.Create(OpCodes.Ldarg_1));
        il.Append(Instruction.Create(OpCodes.Callvirt, hashAdd));
        il.Append(Instruction.Create(OpCodes.Stloc, result));
        il.Append(Instruction.Create(OpCodes.Leave, afterFinally));

        il.Append(finallyStart);
        il.Append(Instruction.Create(OpCodes.Call, monitorExit));
        il.Append(Instruction.Create(OpCodes.Endfinally));

        il.Append(afterFinally);
        il.Append(Instruction.Create(OpCodes.Ret));

        helper.Body.ExceptionHandlers.Add(
            new ExceptionHandler(ExceptionHandlerType.Finally)
            {
                TryStart = tryStart,
                TryEnd = finallyStart,
                HandlerStart = finallyStart,
                HandlerEnd = afterFinally
            });

        mbb.Methods.Add(helper);

        /*
         * Find exactly:
         *
         * callvirt bool HashSet<string>::Add(string)
         *
         * inside _CmA/_MBb/_Dib::MoveNext()
         */

        var moveNext = dib.Methods
            .FirstOrDefault(m =>
                m.Name == "MoveNext" &&
                m.HasBody);

        if (moveNext == null)
        {
            Console.Error.WriteLine("ERROR: MoveNext not found");
            Environment.Exit(4);
        }

        int patched = 0;

        foreach (var ins in moveNext.Body.Instructions)
        {
            if (ins.OpCode != OpCodes.Callvirt)
                continue;

            var mr = ins.Operand as MethodReference;
            if (mr == null)
                continue;

            if (mr.Name != "Add")
                continue;

            if (!mr.DeclaringType.FullName.StartsWith(
                    "System.Collections.Generic.HashSet`1<System.String>"))
                continue;

            Console.WriteLine(
                "patch IL_{0:X4}: {1}",
                ins.Offset,
                mr.FullName);

            ins.OpCode = OpCodes.Call;
            ins.Operand = helper;
            patched++;
        }

        Console.WriteLine("patched=" + patched);

        if (patched != 1)
        {
            Console.Error.WriteLine(
                "ERROR: expected exactly one HashSet<string>.Add; " +
                "output NOT written");
            Environment.Exit(5);
        }

        asm.Write(args[1]);

        Console.WriteLine("written: " + args[1]);
    }
}
