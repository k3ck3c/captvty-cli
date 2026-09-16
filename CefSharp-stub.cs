using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace CefSharp
{
    [Flags]
    public enum UrlRequestFlags
    {
        None = 0,
        AllowStoredCredentials = 4
    }

    public enum LogSeverity { Default, Verbose, Debug, Info, Warning, Error, Fatal, Disable }
    public enum CefErrorCode { None = 0 }
    [Flags] public enum CefEventFlags { None = 0 }
    public enum CefMenuCommand { NotFound = -1 }
    public enum CefReturnValue { Cancel = 0, Continue = 1, ContinueAsync = 2 }
    public enum CefTerminationStatus { AbnormalTermination = 0 }
    public enum FilterStatus { NeedMoreData = 0, Done = 1, Error = 2 }
    public enum KeyType { RawKeyDown = 0, KeyDown, KeyUp, Char }
    public enum UrlRequestStatus { Unknown = 0, Success, IoPending, Canceled, Failed }
    public enum WindowOpenDisposition { Unknown = 0 }

    public class BindingOptions {}

    public class AddressChangedEventArgs : EventArgs {}
    public class ConsoleMessageEventArgs : EventArgs {}
    public class StatusMessageEventArgs : EventArgs {}
    public class TitleChangedEventArgs : EventArgs {}
    public class JavascriptException {}

    namespace Enums
    {
        public enum CursorType { Pointer = 0 }
    }

    namespace Structs
    {
        public struct Size
        {
            public int Width;
            public int Height;
        }

        public struct CursorInfo {}
    }

    namespace Event
    {
        public class JavascriptBindingEventArgs : EventArgs
        {
            public IJavascriptObjectRepository ObjectRepository { get; set; }
            public string ObjectName { get; set; }
        }
    }

    public interface IBrowser {}
    public interface IFrame {}
    public interface IDomNode {}
    public interface IContextMenuParams {}
    public interface IRunContextMenuCallback {}
    public interface IRequestContext {}
    public interface IRequestCallback {}
    public interface ISslInfo {}
    public interface ISelectClientCertificateCallback {}
    public interface ICookieAccessFilter {}
    public interface IResourceHandler {}
    public interface IAuthCallback {}

    public interface IBrowserProcessHandler {}

    public interface IChromiumWebBrowserBase {}
    public interface IWebBrowser : IChromiumWebBrowserBase {}

    public interface IRequest
    {
        string Url { get; set; }
        void SetHeaderByName(string name, string value, bool overwrite);
    }

    public interface IResponse
    {
        int StatusCode { get; }
    }

    public interface IUrlRequest
    {
        IResponse Response { get; }
    }

    public interface IUrlRequestClient
    {
        bool GetAuthCredentials(
            bool isProxy,
            string host,
            int port,
            string realm,
            string scheme,
            IAuthCallback callback);

        void OnDownloadData(IUrlRequest request, Stream data);
        void OnDownloadProgress(IUrlRequest request, long current, long total);
        void OnRequestComplete(IUrlRequest request);
        void OnUploadProgress(IUrlRequest request, long current, long total);
    }

    public interface IJavascriptObjectRepository
    {
        event EventHandler<Event.JavascriptBindingEventArgs> ResolveObject;

        void Register(
            string name,
            object value,
            bool isAsync,
            BindingOptions options);
    }

    public interface IMenuModel
    {
        bool Clear();
    }

    public interface IContextMenuHandler
    {
        void OnBeforeContextMenu(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame,
            IContextMenuParams parameters,
            IMenuModel model);

        bool OnContextMenuCommand(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame,
            IContextMenuParams parameters,
            CefMenuCommand commandId,
            CefEventFlags eventFlags);

        void OnContextMenuDismissed(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame);

        bool RunContextMenu(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame,
            IContextMenuParams parameters,
            IMenuModel model,
            IRunContextMenuCallback callback);
    }

    public interface IDisplayHandler
    {
        void OnAddressChanged(IWebBrowser chromiumWebBrowser, AddressChangedEventArgs addressChangedArgs);

        bool OnAutoResize(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            Structs.Size newSize);

        bool OnConsoleMessage(
            IWebBrowser chromiumWebBrowser,
            ConsoleMessageEventArgs consoleMessageArgs);

        bool OnCursorChange(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IntPtr cursor,
            Enums.CursorType type,
            Structs.CursorInfo customCursorInfo);

        void OnFaviconUrlChange(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IList<string> urls);

        void OnFullscreenModeChange(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            bool fullscreen);

        void OnLoadingProgressChange(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            double progress);

        void OnStatusMessage(
            IWebBrowser chromiumWebBrowser,
            StatusMessageEventArgs statusMessageArgs);

        void OnTitleChanged(
            IWebBrowser chromiumWebBrowser,
            TitleChangedEventArgs titleChangedArgs);

        bool OnTooltipChanged(
            IWebBrowser chromiumWebBrowser,
            ref string text);
    }

    public interface IKeyboardHandler
    {
        bool OnKeyEvent(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            KeyType type,
            int windowsKeyCode,
            int nativeKeyCode,
            CefEventFlags modifiers,
            bool isSystemKey);

        bool OnPreKeyEvent(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            KeyType type,
            int windowsKeyCode,
            int nativeKeyCode,
            CefEventFlags modifiers,
            bool isSystemKey,
            ref bool isKeyboardShortcut);
    }

    public interface IRenderProcessMessageHandler
    {
        void OnContextCreated(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame);

        void OnContextReleased(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame);

        void OnFocusedNodeChanged(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame,
            IDomNode node);

        void OnUncaughtException(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame,
            JavascriptException exception);
    }

    public interface IRequestHandler
    {
        bool GetAuthCredentials(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            string originUrl,
            bool isProxy,
            string host,
            int port,
            string realm,
            string scheme,
            IAuthCallback callback);

        IResourceRequestHandler GetResourceRequestHandler(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame,
            IRequest request,
            bool isNavigation,
            bool isDownload,
            string requestInitiator,
            ref bool disableDefaultHandling);

        bool OnBeforeBrowse(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame,
            IRequest request,
            bool userGesture,
            bool isRedirect);

        bool OnCertificateError(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            CefErrorCode errorCode,
            string requestUrl,
            ISslInfo sslInfo,
            IRequestCallback callback);

        void OnDocumentAvailableInMainFrame(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser);

        bool OnOpenUrlFromTab(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame,
            string targetUrl,
            WindowOpenDisposition targetDisposition,
            bool userGesture);

        void OnRenderProcessTerminated(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            CefTerminationStatus status,
            int errorCode,
            string errorString);

        void OnRenderViewReady(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser);

        bool OnSelectClientCertificate(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            bool isProxy,
            string host,
            int port,
            X509Certificate2Collection certificates,
            ISelectClientCertificateCallback callback);
    }

    public interface IResourceRequestHandler
    {
        ICookieAccessFilter GetCookieAccessFilter(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame,
            IRequest request);

        IResourceHandler GetResourceHandler(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame,
            IRequest request);

        IResponseFilter GetResourceResponseFilter(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame,
            IRequest request,
            IResponse response);

        CefReturnValue OnBeforeResourceLoad(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame,
            IRequest request,
            IRequestCallback callback);

        bool OnProtocolExecution(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame,
            IRequest request);

        void OnResourceLoadComplete(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame,
            IRequest request,
            IResponse response,
            UrlRequestStatus status,
            long receivedContentLength);

        void OnResourceRedirect(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame,
            IRequest request,
            IResponse response,
            ref string newUrl);

        bool OnResourceResponse(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame,
            IRequest request,
            IResponse response);
    }

    public interface IResponseFilter
    {
        bool InitFilter();

        FilterStatus Filter(
            Stream dataIn,
            out long dataInRead,
            Stream dataOut,
            out long dataOutWritten);
    }

    public static class WebBrowserExtensions
    {
        public static bool LoadHtml(
            IWebBrowser browser,
            string html,
            string url)
        {
            return true;
        }

        public static void ExecuteScriptAsync(
            IChromiumWebBrowserBase browser,
            string script)
        {
        }

        public static void LoadHtml(
            IChromiumWebBrowserBase browser,
            string html,
            bool base64Encode)
        {
        }
    }

    namespace Handler
    {
        public class BrowserProcessHandler : IBrowserProcessHandler
        {
            public BrowserProcessHandler() {}

            protected virtual void OnContextInitialized() {}
        }
    }

    namespace Internals
    {
        public class CommandLineArgDictionary :
            Dictionary<string, string>
        {
        }
    }
}
