using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

class TestQuote
{
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--child")
        {
            if (args.Length != 2) return 2;
            Console.Write(args[1]);
            return 0;
        }
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: test-quote.exe captvty-cli.exe");
            return 2;
        }
        Assembly cli = Assembly.LoadFrom(Path.GetFullPath(args[0]));
        MethodInfo quote = cli.GetType("CaptvtyCli", true).GetMethod(
            "Quote", BindingFlags.Static | BindingFlags.NonPublic,
            null, new Type[] { typeof(string) }, null);
        if (quote == null)
        {
            Console.Error.WriteLine("Quote(string) introuvable");
            return 2;
        }
        string self = Assembly.GetExecutingAssembly().Location;
        string[] patterns = {
            "Eldorado", "*/6", "*(3/6)", "Eldorado (?/6)",
            @"Film \?", @"Film \*", @"A\\B", "Film \"Eldorado\"",
            "", @"C:\video\"
        };
        int failed = 0;
        foreach (string pattern in patterns)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = Environment.GetEnvironmentVariable("MONO") ?? "mono";
            psi.Arguments = (string)quote.Invoke(null, new object[] { self })
                + " --child " + (string)quote.Invoke(null, new object[] { pattern });
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            using (Process p = Process.Start(psi))
            {
                string actual = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                bool ok = actual == pattern && p.ExitCode == 0;
                if (!ok) failed++;
                Console.WriteLine("{0} [{1}] -> [{2}] (rc={3})",
                    ok ? "OK" : "FAIL", pattern, actual, p.ExitCode);
            }
        }
        Console.WriteLine("SUMMARY passed={0} failed={1}", patterns.Length - failed, failed);
        return failed == 0 ? 0 : 1;
    }
}
