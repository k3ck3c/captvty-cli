using System;
class TestTextMatchingEscape
{
    static int passed, failed;
    static void Check(string text, string pattern, bool expected)
    {
        bool actual = TextMatching.ContainsText(text, pattern);
        if (actual == expected) passed++; else failed++;
        Console.WriteLine("{0} text=[{1}] pattern=[{2}]", actual == expected ? "OK" : "FAIL", text, pattern);
    }
    static int Main()
    {
        Check("Un Film ? inédit", @"Film \?", true);
        Check("Un Film X inédit", @"Film \?", false);
        Check("Un Film * inédit", @"Film \*", true);
        Check("Un Film X inédit", @"Film \*", false);
        Check("Un Film ?", @"*Film \?", true);
        Check("Un Film X", @"*Film \?", false);
        Check("Un Film ? inédit", @"*Film \?", false);
        Check("A*B", @"A\*?", true);
        Check("AX B", @"A\*?", false);
        Check(@"A\B", @"A\\B", true);
        Check(@"C:\video\", @"C:\video\", true);
        Check(@"A\q", @"A\q", true);
        Check(@"A\xyz", @"A\\*", true);
        Check("Émission ?", @"emission \?", true);
        Check("?", @"\?", true);
        Check("*", @"\*", true);
        Console.WriteLine("SUMMARY passed={0} failed={1}", passed, failed);
        return failed == 0 ? 0 : 1;
    }
}
