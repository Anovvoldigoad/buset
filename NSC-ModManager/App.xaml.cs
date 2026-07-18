using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Diagnostics;

namespace NSC_ModManager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    /// 
    public class RelayCommand : ICommand
    {
        private Action<object> execute;
        private Func<object, bool> canExecute;

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return this.canExecute == null || this.canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            this.execute(parameter);
        }
    }


    public partial class App : Application
    {

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32", SetLastError = true)]
        static extern bool FreeLibrary(IntPtr hModule);

        // wine_get_version hanya ada di ntdll.dll milik Wine. Kalau P/Invoke ini
        // berhasil dipanggil tanpa exception, berarti proses berjalan di atas Wine
        // (termasuk Winlator). Dipakai untuk melewati alur UAC/installer bawaan
        // Windows yang tidak relevan/bisa macet di lingkungan Wine.
        [DllImport("ntdll.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "wine_get_version")]
        private static extern IntPtr wine_get_version();

        private static bool? _isWine;
        private static bool IsRunningOnWine()
        {
            if (_isWine.HasValue) return _isWine.Value;
            try
            {
                wine_get_version();
                _isWine = true;
            } catch
            {
                _isWine = false;
            }
            return _isWine.Value;
        }

        public App()
        {
            InitializeComponent();
        }

        private static bool IsDllPresent(string dllName)
        {
            IntPtr h = IntPtr.Zero;
            try
            {
                h = LoadLibrary(dllName);
                return h != IntPtr.Zero;
            } finally
            {
                if (h != IntPtr.Zero)
                {
                    FreeLibrary(h);
                }
            }
        }

        /// <summary>
        /// Coba jalankan installer vcredist_x86.exe lokal dengan elevasi (UAC).
        /// HANYA dipanggil di Windows asli — lihat App_Startup. Di Wine/Winlator,
        /// prompt UAC (Verb="runas") seringkali gagal diam-diam atau membuat
        /// proses menggantung, jadi jalur ini sengaja dilewati sepenuhnya di sana.
        /// </summary>
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
            } catch
            {
                return false;
            }
        }

        private void App_Startup(object sender, StartupEventArgs e)
        {
            // Di Wine/Winlator, redistributable native lama (vcredist 2010) dan alur
            // UAC-nya bukan tanggung jawab aplikasi ini untuk diinstal/dipaksa — itu
            // urusan setup container Wine (mis. lewat Wine Mono / winetricks, sesuai
            // dokumentasi Winlator sendiri). Memaksa cek+installer di sini hanya
            // menambah risiko macet di startup tanpa manfaat nyata di lingkungan itu.
            if (!IsRunningOnWine())
            {
                const string requiredDll = "MSVCP100.dll";
                const string installerName = "vcredist_x86.exe";

                if (!IsDllPresent(requiredDll))
                {
                    bool installerRun = TryRunBundledInstaller(installerName);

                    if (!IsDllPresent(requiredDll))
                    {
                        string msg;
                        if (installerRun)
                        {
                            msg = "Microsoft Visual C++ 2010 Redistributable was run, but the required library MSVCP100.dll was not found.\n\nInstall the Redistributable manually or place a correct vcredist_x86.exe next to the application.";
                        } else
                        {
                            msg = "Microsoft Visual C++ 2010 Redistributable (x86) is required. Place \"vcredist_x86.exe\" in the application folder or install it manually and restart the program.";
                        }

                        System.Windows.MessageBox.Show(msg, "Missing drivers", MessageBoxButton.OK, MessageBoxImage.Error);
                        Current?.Shutdown();
                        return;
                    }
                }
            }

            // Catatan: pendaftaran NodeNetwork (NNViewRegistrar.RegisterSplat()) sudah
            // dihapus bersamaan dengan paket NodeNetwork/NodeNetworkToolkit karena
            // terbukti tidak dipakai oleh View manapun di proyek ini (dead dependency).
        }
    }
}
