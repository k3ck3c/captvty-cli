using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Reflection;
using System.Text;

class CaptvtyCli
{
    class MediaInfo
    {
        public int Width;
        public int Height;
        public int Bitrate;
        public bool Best;
        public int LO;
        public int ZzA;
        public bool Pk;
        public bool Au;
        public bool UhA;
    }

    class EmissionInfo
    {
        public int Number;
        public string Title;
        public string Subtitle;
        public string Error;
        public List<MediaInfo> Media = new List<MediaInfo>();
    }

    class ProviderResult
    {
        public string Channel;
        public int Count;
        public string Title;
        public List<MediaInfo> Media = new List<MediaInfo>();
        public List<EmissionInfo> Emissions = new List<EmissionInfo>();
    }

    class WorkerRun
    {
        public bool TimedOut;
        public int ExitCode;
        public string Stdout;
        public string Stderr;
    }

    const BindingFlags All =
        BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Static | BindingFlags.Instance;

    static MethodInfo FindMethod(Type t, string name, int argc)
    {
        while (t != null)
        {
            foreach (MethodInfo mi in t.GetMethods(All))
                if (mi.Name == name && mi.GetParameters().Length == argc)
                    return mi;
            t = t.BaseType;
        }
        return null;
    }

    static List<string> ReadChannels(string enginePath)
    {
        Assembly a = Assembly.LoadFrom(enginePath);
        Type uu = a.GetType("_Uu", true);

        System.Collections.IEnumerable channels =
            (System.Collections.IEnumerable)
            uu.GetField("_afB", All).GetValue(null);

        List<string> names = new List<string>();

        foreach (object channel in channels)
        {
            MethodInfo mi = FindMethod(channel.GetType(), "_b9A", 0);
            if (mi == null) continue;

            string name = mi.Invoke(channel, null) as string;
            if (!String.IsNullOrEmpty(name))
                names.Add(name);
        }

        return names;
    }

    static string Quote(string s)
    {
        if (s == null) return "\"\"";
        return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    static WorkerRun RunWorker(
        string worker, string mode, string engine, string channel,
        string query, int timeoutMs)
    {
        ProcessStartInfo psi = new ProcessStartInfo();
        psi.FileName = "mono";
        psi.Arguments =
            Quote(worker) + " " +
            Quote(mode) + " " +
            Quote(engine) + " " +
            Quote(channel) + " " +
            Quote(query);
        psi.UseShellExecute = false;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.CreateNoWindow = true;

        StringBuilder stdout = new StringBuilder();
        StringBuilder stderr = new StringBuilder();

        Process p = new Process();
        p.StartInfo = psi;

        p.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null) stdout.AppendLine(e.Data);
        };

        p.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null) stderr.AppendLine(e.Data);
        };

        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        bool finished = p.WaitForExit(timeoutMs);

        if (!finished)
        {
            try { p.Kill(); } catch { }
            try { p.WaitForExit(); } catch { }

            return new WorkerRun {
                TimedOut = true,
                ExitCode = -1,
                Stdout = stdout.ToString(),
                Stderr = stderr.ToString()
            };
        }

        p.WaitForExit();

        return new WorkerRun {
            TimedOut = false,
            ExitCode = p.ExitCode,
            Stdout = stdout.ToString(),
            Stderr = stderr.ToString()
        };
    }

    static WorkerRun RunGetWorker(
        string worker, string engine, string channel,
        string title, string subtitle, string quality, int timeoutMs)
    {
        ProcessStartInfo psi = new ProcessStartInfo();
        psi.FileName = "mono";
        psi.Arguments =
            Quote(worker) + " get " +
            Quote(engine) + " " +
            Quote(channel) + " " +
            Quote(title) + " " +
            Quote(subtitle) + " " +
            Quote(quality);
        psi.UseShellExecute = false;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.CreateNoWindow = true;

        StringBuilder stdout = new StringBuilder();
        StringBuilder stderr = new StringBuilder();

        Process p = new Process();
        p.StartInfo = psi;

        p.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null) return;

            stdout.AppendLine(e.Data);
            string line = e.Data.TrimStart('\uFEFF');

            if (line.StartsWith("GETMEDIA\t", StringComparison.Ordinal))
            {
                string[] x = line.Split('\t');
                if (x.Length >= 6)
                    Console.WriteLine(
                        "Média: " + x[1] + " - " + x[2] +
                        " - " + x[3] + "x" + x[4] +
                        " - " + x[5] + " kb/s");
            }
            else if (line.StartsWith("GETSTATE\t", StringComparison.Ordinal))
            {
                string[] x = line.Split('\t');
                if (x.Length >= 2)
                {
                    Console.Write("État Captvty: " + x[1]);
                    if (x.Length >= 3 && !String.IsNullOrEmpty(x[2]))
                        Console.Write(" - " + x[2]);
                    Console.WriteLine();
                }
            }
            else if (line.StartsWith("DOWNLOAD\t", StringComparison.Ordinal))
            {
                string[] x = line.Split('\t');
                if (x.Length >= 4)
                {
                    Console.WriteLine("Téléchargement terminé:");
                    Console.WriteLine("  " + x[3]);
                }
            }
        };

        p.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null) stderr.AppendLine(e.Data);
        };

        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        bool finished = p.WaitForExit(timeoutMs);

        if (!finished)
        {
            try { p.Kill(); } catch { }
            try { p.WaitForExit(); } catch { }

            return new WorkerRun {
                TimedOut = true,
                ExitCode = -1,
                Stdout = stdout.ToString(),
                Stderr = stderr.ToString()
            };
        }

        p.WaitForExit();

        return new WorkerRun {
            TimedOut = false,
            ExitCode = p.ExitCode,
            Stdout = stdout.ToString(),
            Stderr = stderr.ToString()
        };
    }

    static int ParseInt(string s)
    {
        int n;
        return Int32.TryParse(s, out n) ? n : 0;
    }

    static bool ParseBool01(string s) { return s == "1"; }

    static ProviderResult ParseWorkerOutput(
        string text, string fallbackChannel)
    {
        if (String.IsNullOrEmpty(text)) return null;
        ProviderResult result = null;
        Dictionary<int, EmissionInfo> emissions =
            new Dictionary<int, EmissionInfo>();

        using (StringReader sr = new StringReader(text))
        {
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                line = line.TrimStart('\uFEFF');

                if (line.StartsWith("RESULT\t", StringComparison.Ordinal))
                {
                    string[] p = line.Split('\t');
                    if (p.Length < 4) continue;

                    int count;
                    if (!Int32.TryParse(p[2], out count)) count = 1;

                    result = new ProviderResult {
                        Channel = String.IsNullOrEmpty(p[1]) ? fallbackChannel : p[1],
                        Count = count,
                        Title = p[3]
                    };
                    continue;
                }

                if (line.StartsWith("EMISSION\t", StringComparison.Ordinal))
                {
                    string[] p = line.Split('\t');
                    if (p.Length < 5) continue;

                    if (result == null)
                        result = new ProviderResult {
                            Channel = fallbackChannel, Count = 1
                        };

                    int no = ParseInt(p[2]);
                    EmissionInfo em = new EmissionInfo {
                        Number = no,
                        Title = p[3],
                        Subtitle = p[4]
                    };
                    result.Emissions.Add(em);
                    emissions[no] = em;
                    continue;
                }

                if (line.StartsWith("INFOERROR\t", StringComparison.Ordinal))
                {
                    string[] p = line.Split('\t');
                    if (p.Length < 4) continue;

                    EmissionInfo em;
                    if (emissions.TryGetValue(ParseInt(p[2]), out em))
                        em.Error = p[3];
                    continue;
                }

                if (line.StartsWith("MEDIA\t", StringComparison.Ordinal))
                {
                    string[] p = line.Split('\t');

                    // V2 protocol: MEDIA channel emission width height bitrate best LO zzA pk au uhA
                    if (p.Length >= 12)
                    {
                        int no = ParseInt(p[2]);
                        EmissionInfo em;
                        if (!emissions.TryGetValue(no, out em))
                            continue;

                        em.Media.Add(new MediaInfo {
                            Width = ParseInt(p[3]),
                            Height = ParseInt(p[4]),
                            Bitrate = ParseInt(p[5]),
                            Best = ParseBool01(p[6]),
                            LO = ParseInt(p[7]),
                            ZzA = ParseInt(p[8]),
                            Pk = ParseBool01(p[9]),
                            Au = ParseBool01(p[10]),
                            UhA = ParseBool01(p[11])
                        });
                        continue;
                    }

                    // Compatibility with V1 worker output.
                    if (p.Length >= 11)
                    {
                        if (result == null)
                            result = new ProviderResult {
                                Channel = fallbackChannel, Count = 1
                            };

                        result.Media.Add(new MediaInfo {
                            Width = ParseInt(p[2]),
                            Height = ParseInt(p[3]),
                            Bitrate = ParseInt(p[4]),
                            Best = ParseBool01(p[5]),
                            LO = ParseInt(p[6]),
                            ZzA = ParseInt(p[7]),
                            Pk = ParseBool01(p[8]),
                            Au = ParseBool01(p[9]),
                            UhA = ParseBool01(p[10])
                        });
                    }
                }
            }
        }

        return result;
    }

    class CacheItem
    {
        public string Channel;
        public string Title;
        public string Subtitle;
    }

    static string NormalizeText(string s)
    {
        if (String.IsNullOrEmpty(s)) return "";
        string d = s.Normalize(NormalizationForm.FormD);
        StringBuilder b = new StringBuilder(d.Length);
        foreach (char c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                b.Append(c);
        return b.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    static bool ContainsText(string text, string wanted)
    {
        return NormalizeText(text).IndexOf(NormalizeText(wanted), StringComparison.Ordinal) >= 0;
    }

    static string JsonEscape(string s)
    {
        if (s == null) return "";
        StringBuilder b = new StringBuilder();
        foreach (char c in s)
        {
            switch (c)
            {
                case '\\': b.Append("\\\\"); break;
                case '"': b.Append("\\\""); break;
                case '\b': b.Append("\\b"); break;
                case '\f': b.Append("\\f"); break;
                case '\n': b.Append("\\n"); break;
                case '\r': b.Append("\\r"); break;
                case '\t': b.Append("\\t"); break;
                default:
                    if (c < 32) b.Append("\\u" + ((int)c).ToString("x4"));
                    else b.Append(c);
                    break;
            }
        }
        return b.ToString();
    }

    static string JsonUnescape(string s)
    {
        StringBuilder b = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c != '\\' || i + 1 >= s.Length) { b.Append(c); continue; }
            char n = s[++i];
            switch (n)
            {
                case '\\': b.Append('\\'); break;
                case '"': b.Append('"'); break;
                case 'b': b.Append('\b'); break;
                case 'f': b.Append('\f'); break;
                case 'n': b.Append('\n'); break;
                case 'r': b.Append('\r'); break;
                case 't': b.Append('\t'); break;
                case 'u':
                    if (i + 4 < s.Length)
                    {
                        int v;
                        if (Int32.TryParse(s.Substring(i + 1, 4), NumberStyles.HexNumber,
                                           CultureInfo.InvariantCulture, out v))
                        { b.Append((char)v); i += 4; }
                    }
                    break;
                default: b.Append(n); break;
            }
        }
        return b.ToString();
    }

    static string ExtractJsonString(string line, string name)
    {
        string marker = "\"" + name + "\":\"";
        int p = line.IndexOf(marker, StringComparison.Ordinal);
        if (p < 0) return "";
        p += marker.Length;
        StringBuilder raw = new StringBuilder();
        bool esc = false;
        for (; p < line.Length; p++)
        {
            char c = line[p];
            if (!esc && c == '"') break;
            raw.Append(c);
            if (esc) esc = false;
            else if (c == '\\') esc = true;
        }
        return JsonUnescape(raw.ToString());
    }

    static List<CacheItem> ReadCache(string path)
    {
        List<CacheItem> items = new List<CacheItem>();
        if (!File.Exists(path)) return items;
        foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
        {
            string line = raw.Trim();
            if (!line.StartsWith("{\"channel\":", StringComparison.Ordinal)) continue;
            CacheItem x = new CacheItem();
            x.Channel = ExtractJsonString(line, "channel");
            x.Title = ExtractJsonString(line, "title");
            x.Subtitle = ExtractJsonString(line, "subtitle");
            if (!String.IsNullOrEmpty(x.Channel)) items.Add(x);
        }
        return items;
    }

    static DateTimeOffset? ReadCacheUpdated(string path)
    {
        if (!File.Exists(path)) return null;
        foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
        {
            string line = raw.Trim();
            if (!line.StartsWith("\"updated\"", StringComparison.Ordinal)) continue;
            int colon = line.IndexOf(':');
            if (colon < 0) continue;
            string value = line.Substring(colon + 1).Trim().TrimEnd(',').Trim();
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                value = JsonUnescape(value.Substring(1, value.Length - 2));
            DateTimeOffset dt;
            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                                        DateTimeStyles.RoundtripKind, out dt))
                return dt;
        }
        return null;
    }

    static Dictionary<string,string> ReadCacheStatus(string path)
    {
        Dictionary<string,string> result =
            new Dictionary<string,string>(StringComparer.CurrentCultureIgnoreCase);
        if (!File.Exists(path)) return result;

        bool inStatus = false;
        foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
        {
            string line = raw.Trim();
            if (!inStatus)
            {
                if (line.StartsWith("\"status\"", StringComparison.Ordinal)) inStatus = true;
                continue;
            }
            if (line.StartsWith("}", StringComparison.Ordinal)) break;
            int colon = line.IndexOf(':');
            if (colon < 0) continue;
            string k = line.Substring(0, colon).Trim().Trim('"');
            string v = line.Substring(colon + 1).Trim().TrimEnd(',').Trim().Trim('"');
            if (k.Length > 0) result[JsonUnescape(k)] = JsonUnescape(v);
        }
        return result;
    }

    static string CacheAge(DateTimeOffset updated)
    {
        TimeSpan age = DateTimeOffset.Now - updated;
        if (age < TimeSpan.Zero) age = TimeSpan.Zero;
        if (age.TotalMinutes < 1) return "moins d'une minute";
        if (age.TotalHours < 1)
        {
            int m = Math.Max(1, (int)Math.Floor(age.TotalMinutes));
            return m + " min";
        }
        if (age.TotalDays < 1)
        {
            int h = Math.Max(1, (int)Math.Floor(age.TotalHours));
            return h + " h";
        }
        int d = Math.Max(1, (int)Math.Floor(age.TotalDays));
        int rh = age.Hours;
        return rh > 0 ? d + " j " + rh + " h" : d + " j";
    }

    static string FindChannel(List<string> channels, string wanted)
    {
        foreach (string c in channels)
            if (String.Equals(c, wanted, StringComparison.CurrentCultureIgnoreCase))
                return c;
        return null;
    }

    static int RunUpdate(string engine, string worker, string cachePath, string[] requested, int timeoutMs)
    {
        List<string> allChannels = ReadChannels(engine);
        List<string> selected = new List<string>();

        if (requested.Length == 0)
        {
            int limit = Math.Min(10, allChannels.Count);
            for (int i = 0; i < limit; i++) selected.Add(allChannels[i]);
        }
        else if (requested.Length == 1 &&
                 String.Equals(requested[0], "--all", StringComparison.OrdinalIgnoreCase))
        {
            selected.AddRange(allChannels);
        }
        else
        {
            foreach (string wanted in requested)
            {
                string c = FindChannel(allChannels, wanted);
                if (c == null)
                {
                    Console.Error.WriteLine("Chaîne inconnue: " + wanted);
                    return 2;
                }
                if (!selected.Contains(c)) selected.Add(c);
            }
        }

        List<CacheItem> oldItems = ReadCache(cachePath);
        Dictionary<string,string> status = ReadCacheStatus(cachePath);
        List<CacheItem> items = new List<CacheItem>(oldItems);

        int n = 0;
        foreach (string channel in selected)
        {
            n++;
            Console.Write("[" + n + "/" + selected.Count + "] " + channel + " ... ");
            Console.Out.Flush();

            WorkerRun run = RunWorker(worker, "dump", engine, channel, "", timeoutMs);
            if (run.TimedOut)
            {
                Console.WriteLine("timeout (ancien cache conservé)");
                status[channel] = "timeout";
                continue;
            }
            if (run.ExitCode != 0)
            {
                Console.WriteLine("erreur (" + run.ExitCode + ") (ancien cache conservé)");
                status[channel] = "error";
                continue;
            }

            List<CacheItem> fresh = new List<CacheItem>();
            using (StringReader sr = new StringReader(run.Stdout))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    line = line.TrimStart('\uFEFF');
                    if (!line.StartsWith("ITEM\t", StringComparison.Ordinal)) continue;
                    string[] p = line.Split('\t');
                    if (p.Length < 5) continue;
                    fresh.Add(new CacheItem { Channel = p[1], Title = p[3], Subtitle = p[4] });
                }
            }

            items.RemoveAll(delegate(CacheItem x) {
                return String.Equals(x.Channel, channel, StringComparison.CurrentCultureIgnoreCase);
            });
            items.AddRange(fresh);
            status[channel] = fresh.Count == 0 ? "empty" : "ok";
            Console.WriteLine("OK (" + fresh.Count + ")");
        }

        string tmp = cachePath + ".tmp";
        using (StreamWriter w = new StreamWriter(tmp, false, new UTF8Encoding(false)))
        {
            w.WriteLine("{");
            w.WriteLine("  \"version\": 2,");
            w.WriteLine("  \"updated\": \"" + JsonEscape(DateTimeOffset.Now.ToString("o")) + "\",");
            w.WriteLine("  \"programs\": [");
            for (int i = 0; i < items.Count; i++)
            {
                CacheItem x = items[i];
                w.Write("    {\"channel\":\"" + JsonEscape(x.Channel) +
                        "\",\"title\":\"" + JsonEscape(x.Title) +
                        "\",\"subtitle\":\"" + JsonEscape(x.Subtitle) + "\"}");
                if (i + 1 < items.Count) w.Write(",");
                w.WriteLine();
            }
            w.WriteLine("  ],");
            w.WriteLine("  \"status\": {");

            List<string> statusNames = new List<string>(status.Keys);
            statusNames.Sort(StringComparer.CurrentCultureIgnoreCase);
            for (int i = 0; i < statusNames.Count; i++)
            {
                string channel = statusNames[i];
                w.Write("    \"" + JsonEscape(channel) + "\": \"" +
                        JsonEscape(status[channel]) + "\"");
                if (i + 1 < statusNames.Count) w.Write(",");
                w.WriteLine();
            }
            w.WriteLine("  }");
            w.WriteLine("}");
        }

        if (File.Exists(cachePath))
        {
            string backup = cachePath + ".bak";
            if (File.Exists(backup)) File.Delete(backup);
            File.Replace(tmp, cachePath, backup);
            try { File.Delete(backup); } catch { }
        }
        else
            File.Move(tmp, cachePath);

        Console.WriteLine();
        Console.WriteLine("Catalogue: " + items.Count + " émission(s)");
        Console.WriteLine("Cache écrit: " + cachePath);
        return 0;
    }

    static int RunCachedSearch(string query, string cachePath)
    {
        List<CacheItem> items = ReadCache(cachePath);
        if (items.Count == 0)
        {
            Console.Error.WriteLine("Cache absent ou vide. Lancez: mono captvty-cli.exe update");
            return 3;
        }

        Dictionary<string,List<CacheItem>> byChannel = new Dictionary<string,List<CacheItem>>(StringComparer.CurrentCultureIgnoreCase);
        foreach (CacheItem x in items)
        {
            if (!ContainsText(x.Title, query) && !ContainsText(x.Subtitle, query)) continue;
            List<CacheItem> list;
            if (!byChannel.TryGetValue(x.Channel, out list))
            { list = new List<CacheItem>(); byChannel[x.Channel] = list; }
            list.Add(x);
        }

        Console.WriteLine("\"" + query + "\" est disponible sur :");
        if (byChannel.Count == 0)
        {
            Console.WriteLine("  aucun résultat");
            DateTimeOffset? emptyUpdated = ReadCacheUpdated(cachePath);
            if (emptyUpdated.HasValue)
                Console.WriteLine("[cache mis à jour il y a " + CacheAge(emptyUpdated.Value) + "]");
            return 0;
        }

        List<string> names = new List<string>(byChannel.Keys);
        names.Sort(StringComparer.CurrentCultureIgnoreCase);
        foreach (string channel in names)
        {
            List<CacheItem> list = byChannel[channel];
            Console.Write("  " + channel);
            if (list.Count > 1) Console.Write(" (" + list.Count + ")");
            if (list.Count > 0 && !String.IsNullOrEmpty(list[0].Title)) Console.Write(" - " + list[0].Title);
            Console.WriteLine();
        }
        Console.WriteLine();
        Console.WriteLine(byChannel.Count + " chaîne(s)");
        DateTimeOffset? updated = ReadCacheUpdated(cachePath);
        if (updated.HasValue)
            Console.WriteLine("[cache mis à jour il y a " + CacheAge(updated.Value) + "]");
        else
            Console.WriteLine("[cache]");
        return 0;
    }

    static int RunTargetedInfo(string channel, string query, string engine, string worker, int timeoutMs)
    {
        Console.Write("[1] " + channel + " ... ");
        Console.Out.Flush();
        WorkerRun run = RunWorker(worker, "info", engine, channel, query, timeoutMs);
        if (run.TimedOut) { Console.WriteLine("timeout"); return 1; }
        if (run.ExitCode != 0) { Console.WriteLine("erreur (" + run.ExitCode + ")"); return run.ExitCode; }
        ProviderResult r = ParseWorkerOutput(run.Stdout, channel);
        if (r == null) { Console.WriteLine("OK"); Console.WriteLine("Aucun résultat."); return 0; }

        int qualityCount = r.Media.Count;
        foreach (EmissionInfo em in r.Emissions) qualityCount += em.Media.Count;
        Console.WriteLine("OK (" + r.Count + ", " + qualityCount + " qualité(s))");
        Console.WriteLine();
        Console.WriteLine("Informations pour \"" + query + "\" :");
        Console.WriteLine();
        Console.WriteLine("  " + r.Channel + " — " + r.Title);
        foreach (EmissionInfo em in r.Emissions)
        {
            Console.WriteLine();
            Console.Write("    " + em.Number + ". " + em.Title);
            if (!String.IsNullOrEmpty(em.Subtitle)) Console.Write(" - " + em.Subtitle);
            Console.WriteLine();
            if (!String.IsNullOrEmpty(em.Error)) { Console.WriteLine("       [résolution impossible: " + em.Error + "]"); continue; }
            em.Media.Sort(CompareMedia);
            foreach (MediaInfo m in em.Media)
                PrintMedia(m, "       ");
        }
        Console.WriteLine();
        Console.WriteLine("1 chaîne(s)");
        return 0;
    }

    static string QualityName(MediaInfo m)
    {
        if (m.Height >= 2160) return "2160p";
        if (m.Height >= 1440) return "1440p";
        if (m.Height >= 1080) return "1080p";
        if (m.Height >= 720)  return "720p";
        if (m.Height >= 576)  return "576p";
        if (m.Height >= 480)  return "480p";
        if (m.Height >= 360)  return "360p";
        if (m.Height > 0)     return m.Height + "p";
        return "?";
    }

    static void PrintMedia(MediaInfo m, string indent)
    {
        Console.Write(indent);
        Console.Write(m.Best ? "* " : "  ");

        if (m.Width > 0 && m.Height > 0)
            Console.Write(m.Width + "x" + m.Height + "  " + QualityName(m));
        else
            Console.Write("résolution inconnue");

        if (m.Bitrate > 0)
        {
            Console.Write("  " + m.Bitrate + " kb/s");
            Console.Write("  (" + (m.Bitrate / 1000.0).ToString("0.00",
                CultureInfo.InvariantCulture) + " Mb/s)");
        }

        if (m.Best) Console.Write("  [choix Captvty]");
        Console.WriteLine();
    }

    static int CompareMedia(MediaInfo x, MediaInfo y)
    {
        if (x.Best != y.Best) return x.Best ? -1 : 1;

        int px = x.Width * x.Height;
        int py = y.Width * y.Height;

        int c = py.CompareTo(px);
        return c != 0 ? c : y.Bitrate.CompareTo(x.Bitrate);
    }

    static int RunSearchOrInfo(
        string mode, string query, string engine, string worker, int timeoutMs)
    {
        List<string> channels = ReadChannels(engine);
        List<ProviderResult> results = new List<ProviderResult>();
        int n = 0;

        foreach (string channel in channels)
        {
            n++;
            Console.Write("[" + n + "] " + channel + " ... ");
            Console.Out.Flush();

            WorkerRun run =
                RunWorker(worker, mode, engine, channel, query, timeoutMs);

            if (run.TimedOut)
            {
                Console.WriteLine("timeout");
                continue;
            }

            if (run.ExitCode != 0)
            {
                Console.WriteLine("erreur (" + run.ExitCode + ")");
                continue;
            }

            ProviderResult r = ParseWorkerOutput(run.Stdout, channel);

            if (r == null)
                Console.WriteLine("OK");
            else
            {
                int qualityCount = r.Media.Count;
                foreach (EmissionInfo em in r.Emissions)
                    qualityCount += em.Media.Count;

                Console.WriteLine(
                    String.Equals(mode, "info", StringComparison.OrdinalIgnoreCase)
                    ? "OK (" + r.Count + ", " + qualityCount + " qualité(s))"
                    : "OK (" + r.Count + ")");
                results.Add(r);
            }
        }

        results.Sort(delegate(ProviderResult x, ProviderResult y)
        {
            return String.Compare(
                x.Channel, y.Channel,
                StringComparison.CurrentCultureIgnoreCase);
        });

        Console.WriteLine();

        if (String.Equals(mode, "search", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("\"" + query + "\" est disponible sur :");

            if (results.Count == 0)
            {
                Console.WriteLine("  aucun résultat");
                return 0;
            }

            foreach (ProviderResult r in results)
            {
                Console.Write("  " + r.Channel);
                if (r.Count > 1) Console.Write(" (" + r.Count + ")");
                if (!String.IsNullOrEmpty(r.Title)) Console.Write(" - " + r.Title);
                Console.WriteLine();
            }

            Console.WriteLine();
            Console.WriteLine(results.Count + " chaîne(s)");
            return 0;
        }

        Console.WriteLine("Informations pour \"" + query + "\" :");

        if (results.Count == 0)
        {
            Console.WriteLine("  aucun résultat");
            return 0;
        }

        foreach (ProviderResult r in results)
        {
            Console.WriteLine();
            Console.WriteLine("  " + r.Channel + " — " + r.Title);

            if (r.Emissions.Count > 0)
            {
                foreach (EmissionInfo em in r.Emissions)
                {
                    Console.WriteLine();
                    Console.Write("    " + em.Number + ". " + em.Title);
                    if (!String.IsNullOrEmpty(em.Subtitle))
                        Console.Write(" - " + em.Subtitle);
                    Console.WriteLine();

                    if (!String.IsNullOrEmpty(em.Error))
                    {
                        Console.WriteLine("       [résolution impossible: " + em.Error + "]");
                        continue;
                    }

                    em.Media.Sort(CompareMedia);

                    if (em.Media.Count == 0)
                    {
                        Console.WriteLine("       aucun média");
                        continue;
                    }

                    foreach (MediaInfo m in em.Media)
                        PrintMedia(m, "       ");
                }
            }
            else
            {
                // Compatibility with V1 worker output.
                r.Media.Sort(CompareMedia);
                foreach (MediaInfo m in r.Media)
                    PrintMedia(m, "    ");
            }
        }

        Console.WriteLine();
        Console.WriteLine(results.Count + " chaîne(s)");
        return 0;
    }

    static int RunList(
        string channel, string query,
        string engine, string worker, int timeoutMs)
    {
        WorkerRun run =
            RunWorker(worker, "list", engine, channel, query, timeoutMs);

        if (run.TimedOut)
        {
            Console.Error.WriteLine("ERREUR: timeout");
            return 1;
        }

        if (run.ExitCode != 0)
        {
            Console.Error.WriteLine("ERREUR list (" + run.ExitCode + ")");
            if (!String.IsNullOrEmpty(run.Stderr))
                Console.Error.Write(run.Stderr);
            return run.ExitCode;
        }

        int count = 0;

        using (StringReader sr = new StringReader(run.Stdout))
        {
            string line;

            while ((line = sr.ReadLine()) != null)
            {
                line = line.TrimStart('\uFEFF');

                if (!line.StartsWith("ITEM\t", StringComparison.Ordinal))
                    continue;

                string[] x = line.Split('\t');
                if (x.Length < 5)
                    continue;

                if (count == 0)
                    Console.WriteLine(
                        "Résultats sur " + channel +
                        " pour \"" + query + "\" :");

                count++;

                Console.Write("  " + x[2] + ". " + x[3]);

                if (!String.IsNullOrEmpty(x[4]))
                    Console.Write(" - " + x[4]);

                Console.WriteLine();
            }
        }

        if (count == 0)
            Console.WriteLine(
                "Aucun résultat sur " + channel +
                " pour \"" + query + "\".");
        else
        {
            Console.WriteLine();
            Console.WriteLine(count + " émission(s)");
        }

        return 0;
    }

    static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        int timeoutSeconds = -1; // -1 = utiliser le défaut de la commande
        int argBase = 0;
        if (args.Length >= 2 && String.Equals(args[0], "--timeout", StringComparison.OrdinalIgnoreCase))
        {
            if (!Int32.TryParse(args[1], out timeoutSeconds) || timeoutSeconds < 0)
            {
                Console.Error.WriteLine("ERREUR: --timeout attend un nombre de secondes >= 0");
                return 2;
            }
            argBase = 2;
        }

        if (args.Length <= argBase)
        {
            Console.Error.WriteLine(
                "Usage:\n" +
                "  mono captvty-cli.exe [--timeout secondes] update [--all | \"chaîne\" ...]\n" +
                "  mono captvty-cli.exe [--timeout secondes] search [--live] \"texte\"\n" +
                "  mono captvty-cli.exe [--timeout secondes] list   \"chaîne\" \"texte\"\n" +
                "  mono captvty-cli.exe [--timeout secondes] info   \"texte\"\n" +
                "  mono captvty-cli.exe [--timeout secondes] info   \"chaîne\" \"texte\"\n" +
                "  mono captvty-cli.exe [--timeout secondes] get \"chaîne\" \"titre\" \"sous-titre\" high\n" +
                "\n" +
                "  --timeout 0 = illimité ; défaut général = 120 secondes");
            return 2;
        }

        string[] cmdArgs = new string[args.Length - argBase];
        Array.Copy(args, argBase, cmdArgs, 0, cmdArgs.Length);
        args = cmdArgs;

        string mode = args[0];
        int defaultTimeoutMs = 120000;
        int timeoutMs;
        if (timeoutSeconds < 0) timeoutMs = defaultTimeoutMs;
        else if (timeoutSeconds == 0) timeoutMs = System.Threading.Timeout.Infinite;
        else if (timeoutSeconds > Int32.MaxValue / 1000) timeoutMs = Int32.MaxValue;
        else timeoutMs = timeoutSeconds * 1000;

        string engine = Path.GetFullPath("Captvty-cli-engine.exe");
        string worker = Path.GetFullPath("captvty-provider.exe");
        string cache = Path.GetFullPath("captvty-cache.json");

        if (String.Equals(mode, "search", StringComparison.OrdinalIgnoreCase))
        {
            bool live = args.Length > 1 && String.Equals(args[1], "--live", StringComparison.OrdinalIgnoreCase);
            int first = live ? 2 : 1;
            if (args.Length <= first)
            {
                Console.Error.WriteLine("Usage: mono captvty-cli.exe search [--live] \"texte\"");
                return 2;
            }
            string searchQuery = String.Join(" ", args, first, args.Length - first);
            if (!live && File.Exists(cache)) return RunCachedSearch(searchQuery, cache);
            if (!File.Exists(engine) || !File.Exists(worker))
            { Console.Error.WriteLine("ERREUR: moteur ou worker introuvable"); return 2; }
            return RunSearchOrInfo("search", searchQuery, engine, worker, timeoutMs);
        }

        if (!File.Exists(engine) || !File.Exists(worker))
        {
            Console.Error.WriteLine("ERREUR: moteur ou worker introuvable");
            return 2;
        }

        if (String.Equals(mode, "update", StringComparison.OrdinalIgnoreCase))
        {
            string[] requested = new string[Math.Max(0, args.Length - 1)];
            if (requested.Length > 0) Array.Copy(args, 1, requested, 0, requested.Length);
            return RunUpdate(engine, worker, cache, requested, timeoutMs);
        }

        if (String.Equals(mode, "list", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length < 3) { Console.Error.WriteLine("Usage: mono captvty-cli.exe list \"chaîne\" \"texte\""); return 2; }
            return RunList(args[1], String.Join(" ", args, 2, args.Length - 2), engine, worker, timeoutMs);
        }

        if (String.Equals(mode, "info", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length < 2) { Console.Error.WriteLine("Usage: mono captvty-cli.exe info [\"chaîne\"] \"texte\""); return 2; }
            if (args.Length >= 3)
                return RunTargetedInfo(args[1], String.Join(" ", args, 2, args.Length - 2), engine, worker, timeoutSeconds < 0 ? 600000 : timeoutMs);
            return RunSearchOrInfo("info", args[1], engine, worker, timeoutMs);
        }

        if (String.Equals(mode, "get", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length != 5)
            {
                Console.Error.WriteLine("Usage: mono captvty-cli.exe get \"chaîne\" \"titre\" \"sous-titre\" high");
                return 2;
            }
            Console.WriteLine("Recherche de \"" + args[2] + "\" / \"" + args[3] + "\" sur " + args[1] + "...");
            WorkerRun run = RunGetWorker(worker, engine, args[1], args[2], args[3], args[4], timeoutSeconds < 0 ? (12 * 60 * 60 * 1000 + 60000) : timeoutMs);
            if (run.TimedOut) { Console.Error.WriteLine("ERREUR: délai maximal dépassé"); return 1; }
            if (run.ExitCode != 0)
            {
                Console.Error.WriteLine("ERREUR get (" + run.ExitCode + ")");
                if (!String.IsNullOrEmpty(run.Stderr)) Console.Error.Write(run.Stderr);
                return run.ExitCode;
            }
            return 0;
        }

        Console.Error.WriteLine("Commande inconnue: " + mode);
        return 2;
    }
}
