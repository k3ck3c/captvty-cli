using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

class CaptvtyProvider
{
    const BindingFlags All =
        BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Static | BindingFlags.Instance;

    static object Call(object obj, string name, params object[] args)
    {
        if (obj == null) return null;
        Type t = obj.GetType();

        while (t != null)
        {
            foreach (MethodInfo mi in t.GetMethods(All))
            {
                if (mi.Name != name) continue;
                if (mi.GetParameters().Length != args.Length) continue;
                return mi.Invoke(obj, args);
            }
            t = t.BaseType;
        }
        return null;
    }

    static object CallStatic(Type t, string name, params object[] args)
    {
        while (t != null)
        {
            foreach (MethodInfo mi in t.GetMethods(All))
            {
                if (!mi.IsStatic || mi.Name != name) continue;
                if (mi.GetParameters().Length != args.Length) continue;
                return mi.Invoke(null, args);
            }
            t = t.BaseType;
        }
        return null;
    }

    static string Str(object obj, string name)
    {
        try
        {
            object v = Call(obj, name);
            return v == null ? "" : Convert.ToString(v);
        }
        catch { return ""; }
    }

    static int IntValue(object obj, string name)
    {
        try
        {
            object v = Call(obj, name);
            return v == null ? 0 : Convert.ToInt32(v);
        }
        catch { return 0; }
    }

    static void WaitTask(object v)
    {
        Task t = v as Task;
        if (t != null) t.Wait();
    }

    static string Clean(string s)
    {
        if (String.IsNullOrEmpty(s)) return "";
        return s.Replace('\t',' ').Replace('\r',' ').Replace('\n',' ');
    }

    static string NormalizeText(string s)
    {
        if (String.IsNullOrEmpty(s)) return "";

        string d = s.Normalize(NormalizationForm.FormD);
        StringBuilder b = new StringBuilder(d.Length);

        foreach (char c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                b.Append(c);

        return b.ToString()
            .Normalize(NormalizationForm.FormC)
            .ToLowerInvariant();
    }

    static bool ContainsText(string text, string wanted)
    {
        return NormalizeText(text).IndexOf(
            NormalizeText(wanted),
            StringComparison.Ordinal) >= 0;
    }

    static List<object> ToList(object obj)
    {
        List<object> r = new List<object>();
        IEnumerable e = obj as IEnumerable;
        if (e == null) return r;
        foreach (object x in e) r.Add(x);
        return r;
    }

    static void GetSize(object media, out int width, out int height)
    {
        width = 0;
        height = 0;

        try
        {
            object v = Call(media, "_P7b");
            if (v == null) return;

            Type t = v.GetType();
            PropertyInfo pw = t.GetProperty("Width", All);
            PropertyInfo ph = t.GetProperty("Height", All);

            if (pw != null) width = Convert.ToInt32(pw.GetValue(v, null));
            if (ph != null) height = Convert.ToInt32(ph.GetValue(v, null));

            FieldInfo fw = t.GetField("Width", All);
            FieldInfo fh = t.GetField("Height", All);

            if (width == 0 && fw != null) width = Convert.ToInt32(fw.GetValue(v));
            if (height == 0 && fh != null) height = Convert.ToInt32(fh.GetValue(v));
        }
        catch { }
    }

    static object FindEmission(
        IEnumerable keys, string channel, string titleWanted, string subtitleWanted,
        out int count, out string representative)
    {
        count = 0;
        representative = "";
        object found = null;

        foreach (object emission in keys)
        {
            if (!String.Equals(
                    Str(emission, "_C5B"), channel,
                    StringComparison.CurrentCultureIgnoreCase))
                continue;

            string title = Str(emission, "_0u");
            string subtitle = Str(emission, "_K8b");
            string episode = Str(emission, "_Kq");

            if (!ContainsText(title, titleWanted) &&
                !ContainsText(subtitle, titleWanted) &&
                !ContainsText(episode, titleWanted))
                continue;

            if (!String.IsNullOrEmpty(subtitleWanted) &&
                !ContainsText(title, subtitleWanted) &&
                !ContainsText(subtitle, subtitleWanted) &&
                !ContainsText(episode, subtitleWanted))
                continue;

            count++;

            if (found == null)
            {
                found = emission;
                representative = !String.IsNullOrEmpty(title) ? title : subtitle;
            }
        }

        return found;
    }

    static ConstructorInfo FindDownloadCtor(Type t)
    {
        foreach (ConstructorInfo ci in t.GetConstructors(All))
            if (ci.GetParameters().Length == 2)
                return ci;
        return null;
    }

    static string DownloadPath(object item)
    {
        try
        {
            object v = Call(item, "_0HA");
            return v == null ? "" : Convert.ToString(v);
        }
        catch { return ""; }
    }

    static int RunGet(
        Assembly a, object emission,
        string channel, string representative)
    {
        WaitTask(Call(emission, "_z7b", false));

        List<object> before = ToList(Call(emission, "_3x"));

        foreach (object media in before)
        {
            try { WaitTask(Call(media, "_bf")); }
            catch { }
        }

        object best = Call(emission, "_YcB");
        if (best == null)
        {
            Console.Error.WriteLine("Aucun média téléchargeable");
            return 4;
        }

        int w, h;
        GetSize(best, out w, out h);
        int bitrate = IntValue(best, "_iUb");

        Console.WriteLine(
            "GETMEDIA\t" + Clean(channel) + "\t" +
            Clean(representative) + "\t" +
            w + "\t" + h + "\t" + bitrate);
        Console.Out.Flush();

        Type rha = a.GetType("_RHA", true);
        object engine = CallStatic(rha, "_PjB", best, false);

        if (engine == null)
        {
            Console.Error.WriteLine("Moteur Captvty introuvable");
            return 5;
        }

        Type exb = a.GetType("_exB", true);
        ConstructorInfo ctor = FindDownloadCtor(exb);

        if (ctor == null)
        {
            Console.Error.WriteLine("Constructeur _exB(media,engine) introuvable");
            return 6;
        }

        object item = ctor.Invoke(new object[] { best, engine });

        Type ipa = a.GetType("_iPA", true);
        CallStatic(ipa, "_D2", item);

        int last = -1;
        DateTime start = DateTime.UtcNow;

        while (true)
        {
            int state = IntValue(item, "_4cB");

            if (state != last)
            {
                Console.WriteLine(
                    "GETSTATE\t" + state + "\t" + Clean(DownloadPath(item)));
                Console.Out.Flush();
                last = state;
            }

            if (state == 7)
            {
                Console.WriteLine(
                    "DOWNLOAD\t" + Clean(channel) + "\t" +
                    Clean(representative) + "\t" +
                    Clean(DownloadPath(item)));
                Console.Out.Flush();
                return 0;
            }

            if (state == 2 || state == 9)
            {
                Console.Error.WriteLine("Téléchargement en échec, état " + state);
                return 7;
            }

            if ((DateTime.UtcNow - start).TotalHours >= 12)
            {
                Console.Error.WriteLine("Téléchargement interrompu après 12 heures");
                return 8;
            }

            Thread.Sleep(1000);
        }
    }

    static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (args.Length < 3)
            return 2;

        string mode = args[0];
        string enginePath = args[1];
        string wantedChannel = args[2];
        string query = args.Length >= 4 ? args[3] : "";
        string subtitleWanted = "";

        if (String.Equals(mode, "get", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length != 6)
            {
                Console.Error.WriteLine(
                    "Usage: mono captvty-provider.exe get ENGINE CHAINE TITRE SOUS_TITRE high");
                return 2;
            }

            subtitleWanted = args[4];

            if (!String.Equals(args[5], "high", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine("V1: seule la qualité high est supportée");
                return 2;
            }
        }
        else if (String.Equals(mode, "catalog", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length != 3)
            {
                Console.Error.WriteLine(
                    "Usage: mono captvty-provider.exe catalog ENGINE CHAINE");
                return 2;
            }
        }
        else if (args.Length != 4)
        {
            Console.Error.WriteLine(
                "Usage: mono captvty-provider.exe search|list|info ENGINE CHAINE RECHERCHE");
            return 2;
        }

        try
        {
            Assembly a = Assembly.LoadFrom(enginePath);

            Type uu = a.GetType("_Uu", true);
            Type a4b = a.GetType("_A4b", true);

            IEnumerable channels =
                (IEnumerable)uu.GetField("_afB", All).GetValue(null);

            object selectedChannel = null;
            string selectedName = null;

            foreach (object channel in channels)
            {
                string name = Str(channel, "_b9A");
                if (String.Equals(
                    name, wantedChannel,
                    StringComparison.CurrentCultureIgnoreCase))
                {
                    selectedChannel = channel;
                    selectedName = name;
                    break;
                }
            }

            if (selectedChannel == null)
            {
                Console.Error.WriteLine("Chaîne introuvable: " + wantedChannel);
                return 3;
            }

            object provider = Call(selectedChannel, "_mbA");
            if (provider == null) return 0;

            ManualResetEvent done = new ManualResetEvent(false);

            EventHandler finished =
                delegate(object sender, EventArgs e) { done.Set(); };

            try
            {
                Call(provider, "_XUB", finished);
                Call(provider, "_Wg");
                done.WaitOne(55000);
            }
            finally
            {
                try { Call(provider, "_chB", finished); } catch { }
                done.Close();
            }

            object reservoir =
                a4b.GetField("_AtA", All).GetValue(null);

            IEnumerable keys =
                (IEnumerable)reservoir.GetType()
                    .GetProperty("Keys")
                    .GetValue(reservoir, null);

            if (String.Equals(mode, "catalog", StringComparison.OrdinalIgnoreCase))
            {
                List<object> catalogItems = new List<object>();

                foreach (object item in keys)
                {
                    if (!String.Equals(
                            Str(item, "_C5B"), selectedName,
                            StringComparison.CurrentCultureIgnoreCase))
                        continue;

                    catalogItems.Add(item);
                }

                Console.WriteLine(
                    "COUNT\t" + Clean(selectedName) + "\t" + catalogItems.Count);

                int shown = 0;
                foreach (object item in catalogItems)
                {
                    shown++;
                    if (shown > 10) break;

                    Console.WriteLine(
                        "ITEM\t" + Clean(selectedName) + "\t" +
                        shown + "\t" +
                        Clean(Str(item, "_0u")) + "\t" +
                        Clean(Str(item, "_K8b")));
                }

                return 0;
            }

            if (String.Equals(mode, "dump", StringComparison.OrdinalIgnoreCase))
            {
                int itemNo = 0;
                foreach (object item in keys)
                {
                    if (!String.Equals(
                            Str(item, "_C5B"), selectedName,
                            StringComparison.CurrentCultureIgnoreCase))
                        continue;

                    itemNo++;
                    Console.WriteLine(
                        "ITEM\t" + Clean(selectedName) + "\t" +
                        itemNo + "\t" +
                        Clean(Str(item, "_0u")) + "\t" +
                        Clean(Str(item, "_K8b")));
                }
                return 0;
            }

            int count;
            string representative;

            object emission = FindEmission(
                keys, selectedName, query, subtitleWanted,
                out count, out representative);

            if (count == 0 || emission == null)
            {
                if (String.Equals(mode, "get", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Error.WriteLine("Aucune émission correspondante");
                    return 4;
                }
                return 0;
            }

            if (String.Equals(mode, "search", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(
                    "RESULT\t" + Clean(selectedName) + "\t" +
                    count + "\t" + Clean(representative));
                return 0;
            }

            if (String.Equals(mode, "list", StringComparison.OrdinalIgnoreCase))
            {
                int itemNo = 0;

                foreach (object item in keys)
                {
                    if (!String.Equals(
                            Str(item, "_C5B"), selectedName,
                            StringComparison.CurrentCultureIgnoreCase))
                        continue;

                    string t = Str(item, "_0u");
                    string st = Str(item, "_K8b");

                    if (!ContainsText(t, query) &&
                        !ContainsText(st, query))
                        continue;

                    itemNo++;

                    Console.WriteLine(
                        "ITEM\t" + Clean(selectedName) + "\t" +
                        itemNo + "\t" +
                        Clean(t) + "\t" +
                        Clean(st));
                }

                return 0;
            }

            if (String.Equals(mode, "info", StringComparison.OrdinalIgnoreCase))
            {
                int emissionNo = 0;

                Console.WriteLine(
                    "RESULT\t" + Clean(selectedName) + "\t" +
                    count + "\t" + Clean(representative));

                foreach (object infoEmission in keys)
                {
                    if (!String.Equals(
                            Str(infoEmission, "_C5B"), selectedName,
                            StringComparison.CurrentCultureIgnoreCase))
                        continue;

                    string infoTitle = Str(infoEmission, "_0u");
                    string infoSubtitle = Str(infoEmission, "_K8b");
                    string infoEpisode = Str(infoEmission, "_Kq");

                    if (!ContainsText(infoTitle, query) &&
                        !ContainsText(infoSubtitle, query) &&
                        !ContainsText(infoEpisode, query))
                        continue;

                    emissionNo++;

                    if (Environment.GetEnvironmentVariable("CAPTVTY_MEDIA_PROBE") == "1")
                    {
                        object backend = null;
                        try { backend = Call(selectedChannel, "_qH"); } catch { }

                        Console.WriteLine("TYPE\tprovider\t" +
                            (provider == null ? "<null>" : provider.GetType().FullName));
                        Console.WriteLine("TYPE\tbackend\t" +
                            (backend == null ? "<null>" : backend.GetType().FullName));
                        Console.WriteLine("TYPE\temission\t" +
                            (infoEmission == null ? "<null>" : infoEmission.GetType().FullName));

                        object probeYtb = null;
                        object probeTzb = null;
                        object probeIlb = null;
                        try
                        {
                            FieldInfo probeIlbField = infoEmission.GetType().GetField(
                                "_ilB",
                                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                            if (probeIlbField != null)
                                probeIlb = probeIlbField.GetValue(infoEmission);
                        }
                        catch { }
                        try { probeYtb = Call(infoEmission, "_YTB"); } catch { }
                        try { probeTzb = Call(infoEmission, "_TZB"); } catch { }
                        Console.WriteLine("TYPE\tYTB\t" +
                            (probeYtb == null ? "<null>" : probeYtb.ToString()));
                        Console.WriteLine("TYPE\tTZB\t" +
                            (probeTzb == null ? "<null>" : probeTzb.ToString()));
                        Console.WriteLine("TYPE\tilB\t" +
                            (probeIlb == null ? "<null>" : probeIlb.ToString()));

                        try
                        {
                            if (backend != null && probeYtb != null)
                            {
                                object taskUrl = Call(backend, "_aEb", probeYtb, false);
                                WaitTask(taskUrl);

                                object resultUrl = taskUrl.GetType()
                                    .GetProperty("Result")
                                    .GetValue(taskUrl, null);

                                Console.WriteLine("DIRECT\t_aEb\t" +
                                    (resultUrl == null ? "<null>" : ToList(resultUrl).Count.ToString()));
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("DIRECT\t_aEb\tERROR\t" +
                                ex.GetType().Name + "\t" + ex.Message);
                        }

                        try
                        {
                            if (backend != null && probeTzb != null)
                            {
                                object taskId = Call(backend, "_01B", probeTzb.ToString(), false);
                                WaitTask(taskId);

                                object resultId = taskId.GetType()
                                    .GetProperty("Result")
                                    .GetValue(taskId, null);

                                Console.WriteLine("DIRECT\t_01B\t" +
                                    (resultId == null ? "<null>" : ToList(resultId).Count.ToString()));
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("DIRECT\t_01B\tERROR\t" +
                                ex.GetType().Name + "\t" + ex.Message);
                        }

                        Console.Out.Flush();
                    }

                    Console.WriteLine(
                        "EMISSION\t" + Clean(selectedName) + "\t" +
                        emissionNo + "\t" +
                        Clean(infoTitle) + "\t" +
                        Clean(infoSubtitle));

                    try
                    {
                        bool probe = Environment.GetEnvironmentVariable("CAPTVTY_MEDIA_PROBE") == "1";

                        if (probe)
                        {
                            List<object> p0 = ToList(Call(infoEmission, "_3x"));
                            Console.WriteLine("PROBE\t" + Clean(selectedName) + "\t" +
                                emissionNo + "\tbefore_BD\t" + p0.Count);
                            Console.Out.Flush();

                            try
                            {
                                object bdTask = Call(infoEmission, "_BD");
                                WaitTask(bdTask);
                                List<object> p1 = ToList(Call(infoEmission, "_3x"));
                                Console.WriteLine("PROBE\t" + Clean(selectedName) + "\t" +
                                    emissionNo + "\tafter_BD\t" + p1.Count);
                            }
                            catch (Exception bdEx)
                            {
                                Console.WriteLine("PROBE\t" + Clean(selectedName) + "\t" +
                                    emissionNo + "\tBD_ERROR\t" +
                                    Clean(bdEx.GetType().Name + ": " + bdEx.Message));
                            }
                            Console.Out.Flush();
                        }

                        if (probe)
                        {
                            try
                            {
                                object detailTask = Call(provider, "_EcA", infoEmission);
                                WaitTask(detailTask);
                                List<object> pd = ToList(Call(infoEmission, "_3x"));
                                Console.WriteLine("PROBE\t" + Clean(selectedName) + "\t" +
                                    emissionNo + "\tafter_EcA\t" + pd.Count);
                            }
                            catch (Exception detailEx)
                            {
                                Console.WriteLine("PROBE\t" + Clean(selectedName) + "\t" +
                                    emissionNo + "\tEcA_ERROR\t" +
                                    Clean(detailEx.GetType().Name + ": " + detailEx.Message));
                            }
                            Console.Out.Flush();
                        }

                        WaitTask(Call(infoEmission, "_z7b", false));

                        List<object> before = ToList(Call(infoEmission, "_3x"));

                        if (probe)
                        {
                            Console.WriteLine("PROBE\t" + Clean(selectedName) + "\t" +
                                emissionNo + "\tafter_z7b\t" + before.Count);
                            Console.Out.Flush();
                        }

                        foreach (object media in before)
                        {
                            try { WaitTask(Call(media, "_bf")); } catch { }
                        }

                        List<object> medias = ToList(Call(infoEmission, "_3x"));

                        if (probe)
                        {
                            Console.WriteLine("PROBE\t" + Clean(selectedName) + "\t" +
                                emissionNo + "\tafter_bf\t" + medias.Count);
                            Console.Out.Flush();
                        }
                        object best = null;
                        try { best = Call(infoEmission, "_YcB"); } catch { }

                        foreach (object media in medias)
                        {
                            int w, h;
                            GetSize(media, out w, out h);

                            Console.WriteLine(
                                "MEDIA\t" + Clean(selectedName) + "\t" +
                                emissionNo + "\t" +
                                w + "\t" + h + "\t" +
                                IntValue(media, "_iUb") + "\t" +
                                (Object.ReferenceEquals(media, best) ? "1" : "0") + "\t" +
                                IntValue(media, "_LO") + "\t" +
                                IntValue(media, "_zzA") + "\t" +
                                (Convert.ToBoolean(Call(media, "_pk")) ? "1" : "0") + "\t" +
                                (Convert.ToBoolean(Call(media, "_au")) ? "1" : "0") + "\t" +
                                (Convert.ToBoolean(Call(media, "_uhA")) ? "1" : "0"));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            "INFOERROR\t" + Clean(selectedName) + "\t" +
                            emissionNo + "\t" + Clean(ex.GetType().Name + ": " + ex.Message));
                    }
                }

                return 0;
            }

            if (count > 1)
            {
                Console.Error.WriteLine(
                    "Recherche ambiguë: " + count + " émissions correspondent");
                return 9;
            }

            return RunGet(a, emission, selectedName, representative);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
