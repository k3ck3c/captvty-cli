using System;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;

class PatchM6
{
    const int JsonPathId = -307253335;

    const string NewJsonPath =
        "clips[0].assets[?(@.video_container == 'm3u8' && @.video_quality == 'hd')].full_physical_path";

    static TypeDefinition FindNested(TypeDefinition parent, string name)
    {
        foreach (TypeDefinition t in parent.NestedTypes)
        {
            if (t.Name == name)
                return t;

            TypeDefinition found = FindNested(t, name);
            if (found != null)
                return found;
        }
        return null;
    }

    static TypeDefinition FindTop(ModuleDefinition module, string name)
    {
        foreach (TypeDefinition t in module.Types)
            if (t.Name == name)
                return t;
        return null;
    }

    static int? GetInt32Constant(Instruction ins)
    {
        if (ins.OpCode == OpCodes.Ldc_I4 && ins.Operand is int)
            return (int)ins.Operand;

        if (ins.OpCode == OpCodes.Ldc_I4_S && ins.Operand is sbyte)
            return (sbyte)ins.Operand;

        if (ins.OpCode == OpCodes.Ldc_I4_M1) return -1;
        if (ins.OpCode == OpCodes.Ldc_I4_0) return 0;
        if (ins.OpCode == OpCodes.Ldc_I4_1) return 1;
        if (ins.OpCode == OpCodes.Ldc_I4_2) return 2;
        if (ins.OpCode == OpCodes.Ldc_I4_3) return 3;
        if (ins.OpCode == OpCodes.Ldc_I4_4) return 4;
        if (ins.OpCode == OpCodes.Ldc_I4_5) return 5;
        if (ins.OpCode == OpCodes.Ldc_I4_6) return 6;
        if (ins.OpCode == OpCodes.Ldc_I4_7) return 7;
        if (ins.OpCode == OpCodes.Ldc_I4_8) return 8;

        return null;
    }

    static bool IsStringDecryptorCall(Instruction ins)
    {
        if (ins == null || ins.OpCode != OpCodes.Call)
            return false;

        MethodReference mr = ins.Operand as MethodReference;
        if (mr == null)
            return false;

        GenericInstanceMethod gim = mr as GenericInstanceMethod;
        if (gim != null &&
            gim.GenericArguments.Count == 1 &&
            gim.GenericArguments[0].FullName == "System.String")
            return true;

        // Fallback utile avec certains rendus Cecil de méthodes globales génériques.
        return mr.ReturnType != null &&
               mr.ReturnType.FullName == "System.String";
    }

    static bool IsSelectTokensCall(Instruction ins)
    {
        if (ins == null ||
            (ins.OpCode != OpCodes.Callvirt && ins.OpCode != OpCodes.Call))
            return false;

        MethodReference mr = ins.Operand as MethodReference;
        if (mr == null)
            return false;

        return mr.Name == "SelectTokens" &&
               mr.Parameters.Count == 1 &&
               mr.Parameters[0].ParameterType.FullName == "System.String";
    }

    static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine(
                "Usage: mono patch-m6.exe INPUT.exe OUTPUT.exe");
            return 2;
        }

        string input = args[0];
        string output = args[1];

        if (!File.Exists(input))
        {
            Console.Error.WriteLine("Erreur: fichier introuvable: " + input);
            return 2;
        }

        if (Path.GetFullPath(input) == Path.GetFullPath(output))
        {
            Console.Error.WriteLine(
                "Erreur: INPUT et OUTPUT doivent être différents.");
            return 2;
        }

        AssemblyDefinition asm = null;

        try
        {
            asm = AssemblyDefinition.ReadAssembly(input);
            ModuleDefinition module = asm.MainModule;

            TypeDefinition t3Ob = FindTop(module, "_3Ob");
            if (t3Ob == null)
                throw new InvalidOperationException("type _3Ob introuvable");

            TypeDefinition tBo = FindNested(t3Ob, "_Bo");
            if (tBo == null)
                throw new InvalidOperationException("type _3Ob/_Bo introuvable");

            MethodDefinition moveNext = null;
            foreach (MethodDefinition m in tBo.Methods)
            {
                if (m.Name == "MoveNext" &&
                    !m.HasParameters &&
                    m.ReturnType.FullName == "System.Void")
                {
                    if (moveNext != null)
                        throw new InvalidOperationException(
                            "plusieurs MoveNext() trouvés dans _3Ob/_Bo");
                    moveNext = m;
                }
            }

            if (moveNext == null || !moveNext.HasBody)
                throw new InvalidOperationException(
                    "_3Ob/_Bo::MoveNext() introuvable ou sans corps");

            int idCount = 0;
            int fullMatches = 0;
            Instruction decryptCall = null;
            Instruction selectTokens = null;

            var ins = moveNext.Body.Instructions;

            for (int i = 0; i < ins.Count; i++)
            {
                int? v = GetInt32Constant(ins[i]);
                if (!v.HasValue || v.Value != JsonPathId)
                    continue;

                idCount++;

                Instruction candidateDecrypt = null;
                Instruction candidateSelect = null;

                // Monodis montre les trois opérations contiguës, mais on tolère
                // quelques instructions intermédiaires pour ne pas dépendre du
                // rendu exact de Cecil.
                int max = Math.Min(ins.Count, i + 9);
                for (int j = i + 1; j < max; j++)
                {
                    if (candidateDecrypt == null && IsStringDecryptorCall(ins[j]))
                    {
                        candidateDecrypt = ins[j];
                        continue;
                    }

                    if (candidateDecrypt != null && IsSelectTokensCall(ins[j]))
                    {
                        candidateSelect = ins[j];
                        break;
                    }
                }

                if (candidateDecrypt != null && candidateSelect != null)
                {
                    fullMatches++;
                    decryptCall = candidateDecrypt;
                    selectTokens = candidateSelect;
                }
            }

            if (idCount != 1)
                throw new InvalidOperationException(
                    "id JSONPath " + JsonPathId +
                    " attendu 1 fois, trouvé " + idCount);

            if (fullMatches != 1)
                throw new InvalidOperationException(
                    "séquence déchiffreur/SelectTokens attendue 1 fois, trouvée " +
                    fullMatches);

            // Vérification supplémentaire : SelectTokens doit suivre le
            // déchiffreur à très courte distance.
            int decryptIndex = ins.IndexOf(decryptCall);
            int selectIndex = ins.IndexOf(selectTokens);
            if (selectIndex <= decryptIndex || selectIndex - decryptIndex > 6)
                throw new InvalidOperationException(
                    "distance inattendue entre déchiffreur et SelectTokens");

            ILProcessor il = moveNext.Body.GetILProcessor();

            // L'ancien JSONPath reste produit par le déchiffreur. On le jette,
            // puis on pousse le filtre corrigé. Aucun opcode existant n'est
            // remplacé : les cibles de branches restent donc intactes.
            Instruction popOld = il.Create(OpCodes.Pop);
            Instruction loadNew = il.Create(OpCodes.Ldstr, NewJsonPath);

            il.InsertAfter(decryptCall, popOld);
            il.InsertAfter(popOld, loadNew);

            asm.Write(output);

            Console.WriteLine("M6 patch OK");
            Console.WriteLine("  méthode : _3Ob/_Bo::MoveNext()");
            Console.WriteLine("  id      : " + JsonPathId);
            Console.WriteLine("  filtre  : " + NewJsonPath);
            Console.WriteLine("  sortie  : " + output);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                "Erreur: " + ex.GetType().Name + ": " + ex.Message);

            try
            {
                if (File.Exists(output))
                    File.Delete(output);
            }
            catch { }

            return 1;
        }
        finally
        {
            if (asm != null)
                asm.Dispose();
        }
    }
}
