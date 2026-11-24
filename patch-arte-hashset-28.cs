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
                "usage: patch-arte-hashset-28.exe input.exe output.exe");
            Environment.Exit(2);
        }

        var asm = AssemblyDefinition.ReadAssembly(args[0]);
        var mod = asm.MainModule;

        var xfb = mod.GetTypes()
            .FirstOrDefault(t => t.FullName == "_Xfb");

        var oob = mod.GetTypes()
            .FirstOrDefault(t => t.FullName == "_Xfb/_oob");

        if (xfb == null || oob == null)
        {
            Console.Error.WriteLine("ERROR: _Xfb/_oob not found");
            Environment.Exit(3);
        }

        var boolType = mod.TypeSystem.Boolean;
        var stringType = mod.TypeSystem.String;

        var hashOpen = mod.ImportReference(
            typeof(ISet<>));

        var hashString = new GenericInstanceType(hashOpen);
        hashString.GenericArguments.Add(stringType);

        var helper = new MethodDefinition(
            "_LockedAdd",
            MethodAttributes.Assembly |
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
            typeof(ISet<string>).GetMethod(
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

        xfb.Methods.Add(helper);

        var targets = new[]
        {
            xfb.Methods.Single(m =>
                m.Name == "_0gB" && m.HasBody),

            oob.Methods.Single(m =>
                m.Name == "MoveNext" && m.HasBody)
        };

        int[] expectedOffsets = { 0x00F7, 0x00E1 };

        int total = 0;

        for (int i = 0; i < targets.Length; i++)
        {
            var method = targets[i];

            var matches = method.Body.Instructions
                .Where(ins =>
                {
                    if (ins.OpCode != OpCodes.Callvirt)
                        return false;

                    var mr = ins.Operand as MethodReference;

                    return mr != null &&
                        mr.Name == "Add" &&
                        mr.DeclaringType.FullName ==
                            "System.Collections.Generic.ISet`1<System.String>" &&
                        mr.Parameters.Count == 1 &&
                        mr.ReturnType.FullName == "System.Boolean";
                })
                .ToList();

            if (matches.Count != 1 ||
                matches[0].Offset != expectedOffsets[i])
            {
                Console.Error.WriteLine(
                    "ERROR: unexpected Add call in " +
                    method.FullName);

                Environment.Exit(5);
            }

            var target = matches[0];

            Console.WriteLine(
                "patch {0} IL_{1:X4}: {2}",
                method.FullName,
                target.Offset,
                target.Operand);

            target.OpCode = OpCodes.Call;
            target.Operand = helper;

            total++;
        }

        Console.WriteLine("patched=" + total);

        asm.Write(args[1]);

        Console.WriteLine("written: " + args[1]);
    }
}
