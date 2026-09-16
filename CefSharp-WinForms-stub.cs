using System;
using CefSharp;

namespace CefSharp.WinForms.Host
{
    public class ChromiumHostControlBase
    {
        public event EventHandler IsBrowserInitializedChanged;

        protected void RaiseIsBrowserInitializedChanged()
        {
            var h = IsBrowserInitializedChanged;
            if (h != null)
                h(this, EventArgs.Empty);
        }
    }
}

namespace CefSharp.WinForms
{
    internal sealed class JavascriptObjectRepository :
        IJavascriptObjectRepository
    {
        public event EventHandler<
            CefSharp.Event.JavascriptBindingEventArgs>
            ResolveObject;

        public void Register(
            string name,
            object value,
            bool isAsync,
            BindingOptions options)
        {
        }
    }

    public class CefSettings : CefSettingsBase
    {
        public CefSettings() {}
    }

    public class ChromiumWebBrowser :
        Host.ChromiumHostControlBase,
        IWebBrowser
    {
        readonly IJavascriptObjectRepository repository =
            new JavascriptObjectRepository();

        public ChromiumWebBrowser(
            string address,
            IRequestContext requestContext)
        {
            IsBrowserInitialized = true;
        }

        public IDisplayHandler DisplayHandler
        {
            get;
            set;
        }

        public IJavascriptObjectRepository
            JavascriptObjectRepository
        {
            get { return repository; }
        }

        public bool CanExecuteJavascriptInMainFrame
        {
            get { return true; }
        }

        public bool IsBrowserInitialized
        {
            get;
            private set;
        }

        public bool IsDisposed
        {
            get;
            private set;
        }

        public IKeyboardHandler KeyboardHandler
        {
            get;
            set;
        }

        public IContextMenuHandler MenuHandler
        {
            get;
            set;
        }

        public IRenderProcessMessageHandler
            RenderProcessMessageHandler
        {
            get;
            set;
        }

        public IRequestHandler RequestHandler
        {
            get;
            set;
        }
    }
}
