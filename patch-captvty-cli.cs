using System;
using Mono.Cecil;
using Mono.Cecil.Cil;

class P
{
    static MethodDefinition FindUnique(
        TypeDefinition type,
        string name)
    {
        MethodDefinition found = null;
        int count = 0;

        foreach (MethodDefinition m in type.Methods)
        {
            if (m.Name == name)
            {
                found = m;
                count++;
            }
        }

        if (count != 1)
            throw new Exception(
                type.FullName + "::" + name +
                " : " + count + " méthode(s) trouvée(s)");

        if (!found.HasBody)
            throw new Exception(
                type.FullName + "::" + name +
                " n'a pas de corps");

        if (found.ReturnType.FullName != "System.Void")
            throw new Exception(
                type.FullName + "::" + name +
                " retourne " + found.ReturnType.FullName);

        return found;
    }

    static void ReplaceByRet(MethodDefinition m)
    {
        m.Body.Instructions.Clear();
        m.Body.ExceptionHandlers.Clear();
        m.Body.Variables.Clear();
        m.Body.InitLocals = false;

        ILProcessor il = m.Body.GetILProcessor();
        il.Append(il.Create(OpCodes.Ret));

        m.Body.MaxStackSize = 0;
    }

    static void Main(string[] args)
    {
        if (args.Length != 2)
            throw new Exception(
                "usage: patch-captvty-cli input.exe output.exe");

        string input = args[0];
        string output = args[1];

        AssemblyDefinition asm =
            AssemblyDefinition.ReadAssembly(input);

        ModuleDefinition mod = asm.MainModule;

        TypeDefinition exb = mod.GetType("_exB");

        if (exb == null)
            throw new Exception("_exB introuvable");

        MethodDefinition gua =
            FindUnique(exb, "_GUA");

        MethodDefinition xfb =
            FindUnique(exb, "_XFb");

        Console.WriteLine("Base : " + input);
        Console.WriteLine("Patch : " + gua.FullName);
        Console.WriteLine("Patch : " + xfb.FullName);

        ReplaceByRet(gua);
        ReplaceByRet(xfb);

        asm.Write(output);

        Console.WriteLine(
            "OK : moteur CLI écrit dans " + output);
    }
}
