using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

class ProbeTf1Media
{
    const BindingFlags ALL =
        BindingFlags.Instance | BindingFlags.Static |
        BindingFlags.Public | BindingFlags.NonPublic;

    static object Await(object x)
    {
        if (x == null) return null;
        Task t = x as Task;
        if (t == null) return x;
        t.GetAwaiter().GetResult();
        PropertyInfo p = t.GetType().GetProperty("Result");
        return p == null ? null : p.GetValue(t, null);
    }

    static object Call(object o, string name, params object[] args)
    {
        Type t = o as Type;
        object target = t == null ? o : null;
        if (t == null) t = o.GetType();

        MethodInfo[] mm = t.GetMethods(ALL)
            .Where(m => m.Name == name &&
                        m.GetParameters().Length == args.Length).ToArray();

        foreach (MethodInfo m in mm) {
            try { return m.Invoke(target, args); }
            catch (ArgumentException) { }
        }
        throw new MissingMethodException(t.FullName, name);
    }

    static object New0(Type t)
    {
        ConstructorInfo c = t.GetConstructors(ALL)
            .FirstOrDefault(x => x.GetParameters().Length == 0);
        if (c == null)
            throw new InvalidOperationException(
                "Pas de constructeur sans argument pour " + t.FullName);
        return c.Invoke(new object[0]);
    }

    static List<object> ToList(object x)
    {
        List<object> r = new List<object>();
        if (x == null) return r;
        IEnumerable e = x as IEnumerable;
        if (e == null) return r;
        foreach (object o in e) if (o != null) r.Add(o);
        return r;
    }

    static string S(object o, string name)
    {
        try {
            object x = Call(o, name);
            return x == null ? "" : x.ToString();
        } catch { return ""; }
    }

    static int I(object o, string name)
    {
        try { return Convert.ToInt32(Call(o, name)); }
        catch { return 0; }
    }

    static void Main(string[] args)
    {
        try {
            string engine = args.Length > 0
                ? args[0] : "Captvty-cli-engine.exe";
            string channelName = args.Length > 1 ? args[1] : "TF1";
            string needle = args.Length > 2
                ? args[2] : "celui qui venait de dire oui";

            string user = Environment.GetEnvironmentVariable("TF1_USER");
            string pass = Environment.GetEnvironmentVariable("TF1_PASS");
            if (String.IsNullOrWhiteSpace(user) || String.IsNullOrEmpty(pass)) {
                Console.Error.WriteLine("TF1_USER ou TF1_PASS absent.");
                Environment.Exit(2);
            }

            Assembly a = Assembly.LoadFrom(Path.GetFullPath(engine));

            Type scb = a.GetTypes().FirstOrDefault(t => t.Name == "_sCb");
            if (scb == null) throw new Exception("_sCb introuvable");

            MethodInfo me = scb.GetMethods(ALL).Single(m =>
                m.Name == "_Me" &&
                m.GetParameters().Length == 3 &&
                m.GetParameters()[1].ParameterType == typeof(string) &&
                m.GetParameters()[2].ParameterType == typeof(string));

            object helper = New0(me.GetParameters()[0].ParameterType);
            Console.WriteLine("AUTH _Me...");
            object auth = Await(me.Invoke(
                me.IsStatic ? null : New0(scb),
                new object[] { helper, user, pass }));

            if (auth == null) throw new Exception("_Me a retourné null");
            Console.WriteLine("AUTH Gigya : OK");

            /*
             * Important:
             * _Me() fournit la réponse Gigya, mais Captvty obtient ensuite
             * son Bearer via _ZS(), qui met à jour _sCb::_juB.
             *
             * On appelle donc _ZS() : il doit réutiliser les credentials
             * statiques remplis par le chemin d'authentification. Si ce
             * point diffère dans ce build, le diagnostic ci-dessous nous
             * dira exactement où adapter.
             */
            MethodInfo zs = scb.GetMethods(ALL).FirstOrDefault(m =>
                m.Name == "_ZS" && m.GetParameters().Length == 0);
            if (zs == null) throw new Exception("_sCb::_ZS() introuvable");

            Console.WriteLine("AUTH _ZS...");
            object zsr = Await(zs.Invoke(
                zs.IsStatic ? null : New0(scb), new object[0]));
            Console.WriteLine("AUTH _ZS : " +
                (zsr == null ? "<null>" : zsr.ToString()));

            FieldInfo jub = scb.GetField("_juB", ALL);
            object bearer = jub == null ? null : jub.GetValue(null);
            Console.WriteLine("Bearer dans _sCb::_juB : " +
                (bearer == null || String.IsNullOrEmpty(bearer.ToString())
                    ? "NON" : "OUI"));

            if (bearer == null || String.IsNullOrEmpty(bearer.ToString()))
                throw new Exception("Bearer non installé dans _sCb::_juB");

            // Trouver la chaîne via _Uu::_afB.
            Type uu = a.GetTypes().FirstOrDefault(t => t.Name == "_Uu");
            FieldInfo afb = uu == null ? null : uu.GetField("_afB", ALL);
            if (afb == null) throw new Exception("_Uu::_afB introuvable");

            object channel = null;
            foreach (object ch in ToList(afb.GetValue(null))) {
                string n = S(ch, "_b9A");
                if (String.IsNullOrEmpty(n)) n = S(ch, "_GAB");
                if (String.Equals(n, channelName,
                                  StringComparison.OrdinalIgnoreCase)) {
                    channel = ch;
                    break;
                }
            }
            if (channel == null)
                throw new Exception("Chaîne introuvable : " + channelName);

            object provider = Call(channel, "_mbA");
            if (provider == null) throw new Exception("provider null");

            // Charger le catalogue du provider.
            bool done = false;
            EventHandler eh = delegate(object sender, EventArgs e) { done = true; };
            Call(provider, "_XUB", eh);
            Call(provider, "_Wg");

            DateTime limit = DateTime.UtcNow.AddSeconds(60);
            while (!done && DateTime.UtcNow < limit)
                System.Threading.Thread.Sleep(100);
            try { Call(provider, "_chB", eh); } catch { }

            Type a4b = a.GetTypes().FirstOrDefault(t => t.Name == "_A4b");
            FieldInfo ata = a4b == null ? null : a4b.GetField("_AtA", ALL);
            if (ata == null) throw new Exception("_A4b::_AtA introuvable");

            object emission = null;
            foreach (object e in ToList(ata.GetValue(null))) {
                object ec = null;
                try { ec = Call(e, "_C5B"); } catch { }
                if (ec != null && !Object.ReferenceEquals(ec, channel))
                    continue;

                string title = S(e, "_0u");
                string sub = S(e, "_K8b");
                string all = title + " " + sub;
                if (all.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) {
                    emission = e;
                    Console.WriteLine("Emission : " + title +
                        (String.IsNullOrEmpty(sub) ? "" : " - " + sub));
                    break;
                }
            }
            if (emission == null)
                throw new Exception("Emission non trouvée : " + needle);

            Console.WriteLine("avant _z7b : " +
                ToList(Call(emission, "_3x")).Count);

            Await(Call(emission, "_z7b", false));

            List<object> media = ToList(Call(emission, "_3x"));
            Console.WriteLine("après _z7b : " + media.Count);

            foreach (object m in media) {
                try { Await(Call(m, "_bf")); } catch { }
            }

            media = ToList(Call(emission, "_3x"));
            Console.WriteLine("après _bf   : " + media.Count);

            object best = null;
            try { best = Call(emission, "_YcB"); } catch { }

            foreach (object m in media) {
                string size = "";
                try {
                    object sz = Call(m, "_P7b");
                    if (sz != null) size = sz.ToString();
                } catch { }

                int br = I(m, "_iUb");
                Console.WriteLine(
                    (Object.ReferenceEquals(m, best) ? "* " : "  ") +
                    size + "  " + br + " kb/s" +
                    (Object.ReferenceEquals(m, best)
                        ? "  [choix Captvty]" : ""));
            }

            if (media.Count == 0) Environment.Exit(5);
            Console.WriteLine("TF1 MEDIA : OK");
        }
        catch (TargetInvocationException e) {
            Exception x = e.InnerException ?? e;
            Console.Error.WriteLine("ERREUR : " +
                x.GetType().Name + ": " + x.Message);
            Environment.Exit(10);
        }
        catch (Exception e) {
            Console.Error.WriteLine("ERREUR : " +
                e.GetType().Name + ": " + e.Message);
            Environment.Exit(11);
        }
    }
}
