using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KeePassWinHello
{
    class WinHelloProviderForegroundDecorator : IAuthProvider
    {
        private readonly IAuthProvider _winHelloProvider;
        private readonly UIContextManager _uiContextManager;

        public WinHelloProviderForegroundDecorator(IAuthProvider provider, UIContextManager uiContextManager)
        {
            if (provider == null)
                throw new ArgumentNullException("provider");

            _winHelloProvider = provider;
            _uiContextManager = uiContextManager;
        }

        public AuthCacheType CurrentCacheType
        {
            get
            {
                return _winHelloProvider.CurrentCacheType;
            }
        }

        public void ClaimCurrentCacheType(AuthCacheType newType)
        {
            _winHelloProvider.ClaimCurrentCacheType(newType);
        }

        public byte[] Encrypt(byte[] data)
        {
            return _winHelloProvider.Encrypt(data);
        }

        public byte[] PromptToDecrypt(byte[] data)
        {
            using (var tokenSource = new CancellationTokenSource())
            {
                Win32Window.AllowAllSetForeground();
                var existingDialogs = Win32Window.FindAll(PromptWindowClass);
                Task.Factory.StartNew(() => MakePromptWindowForegroundSafe(existingDialogs), tokenSource.Token);

                try
                {
                    var result = _winHelloProvider.PromptToDecrypt(data);
                    BringKeePassMainWindowToFrontSafe();
                    return result;
                }
                catch
                {
                    tokenSource.Cancel();
                    throw;
                } 
            }
        }

        private void BringKeePassMainWindowToFrontSafe()
        {
            try
            {
                var keePassWindowHandle = _uiContextManager.CurrentContext.ParentWindowHandle; //should not be null
                Win32Window.GetOrNull(keePassWindowHandle).EnsureForeground();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Fail(ex.Message);
            }
        }

#if DEBUG
        // The dummy provider shows a message box with this fixed title
        private const string PromptWindowClass = null;
        private const string PromptWindowTitle = "Windows Security";
#else
        // The title is localized ("Windows-Sicherheit" on German Windows), so match the class only
        // and take the dialog that appears after the prompt was started
        private const string PromptWindowClass = "Credential Dialog Xaml Host";
        private const string PromptWindowTitle = null;
#endif

        private static void MakePromptWindowForegroundSafe(ICollection<IntPtr> existingDialogs)
        {
            try
            {
                var win = Win32Window.FindNew(PromptWindowClass, PromptWindowTitle, existingDialogs, 2000);
                if (win != null)
                    win.EnsureForeground();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Fail(ex.Message);
            }
        }
    }
}
