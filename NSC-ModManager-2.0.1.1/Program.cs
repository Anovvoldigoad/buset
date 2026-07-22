using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using NSC_ModManager.Compat;
using NSC_ModManager.View;

namespace NSC_ModManager
{
    internal static class Program
    {
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [STAThread]
        private static void Main()
        {
            ApplicationConfiguration_Initialize();

            // ---- same MSVCP100.dll / vcredist check that used to live in App_Startup ----
            const string requiredDll = "MSVCP100.dll";
            const string installerName = "vcredist_x86.exe";

            if (!IsDllPresent(requiredDll))
            {
                bool installerRun = TryRunBundledInstaller(installerName);

                if (!IsDllPresent(requiredDll))
                {
                    string msg = installerRun
                        ? "Microsoft Visual C++ 2010 Redistributable was run, but the required library MSVCP100.dll was not found.\n\nInstall the Redistributable manually or place a correct vcredist_x86.exe next to the application."
                        : "Microsoft Visual C++ 2010 Redistributable (x86) is required. Place \"vcredist_x86.exe\" in the application folder or install it manually and restart the program.";

                    MessageBox.Show(msg, "Missing drivers", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            // ---- wire up the System.Windows.* compat shims the ViewModel layer relies on ----
            string langPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Localization", "lang.xaml");
            LocalizationLoader.LoadInto(System.Windows.Application.Current.Resources, langPath);

            var mainForm = new TitleForm();

            System.Windows.DispatcherProvider.InvokeAction = action =>
            {
                if (mainForm.InvokeRequired)
                    mainForm.Invoke(action);
                else
                    action();
            };
            System.Windows.DispatcherProvider.CheckAccessFunc = () => !mainForm.InvokeRequired;

            Application.Run(mainForm);
        }

        private static void ApplicationConfiguration_Initialize()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
        }

        private static bool IsDllPresent(string dllName)
        {
            IntPtr h = IntPtr.Zero;
            try
            {
                h = LoadLibrary(dllName);
                return h != IntPtr.Zero;
            }
            finally
            {
                if (h != IntPtr.Zero) FreeLibrary(h);
            }
        }

        private static bool TryRunBundledInstaller(string installerFileName, int timeoutMilliseconds = 120000)
        {
            string exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, installerFileName);
            if (!File.Exists(exePath)) return false;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "/q /norestart",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true
                };

                using (var p = Process.Start(psi))
                {
                    if (p == null) return false;
                    bool exited = p.WaitForExit(timeoutMilliseconds);
                    if (!exited)
                    {
                        try { p.Kill(); } catch { }
                        return false;
                    }
                    return p.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
