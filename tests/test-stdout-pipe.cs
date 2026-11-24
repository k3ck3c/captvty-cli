using System;
using System.IO;
using System.Text;

class TestStdoutPipe
{
    static int Main()
    {
        try
        {
            Console.OutputEncoding = new UTF8Encoding(false);

            Console.SetOut(
                new StdoutPipeWriter(Console.Out));

            for (int i = 0; i < 100000; i++)
                Console.WriteLine("Test ligne " + i);

            Console.Out.Flush();
            return 0;
        }
        catch (StdoutPipeClosedException)
        {
            return 141;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine(
                "HResult : 0x" + ex.HResult.ToString("X8"));

            Console.Error.WriteLine(ex.Message);
            return 42;
        }
    }
}
