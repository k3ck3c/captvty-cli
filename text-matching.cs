using System;
using System.Globalization;
using System.Text;
using System.Collections.Generic;

internal static class TextMatching
{
    public static string NormalizeText(string s)
    {
        if (String.IsNullOrEmpty(s)) return "";
        string d = s.Normalize(NormalizationForm.FormD);
        StringBuilder b = new StringBuilder(d.Length);
        foreach (char c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                b.Append(c);
        return b.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    private enum Kind { Literal, Star, Any }
    private struct Token
    {
        public Kind Type;
        public char Value;
        public Token(Kind type, char value) { Type = type; Value = value; }
    }

    public static bool ContainsText(string text, string wanted)
    {
        string t = NormalizeText(text);
        string p = NormalizeText(wanted);
        List<Token> tokens = new List<Token>();
        StringBuilder literal = new StringBuilder();
        bool wildcard = false;
        for (int i = 0; i < p.Length; i++)
        {
            char c = p[i];
            if (c == '\\' && i + 1 < p.Length &&
                (p[i + 1] == '*' || p[i + 1] == '?' || p[i + 1] == '\\'))
            {
                c = p[++i];
            }
            else if (c == '*' || c == '?')
            {
                tokens.Add(new Token(c == '*' ? Kind.Star : Kind.Any, c));
                wildcard = true;
                continue;
            }
            tokens.Add(new Token(Kind.Literal, c));
            literal.Append(c);
        }
        if (!wildcard)
            return t.IndexOf(literal.ToString(), StringComparison.Ordinal) >= 0;
        return WildcardMatch(t, tokens);
    }

    private static bool WildcardMatch(string text, List<Token> pattern)
    {
        int t = 0, p = 0, star = -1, retry = 0;
        while (t < text.Length)
        {
            if (p < pattern.Count &&
                (pattern[p].Type == Kind.Any ||
                 (pattern[p].Type == Kind.Literal && pattern[p].Value == text[t])))
            { t++; p++; }
            else if (p < pattern.Count && pattern[p].Type == Kind.Star)
            { star = p++; retry = t; }
            else if (star >= 0)
            { p = star + 1; t = ++retry; }
            else return false;
        }
        while (p < pattern.Count && pattern[p].Type == Kind.Star) p++;
        return p == pattern.Count;
    }
}
