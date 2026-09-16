using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using CefSharp.Internals;

namespace CefSharp
{
    public class CefSettingsBase
    {
        readonly CommandLineArgDictionary args =
            new CommandLineArgDictionary();

        public CommandLineArgDictionary CefCommandLineArgs
        {
            get { return args; }
        }

        public string CachePath { get; set; }
        public LogSeverity LogSeverity { get; set; }
        public string Locale { get; set; }
    }

    public class Request : IRequest
    {
        readonly Dictionary<string, string> headers =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        public string Method { get; set; }
        public string Url { get; set; }
        public UrlRequestFlags Flags { get; set; }

        public void SetHeaderByName(
            string name,
            string value,
            bool overwrite)
        {
            if (overwrite || !headers.ContainsKey(name))
                headers[name] = value;
        }

        internal IEnumerable<KeyValuePair<string, string>>
            Headers
        {
            get { return headers; }
        }
    }

    internal class Response : IResponse
    {
        public int StatusCode { get; set; }
    }

    public class UrlRequest : IUrlRequest
    {
        private Response response;

        public IResponse Response
        {
            get { return response; }
        }

        public UrlRequest(
            IRequest request,
            IUrlRequestClient client)
        {
            Request r = request as Request;

            if (r == null)
                throw new ArgumentException(
                    "request is not CefSharp.Request");

            Task.Run(async () =>
            {
                try
                {
                    using (var http = new HttpClient())
                    using (var msg = new HttpRequestMessage(
                        new HttpMethod(
                            String.IsNullOrEmpty(r.Method)
                                ? "GET" : r.Method),
                        r.Url))
                    {
                        foreach (var h in r.Headers)
                            msg.Headers.TryAddWithoutValidation(
                                h.Key, h.Value);

                        using (var resp =
                            await http.SendAsync(msg))
                        {
                            byte[] data =
                                await resp.Content
                                    .ReadAsByteArrayAsync();

                            response = new Response {
                                StatusCode =
                                    (int)resp.StatusCode
                            };

                            using (var ms =
                                new MemoryStream(
                                    data, false))
                            {
                                client.OnDownloadData(
                                    this, ms);
                            }

                            client.OnRequestComplete(this);
                        }
                    }
                }
                catch
                {
                    response = new Response {
                        StatusCode = 599
                    };

                    using (var ms =
                        new MemoryStream(
                            new byte[0], false))
                    {
                        client.OnDownloadData(this, ms);
                    }

                    client.OnRequestComplete(this);
                }
            });
        }
    }

    public static class Cef
    {
        public static TaskFactory IOThreadTaskFactory
        {
            get { return Task.Factory; }
        }

        public static Task<bool> InitializeAsync(
            CefSettingsBase settings,
            bool performDependencyCheck,
            IBrowserProcessHandler browserProcessHandler)
        {
            return Task.FromResult(true);
        }

        public static bool? IsInitialized
        {
            get { return true; }
        }

        public static void EnableWaitForBrowsersToClose()
        {
        }
    }
}
