using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

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

    class AudioInfo
    {
        public string Language;
        public int Bitrate;
        public bool Default;
    }

    class EmissionInfo
    {
        public int Number;
        public string Title;
        public string Subtitle;
        public string Error;
        public int DurationSeconds;
        public List<AudioInfo> Audio = new List<AudioInfo>();
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

    static string FindMissingRuntimeDependency(string enginePath, string workerPath)
    {
        if (!File.Exists(enginePath))
            return Path.GetFileName(enginePath);

        if (!File.Exists(workerPath))
            return Path.GetFileName(workerPath);

        string dir = Path.GetDirectoryName(enginePath);
        string[] required = {
            "CefSharp.dll",
            "CefSharp.Core.dll",
            "CefSharp.WinForms.dll"
        };

        foreach (string name in required)
        {
            if (!File.Exists(Path.Combine(dir, name)))
                return name;
        }

        return null;
    }

    static bool CheckRuntimeDependencies(string enginePath, string workerPath)
    {
        string missing = FindMissingRuntimeDependency(enginePath, workerPath);
        if (missing == null)
            return true;

        Console.Error.WriteLine(
            "ERREUR: dépendance runtime introuvable: " + missing);
        return false;
    }

    static Assembly ResolveLocalAssembly(object sender, ResolveEventArgs args)
    {
        string exeDir = Path.GetDirectoryName(
            Assembly.GetExecutingAssembly().Location);

        if (String.IsNullOrEmpty(exeDir))
            return null;

        string name;

        try
        {
            name = new AssemblyName(args.Name).Name + ".dll";
        }
        catch
        {
            return null;
        }

        string[] paths = {
            Path.Combine(exeDir, name),
            Path.Combine(exeDir, "bin", name)
        };

        foreach (string path in paths)
            if (File.Exists(path))
                return Assembly.LoadFrom(path);

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

    static void SetWorkerMonoPath(ProcessStartInfo psi)
    {
        string exeDir = Path.GetDirectoryName(
            Assembly.GetExecutingAssembly().Location);

        if (String.IsNullOrEmpty(exeDir))
            return;

        string monoPath =
            exeDir + Path.PathSeparator +
            Path.Combine(exeDir, "bin");

        string old = Environment.GetEnvironmentVariable("MONO_PATH");
        if (!String.IsNullOrEmpty(old))
            monoPath += Path.PathSeparator + old;

        psi.EnvironmentVariables["MONO_PATH"] = monoPath;
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
        SetWorkerMonoPath(psi);

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
        SetWorkerMonoPath(psi);

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

                if (line.StartsWith("DURATION\t", StringComparison.Ordinal))
                {
                    string[] p = line.Split('\t');
                    if (p.Length < 4) continue;

                    EmissionInfo em;
                    if (emissions.TryGetValue(ParseInt(p[2]), out em))
                        em.DurationSeconds = ParseInt(p[3]);
                    continue;
                }

                if (line.StartsWith("AUDIO\t", StringComparison.Ordinal))
                {
                    string[] p = line.Split('\t');
                    if (p.Length < 6) continue;

                    EmissionInfo em;
                    if (emissions.TryGetValue(ParseInt(p[2]), out em))
                    {
                        em.Audio.Add(new AudioInfo {
                            Language = p[3],
                            Bitrate = ParseInt(p[4]),
                            Default = p[5] == "1"
                        });
                    }
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

    class T18Product
    {
        public string title { get; set; }
    }

    class T18Program
    {
        public string title { get; set; }
        public string duration { get; set; }
        public string broadcastedAt { get; set; }
        public string url { get; set; }
        public T18Product product { get; set; }
    }

    class T18DailymotionQuality
    {
        public string type { get; set; }
        public string url { get; set; }
    }

    class T18DailymotionMetadata
    {
        public string id { get; set; }
        public int duration { get; set; }
        public Dictionary<string, T18DailymotionQuality[]> qualities { get; set; }
    }

    class YtDlpFormat
    {
        public string format_id { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public double tbr { get; set; }
        public string ext { get; set; }
        public string protocol { get; set; }
    }

    class YtDlpInfo
    {
        public string id { get; set; }
        public double duration { get; set; }
        public YtDlpFormat[] formats { get; set; }
    }

    class T18MediaResult
    {
        public string MasterUrl;
        public YtDlpInfo Info;
    }

    static string FindYtDlp()
    {
        string exeDir =
            Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location);

        if (!String.IsNullOrEmpty(exeDir))
        {
            string privateYtDlp = Path.Combine(
                exeDir, ".ytdlp-venv/bin/yt-dlp");

            if (File.Exists(privateYtDlp))
                return privateYtDlp;
        }

        return "yt-dlp";
    }

    static WorkerRun RunYtDlp(string arguments, int timeoutMs)
    {
        ProcessStartInfo psi = new ProcessStartInfo();
        psi.FileName = FindYtDlp();
        psi.Arguments = arguments;
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

        bool finished =
            timeoutMs <= 0
                ? p.WaitForExit(-1)
                : p.WaitForExit(timeoutMs);

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

    static T18MediaResult ResolveT18Formats(
        T18Program program, int timeoutMs)
    {
        const string ua =
            "User-Agent: Mozilla/5.0 (X11; Linux x86_64; rv:140.0) Gecko/20100101 Firefox/140.0";

        if (program == null || String.IsNullOrEmpty(program.url))
            throw new Exception("URL T18 absente");

        // 1. Page de l'émission -> ID Dailymotion
        string html = RunCurl(
            program.url,
            new string[] { ua },
            timeoutMs);

        Match m = Regex.Match(
            html,
            @"data-dailymotion-video-id=[""']([^""']+)[""']");

        if (!m.Success)
            throw new Exception(
                "identifiant Dailymotion introuvable dans la page T18");

        string videoId = m.Groups[1].Value;

        // 2. Métadonnées Dailymotion -> master HLS signé
        string metadataUrl =
            "https://www.dailymotion.com/player/metadata/video/" +
            videoId +
            "?embedder=https%3A%2F%2Ft18.fr";

        string metadataJson = RunCurl(
            metadataUrl,
            new string[] { ua },
            timeoutMs);

        JavaScriptSerializer json = new JavaScriptSerializer();

        T18DailymotionMetadata metadata =
            json.Deserialize<T18DailymotionMetadata>(metadataJson);

        if (metadata == null ||
            metadata.qualities == null ||
            !metadata.qualities.ContainsKey("auto") ||
            metadata.qualities["auto"] == null)
            throw new Exception(
                "master HLS absent des métadonnées Dailymotion");

        string masterUrl = null;

        foreach (T18DailymotionQuality q in metadata.qualities["auto"])
        {
            if (q != null &&
                q.type == "application/x-mpegURL" &&
                !String.IsNullOrEmpty(q.url))
            {
                masterUrl = q.url;
                break;
            }
        }

        if (String.IsNullOrEmpty(masterUrl))
            throw new Exception(
                "URL HLS Dailymotion introuvable");

        // 3. yt-dlp -> formats disponibles
        WorkerRun run = RunYtDlp(
            "--impersonate chrome -J --no-download " +
            Quote(masterUrl),
            timeoutMs);

        if (run.TimedOut)
            throw new Exception("yt-dlp : délai dépassé");

        if (run.ExitCode != 0)
            throw new Exception(
                "yt-dlp a échoué (code " +
                run.ExitCode + "): " +
                run.Stderr.Trim());

        YtDlpInfo info =
            json.Deserialize<YtDlpInfo>(run.Stdout);

        if (info == null || info.formats == null)
            throw new Exception(
                "yt-dlp n'a retourné aucun format");

        return new T18MediaResult {
            MasterUrl = masterUrl,
            Info = info
        };
    }

    static int CompareT18Format(YtDlpFormat x, YtDlpFormat y)
    {
        long px = (long)x.width * x.height;
        long py = (long)y.width * y.height;

        int c = py.CompareTo(px);
        return c != 0 ? c : y.tbr.CompareTo(x.tbr);
    }

    static List<YtDlpFormat> GetT18Formats(T18MediaResult resolved)
    {
        List<YtDlpFormat> result = new List<YtDlpFormat>();

        if (resolved == null ||
            resolved.Info == null ||
            resolved.Info.formats == null)
            return result;

        foreach (YtDlpFormat f in resolved.Info.formats)
        {
            if (f == null ||
                String.IsNullOrEmpty(f.format_id) ||
                f.width <= 0 ||
                f.height <= 0 ||
                f.tbr <= 0)
                continue;

            result.Add(f);
        }

        result.Sort(CompareT18Format);
        return result;
    }

    static List<MediaInfo> ResolveT18Media(
        T18Program program, int timeoutMs)
    {
        T18MediaResult resolved =
            ResolveT18Formats(program, timeoutMs);

        List<MediaInfo> result = new List<MediaInfo>();

        foreach (YtDlpFormat f in GetT18Formats(resolved))
        {
            if (f == null ||
                f.width <= 0 ||
                f.height <= 0 ||
                f.tbr <= 0)
                continue;

            result.Add(new MediaInfo {
                Width = f.width,
                Height = f.height,
                Bitrate = (int)Math.Round(f.tbr),
                Best = false
            });
        }

        result.Sort(CompareMedia);
        return result;
    }

    static string RunCurl(string url, string[] headers, int timeoutMs)
    {
        ProcessStartInfo psi = new ProcessStartInfo();
        psi.FileName = "curl";
        psi.UseShellExecute = false;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.CreateNoWindow = true;

        StringBuilder args = new StringBuilder();
        args.Append("-sS -L");

        if (timeoutMs > 0)
        {
            double seconds = timeoutMs / 1000.0;
            args.Append(" --max-time ");
            args.Append(seconds.ToString("0.###", CultureInfo.InvariantCulture));
        }

        if (headers != null)
        {
            foreach (string h in headers)
            {
                args.Append(" -H ");
                args.Append(Quote(h));
            }
        }

        args.Append(" ");
        args.Append(Quote(url));

        psi.Arguments = args.ToString();

        using (Process p = Process.Start(psi))
        {
            string stdout = p.StandardOutput.ReadToEnd();
            string stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();

            if (p.ExitCode != 0)
                throw new Exception(
                    "curl a échoué (code " + p.ExitCode + "): " +
                    stderr.Trim());

            return stdout;
        }
    }

    static List<T18Program> FetchT18Programs(int timeoutMs)
    {
        const string ua =
            "User-Agent: Mozilla/5.0 (X11; Linux x86_64; rv:140.0) Gecko/20100101 Firefox/140.0";

        string html = RunCurl(
            "https://t18.fr/replay",
            new string[] { ua },
            timeoutMs);

        // N'accepte que /prog/slug, pas /prog/slug/episode.
        Regex re = new Regex("href=\"/prog/([^\"/]+)\"");
        HashSet<string> slugs = new HashSet<string>();

        foreach (Match m in re.Matches(html))
            slugs.Add(m.Groups[1].Value);

        JavaScriptSerializer json = new JavaScriptSerializer();
        List<T18Program> result = new List<T18Program>();

        foreach (string slug in slugs)
        {
            for (int page = 1; page <= 99; page++)
            {
                string url =
                    "https://t18.fr/api/products/" +
                    slug +
                    "/programs?p=" +
                    page.ToString(CultureInfo.InvariantCulture);

                string body = RunCurl(
                    url,
                    new string[] {
                        ua,
                        "X-Requested-With: XMLHttpRequest"
                    },
                    timeoutMs);

                T18Program[] programs =
                    json.Deserialize<T18Program[]>(body);

                if (programs == null || programs.Length == 0)
                    break;

                foreach (T18Program program in programs)
                    if (program != null)
                        result.Add(program);
            }
        }

        return result;
    }

    static List<CacheItem> FetchT18Catalog(int timeoutMs)
    {
        List<T18Program> programs = FetchT18Programs(timeoutMs);
        List<CacheItem> result = new List<CacheItem>();

        foreach (T18Program program in programs)
        {
            result.Add(new CacheItem {
                Channel = "T18",
                Title = program.product != null
                    ? program.product.title
                    : "",
                Subtitle = program.title ?? ""
            });
        }

        return result;
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

    static string SafeFileStem(string text)
    {
        if (String.IsNullOrEmpty(text))
            return "video";

        StringBuilder b = new StringBuilder(text.Length);
        bool underscore = false;

        foreach (char ch in text)
        {
            if (Char.IsLetterOrDigit(ch) ||
                ch == '-' || ch == '_' || ch == '.')
            {
                b.Append(ch);
                underscore = false;
            }
            else if (!underscore)
            {
                b.Append('_');
                underscore = true;
            }
        }

        string clean = b.ToString().Trim('_');

        while (clean.Contains("_."))
            clean = clean.Replace("_.", ".");

        return clean.Length == 0 ? "video" : clean;
    }

    static int RunGetT18(
        string title, string subtitle, string quality, int timeoutMs)
    {
        List<T18Program> programs;

        try
        {
            if (Environment.GetEnvironmentVariable(
                    "CAPTVTY_TEST_T18_FETCH_FAIL") == "1")
                throw new Exception(
                    "[TEST] échec simulé de récupération T18");
            programs = FetchT18Programs(timeoutMs);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                "Recherche T18 impossible: " + ex.Message);
            return 7;
        }
        List<T18Program> matches = new List<T18Program>();

        foreach (T18Program program in programs)
        {
            string productTitle =
                program.product != null ? program.product.title : "";

            if (ContainsText(productTitle, title) &&
                ContainsText(program.title ?? "", subtitle))
                matches.Add(program);
        }

        if (matches.Count == 0)
        {
            Console.Error.WriteLine(
                "Aucune émission T18 correspondante");
            return 3;
        }

        if (matches.Count != 1)
        {
            Console.Error.WriteLine(
                "Recherche T18 ambiguë: " +
                matches.Count + " émissions correspondent");

            foreach (T18Program p in matches)
            {
                Console.Error.WriteLine(
                    "  " +
                    (p.product != null ? p.product.title : "") +
                    " — " +
                    (p.title ?? ""));
            }

            return 3;
        }

        T18Program selectedProgram = matches[0];

        Console.WriteLine(
            "Résolution des médias pour \"" +
            (selectedProgram.title ?? "") + "\"...");

        T18MediaResult resolved;

        bool simulateResolveFailure =
            Environment.GetEnvironmentVariable(
                "CAPTVTY_TEST_T18_RESOLVE_FAIL") == "1";

        try
        {
            if (simulateResolveFailure)
                throw new Exception(
                    "[TEST] échec simulé de résolution T18");

            resolved =
                ResolveT18Formats(selectedProgram, timeoutMs);
        }
        catch (Exception firstEx)
        {
            Console.Error.WriteLine(
                "Première résolution T18 en échec: " +
                firstEx.Message);
            Console.Error.WriteLine(
                "Nouvelle résolution Dailymotion...");

            try
            {
                if (simulateResolveFailure)
                    throw new Exception(
                        "[TEST] échec simulé de résolution T18");

                resolved =
                    ResolveT18Formats(selectedProgram, timeoutMs);
            }
            catch (Exception secondEx)
            {
                Console.Error.WriteLine(
                    "Résolution T18 en échec après deux essais: " +
                    secondEx.Message);
                return 7;
            }
        }

        List<YtDlpFormat> formats =
            GetT18Formats(resolved);

        if (formats.Count == 0)
        {
            Console.Error.WriteLine(
                "Aucun média T18 téléchargeable");
            return 4;
        }

        YtDlpFormat selected = null;

        if (String.Equals(
                quality, "high",
                StringComparison.OrdinalIgnoreCase))
        {
            selected = formats[0];
        }
        else if (String.Equals(
                     quality, "low",
                     StringComparison.OrdinalIgnoreCase))
        {
            selected = formats[formats.Count - 1];
        }
        else
        {
            int n;

            if (!Int32.TryParse(quality, out n) ||
                n < 1 || n > formats.Count)
            {
                Console.Error.WriteLine(
                    "Qualité invalide: " + quality +
                    " (attendu: high, low ou 1.." +
                    formats.Count + ")");
                return 2;
            }

            selected = formats[n - 1];
        }

        Console.WriteLine(
            "Média: " +
            selected.width + "x" + selected.height +
            "  " +
            selected.format_id +
            "  " +
            Math.Round(selected.tbr) + " kb/s");

        string seriesTitle =
            selectedProgram.product != null
                ? selectedProgram.product.title
                : "T18";

        string outputStem = SafeFileStem(
            seriesTitle + "_" +
            (selectedProgram.title ?? "") +
            "_T18_" +
            selected.height + "p");

        string outputTemplate =
            outputStem + ".%(ext)s";

        Console.WriteLine(
            "Téléchargement T18 via yt-dlp...");
        Console.WriteLine(
            "Fichier: " + outputTemplate);

        bool simulateFirstFailure =
            Environment.GetEnvironmentVariable(
                "CAPTVTY_TEST_T18_RETRY") == "1";

        WorkerRun run;

        if (simulateFirstFailure)
        {
            Console.Error.WriteLine(
                "[TEST] Simulation d'un échec du premier téléchargement T18");

            run = new WorkerRun();
            run.ExitCode = 99;
        }
        else
        {
            run = RunYtDlp(
                "--impersonate chrome " +
                "--print " + Quote("after_move:CAPTVTY_FILE=%(filepath)s") + " " +
                "-f " + Quote(selected.format_id) + " " +
                "-o " + Quote(outputTemplate) + " " +
                Quote(resolved.MasterUrl),
                timeoutMs);
        }

        if (run.TimedOut)
        {
            Console.Error.WriteLine(
                "Téléchargement T18: délai maximal dépassé");
            return 8;
        }

        if (run.ExitCode != 0)
        {
            Console.Error.WriteLine(
                "Premier téléchargement T18 en échec (code " +
                run.ExitCode + ")");
            Console.Error.WriteLine(
                "Nouvelle résolution Dailymotion et second essai...");

            resolved =
                ResolveT18Formats(selectedProgram, timeoutMs);

            List<YtDlpFormat> retryFormats =
                GetT18Formats(resolved);

            if (retryFormats.Count == 0)
            {
                Console.Error.WriteLine(
                    "Aucun média T18 disponible au second essai");
                return 7;
            }

            YtDlpFormat retrySelected = null;

            foreach (YtDlpFormat f in retryFormats)
            {
                if (String.Equals(
                        f.format_id,
                        selected.format_id,
                        StringComparison.Ordinal))
                {
                    retrySelected = f;
                    break;
                }
            }

            if (retrySelected == null)
            {
                if (String.Equals(
                        quality, "high",
                        StringComparison.OrdinalIgnoreCase))
                {
                    retrySelected = retryFormats[0];
                }
                else if (String.Equals(
                             quality, "low",
                             StringComparison.OrdinalIgnoreCase))
                {
                    retrySelected =
                        retryFormats[retryFormats.Count - 1];
                }
                else
                {
                    int n;
                    if (!Int32.TryParse(quality, out n) ||
                        n < 1 || n > retryFormats.Count)
                    {
                        Console.Error.WriteLine(
                            "Qualité demandée indisponible au second essai");
                        return 7;
                    }

                    retrySelected = retryFormats[n - 1];
                }
            }

            selected = retrySelected;

            Console.Error.WriteLine(
                "Second essai: " +
                selected.width + "x" + selected.height +
                "  " + selected.format_id +
                "  " + Math.Round(selected.tbr) + " kb/s");

            run = RunYtDlp(
                "--impersonate chrome " +
                "--print " + Quote("after_move:CAPTVTY_FILE=%(filepath)s") + " " +
                "-f " + Quote(selected.format_id) + " " +
                "-o " + Quote(outputTemplate) + " " +
                Quote(resolved.MasterUrl),
                timeoutMs);

            if (run.TimedOut)
            {
                Console.Error.WriteLine(
                    "Téléchargement T18: délai maximal dépassé");
                return 8;
            }

            if (run.ExitCode != 0)
            {
                Console.Error.WriteLine(
                    "Second téléchargement T18 en échec (code " +
                    run.ExitCode + ")");

                if (!String.IsNullOrEmpty(run.Stderr))
                    Console.Error.Write(run.Stderr);

                return 7;
            }
        }

        string finalPath = "";

        if (!String.IsNullOrEmpty(run.Stdout))
        {
            string[] lines = run.Stdout.Split(
                new char[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                const string prefix = "CAPTVTY_FILE=";

                if (line.StartsWith(
                        prefix,
                        StringComparison.Ordinal))
                {
                    finalPath =
                        line.Substring(prefix.Length).Trim();
                }
            }
        }

        if (String.IsNullOrEmpty(finalPath))
        {
            Console.Error.WriteLine(
                "Téléchargement terminé, mais yt-dlp " +
                "n'a pas indiqué le fichier final");
            return 7;
        }

        Console.WriteLine(
            "Téléchargement terminé: " + finalPath);

        return 0;
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
        bool t18Only =
            requested.Length == 1 &&
            String.Equals(requested[0], "T18", StringComparison.OrdinalIgnoreCase);

        List<string> allChannels = t18Only
            ? new List<string> { "T18" }
            : ReadChannels(engine);

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

            List<CacheItem> fresh;

            if (String.Equals(channel, "T18", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    fresh = FetchT18Catalog(timeoutMs);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("erreur T18 (ancien cache conservé)");
                    Console.Error.WriteLine("T18: " + ex.Message);
                    status[channel] = "error";
                    continue;
                }
            }
            else
            {
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

                fresh = new List<CacheItem>();
                using (StringReader sr = new StringReader(run.Stdout))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        line = line.TrimStart('\uFEFF');
                        if (!line.StartsWith("ITEM\t", StringComparison.Ordinal)) continue;
                        string[] fields = line.Split('\t');
                        if (fields.Length < 5) continue;
                        fresh.Add(new CacheItem {
                            Channel = fields[1],
                            Title = fields[3],
                            Subtitle = fields[4]
                        });
                    }
                }
            }

            if (fresh.Count == 0)
            {
                Console.WriteLine("vide (ancien cache conservé)");
                status[channel] = "empty";
                continue;
            }

            items.RemoveAll(delegate(CacheItem x) {
                return String.Equals(x.Channel, channel, StringComparison.CurrentCultureIgnoreCase);
            });
            items.AddRange(fresh);
            status[channel] = "ok";
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

    static int T18DurationSeconds(string duration)
    {
        if (String.IsNullOrEmpty(duration))
            return 0;

        Match m = Regex.Match(
            duration,
            @"^PT(?:(\d+)H)?(?:(\d+)M)?(?:(\d+)S)?$");

        if (!m.Success)
            return 0;

        int h = m.Groups[1].Success ? Int32.Parse(m.Groups[1].Value) : 0;
        int min = m.Groups[2].Success ? Int32.Parse(m.Groups[2].Value) : 0;
        int sec = m.Groups[3].Success ? Int32.Parse(m.Groups[3].Value) : 0;

        return h * 3600 + min * 60 + sec;
    }

    static int RunTargetedInfoT18(string query, int timeoutMs)
    {
        Console.Write("[1] T18 ... ");
        Console.Out.Flush();

        List<T18Program> programs;

        try
        {
            programs = FetchT18Programs(timeoutMs);
        }
        catch (Exception ex)
        {
            Console.WriteLine("erreur");
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        List<T18Program> found = new List<T18Program>();

        foreach (T18Program program in programs)
        {
            if (program == null)
                continue;

            string productTitle =
                program.product != null
                    ? program.product.title ?? ""
                    : "";

            if (ContainsText(productTitle, query) ||
                ContainsText(program.title, query))
                found.Add(program);
        }

        Console.WriteLine("OK (" + found.Count + ")");

        if (found.Count == 0)
        {
            Console.WriteLine("Aucun résultat.");
            return 0;
        }

        Console.WriteLine();
        Console.WriteLine("Informations pour \"" + query + "\" :");
        Console.WriteLine();

        string title =
            found[0].product != null
                ? found[0].product.title ?? ""
                : "";

        Console.WriteLine("  T18 — " + title);

        int number = 0;

        foreach (T18Program program in found)
        {
            number++;

            Console.WriteLine();

            Console.Write("    " + number + ". " +
                          (program.title ?? ""));

            int durationSeconds =
                T18DurationSeconds(program.duration);

            if (durationSeconds > 0)
                Console.Write(
                    "  [" +
                    (durationSeconds / 60) + ":" +
                    (durationSeconds % 60).ToString("00") +
                    "]");

            Console.WriteLine();

            try
            {
                List<MediaInfo> media;

                try
                {
                    media = ResolveT18Media(program, timeoutMs);
                }
                catch (Exception firstEx)
                {
                    Console.WriteLine(
                        "       première résolution T18 en échec : " +
                        firstEx.Message);
                    Console.WriteLine(
                        "       nouvelle résolution Dailymotion...");

                    media = ResolveT18Media(program, timeoutMs);
                }

                foreach (MediaInfo m in media)
                    PrintMedia(
                        m,
                        "       ",
                        durationSeconds,
                        null);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "       média T18 : " + ex.Message);
            }
        }

        Console.WriteLine();
        Console.WriteLine("1 chaîne(s)");

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
            if (em.DurationSeconds > 0)
                Console.Write("  [" +
                    (em.DurationSeconds / 60) + ":" +
                    (em.DurationSeconds % 60).ToString("00") + "]");
            Console.WriteLine();
            if (!String.IsNullOrEmpty(em.Error)) { Console.WriteLine("       [résolution impossible: " + em.Error + "]"); continue; }
            em.Media.Sort(CompareMedia);
            foreach (MediaInfo m in em.Media)
                PrintMedia(m, "       ", em.DurationSeconds, em.Audio);
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

    static int ExtraAudioBitrate(List<AudioInfo> audio)
    {
        if (audio == null || audio.Count == 0)
            return 0;

        int total = 0;
        int included = 0;
        int first = 0;

        foreach (AudioInfo a in audio)
        {
            if (a.Bitrate <= 0)
                continue;

            total += a.Bitrate;

            if (first == 0)
                first = a.Bitrate;

            if (a.Default && included == 0)
                included = a.Bitrate;
        }

        // HLS BANDWIDTH already includes one audio rendition.
        if (included == 0)
            included = first;

        int extra = total - included;
        return extra > 0 ? extra : 0;
    }

    static void PrintMedia(MediaInfo m, string indent,
        int durationSeconds = 0, List<AudioInfo> audio = null)
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

            if (durationSeconds > 0)
            {
                int extraAudioKbps = ExtraAudioBitrate(audio);

                // HLS BANDWIDTH includes one audio rendition.
                // Add any additional audio renditions muxed by Captvty,
                // then apply Captvty's own 4% size margin.
                double sizeMB =
                    (m.Bitrate + extraAudioKbps) *
                    (double)durationSeconds / 8000.0 * 1.04;

                if (sizeMB >= 1000.0)
                    Console.Write("  ~" +
                        (sizeMB / 1000.0).ToString("0.00",
                            CultureInfo.InvariantCulture) + " Go (estimé)");
                else
                    Console.Write("  ~" +
                        sizeMB.ToString("0",
                            CultureInfo.InvariantCulture) + " Mo (estimé)");
            }
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
                        PrintMedia(m, "       ", em.DurationSeconds, em.Audio);
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
        string engine, string worker, string cachePath, int timeoutMs)
    {
        if (String.Equals(channel, "T18", StringComparison.OrdinalIgnoreCase))
        {
            List<CacheItem> items = ReadCache(cachePath);
            string nq = NormalizeText(query);
            int t18Count = 0;

            foreach (CacheItem item in items)
            {
                if (!String.Equals(
                        item.Channel, "T18",
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                string text = NormalizeText(
                    (item.Title ?? "") + " " + (item.Subtitle ?? ""));

                if (nq.Length > 0 && !text.Contains(nq))
                    continue;

                if (t18Count == 0)
                    Console.WriteLine(
                        "Résultats sur T18 pour \"" + query + "\" :");

                t18Count++;

                Console.Write("  " + t18Count + ". " + item.Title);

                if (!String.IsNullOrEmpty(item.Subtitle))
                    Console.Write(" - " + item.Subtitle);

                Console.WriteLine();
            }

            if (t18Count == 0)
                Console.WriteLine(
                    "Aucun résultat sur T18 pour \"" + query + "\".");
            else
            {
                Console.WriteLine();
                Console.WriteLine(t18Count + " émission(s)");
            }

            return 0;
        }

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
        AppDomain.CurrentDomain.AssemblyResolve += ResolveLocalAssembly;

        Console.OutputEncoding = Encoding.UTF8;

        if (args.Length == 1 &&
            String.Equals(args[0], "--version", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("captvty-cli 2.7.1");
            return 0;
        }

        int timeoutSeconds = -1; // -1 = utiliser le défaut de la commande
        int argBase = 0;

        if (args.Length >= 1 &&
            String.Equals(args[0], "--timeout", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length < 2 ||
                !Int32.TryParse(args[1], out timeoutSeconds) || timeoutSeconds < 0)
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
                "  mono captvty-cli.exe [--timeout secondes] get \"chaîne\" \"titre\" \"sous-titre\" high|low|N");
            return 2;
        }

        string[] cmdArgs = new string[args.Length - argBase];
        Array.Copy(args, argBase, cmdArgs, 0, cmdArgs.Length);
        args = cmdArgs;

        string mode = args[0];

        int defaultTimeoutMs = 120000;
        int timeoutMs;

        if (timeoutSeconds < 0)
            timeoutMs = defaultTimeoutMs;
        else if (timeoutSeconds == 0)
            timeoutMs = System.Threading.Timeout.Infinite;
        else if (timeoutSeconds > Int32.MaxValue / 1000)
            timeoutMs = Int32.MaxValue;
        else
            timeoutMs = timeoutSeconds * 1000;

        string exeDir = Path.GetDirectoryName(
            Assembly.GetExecutingAssembly().Location);

        string engine = Path.Combine(exeDir, "Captvty-cli-engine.exe");
        string worker = Path.Combine(exeDir, "captvty-provider.exe");
        string cache = Path.Combine(exeDir, "captvty-cache.json");

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
            if (!CheckRuntimeDependencies(engine, worker))
                return 2;
            return RunSearchOrInfo("search", searchQuery, engine, worker, timeoutMs);
        }

        if (String.Equals(mode, "update", StringComparison.OrdinalIgnoreCase))
        {
            if (!CheckRuntimeDependencies(engine, worker))
                return 2;

            string[] requested = new string[Math.Max(0, args.Length - 1)];
            if (requested.Length > 0) Array.Copy(args, 1, requested, 0, requested.Length);
            return RunUpdate(engine, worker, cache, requested, timeoutMs);
        }

        if (String.Equals(mode, "list", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length < 3) { Console.Error.WriteLine("Usage: mono captvty-cli.exe list \"chaîne\" \"texte\""); return 2; }
            bool t18List =
                String.Equals(args[1], "T18", StringComparison.OrdinalIgnoreCase);

            if (!t18List && !CheckRuntimeDependencies(engine, worker))
                return 2;

            return RunList(args[1], String.Join(" ", args, 2, args.Length - 2),
                           engine, worker, cache, timeoutMs);
        }

        if (String.Equals(mode, "info", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: mono captvty-cli.exe info [\"chaîne\"] \"texte\"");
                return 2;
            }

            string infoQuery =
                args.Length >= 3
                    ? String.Join(" ", args, 2, args.Length - 2)
                    : args[1];

            if (String.IsNullOrWhiteSpace(infoQuery))
            {
                Console.Error.WriteLine(
                    "Erreur: le texte de recherche ne peut pas être vide.");
                Console.Error.WriteLine(
                    "Usage: mono captvty-cli.exe info [\"chaîne\"] \"texte\"");
                return 2;
            }

            if (args.Length >= 3 &&
                String.Equals(args[1], "T18", StringComparison.OrdinalIgnoreCase))
                return RunTargetedInfoT18(
                    infoQuery,
                    timeoutSeconds < 0 ? 600000 : timeoutMs);

            if (args.Length == 2)
            {
                Console.WriteLine(
                    "Recherche de l'émission \"" + infoQuery +
                    "\" dans toutes les chaînes TV...");
                Console.WriteLine();
            }

            if (!CheckRuntimeDependencies(engine, worker))
                return 2;

            if (args.Length >= 3)
                return RunTargetedInfo(
                    args[1], infoQuery, engine, worker,
                    timeoutSeconds < 0 ? 600000 : timeoutMs);

            return RunSearchOrInfo(
                "info", infoQuery, engine, worker, timeoutMs);
        }

        if (String.Equals(mode, "get", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length != 5)
            {
                Console.Error.WriteLine("Usage: mono captvty-cli.exe get \"chaîne\" \"titre\" \"sous-titre\" high|low|N");
                return 2;
            }
            if (String.Equals(args[1], "T18", StringComparison.OrdinalIgnoreCase))
            {
                int getTimeoutMs =
                    timeoutSeconds < 0
                        ? (12 * 60 * 60 * 1000 + 60000)
                        : timeoutMs;

                Console.WriteLine(
                    "Recherche de \"" + args[2] +
                    "\" / \"" + args[3] +
                    "\" sur T18...");

                return RunGetT18(
                    args[2], args[3], args[4], getTimeoutMs);
            }

            if (!CheckRuntimeDependencies(engine, worker))
                return 2;
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
