using System.Linq;
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
            if (!File.Exists(Path.Combine(dir, name)) &&
                !File.Exists(Path.Combine(dir, "bin", "cefsharp", name)))
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
        string worker = Path.Combine(
            Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location),
            "captvty-provider-28.exe");

        ProcessStartInfo psi = new ProcessStartInfo();
        ConfigureMonoPath(psi, enginePath);
        psi.FileName = "mono";
        psi.Arguments =
            Quote(worker) + " channels " + Quote(enginePath);

        psi.UseShellExecute = false;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.CreateNoWindow = true;

        List<string> names = new List<string>();

        using (Process p = Process.Start(psi))
        {
            string output = p.StandardOutput.ReadToEnd();
            string errors = p.StandardError.ReadToEnd();

            p.WaitForExit();

            if (p.ExitCode != 0)
                throw new Exception(
                    "Énumération des chaînes impossible (" +
                    p.ExitCode + "): " + errors);

            using (StringReader sr = new StringReader(output))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    line = line.TrimStart('\uFEFF');

                    if (!line.StartsWith("CHANNEL\t",
                                         StringComparison.Ordinal))
                        continue;

                    string name = line.Substring(8);

                    if (!String.IsNullOrEmpty(name) &&
                        !names.Contains(name))
                        names.Add(name);
                }
            }
        }

        if (names.Count == 0)
            throw new Exception(
                "Aucune chaîne retournée par le fournisseur");

        return names;
    }

    static string Quote(string s)
    {
        if (s == null) return "\"\"";
        return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    static void ConfigureMonoPath(ProcessStartInfo psi, string engine)
    {
        string dir = Path.GetDirectoryName(Path.GetFullPath(engine));

        string monoPath = String.Join(":", new string[] {
            dir,
            Path.Combine(dir, "bin"),
            Path.Combine(dir, "bin", "cefsharp")
        });

        string previous = Environment.GetEnvironmentVariable("MONO_PATH");

        if (!String.IsNullOrEmpty(previous))
            monoPath += ":" + previous;

        psi.EnvironmentVariables["MONO_PATH"] = monoPath;
        psi.WorkingDirectory = dir;
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
        ConfigureMonoPath(psi, engine);

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
        ConfigureMonoPath(psi, engine);

        StringBuilder stdout = new StringBuilder();
        StringBuilder stderr = new StringBuilder();

        Process p = new Process();
        p.StartInfo = psi;

        bool stdoutClosed = false;
        IOException stdoutError = null;

        p.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null) return;

            stdout.AppendLine(e.Data);

            if (stdoutClosed || stdoutError != null)
                return;

            try
            {
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
            }
            catch (StdoutPipeClosedException)
            {
                stdoutClosed = true;
            }
            catch (IOException ex)
            {
                stdoutError = ex;
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

        if (stdoutError != null)
            throw stdoutError;

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

                if (line.StartsWith("ITEM\t", StringComparison.Ordinal))
                {
                    string[] p = line.Split('\t');
                    if (p.Length < 5) continue;

                    if (result == null)
                        result = new ProviderResult {
                            Channel = String.IsNullOrEmpty(p[1])
                                ? fallbackChannel : p[1],
                            Count = 0,
                            Title = p[3]
                        };

                    result.Emissions.Add(new EmissionInfo {
                        Number = ParseInt(p[2]),
                        Title = p[3],
                        Subtitle = p[4]
                    });

                    result.Count++;
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
        public string EpisodeId;
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

    class Tv5AuthResponse
    {
        public string sessionToken;
    }

    class Tv5Subtitle
    {
        public string language;
        public string url;
    }

    class Tv5PlayResponse
    {
        public Tv5Subtitle[] subtitles;
    }

    const string Tv5RedBeeBase =
        "https://exposure.api.redbee.live/v2/customer/" +
        "TV5MONDE/businessunit/TV5MONDEplus";

    static string Tv5Authenticate(int timeoutMs)
    {
        JavaScriptSerializer json =
            new JavaScriptSerializer();

        string deviceId = Guid.NewGuid().ToString();

        string authBody =
            "{\"deviceId\":\"" + deviceId + "\"," +
            "\"device\":{" +
            "\"deviceId\":\"" + deviceId + "\"," +
            "\"name\":\"Mozilla Firefox 102\"," +
            "\"type\":\"WEB\"}}";

        string authJson = RunCurlPost(
            Tv5RedBeeBase + "/auth/anonymous",
            new string[] {
                "Content-Type: application/json;charset=utf-8"
            },
            authBody,
            timeoutMs);

        Tv5AuthResponse auth =
            json.Deserialize<Tv5AuthResponse>(authJson);

        if (auth == null ||
            String.IsNullOrEmpty(auth.sessionToken))
            throw new Exception(
                "sessionToken RedBee absent");

        return auth.sessionToken;
    }

    static Tv5PlayResponse Tv5GetPlay(
        string assetId, string sessionToken, int timeoutMs)
    {
        string playJson = RunCurl(
            Tv5RedBeeBase + "/entitlement/" +
            assetId + "/play",
            new string[] {
                "Authorization: Bearer " + sessionToken,
                "Accept: application/json, text/plain, */*"
            },
            timeoutMs);

        JavaScriptSerializer json =
            new JavaScriptSerializer();

        Tv5PlayResponse play =
            json.Deserialize<Tv5PlayResponse>(playJson);

        if (play == null)
            throw new Exception(
                "réponse RedBee /play invalide");

        return play;
    }

    static int RunTv5Subs(
        string assetId, string language, string output, int timeoutMs)
    {
        try
        {
            string sessionToken =
                Tv5Authenticate(timeoutMs);

            Tv5PlayResponse play =
                Tv5GetPlay(
                    assetId, sessionToken, timeoutMs);

            if (play.subtitles == null)
                throw new Exception(
                    "aucun sous-titre annoncé par RedBee");

            string subtitleUrl = null;

            foreach (Tv5Subtitle sub in play.subtitles)
            {
                if (sub != null &&
                    String.Equals(
                        sub.language,
                        language,
                        StringComparison.OrdinalIgnoreCase))
                {
                    subtitleUrl = sub.url;
                    break;
                }
            }

            if (String.IsNullOrEmpty(subtitleUrl))
            {
                Console.Error.WriteLine(
                    "Aucun sous-titre '" + language +
                    "' disponible pour " + assetId);
                return 3;
            }

            Console.WriteLine(
                "TV5MONDE+ " + assetId +
                " : sous-titres " + language);
            Console.WriteLine(subtitleUrl);

            string webvtt = RunCurl(
                subtitleUrl,
                null,
                timeoutMs);

            if (!webvtt.StartsWith("WEBVTT"))
                throw new Exception(
                    "la réponse reçue n'est pas un WebVTT");

            File.WriteAllText(
                output,
                webvtt,
                new UTF8Encoding(false));

            Console.WriteLine(
                "Sous-titres enregistrés : " + output);

            return 0;
        }
        catch (Exception ex) when (!(ex is StdoutPipeClosedException))
        {
            Console.Error.WriteLine(
                "ERREUR TV5: " + ex.Message);
            return 1;
        }
    }

    static string RunCurlPost(
        string url, string[] headers, string data, int timeoutMs)
    {
        ProcessStartInfo psi = new ProcessStartInfo();
        psi.FileName = "curl";
        psi.UseShellExecute = false;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.CreateNoWindow = true;

        StringBuilder args = new StringBuilder();
        args.Append("-sS -L -X POST");

        if (timeoutMs > 0)
        {
            double seconds = timeoutMs / 1000.0;
            args.Append(" --max-time ");
            args.Append(
                seconds.ToString("0.###", CultureInfo.InvariantCulture));
        }

        if (headers != null)
        {
            foreach (string h in headers)
            {
                args.Append(" -H ");
                args.Append(Quote(h));
            }
        }

        args.Append(" --data ");
        args.Append(Quote(data));
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
                    "curl POST a échoué (code " + p.ExitCode + "): " +
                    stderr.Trim());

            return stdout;
        }
    }

    static List<T18Program> FetchT18Programs(int timeoutMs)
    {
        const string ua =
            "User-Agent: Mozilla/5.0 (X11; Linux x86_64; rv:140.0) Gecko/20100101 Firefox/140.0";

        string html = RunCurl(
            "https://t18.fr/",
            new string[] { ua },
            timeoutMs);

        // Liens absolus ou relatifs, programmes ou épisodes.
        Regex re = new Regex(
            @"href=""(?:https://t18\.fr)?/prog/([^""/]+-([0-9]+))(?=/|"")");

        Dictionary<string,string> products =
            new Dictionary<string,string>();

        foreach (Match m in re.Matches(html))
            products[m.Groups[2].Value] = m.Groups[1].Value;

        if (products.Count == 0)
            throw new Exception("Aucun programme T18 détecté");

        JavaScriptSerializer json = new JavaScriptSerializer();
        json.MaxJsonLength = Int32.MaxValue;

        List<T18Program> result = new List<T18Program>();

        foreach (var product in products)
        {
            string id = product.Key;
            string programUrl =
                "https://t18.fr/prog/" + product.Value;

            string programHtml = RunCurl(
                programUrl,
                new string[] { ua },
                timeoutMs);

            Match pagination = Regex.Match(
                programHtml,
                @"data-pagination-pages-count=""([0-9]+)""");

            int pages = pagination.Success
                ? Int32.Parse(pagination.Groups[1].Value)
                : 1;

            for (int page = 1; page <= pages; page++)
            {
                string url =
                    "https://t18.fr/api/products/" +
                    id + "/replays?p=" +
                    page.ToString(CultureInfo.InvariantCulture);

                string body = RunCurl(
                    url,
                    new string[] {
                        ua,
                        "X-Requested-With: XMLHttpRequest",
                        "Referer: " + programUrl
                    },
                    timeoutMs);

                T18Program[] programs =
                    json.Deserialize<T18Program[]>(body);

                if (programs == null)
                    throw new Exception("Réponse T18 invalide: " + url);

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
        return TextMatching.NormalizeText(s);
    }

    static bool ContainsText(string text, string wanted)
    {
        return TextMatching.ContainsText(text, wanted);
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
        catch (Exception ex) when (!(ex is StdoutPipeClosedException))
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
        catch (Exception firstEx) when (!(firstEx is StdoutPipeClosedException))
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
            catch (Exception secondEx) when (!(secondEx is StdoutPipeClosedException))
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

    static Dictionary<string, object> ReadJsonRoot(string path)
    {
        if (!File.Exists(path)) return null;

        var serializer = new JavaScriptSerializer();
        serializer.MaxJsonLength = Int32.MaxValue;

        return serializer.DeserializeObject(
            File.ReadAllText(path, Encoding.UTF8))
            as Dictionary<string, object>;
    }

    static Dictionary<string, object> JsonObject(object value)
    {
        return value as Dictionary<string, object>;
    }

    static string JsonField(Dictionary<string, object> obj, string key)
    {
        object value;
        if (obj == null || !obj.TryGetValue(key, out value) || value == null)
            return "";

        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    static List<CacheItem> ReadCache(string path)
    {
        List<CacheItem> items = new List<CacheItem>();
        var root = ReadJsonRoot(path);
        if (root == null) return items;

        object value;
        if (!root.TryGetValue("programs", out value)) return items;

        object[] programs = value as object[];
        if (programs == null) return items;

        foreach (object entry in programs)
        {
            var obj = JsonObject(entry);
            if (obj == null) continue;

            CacheItem x = new CacheItem();
            x.Channel = JsonField(obj, "channel");
            x.Title = JsonField(obj, "title");
            x.Subtitle = JsonField(obj, "subtitle");
            x.EpisodeId = JsonField(obj, "episode_id");

            if (!String.IsNullOrEmpty(x.Channel))
                items.Add(x);
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
        var result = new Dictionary<string,string>(
            StringComparer.CurrentCultureIgnoreCase);

        var root = ReadJsonRoot(path);
        if (root == null) return result;

        object value;
        if (!root.TryGetValue("status", out value)) return result;

        var status = JsonObject(value);
        if (status == null) return result;

        foreach (var entry in status)
            result[entry.Key] = entry.Value == null
                ? "" : Convert.ToString(entry.Value, CultureInfo.InvariantCulture);

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

    class UpdateSession
    {
        public string Started;
        public bool Completed;
        public List<string> Channels = new List<string>();
        public List<string> Done = new List<string>();
    }




    static UpdateSession ReadUpdateSession(string path)
    {
        var root = ReadJsonRoot(path);
        if (root == null) return null;

        object value;
        if (!root.TryGetValue("update_session", out value)) return null;

        var obj = JsonObject(value);
        if (obj == null) return null;

        UpdateSession result = new UpdateSession();
        result.Started = JsonField(obj, "started");

        object completed;
        if (obj.TryGetValue("completed", out completed) &&
            completed is bool)
            result.Completed = (bool)completed;

        object channels;
        if (obj.TryGetValue("channels", out channels) && channels is object[])
            foreach (object channel in (object[])channels)
                if (channel is string)
                    result.Channels.Add((string)channel);

        object done;
        if (obj.TryGetValue("done", out done) && done is object[])
            foreach (object channel in (object[])done)
                if (channel is string)
                    result.Done.Add((string)channel);

        return result;
    }

    static List<string> ReadStringArray(string line)
    {
        List<string> result = new List<string>();

        int a = line.IndexOf('[');
        int b = line.LastIndexOf(']');
        if (a < 0 || b < a) return result;

        string content = line.Substring(a + 1, b - a - 1);

        foreach (System.Text.RegularExpressions.Match m in
                 System.Text.RegularExpressions.Regex.Matches(
                     content, @"""(?:\\.|[^""\\])*"""))
        {
            string v = m.Value;
            result.Add(JsonUnescape(v.Substring(1, v.Length - 2)));
        }

        return result;
    }

    static string JsonStringArray(List<string> values)
    {
        List<string> quoted = new List<string>();

        foreach (string value in values)
            quoted.Add("\"" + JsonEscape(value) + "\"");

        return "[" + String.Join(", ", quoted.ToArray()) + "]";
    }

    static void SaveCache(
        string cachePath,
        List<CacheItem> items,
        Dictionary<string,string> status,
        UpdateSession session = null)
    {
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
                        "\",\"subtitle\":\"" + JsonEscape(x.Subtitle) + "\",\"episode_id\":\"" + JsonEscape(x.EpisodeId ?? "") + "\"}");
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
            w.WriteLine(session == null ? "  }" : "  },");
            if (session != null)
            {
                w.WriteLine("  \"update_session\": {");
                w.WriteLine("    \"started\": \"" +
                            JsonEscape(session.Started) + "\",");
                w.WriteLine("    \"completed\": " +
                            (session.Completed ? "true" : "false") + ",");
                w.WriteLine("    \"channels\": " + JsonStringArray(session.Channels) + ",");
                w.WriteLine("    \"done\": " + JsonStringArray(session.Done));
                w.WriteLine("  }");
            }
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

    }

    static int RunUpdate(string engine, string worker, string cachePath, string[] requested, int timeoutMs)
    {
        bool resume = requested.Length == 1 &&
            String.Equals(requested[0], "--resume",
                          StringComparison.OrdinalIgnoreCase);

        bool all = requested.Length == 1 &&
            String.Equals(requested[0], "--all",
                          StringComparison.OrdinalIgnoreCase);

        UpdateSession session = ReadUpdateSession(cachePath);

        if (resume && (session == null || session.Completed ||
                       session.Channels.Count == 0))
        {
            Console.Error.WriteLine("Aucune campagne interrompue à reprendre");
            return 2;
        }

        bool t18Only =
            requested.Length == 1 &&
            String.Equals(requested[0], "T18", StringComparison.OrdinalIgnoreCase);

        if (!resume && !t18Only &&
            !CheckRuntimeDependencies(engine, worker))
            return 2;

        List<string> allChannels = resume
            ? session.Channels
            : t18Only
                ? new List<string> { "T18" }
                : ReadChannels(engine);

        List<string> selected = new List<string>();

        if (requested.Length == 0)
        {
            int limit = Math.Min(10, allChannels.Count);
            for (int i = 0; i < limit; i++) selected.Add(allChannels[i]);
        }
        else if (all)
        {
            selected.AddRange(allChannels);
            session = new UpdateSession();
            session.Started = DateTimeOffset.Now.ToString("o");
            session.Channels.AddRange(selected);
        }
        else if (resume)
        {
            foreach (string channel in session.Channels)
                if (!session.Done.Contains(channel))
                    selected.Add(channel);
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

        if (resume)
        {
            foreach (string channel in selected)
            {
                if (!String.Equals(channel, "T18",
                                   StringComparison.OrdinalIgnoreCase))
                {
                    if (!CheckRuntimeDependencies(engine, worker))
                        return 2;
                    break;
                }
            }
        }

        List<CacheItem> oldItems = ReadCache(cachePath);
        Dictionary<string,string> status = ReadCacheStatus(cachePath);
        List<CacheItem> items = new List<CacheItem>(oldItems);

        if (all)
            SaveCache(cachePath, items, status, session);

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
                catch (Exception ex) when (!(ex is StdoutPipeClosedException))
                {
                    Console.WriteLine("erreur T18 (ancien cache conservé)");
                    Console.Error.WriteLine("T18: " + ex.Message);
                    status[channel] = "error";
                    SaveCache(cachePath, items, status, session);
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
                    SaveCache(cachePath, items, status, session);
                    continue;
                }
                if (run.ExitCode != 0)
                {
                    Console.WriteLine("erreur (" + run.ExitCode + ") (ancien cache conservé)");
                    if (!String.IsNullOrWhiteSpace(run.Stderr))
                        Console.Error.WriteLine(run.Stderr.TrimEnd());
                    status[channel] = "error";
                    SaveCache(cachePath, items, status, session);
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
                            Subtitle = fields[4],
                            EpisodeId = fields.Length > 5 ? fields[5] : null
                        });
                    }
                }
            }

            if (fresh.Count == 0)
            {
                Console.WriteLine("vide (ancien cache conservé)");
                status[channel] = "empty";
                SaveCache(cachePath, items, status, session);
                continue;
            }

            items.RemoveAll(delegate(CacheItem x) {
                return String.Equals(x.Channel, channel, StringComparison.CurrentCultureIgnoreCase);
            });
            items.AddRange(fresh);
            status[channel] = "ok";

            if ((all || resume) && session != null &&
                !session.Done.Contains(channel))
                session.Done.Add(channel);

            SaveCache(cachePath, items, status, session);
            Console.WriteLine("OK (" + fresh.Count + ")");
        }

        if ((all || resume) && session != null)
            session.Completed = session.Done.Count == session.Channels.Count;

        SaveCache(cachePath, items, status, session);

        Console.WriteLine();
        Console.WriteLine("Catalogue: " + items.Count + " émission(s)");
        Console.WriteLine("Cache écrit: " + cachePath);
        return 0;
    }

    static void PrintSearchTitles(string channel, IEnumerable<string> titles)
    {
        SortedDictionary<string,int> counts =
            new SortedDictionary<string,int>(
                StringComparer.CurrentCultureIgnoreCase);

        foreach (string raw in titles)
        {
            string title = String.IsNullOrWhiteSpace(raw)
                ? "(sans titre)" : raw.Trim();

            int count;
            counts.TryGetValue(title, out count);
            counts[title] = count + 1;
        }

        foreach (KeyValuePair<string,int> entry in counts)
        {
            Console.Write("  " + channel);
            if (entry.Value > 1)
                Console.Write(" (" + entry.Value + ")");
            Console.WriteLine(" - " + entry.Key);
        }
    }

    static int RunLiveSearchT18(string query, int timeoutMs)
    {
        List<CacheItem> items;

        try
        {
            items = FetchT18Catalog(timeoutMs);
        }
        catch (Exception ex) when (!(ex is StdoutPipeClosedException))
        {
            Console.Error.WriteLine("ERREUR T18: " + ex.Message);
            return 1;
        }

        List<string> titles = new List<string>();

        foreach (CacheItem item in items)
        {
            if (ContainsText(item.Title, query) ||
                ContainsText(item.Subtitle, query))
                titles.Add(item.Title);
        }

        Console.WriteLine("\"" + query + "\" est disponible sur :");

        if (titles.Count == 0)
        {
            Console.WriteLine("  aucun résultat");
            return 0;
        }

        PrintSearchTitles("T18", titles);

        Console.WriteLine();
        Console.WriteLine("1 chaîne(s)");

        return 0;
    }

    static int RunCachedSearch(string query, string cachePath, string selectedChannel = null)
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
            if (!String.IsNullOrEmpty(selectedChannel) &&
                !String.Equals(x.Channel, selectedChannel,
                    StringComparison.CurrentCultureIgnoreCase))
                continue;

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
            PrintSearchTitles(channel,
                list.ConvertAll(x => x.Title));
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
            @"^P(?:0Y)?(?:0M)?(?:0D)?T(?:(\d+)H)?(?:(\d+)M)?(?:(\d+)S)?$");

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
        catch (Exception ex) when (!(ex is StdoutPipeClosedException))
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

            bool matches =
                ContainsText(productTitle, query) ||
                ContainsText(program.title, query);

            int separator = query.IndexOf(
                " - ", StringComparison.Ordinal);

            if (!matches && separator > 0)
            {
                string titleQuery =
                    query.Substring(0, separator).Trim();

                string subtitleQuery =
                    query.Substring(separator + 3).Trim();

                matches =
                    ContainsText(productTitle, titleQuery) &&
                    ContainsText(program.title, subtitleQuery);
            }

            if (matches)
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
                catch (Exception firstEx) when (!(firstEx is StdoutPipeClosedException))
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
            catch (Exception ex) when (!(ex is StdoutPipeClosedException))
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
        string mode, string query, string engine, string worker,
        int timeoutMs, string selectedChannel = null)
    {
        List<string> channels = String.IsNullOrEmpty(selectedChannel)
            ? ReadChannels(engine)
            : new List<string> { selectedChannel };
        List<ProviderResult> results = new List<ProviderResult>();
        int n = 0;

        foreach (string channel in channels)
        {
            n++;
            Console.Write("[" + n + "] " + channel + " ... ");
            Console.Out.Flush();

            WorkerRun run =
                RunWorker(worker,
                    String.Equals(mode, "search", StringComparison.OrdinalIgnoreCase)
                        ? "list" : mode,
                    engine, channel, query, timeoutMs);

            if (run.TimedOut)
            {
                Console.WriteLine("timeout");
                if (!String.IsNullOrEmpty(selectedChannel)) return 1;
                continue;
            }

            if (run.ExitCode != 0)
            {
                Console.WriteLine("erreur (" + run.ExitCode + ")");
                if (!String.IsNullOrEmpty(selectedChannel)) return run.ExitCode;
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
                PrintSearchTitles(r.Channel,
                    r.Emissions.ConvertAll(x => x.Title));
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

    class Tv5CatalogShow
    {
        public string Title;
        public string AssetId;
        public Dictionary<string, string> Episodes =
            new Dictionary<string, string>();
    }

    static bool IsTv5Channel(string channel)
    {
        return
            String.Equals(
                channel, "TV5 Monde",
                StringComparison.OrdinalIgnoreCase) ||
            String.Equals(
                channel, "TV5MONDE+",
                StringComparison.OrdinalIgnoreCase) ||
            String.Equals(
                channel, "TV5",
                StringComparison.OrdinalIgnoreCase);
    }

    static void FindTv5Shows(
        object node, List<Tv5CatalogShow> shows)
    {
        Dictionary<string, object> d =
            node as Dictionary<string, object>;

        if (d != null)
        {
            object assetObj;
            object episodesObj;
            object localizedObj;

            if (d.TryGetValue("assetId", out assetObj) &&
                d.TryGetValue("episodes", out episodesObj) &&
                d.TryGetValue("localized", out localizedObj))
            {
                Dictionary<string, object> episodes =
                    episodesObj as Dictionary<string, object>;

                object[] localized =
                    localizedObj as object[];

                string title = null;

                if (localized != null && localized.Length > 0)
                {
                    Dictionary<string, object> loc =
                        localized[0] as Dictionary<string, object>;

                    object titleObj;

                    if (loc != null &&
                        loc.TryGetValue("title", out titleObj))
                        title = Convert.ToString(titleObj);
                }

                if (episodes != null &&
                    episodes.Count > 0 &&
                    !String.IsNullOrEmpty(title))
                {
                    Tv5CatalogShow show = new Tv5CatalogShow();

                    show.Title = title;
                    show.AssetId = Convert.ToString(assetObj);

                    foreach (KeyValuePair<string, object> ep in episodes)
                    {
                        string slug = Convert.ToString(ep.Value);

                        if (!String.IsNullOrEmpty(ep.Key) &&
                            !String.IsNullOrEmpty(slug))
                            show.Episodes[ep.Key] = slug;
                    }

                    bool duplicate = false;

                    foreach (Tv5CatalogShow old in shows)
                    {
                        if (String.Equals(
                                old.AssetId,
                                show.AssetId,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            duplicate = true;
                            break;
                        }
                    }

                    if (!duplicate)
                        shows.Add(show);
                }
            }

            foreach (KeyValuePair<string, object> kv in d)
                FindTv5Shows(kv.Value, shows);

            return;
        }

        object[] array = node as object[];

        if (array != null)
        {
            foreach (object x in array)
                FindTv5Shows(x, shows);
        }
    }

    static string Tv5EpisodeLabel(string slug)
    {
        if (String.IsNullOrEmpty(slug))
            return "";

        Match m = Regex.Match(
            slug,
            @"-s-(\d+)-e(\d+)(?:-(.*))?$",
            RegexOptions.IgnoreCase);

        if (!m.Success)
            return slug;

        int season;
        int episode;

        Int32.TryParse(m.Groups[1].Value, out season);
        Int32.TryParse(m.Groups[2].Value, out episode);

        string label =
            "S" + season + " E" + episode;

        string tail = m.Groups[3].Value;

        if (!String.IsNullOrEmpty(tail))
        {
            string[] words = tail.Split('-');

            StringBuilder title = new StringBuilder();

            foreach (string word in words)
            {
                if (word.Length == 0)
                    continue;

                if (title.Length > 0)
                    title.Append(" ");

                title.Append(word);
            }

            if (title.Length > 0)
                label += " - " + title.ToString();
        }

        return label;
    }

    static int CompareTv5Episodes(
        KeyValuePair<string, string> a,
        KeyValuePair<string, string> b)
    {
        Match ma = Regex.Match(
            a.Value ?? "",
            @"-s-(\d+)-e(\d+)",
            RegexOptions.IgnoreCase);

        Match mb = Regex.Match(
            b.Value ?? "",
            @"-s-(\d+)-e(\d+)",
            RegexOptions.IgnoreCase);

        if (ma.Success && mb.Success)
        {
            int sa = Int32.Parse(ma.Groups[1].Value);
            int sb = Int32.Parse(mb.Groups[1].Value);

            int c = sa.CompareTo(sb);
            if (c != 0)
                return c;

            int ea = Int32.Parse(ma.Groups[2].Value);
            int eb = Int32.Parse(mb.Groups[2].Value);

            c = ea.CompareTo(eb);
            if (c != 0)
                return c;
        }

        return String.Compare(
            a.Value, b.Value,
            StringComparison.CurrentCultureIgnoreCase);
    }

    static int RunTv5List(string query, int timeoutMs)
    {
        const string url =
            "https://www.tv5mondeplus.com/api/fr/component/series";

        try
        {
            string body = RunCurl(
                url,
                new string[] {
                    "Accept: application/json"
                },
                timeoutMs);

            JavaScriptSerializer json =
                new JavaScriptSerializer();

            object root =
                json.DeserializeObject(body);

            List<Tv5CatalogShow> shows =
                new List<Tv5CatalogShow>();

            FindTv5Shows(root, shows);

            int count = 0;

            foreach (Tv5CatalogShow show in shows)
            {
                if (!ContainsText(show.Title ?? "", query))
                    continue;

                count++;

                Console.WriteLine();
                Console.WriteLine(
                    show.Title +
                    "  [" + show.AssetId + "]");

                List<KeyValuePair<string, string>> episodes =
                    new List<KeyValuePair<string, string>>(
                        show.Episodes);

                episodes.Sort(CompareTv5Episodes);

                int n = 0;

                foreach (KeyValuePair<string, string> ep in episodes)
                {
                    n++;

                    Console.WriteLine(
                        "  " + n + ". " +
                        Tv5EpisodeLabel(ep.Value) +
                        "  [" + ep.Key + "]");
                }
            }

            if (count == 0)
            {
                Console.WriteLine(
                    "Aucun résultat sur TV5 Monde pour \"" +
                    query + "\".");
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine(
                    count + " série(s)");
            }

            return 0;
        }
        catch (Exception ex) when (!(ex is StdoutPipeClosedException))
        {
            Console.Error.WriteLine(
                "ERREUR catalogue TV5: " + ex.Message);
            return 1;
        }
    }

    static string Tv5ShortLanguage(string language)
    {
        if (String.IsNullOrEmpty(language))
            return "?";

        switch (language.ToLowerInvariant())
        {
            case "fra": return "fr";
            case "eng": return "en";
            case "spa": return "es";
            case "deu": return "de";
            case "ger": return "de";
            case "ron": return "ro";
            case "rum": return "ro";
            case "ara": return "ar";
            case "lin": return "ln";
            case "swa": return "sw";
            default:    return language;
        }
    }

    static List<Tv5CatalogShow> FindMatchingTv5Shows(
        string query, int timeoutMs)
    {
        const string url =
            "https://www.tv5mondeplus.com/api/fr/component/series";

        string body = RunCurl(
            url,
            new string[] {
                "Accept: application/json"
            },
            timeoutMs);

        JavaScriptSerializer json =
            new JavaScriptSerializer();

        object root = json.DeserializeObject(body);

        List<Tv5CatalogShow> shows =
            new List<Tv5CatalogShow>();

        FindTv5Shows(root, shows);

        List<Tv5CatalogShow> found =
            new List<Tv5CatalogShow>();

        foreach (Tv5CatalogShow show in shows)
        {
            if (ContainsText(show.Title ?? "", query))
                found.Add(show);
        }

        return found;
    }

    static bool Tv5EpisodeMatches(
        string slug, string selector)
    {
        string ns = NormalizeText(selector ?? "");

        Match m = Regex.Match(
            slug ?? "",
            @"-s-(\d+)-e(\d+)",
            RegexOptions.IgnoreCase);

        if (!m.Success)
            return false;

        string season = m.Groups[1].Value;
        string episode = m.Groups[2].Value;

        string a = NormalizeText(
            "S" + season + " E" + episode);

        string b = NormalizeText(
            "S" + season + "E" + episode);

        return ns == a || ns == b;
    }

    static string Tv5RedBeeLanguage(string language)
    {
        if (String.IsNullOrEmpty(language))
            return null;

        switch (language.ToLowerInvariant())
        {
            case "fr": return "fra";
            case "en": return "eng";
            case "es": return "spa";
            case "de": return "deu";
            case "ro": return "ron";
            case "ar": return "ara";
            case "ln": return "lin";
            case "sw": return "swa";
            default:   return language.ToLowerInvariant();
        }
    }

    static string Tv5SafeFileName(string text)
    {
        if (String.IsNullOrEmpty(text))
            return "tv5";

        string s = NormalizeText(text);

        s = Regex.Replace(s, @"[^a-zA-Z0-9]+", "-");
        s = s.Trim('-');

        return s.Length == 0 ? "tv5" : s;
    }

    static int RunTv5Get(
        string query, string episodeSelector,
        string languageSelector, int timeoutMs)
    {
        try
        {
            List<Tv5CatalogShow> matchingShows =
                FindMatchingTv5Shows(query, timeoutMs);

            if (matchingShows.Count > 1)
            {
                Console.Error.WriteLine(
                    "Recherche ambiguë: " + matchingShows.Count +
                    " séries correspondent");

                foreach (Tv5CatalogShow candidate in matchingShows)
                    Console.Error.WriteLine(
                        "  " + candidate.Title +
                        " [" + candidate.AssetId + "]");

                return 9;
            }

            Tv5CatalogShow show =
                matchingShows.Count == 1 ? matchingShows[0] : null;

            if (show == null)
            {
                Console.Error.WriteLine(
                    "Aucun résultat sur TV5 Monde pour \"" +
                    query + "\".");
                return 3;
            }

            List<KeyValuePair<string, string>> episodes =
                new List<KeyValuePair<string, string>>(
                    show.Episodes);

            episodes.Sort(CompareTv5Episodes);

            KeyValuePair<string, string>? selected = null;

            foreach (KeyValuePair<string, string> ep in episodes)
            {
                if (Tv5EpisodeMatches(
                        ep.Value, episodeSelector))
                {
                    selected = ep;
                    break;
                }
            }

            if (!selected.HasValue)
            {
                Console.Error.WriteLine(
                    "Épisode \"" + episodeSelector +
                    "\" introuvable.");
                return 3;
            }

            KeyValuePair<string, string> episode =
                selected.Value;

            Match sem = Regex.Match(
                episode.Value ?? "",
                @"-s-(\d+)-e(\d+)",
                RegexOptions.IgnoreCase);

            string episodeTag;

            if (sem.Success)
            {
                int season =
                    Int32.Parse(sem.Groups[1].Value);
                int number =
                    Int32.Parse(sem.Groups[2].Value);

                episodeTag =
                    "S" + season.ToString("00") +
                    "E" + number.ToString("00");
            }
            else
            {
                episodeTag = episode.Key;
            }

            Console.WriteLine(
                show.Title + " - " +
                Tv5EpisodeLabel(episode.Value));
            Console.WriteLine(
                "AssetId : " + episode.Key);

            string token =
                Tv5Authenticate(timeoutMs);

            Tv5PlayResponse play =
                Tv5GetPlay(
                    episode.Key, token, timeoutMs);

            if (play.subtitles == null ||
                play.subtitles.Length == 0)
            {
                Console.Error.WriteLine(
                    "Aucun sous-titre disponible.");
                return 3;
            }

            List<string> requested =
                new List<string>();

            bool all =
                String.Equals(
                    languageSelector,
                    "all",
                    StringComparison.OrdinalIgnoreCase);

            if (!all)
            {
                string[] parts =
                    languageSelector.Split(',');

                foreach (string part in parts)
                {
                    string lang =
                        Tv5RedBeeLanguage(part.Trim());

                    if (!String.IsNullOrEmpty(lang) &&
                        !requested.Contains(lang))
                        requested.Add(lang);
                }

                if (requested.Count == 0)
                {
                    Console.Error.WriteLine(
                        "Aucune langue demandée.");
                    return 2;
                }
            }

            int downloaded = 0;

            foreach (Tv5Subtitle sub in play.subtitles)
            {
                if (sub == null ||
                    String.IsNullOrEmpty(sub.language) ||
                    String.IsNullOrEmpty(sub.url))
                    continue;

                bool wanted = all;

                if (!wanted)
                {
                    foreach (string lang in requested)
                    {
                        if (String.Equals(
                                sub.language,
                                lang,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            wanted = true;
                            break;
                        }

                        // TV5/RedBee peut employer les variantes
                        // historiques ger/deu et rum/ron.
                        if ((lang == "deu" &&
                             String.Equals(
                                 sub.language, "ger",
                                 StringComparison.OrdinalIgnoreCase)) ||
                            (lang == "ron" &&
                             String.Equals(
                                 sub.language, "rum",
                                 StringComparison.OrdinalIgnoreCase)))
                        {
                            wanted = true;
                            break;
                        }
                    }
                }

                if (!wanted)
                    continue;

                string shortLang =
                    Tv5ShortLanguage(sub.language);

                string output =
                    Tv5SafeFileName(show.Title) +
                    "-" + episodeTag +
                    "." + shortLang + ".vtt";

                Console.WriteLine(
                    "Téléchargement " +
                    shortLang + " ...");

                string webvtt =
                    RunCurl(
                        sub.url,
                        null,
                        timeoutMs);

                if (!webvtt.StartsWith("WEBVTT"))
                {
                    Console.Error.WriteLine(
                        "ERREUR: " + shortLang +
                        " n'est pas un WebVTT.");
                    continue;
                }

                File.WriteAllText(
                    output,
                    webvtt,
                    new UTF8Encoding(false));

                Console.WriteLine(
                    "  -> " + output);

                downloaded++;
            }

            if (!all)
            {
                foreach (string lang in requested)
                {
                    bool found = false;

                    foreach (Tv5Subtitle sub in play.subtitles)
                    {
                        if (sub == null)
                            continue;

                        if (String.Equals(
                                sub.language,
                                lang,
                                StringComparison.OrdinalIgnoreCase) ||
                            (lang == "deu" &&
                             String.Equals(
                                 sub.language, "ger",
                                 StringComparison.OrdinalIgnoreCase)) ||
                            (lang == "ron" &&
                             String.Equals(
                                 sub.language, "rum",
                                 StringComparison.OrdinalIgnoreCase)))
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                        Console.Error.WriteLine(
                            "Langue indisponible : " +
                            Tv5ShortLanguage(lang));
                }
            }

            if (downloaded == 0)
                return 3;

            Console.WriteLine();
            Console.WriteLine(
                downloaded +
                " fichier(s) de sous-titres téléchargé(s).");

            return 0;
        }
        catch (Exception ex) when (!(ex is StdoutPipeClosedException))
        {
            Console.Error.WriteLine(
                "ERREUR TV5 get: " + ex.Message);
            return 1;
        }
    }

    static int RunTv5Info(
        string query, string episodeSelector, int timeoutMs)
    {
        try
        {
            List<Tv5CatalogShow> matchingShows =
                FindMatchingTv5Shows(query, timeoutMs);

            if (matchingShows.Count > 1)
            {
                Console.Error.WriteLine(
                    "Recherche ambiguë: " + matchingShows.Count +
                    " séries correspondent");

                foreach (Tv5CatalogShow candidate in matchingShows)
                    Console.Error.WriteLine(
                        "  " + candidate.Title +
                        " [" + candidate.AssetId + "]");

                return 9;
            }

            Tv5CatalogShow show =
                matchingShows.Count == 1 ? matchingShows[0] : null;

            if (show == null)
            {
                Console.WriteLine(
                    "Aucun résultat sur TV5 Monde pour \"" +
                    query + "\".");
                return 0;
            }

            List<KeyValuePair<string, string>> episodes =
                new List<KeyValuePair<string, string>>(
                    show.Episodes);

            episodes.Sort(CompareTv5Episodes);

            Console.WriteLine(show.Title);
            Console.WriteLine(
                "AssetId série : " + show.AssetId);
            Console.WriteLine();

            if (String.IsNullOrEmpty(episodeSelector))
            {
                int n = 0;

                foreach (KeyValuePair<string, string> ep in episodes)
                {
                    n++;

                    Console.WriteLine(
                        "  " + n + ". " +
                        Tv5EpisodeLabel(ep.Value) +
                        "  [" + ep.Key + "]");
                }

                Console.WriteLine();
                Console.WriteLine(
                    episodes.Count + " épisode(s)");

                return 0;
            }

            KeyValuePair<string, string>? selected = null;

            foreach (KeyValuePair<string, string> ep in episodes)
            {
                if (Tv5EpisodeMatches(
                        ep.Value, episodeSelector))
                {
                    selected = ep;
                    break;
                }
            }

            if (!selected.HasValue)
            {
                Console.Error.WriteLine(
                    "Épisode \"" + episodeSelector +
                    "\" introuvable.");
                return 3;
            }

            KeyValuePair<string, string> episode =
                selected.Value;

            Console.WriteLine(
                Tv5EpisodeLabel(episode.Value));
            Console.WriteLine(
                "AssetId : " + episode.Key);

            string token =
                Tv5Authenticate(timeoutMs);

            Tv5PlayResponse play =
                Tv5GetPlay(
                    episode.Key, token, timeoutMs);

            Console.Write("Sous-titres :");

            if (play.subtitles == null ||
                play.subtitles.Length == 0)
            {
                Console.WriteLine(" aucun");
                return 0;
            }

            foreach (Tv5Subtitle sub in play.subtitles)
            {
                if (sub == null)
                    continue;

                Console.Write(
                    " " +
                    Tv5ShortLanguage(sub.language));
            }

            Console.WriteLine();

            return 0;
        }
        catch (Exception ex) when (!(ex is StdoutPipeClosedException))
        {
            Console.Error.WriteLine(
                "ERREUR TV5 info: " + ex.Message);
            return 1;
        }
    }

    static int RunList(
        string channel, string query,
        string engine, string worker, string cachePath, int timeoutMs)
    {
        if (IsTv5Channel(channel))
            return RunTv5List(query, timeoutMs);

        if (String.Equals(channel, "T18", StringComparison.OrdinalIgnoreCase))
        {
            List<CacheItem> t18Items = ReadCache(cachePath);
            int t18Count = 0;

            foreach (CacheItem item in t18Items)
            {
                if (!String.Equals(
                        item.Channel, "T18",
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                string text =
                    (item.Title ?? "") + " " + (item.Subtitle ?? "");

                if (!ContainsText(item.Title, query) &&
                    !ContainsText(item.Subtitle, query) &&
                    !ContainsText(text, query))
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

        List<string[]> items = new List<string[]>();

        using (StringReader sr = new StringReader(run.Stdout))
        {
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                line = line.TrimStart('\uFEFF');
                if (!line.StartsWith("ITEM\t",
                                     StringComparison.Ordinal))
                    continue;

                string[] x = line.Split('\t');
                if (x.Length >= 5)
                    items.Add(x);
            }
        }

        if (items.Count == 0)
        {
            Console.WriteLine("Aucun résultat sur " + channel +
                              " pour \"" + query + "\".");
            return 0;
        }

        // Trier les épisodes SxxExx avant les autres entrées.
        // List.Sort est stable uniquement si l'on conserve
        // explicitement l'ordre initial comme critère secondaire.
        var sorted = items.Select((x, i) => new {
            Fields = x,
            Index = i,
            Match = System.Text.RegularExpressions.Regex.Match(
                x.Length > 5 ? x[5] : "",
                @"^S(\d+)E(\d+)\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        }).OrderBy(x => x.Match.Success ? 0 : 1)
          .ThenBy(x => x.Match.Success ?
              int.Parse(x.Match.Groups[1].Value) : 0)
          .ThenBy(x => x.Match.Success ?
              int.Parse(x.Match.Groups[2].Value) : 0)
          .ThenBy(x => x.Index)
          .ToList();

        Console.WriteLine("Résultats sur " + channel +
                          " pour \"" + query + "\" :");

        foreach (var entry in sorted)
        {
            string[] x = entry.Fields;

            Console.Write("  " + x[2] + ". ");

            if (entry.Match.Success)
                Console.Write(entry.Match.Value + "  ");

            Console.Write(x[3]);

            if (!String.IsNullOrEmpty(x[4]))
                Console.Write(" - " + x[4]);

            Console.WriteLine();
        }

        Console.WriteLine();
        Console.WriteLine(items.Count + " émission(s)");
        return 0;
    }

    static int Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = new UTF8Encoding(false);

            Console.SetOut(
                new StdoutPipeWriter(Console.Out));

            int rc = RunMain(args);

            Console.Out.Flush();

            return rc;
        }
        catch (StdoutPipeClosedException)
        {
            return 141;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine("ERREUR: " + ex.Message);
            return 1;
        }
    }

    static int RunMain(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += ResolveLocalAssembly;


        if (args.Length == 1 &&
            String.Equals(args[0], "--version", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("captvty-cli 2.9.3");
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
                "  mono captvty-cli.exe [--timeout secondes] update [--all | --resume | \"chaîne\" ...]\n" +
                "  mono captvty-cli.exe [--timeout secondes] search [--live] [--channel \"chaîne\"] \"texte\"\n" +
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

        string engine = Path.Combine(exeDir, "Captvty-cli-engine-28-progress.exe");
        string worker = Path.Combine(exeDir, "captvty-provider-28.exe");
        string cache = Path.Combine(exeDir, "captvty-cache-28.json");

        if (String.Equals(mode, "tv5subs", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length < 2 || args.Length > 4)
            {
                Console.Error.WriteLine(
                    "Usage: mono captvty-cli.exe tv5subs ASSET_ID [LANG] [OUTPUT.vtt]");
                return 2;
            }

            string assetId = args[1];
            string language =
                args.Length >= 3 ? args[2] : "fra";
            string output =
                args.Length >= 4
                    ? args[3]
                    : assetId + "-" + language + ".vtt";

            return RunTv5Subs(
                assetId, language, output, timeoutMs);
        }

        if (String.Equals(mode, "search", StringComparison.OrdinalIgnoreCase))
        {
            bool live = false;
            string selectedChannel = null;
            int first = 1;

            while (first < args.Length)
            {
                if (String.Equals(args[first], "--live",
                    StringComparison.OrdinalIgnoreCase))
                {
                    live = true;
                    first++;
                }
                else if (String.Equals(args[first], "--channel",
                    StringComparison.OrdinalIgnoreCase))
                {
                    if (first + 1 >= args.Length ||
                        String.IsNullOrWhiteSpace(args[first + 1]) ||
                        args[first + 1].StartsWith("--"))
                    {
                        Console.Error.WriteLine("ERREUR: --channel attend un nom de chaîne");
                        return 2;
                    }

                    selectedChannel = args[first + 1];
                    first += 2;
                }
                else
                    break;
            }

            if (args.Length <= first)
            {
                Console.Error.WriteLine("Usage: mono captvty-cli.exe search [--live] [--channel \"chaîne\"] \"texte\"");
                return 2;
            }

            string searchQuery = String.Join(" ", args, first, args.Length - first);

            if (!live && File.Exists(cache))
                return RunCachedSearch(searchQuery, cache, selectedChannel);

            if (String.Equals(selectedChannel, "T18",
                StringComparison.OrdinalIgnoreCase))
                return RunLiveSearchT18(searchQuery, timeoutMs);

            if (!CheckRuntimeDependencies(engine, worker))
                return 2;

            return RunSearchOrInfo("search", searchQuery,
                engine, worker, timeoutMs, selectedChannel);
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
            bool t18List =
                String.Equals(args[1], "T18", StringComparison.OrdinalIgnoreCase);
            bool tv5List = IsTv5Channel(args[1]);

            if (!t18List && !tv5List &&
                !CheckRuntimeDependencies(engine, worker))
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
                IsTv5Channel(args[1]))
            {
                string tv5Query = args[2];
                string tv5Episode =
                    args.Length >= 4 ? args[3] : null;

                return RunTv5Info(
                    tv5Query,
                    tv5Episode,
                    timeoutSeconds < 0 ? 600000 : timeoutMs);
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
            bool tv5Get =
                args.Length >= 2 &&
                IsTv5Channel(args[1]);

            if ((!tv5Get && args.Length != 5) ||
                (tv5Get && args.Length != 4 && args.Length != 5))
            {
                if (tv5Get)
                    Console.Error.WriteLine(
                        "Usage: mono captvty-cli.exe get \"TV5 Monde\" \"titre\" \"S1 E1\" [fr|fr,es,...|all]");
                else
                    Console.Error.WriteLine(
                        "Usage: mono captvty-cli.exe get \"chaîne\" \"titre\" \"sous-titre\" high|low|N");
                return 2;
            }
            if (IsTv5Channel(args[1]))
            {
                int getTimeoutMs =
                    timeoutSeconds < 0
                        ? 600000
                        : timeoutMs;

                string tv5Language;

                if (args.Length == 4)
                {
                    tv5Language = "fr";
                    Console.WriteLine(
                        "Aucune langue indiquée : français (fr) téléchargé par défaut.");
                }
                else
                {
                    tv5Language = args[4];
                }

                return RunTv5Get(
                    args[2],
                    args[3],
                    tv5Language,
                    getTimeoutMs);
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
