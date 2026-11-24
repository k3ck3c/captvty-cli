using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using System.Diagnostics;

class TestHashSet
{
    delegate bool AddDelegate(ISet<string> h, string s);

    static int Main(string[] args)
    {
        var asm = Assembly.LoadFrom(args[0]);

        var type = asm.GetType("_Xfb", true);

        var method = type.GetMethod(
            "_LockedAdd",
            BindingFlags.Static |
            BindingFlags.NonPublic);

        var add = (AddDelegate)Delegate.CreateDelegate(
            typeof(AddDelegate), method);

        const int workers = 8;
        const int iterations = 50000;

        for (int round = 1; round <= 5; round++)
        {
            var h = new HashSet<string>();
            var sw = Stopwatch.StartNew();

            Parallel.For(0, workers, worker =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    string key = worker + ":" + i;

                    if (!add(h, key))
                        throw new Exception("Unexpected duplicate");

                    if (add(h, key))
                        throw new Exception("Duplicate accepted");
                }
            });

            sw.Stop();

            int expected = workers * iterations;

            Console.WriteLine(
                "Round {0}: count={1} expected={2} time={3}ms",
                round, h.Count, expected,
                sw.ElapsedMilliseconds);

            if (h.Count != expected)
                return 1;
        }

        Console.WriteLine("ALL TESTS PASSED");
        return 0;
    }
}
