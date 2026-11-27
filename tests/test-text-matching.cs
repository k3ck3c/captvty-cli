using System;

class TestTextMatching
{
    static int passed, failed;

    static void Check(string text, string pattern, bool expected)
    {
        bool actual = TextMatching.ContainsText(text, pattern);
        bool ok = actual == expected;

        if (ok) passed++;
        else failed++;

        Console.WriteLine(
            "{0} text=[{1}] pattern=[{2}] expected={3} actual={4}",
            ok ? "OK" : "FAIL",
            text, pattern, expected, actual);
    }

    static int Main()
    {
        // Historical behaviour
        Check("Eldorado (3/6)", "Eldorado", true);
        Check("Eldorado (3/6)", "/6", true);
        Check("Eldorado (3/6)", "4/6", false);
        Check("Eldorado", "", true);
        Check("Eldorado", null, true);
        Check(null, "Eldorado", false);
        Check("Eldorado", "ELDORADO", true);
        Check("Éldorado", "eldorado", true);

        // Wildcards
        Check("Eldorado (3/6)", "Eldorado*", true);
        Check("Eldorado (3/6)", "*3/6*", true);
        Check("Eldorado (3/6)", "*/6", false);
        Check("Eldorado (3/6)", "*/6)", true);
        Check("Eldorado (3/6)", "*(3/6)", true);
        Check("Eldorado (3/6)", "*(4/6)", false);
        Check("Eldorado (3/6)", "Eldorado (?/6)", true);
        Check("Eldorado (3/6)", "Eldorado (??/6)", false);
        Check("Eldorado (3/6)", "*", true);
        Check("Eldorado (3/6)", "**", true);
        Check("Éldorado (3/6)", "ELDORADO*", true);
        Check("Le grand Eldorado", "*Eldorado", true);
        Check("Le grand Eldorado", "Eldorado*", false);

        // Empty strings
        Check("", "*", true);
        Check("", "?", false);
        Check("", "", true);
        Check(null, "*", true);

        // Literal punctuation
        Check("Film [2026]", "Film [*]", true);
        Check("Film (2026)", "Film [*]", false);
        Check("A.B", "A.*", true);
        Check("AXB", "A.*", false);
        Check("A+B", "A+*", true);

        // Multiple wildcards
        Check("abcdef", "a*c*f", true);
        Check("abcdef", "a*d*b", false);
        Check("abcdef", "a**?*f", true);
        Check("abcdef", "*?*?*", true);
        Check("a", "*?*?*", false);
        Check("abc", "???", true);
        Check("abc", "????", false);
        Check("ababa", "*aba", true);
        Check("ababa", "*abb", false);
        Check("abc", "abc*", true);

        Console.WriteLine();
        Console.WriteLine(
            "SUMMARY passed={0} failed={1}",
            passed, failed);

        return failed == 0 ? 0 : 1;
    }
}
