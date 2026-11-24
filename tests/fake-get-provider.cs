using System;
using System.IO;
using System.Threading;

class FakeGetProvider
{
    static int Main()
    {
        Console.WriteLine(
            "GETMEDIA\t1\tTest vidéo\t1920\t1080\t4200");

        Console.Out.Flush();

        for (int i = 0; i < 10; i++)
        {
            Console.WriteLine(
                "GETSTATE\t1\tProgression " + (i * 10) + "%");

            Console.Out.Flush();
            Thread.Sleep(200);
        }

        File.WriteAllText(
            "fake-download-complete.txt",
            "Téléchargement terminé");

        Console.WriteLine("GETSTATE\t5\tvideo.tmp.mp4");
        Console.WriteLine("GETSTATE\t8\tvideo.mp4");
        Console.WriteLine("GETSTATE\t7\tvideo.mkv");

        Console.WriteLine(
            "DOWNLOAD\tTEST TV\tTest vidéo\tvideo.mkv");

        Console.Out.Flush();

        return 0;
    }
}
