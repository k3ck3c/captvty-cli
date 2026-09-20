using System;

class FakeProvider
{
    static void Main()
    {
        Console.WriteLine("CHANNEL\tTEST TV");
        Console.WriteLine("TITLE\tTest audio");

        Console.WriteLine("EMISSION\tTEST TV\t1\tSans audio\tTest");
        Console.WriteLine("DURATION\tTEST TV\t1\t1000");
        Console.WriteLine("MEDIA\tTEST TV\t1\t1280\t720\t800\t1\t0\t0\t0\t0\t0");

        Console.WriteLine("EMISSION\tTEST TV\t2\tUne piste\tTest");
        Console.WriteLine("DURATION\tTEST TV\t2\t1000");
        Console.WriteLine("AUDIO\tTEST TV\t2\tfr\t192\t1");
        Console.WriteLine("MEDIA\tTEST TV\t2\t1280\t720\t800\t1\t0\t0\t0\t0\t0");

        Console.WriteLine("EMISSION\tTEST TV\t3\tDeux pistes\tTest");
        Console.WriteLine("DURATION\tTEST TV\t3\t1000");
        Console.WriteLine("AUDIO\tTEST TV\t3\tfr\t192\t1");
        Console.WriteLine("AUDIO\tTEST TV\t3\tqad\t192\t0");
        Console.WriteLine("MEDIA\tTEST TV\t3\t1280\t720\t800\t1\t0\t0\t0\t0\t0");
    }
}
