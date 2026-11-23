using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

class PatchDpi28
{
    static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.WriteLine("Usage: patch-dpi-28.exe input.exe output.exe");
            return 1;
        }

        var asm = AssemblyDefinition.ReadAssembly(args[0], new ReaderParameters { InMemory = true });
        var type = asm.MainModule.Types.First(t => t.Name == "_RMb");
        var method = type.Methods.First(m => m.Name == "_Srb");

        var il = method.Body.Instructions;

        int index = -1;

        for (int i = 5; i < il.Count; i++)
        {
            if (il[i].OpCode == OpCodes.Callvirt &&
                il[i].Operand.ToString().Contains("Bitmap::SetResolution"))
            {
                index = i;
                break;
            }
        }

        if (index < 5)
        {
            Console.WriteLine("SetResolution introuvable");
            return 2;
        }

        // Supprimer les 6 instructions :
        // bitmap, image, HorizontalResolution,
        // image, VerticalResolution, SetResolution.
        // Les 6 instructions deviennent NOP.

        for (int i = index - 5; i <= index; i++)
        {
            il[i].OpCode = OpCodes.Nop;
            il[i].Operand = null;
        }

        asm.Write(args[1]);

        Console.WriteLine("Patch DPI appliqué : _RMb::_Srb");
        return 0;
    }
}
