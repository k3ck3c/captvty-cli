using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;

public class FakeDownload
{
    public string Primary;
    public List<string> Paths = new List<string>();

    public string _OOA()
    {
        return Primary;
    }

    public List<string> _OEA()
    {
        return Paths;
    }
}

class TestFinalPath
{
    static MethodInfo resolve;
    static int passed;
    static int failed;

    static void Test(string name, FakeDownload item, string expected)
    {
        Stopwatch sw = Stopwatch.StartNew();

        string result = (string)resolve.Invoke(
            null, new object[] { item });

        bool ok = result == expected;

        Console.WriteLine(
            "{0} | {1} | {2} ms",
            ok ? "OK" : "FAIL",
            name,
            sw.ElapsedMilliseconds);

        if (ok)
            passed++;
        else
        {
            failed++;
            Console.WriteLine("  Attendu : " + expected);
            Console.WriteLine("  Obtenu  : " + result);
        }
    }

    static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(
            Path.GetFullPath(args[0]));

        Type provider = asm.GetType("CaptvtyProvider", true);

        resolve = provider.GetMethod(
            "ResolveFinalDownloadPath",
            BindingFlags.Static | BindingFlags.NonPublic);

        string dir = Path.Combine(
            Path.GetTempPath(),
            "captvty-test-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(dir);

        try
        {
            string mp4 = Path.Combine(dir, "video.mp4");
            string mkv = Path.Combine(dir, "video_ST.mkv");
            string tmp = Path.Combine(dir, "video.tmp.mp4");
            string vtt = Path.Combine(dir, "video.fr.vtt");
            string mp3 = Path.Combine(dir, "radio.mp3");
            string delayed = Path.Combine(dir, "delayed.mkv");

            File.WriteAllText(mp4, "test");
            File.WriteAllText(mkv, "test");
            File.WriteAllText(tmp, "test");
            File.WriteAllText(vtt, "test");
            File.WriteAllText(mp3, "test");

            Test("MKV principal", new FakeDownload {
                Primary = mkv,
                Paths = new List<string> { mp4, mkv }
            }, mkv);

            Test("MP4 principal", new FakeDownload {
                Primary = mp4
            }, mp4);

            Test("Radio MP3", new FakeDownload {
                Primary = mp3
            }, mp3);

            File.Delete(mp4);

            Test("Jossi 1/6 : MP4 supprime", new FakeDownload {
                Primary = mp4,
                Paths = new List<string> { mp4, mkv }
            }, mkv);

            Test("Ignorer les sous-titres", new FakeDownload {
                Primary = vtt,
                Paths = new List<string> { vtt, mkv }
            }, mkv);

            Test("Ignorer les fichiers temporaires", new FakeDownload {
                Primary = tmp,
                Paths = new List<string> { tmp, mkv }
            }, mkv);

            Thread worker = new Thread(delegate()
            {
                Thread.Sleep(1200);
                File.WriteAllText(delayed, "test");
            });

            worker.Start();

            Test("Fichier apparaissant avec retard", new FakeDownload {
                Primary = delayed
            }, delayed);

            worker.Join();

            Test("Aucun fichier valide", new FakeDownload {
                Primary = mp4,
                Paths = new List<string> { mp4, tmp, vtt }
            }, "");

            Test("Liste nulle", new FakeDownload {
                Primary = "",
                Paths = null
            }, "");

            Test("Plusieurs candidats ambigus", new FakeDownload {
                Primary = mp4,
                Paths = new List<string> { mkv, mp3 }
            }, "");
        }
        finally
        {
            Directory.Delete(dir, true);
        }

        Console.WriteLine();
        Console.WriteLine(
            "SUMMARY passed={0} failed={1}",
            passed, failed);

        return failed == 0 ? 0 : 1;
    }
}
