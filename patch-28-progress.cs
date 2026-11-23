using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

class Program
{
    static void Main(string[] args)
    {
        var asm = AssemblyDefinition.ReadAssembly(args[0]);

        var type = asm.MainModule.Types
            .First(t => t.Name == "_qh")
            .NestedTypes.First(t => t.Name == "_X4B");

        foreach (var name in new[] { "_qJB", "_a0" })
        {
            var method = type.Methods.First(m => m.Name == name);
            var il = method.Body.GetILProcessor();

            var first = method.Body.Instructions[0];

            if (first.OpCode != OpCodes.Call ||
                ((MethodReference)first.Operand).Name != "_4HA")
                throw new Exception("Structure inattendue: " + name);

            var next = first.Next;

            var dup = il.Create(OpCodes.Dup);
            var test = il.Create(OpCodes.Brtrue, next);
            var pop = il.Create(OpCodes.Pop);
            var ret = il.Create(OpCodes.Ret);

            il.InsertAfter(first, dup);
            il.InsertAfter(dup, test);
            il.InsertAfter(test, pop);
            il.InsertAfter(pop, ret);

            method.Body.MaxStackSize += 1;

            Console.WriteLine("Patched: " + method.FullName);
        }

        asm.Write(args[1]);
        Console.WriteLine("Patch OK");
    }
}
