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
using System.Windows.Markup;
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
            // ================================================================
            // SELF-CHECK: pastikan Invariant Globalization Mode BENAR-BENAR mati
            // ================================================================
            // .NET 8 SDK terbukti (github.com/dotnet/aspnetcore/issues/52319)
            // bisa diam-diam mengaktifkan InvariantGlobalization=true tanpa
            // pernah ditulis eksplisit di manapun. Kita sudah paksa false di 3
            // lapis (csproj, MSBuild property saat build, workflow env var) —
            // tapi kalau ENTAH BAGAIMANA masih ke-trigger dari sumber lain yang
            // belum ketahuan, mending ketahuan di sini dengan pesan jelas,
            // daripada nyasar jadi crash "MS.Internal.FontCache.MajorLanguages"
            // yang membingungkan dan seolah-olah bug WPF acak.
            try
            {
                _ = new System.Globalization.CultureInfo("en");
            } catch (System.Globalization.CultureNotFoundException)
            {
                try
                {
                    System.Windows.MessageBox.Show(
                        "FATAL: Invariant Globalization Mode masih aktif meski sudah " +
                        "dipaksa false di csproj & workflow build. WPF TIDAK BISA jalan " +
                        "sama sekali di mode ini.\n\n" +
                        "Ini bukan bug biasa — kemungkinan ada env var " +
                        "DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 yang masih ter-set di " +
                        "level OS/container Winlator itu sendiri (bukan dari aplikasi " +
                        "ini), atau ada runtimeconfig.json basi dari build lama yang " +
                        "belum ke-replace.\n\n" +
                        "Cek: 1) hapus total folder lama, extract ulang dari zip terbaru. " +
                        "2) pastikan tidak ada env var DOTNET_SYSTEM_GLOBALIZATION_INVARIANT " +
                        "di pengaturan container Winlator.",
                        "NSC Mod Manager - Invariant Globalization masih aktif",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                } catch { }
                Environment.Exit(1);
                return;
            }

            // ================================================================
            // FIX UTAMA crash "Cannot find non-neutral culture related to 'en-us'"
            // ================================================================
            // Root cause sebenarnya: XmlLanguage.GetSpecificCulture() (dipanggil WPF
            // untuk SETIAP Binding lewat BindingExpressionBase.GetCulture(), termasuk
            // binding BitmapImage.UriSource di TitleView) butuh ENUMERASI seluruh locale
            // terpasang (CultureInfo.GetCultures(CultureTypes.SpecificCultures)) untuk
            // mencari padanan spesifik dari default Language="en-US" milik WPF. Enumerasi
            // locale inilah yang tidak lengkap/rusak di Wine — BUKAN sekadar "ICU hilang"
            // seperti dugaan awal.
            //
            // <InvariantGlobalization>true</InvariantGlobalization> (percobaan sebelumnya)
            // JUSTRU MEMPERPARAH: di mode itu CultureInfo.GetCultures() SELALU kosong by
            // design (didokumentasikan resmi Microsoft), jadi error ini malah DIJAMIN selalu
            // terjadi, bukan cuma di Wine. Makanya setelah dipasang, crash-nya identik persis
            // — bukan gagal fix, tapi fix yang salah arah. Sudah dikembalikan di csproj.
            //
            // Fix yang benar: override default Language jadi XmlLanguage.Empty. Berdasarkan
            // dokumentasi resmi GetSpecificCulture(): "If this XmlLanguage is equal to
            // XmlLanguage.Empty then this method will return CultureInfo.InvariantCulture" —
            // artinya kalau Language di-set ke tag kosong, method itu LANGSUNG balikin
            // InvariantCulture TANPA enumerasi locale sama sekali. Titik gagalnya dilewati
            // total, bukan ditambal.
            //
            // WAJIB dipanggil sebelum InitializeComponent() / FrameworkElement pertama dibuat.
            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(string.Empty)));

            // Dipasang PALING AWAL, sebelum InitializeComponent, supaya menangkap
            // exception apa pun sedini mungkin di startup.
            //
            // Log Wine (wine_wfm_*.txt) yang dikirim menunjukkan proses berhenti
            // lewat RaiseFailFastException setelah exception native 0xe0434352
            // (kode SEH standar yang dipakai CoreCLR untuk merepresentasikan
            // managed exception) — itu tanda ADA EXCEPTION C# YANG TIDAK TERTANGANI,
            // tapi Wine cuma mencatat native call-stack (kernelbase.dll/coreclr.dll),
            // BUKAN pesan/stack trace C# aslinya. Tanpa handler ini kita cuma bisa
            // menebak. Dengan handler ini, exception sesungguhnya (Message + tipe +
            // StackTrace C#) akan ditulis ke "crash.log" di folder yang sama dengan
            // exe, supaya bisa didiagnosis dari data asli, bukan tebakan.
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                LogCrash("AppDomain.UnhandledException", e.ExceptionObject as Exception, terminating: e.IsTerminating);

            this.DispatcherUnhandledException += (s, e) =>
            {
                LogCrash("DispatcherUnhandledException", e.Exception, terminating: false);
                // Coba tampilkan pesan ke user & JANGAN langsung crash kalau masih bisa dilanjutkan.
                try
                {
                    System.Windows.MessageBox.Show(
                        "Terjadi error yang tidak tertangani:\n\n" + e.Exception.GetType().FullName + ": " + e.Exception.Message +
                        "\n\nDetail lengkap sudah ditulis ke crash.log di folder aplikasi.",
                        "NSC Mod Manager - Error", MessageBoxButton.OK, MessageBoxImage.Error);
                } catch { /* UI subsystem sendiri mungkin yang bermasalah — abaikan */ }
                e.Handled = true; // Cegah proses langsung mati kalau errornya bisa dilewati.
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                LogCrash("TaskScheduler.UnobservedTaskException", e.Exception, terminating: false);
                e.SetObserved();
            };

            InitializeComponent();
        }

        private static void LogCrash(string source, Exception ex, bool terminating)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                string content =
                    $"===== {DateTime.Now:yyyy-MM-dd HH:mm:ss} | Source: {source} | Terminating: {terminating} =====\n" +
                    (ex?.ToString() ?? "(exception object null / bukan Exception)") +
                    "\n\n";
                File.AppendAllText(logPath, content);
            } catch
            {
                // Kalau nulis log pun gagal, tidak ada lagi yang bisa dilakukan di titik ini.
            }
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
