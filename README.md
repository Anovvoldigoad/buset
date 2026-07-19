# NSC Mod Manager — Repo Migrasi UI Winlator

Repo ini berisi dua proyek sebagai sibling folder (dibutuhkan oleh `ProjectReference` di `NSC-ModManager.csproj`):

```
/NSC-ModManager/   <- aplikasi utama (WPF, net8.0-windows, x64)
/XFBIN_LIB/        <- library parser XFBIN (net7.0, TIDAK disentuh sama sekali)
/.github/workflows/build.yml  <- CI build otomatis (Release, win-x64)
```

## Status Perubahan (sudah dieksekusi)

| # | Perubahan | File terdampak |
|---|---|---|
| 1 | `ModernWpf.MessageBox.Show` → `System.Windows.MessageBox.Show` | 41+ file `ViewModel/*.cs`, `Properties/Program.cs` |
| 2 | Hapus `ui:ThemeResources`/`ui:XamlControlsResources`, ganti tema kustom `BasicDarkTheme.xaml` (solid color, tanpa DWM/Acrylic) | `App.xaml`, `Resources/Styles/BasicDarkTheme.xaml` (baru) |
| 3 | Hapus `using ModernWpf/NodeNetwork/Xceed.Wpf.AvalonDock.Themes`, hapus `NNViewRegistrar.RegisterSplat()` (dead dependency), tambah deteksi Wine (`wine_get_version`) untuk skip alur UAC/vcredist installer di Wine | `App.xaml.cs` |
| 4 | `ui:ToggleSwitch` → `CheckBox` standar WPF | `View/TitleView.xaml`, `View/TitleView.xaml.cs` |
| 5 | Hapus namespace `xmlns:ui`/`xmlns:xcdg` yang sudah tidak dipakai elemen apa pun | `View/TitleView.xaml`, `View/CharacterRosterEditorView.xaml`, `View/CharacterRosterEditorNS4View.xaml` |
| 6 | `CommonOpenFileDialog` (WindowsAPICodePack, COM `IFileOpenDialog`) → `Microsoft.Win32.OpenFolderDialog` (native .NET 8+) | `ViewModel/TitleViewModel.cs`, `MessageInfoS4ViewModel.cs`, `NUS3BANKViewModel.cs`, `MessageInfoViewModel.cs` |
| 7 | Hapus `using Microsoft.WindowsAPICodePack.*` yang sudah mati total | 7 file `ViewModel/*.cs` |
| 8 | `CheckGitHubNewerVersion()` dibungkus try-catch + guard list kosong (sebelumnya fire-and-forget tanpa pengaman → risiko crash/hang startup) | `ViewModel/TitleViewModel.cs` |
| 9 | Hapus dead code class `YaCpkTool` (pemanggil in-process `CpkMaker.dll`, native x86-only, proprietary CRI Middleware, terkonfirmasi tak dipanggil di manapun) | `Properties/Program.cs` |
| 10 | Hapus referensi `CpkMaker.dll` dari csproj + file fisiknya | `NSC-ModManager.csproj` |
| 11 | Font "CC2 RocknRoll Latin DB" (Comicraft, **komersial**, tidak pernah benar-benar ter-bundle) → **Bangers** (Google Fonts, SIL OFL 1.1, bebas dibundel) | `Resources/Fonts/Bangers-Regular.ttf` (baru), `App.xaml` |
| 12 | `TargetFramework`: `net10.0-windows10.0.26100.0` → `net8.0-windows` (LTS) | `NSC-ModManager.csproj` |
| 13 | `PlatformTarget`: `x86` → `x64`, `Prefer32Bit`: `false` | `NSC-ModManager.csproj` |
| 14 | Hapus paket NuGet: `ModernWpfUI`, `ModernWpf.MessageBox`, `WindowsAPICodePack-Shell`, `NodeNetwork`, `NodeNetworkToolkit` | `NSC-ModManager.csproj` |
| 15 | **[Fix build #1]** Kembalikan `DynamicData` sebagai direct `PackageReference` (v9.4.1) — dipakai langsung di 18 file `Model`/`ViewModel`/`View`, kemarin cuma nebeng transitif lewat `NodeNetworkToolkit` dan sempat ikut hilang | `NSC-ModManager.csproj` |
| 16 | **[Fix build #2]** `XFBIN_LIB-main.zip` yang di-upload ternyata versi LEBIH LAMA dari yang dibutuhkan `NUS3BANKViewModel.cs` (bug pra-existing, bukan dari migrasi UI). Tambah `XFBIN_READER.FindChunks`/`FoundChunk` dan `XFBIN_WRITER.RepackXfbinData`/`ChangeChunkNameAndPath` — semua dibangun ulang memakai rantai resolusi index & rumus penghitungan ukuran section yang **sudah ada dan terbukti benar** di `ReadXFBIN`/`ReadDirectoryXFBIN`/`RepackXFBIN`, bukan logika baru yang ditebak | `XFBIN_LIB/XFBIN_READER.cs`, `XFBIN_LIB/XFBIN_WRITER.cs` |
| 17 | **[Fix runtime]** App hang di Winlator (Arm64EC+FEXCore): proses jalan (CPU terpakai) tapi window tidak pernah muncul. Diagnosis: CoreCLR .NET 6+ pakai proteksi memori JIT **W^X** (mprotect RW↔RX bolak-balik tiap JIT compile), yang deadlock dengan translation cache FEXCore/Box64/QEMU-user — pola ini sudah dikenal luas di komunitas emulasi. Fix: `DOTNET_EnableWriteXorExecute=0` lewat launcher | `NSC-ModManager/Launch-Winlator.bat` (baru) |
| 18 | **[Instrumentasi crash]** Setelah fix #17, app lolos dari hang tapi tetap **crash** (log Wine menunjukkan `RaiseFailFastException` setelah exception native `0xe0434352` — kode SEH standar CoreCLR untuk managed exception). Wine cuma mencatat native call-stack (`kernelbase.dll`/`coreclr.dll`), **bukan** pesan/stack trace C# aslinya, jadi akar masalahnya belum bisa dipastikan dari log itu saja. Ditambahkan global exception handler (`AppDomain.UnhandledException`, `DispatcherUnhandledException`, `TaskScheduler.UnobservedTaskException`) yang menulis exception C# yang sesungguhnya (tipe + pesan + stack trace) ke **`crash.log`** di folder yang sama dengan exe | `App.xaml.cs` |
| 19 | **[Fix #19a, ternyata salah arah]** ~~`<InvariantGlobalization>true</InvariantGlobalization>`~~ — SUDAH DIBATALKAN. Ternyata mode ini membuat `CultureInfo.GetCultures()` **selalu kosong by design** (didokumentasikan resmi Microsoft), sehingga error "cannot find non-neutral culture" malah **dijamin selalu terjadi**, bukan cuma di Wine. Terbukti dari `crash.log` kedua yang identik persis dengan yang pertama meski fix sudah di-build | `NSC-ModManager.csproj` (dikembalikan) |
| 20 | **[Fix #19b, root cause sebenarnya]** `XmlLanguage.GetSpecificCulture()` (dipanggil WPF untuk **setiap** `Binding`, termasuk `BitmapImage.UriSource` di `TitleView`) butuh **enumerasi locale terpasang** (`CultureInfo.GetCultures(CultureTypes.SpecificCultures)`) untuk mencari padanan spesifik dari default `Language="en-US"` bawaan WPF — enumerasi inilah yang rusak/kosong di Wine. Fix: `FrameworkElement.LanguageProperty.OverrideMetadata(...)` di awal `App()` men-set `Language` global ke `XmlLanguage.Empty`, yang menurut dokumentasi resmi membuat `GetSpecificCulture()` langsung return `CultureInfo.InvariantCulture` **tanpa enumerasi locale sama sekali** — titik gagalnya dilewati total, berlaku otomatis untuk semua Window (`TitleView`, `CharacterRosterEditorView`, dst) karena di-override di level tipe, bukan per-XAML | `App.xaml.cs` |

### ⚠️ Langkah selanjutnya (penting)

Build ini **belum memperbaiki crash-nya** — cuma menambah "kamera dashcam" supaya crash berikutnya kelihatan penyebab aslinya di C#, bukan cuma native trace Wine yang buta soal detail managed exception. Setelah rebuild & coba jalankan lagi lewat `Launch-Winlator.bat`, kalau masih crash, cek file **`crash.log`** di folder yang sama dengan `NSC_ModManager.exe` (kemungkinan besar akan muncul karena `AppDomain.UnhandledException` terpasang paling awal) dan kirim isinya — dari situ baru bisa dipastikan fix yang tepat, bukan tebak-tebakan lagi.

### Cara pakai launcher di Winlator

**Jangan jalankan `NSC_ModManager.exe` langsung** dari Winlator. Arahkan shortcut/launcher Winlator ke **`Launch-Winlator.bat`** (ada di folder hasil publish yang sama dengan exe-nya) — file ini men-set `DOTNET_EnableWriteXorExecute=0` dulu sebelum menjalankan exe utama, supaya CoreCLR tidak deadlock lawan FEXCore.

Kalau setelah pakai launcher ini masih hang, coba aktifkan (hapus `REM` di depan baris) satu per satu secara bertahap di `Launch-Winlator.bat`, dari yang paling ringan dampaknya dulu:
1. `DOTNET_TieredPGO=0` — matikan profiling JIT tier kedua (thread background tambahan)
2. `DOTNET_TC_QuickJitForLoops=0` — matikan quick-JIT khusus loop
3. `DOTNET_gcServer=0` — paksa Workstation GC (lebih ringan, kurang multi-thread) kalau ternyata Server GC aktif

Kalau semua kombinasi di atas tetap hang, kemungkinan besar bukan lagi soal konfigurasi runtime, tapi FEXCore versi yang dipakai memang belum kompatibel penuh dengan CoreCLR .NET 8 — coba bandingkan dengan container **Arm64EC + WowBox64** sebagai alternatif (beda jalur emulasi, kadang hasilnya beda untuk kasus JIT-heavy seperti .NET).


**Yang TIDAK disentuh sama sekali** (sesuai permintaan awal): `Model/*.cs`, seluruh logic parsing di `ViewModel/*.cs` (di luar baris dialog/MessageBox yang disebut eksplisit di atas), `XfbinParser.cs`, `BinaryReader.cs`, `Converter/*.cs`, `Controls/KuramaControl.xaml`/`LoadingControl.xaml` (memang tidak pernah pakai ModernWpf), seluruh proyek `XFBIN_LIB`.

## 21+. Tindak lanjut dari AUDIT_LOG.md sesi lain (versi 32-bit/WinNative)

User membagikan `AUDIT_LOG.md` dari sesi debugging **terpisah** (repo lain, target 32-bit + fork WinNative, keputusan struktural berbeda seperti `XFBIN_LIB` sebagai prebuilt `.dll` alih-alih source). **Tidak semua temuan di sana ditransfer mentah-mentah** — cuma yang murni soal WPF-vs-Wine (independen dari x86/x64) DAN terbukti nyata ada di repo x64 kita ini (dicek satu-satu lewat `grep`, bukan asumsi):

| # | Temuan (dari sesi lain) | Terbukti ada di repo kita? | Tindakan |
|---|---|---|---|
| A | Dead code `<Style TargetType="ToggleButton" BasedOn="{StaticResource NoChromeButton}"/>` — bug laten (`Button` bukan base type valid utk `ToggleButton`) | ✅ Ya, baris 74 `TitleView.xaml` | **Dihapus** |
| B | `ComboBox`/`Menu`/`MenuItem` cuma di-restyle `Setter` biasa → chrome native (`Microsoft.Windows.Themes.*`) masih aktif → `UriFormatException`/`NullReferenceException` di Wine | ✅ Ya — `ComboBox` kita cuma Setter, `TitleView.xaml` punya `<Menu>` beneran dgn submenu (12 kemunculan) | **`ComboBox`, `ComboBoxItem`, `ScrollBar`, `Menu`, `MenuItem` diberi `ControlTemplate` PENUH** di `BasicDarkTheme.xaml` (nol chrome native). `ToggleButton` internal `ComboBox` diberi `Style="{x:Null}"` eksplisit (pertahanan lapis 2 terhadap item A) |
| C | `DataGridColumnHeader` chrome native | ❌ Tidak relevan — **repo kita nol pemakaian `DataGrid`** di 3 View manapun | Tidak ada tindakan |
| D | `Process.Start(UseShellExecute=true)` + `WaitForExit()` tanpa timeout → deadlock permanen kalau child process gagal spawn lewat shell di Wine | ✅ Ya — persis pola sama di `RunRepackProcess`, `RunExtractProcess`, `RemoveZoneIdentifier` (PowerShell fallback), auto-launch `NSUNSC.exe`/`NSUNS4.exe` | Class baru **`ProcessLauncher`** (`TitleViewModel.cs`): semua exe eksternal dijalankan lewat wrapper `cmd.exe`+`.bat` sementara (bukan `Process.Start` langsung), `UseShellExecute=false`, **timeout 3 menit** utk yang ditunggu (`RunAndWait`), timeout 30 detik utk yang fire-and-forget (`RunDetached`). PowerShell `Unblock-File` juga dikasih timeout 5 detik |
| E | `ZipFile.ExtractToDirectory` butuh native shim `System.IO.Compression.Native` yang **terbukti gagal load** di Wine (`status=c0000135`) → bisa jadi `ACCESS_VIOLATION` native, bukan exception .NET yang bisa ditangkap | ✅ Ya — 3 titik persis (`TitleViewModel.cs` 2×, `TitleView.xaml.cs` 1×) | Diganti **`RepackHelper.ExtractZipSafe()`** pakai **SharpZipLib** (`ICSharpCode.SharpZipLib.Zip.FastZip`, sudah jadi `PackageReference`, pure-managed, tidak butuh native shim) |
| F | `MessageInfoS4ViewModel.cs` di namespace `NSC_Toolbox.ViewModel` (beda dari `NSC_ModManager.ViewModel` file lain) — jejak rename project lama | ✅ Ya, dikonfirmasi | **Belum diubah** (tidak menyebabkan error build/runtime apa pun di repo kita saat ini karena kita tidak punya `DialogHelper`/cross-reference dari file itu) — cukup dicatat di sini sebagai jebakan kalau nanti ada yang menambah pemakaian baru lintas-file dari `MessageInfoS4ViewModel.cs` |
| G | Font `CombineUriWithFaceIndex`/`GlyphTypeface` `UriFormatException` yang TIDAK bisa diperbaiki lewat ganti string `FontFamily` apa pun (5 percobaan gagal semua di sesi lain), butuh self-healing fallback permanen | ⚠️ Belum terjadi di repo kita (font crash kita kemarin soal `XmlLanguage`/culture, sudah fix beda, lihat item #20 di atas) | **Tidak diimplementasikan preemptif** — beda kelas bug dari yang sudah kita alami, dan self-healing yang menelan exception diam-diam berisiko menutupi bug baru yang justru perlu kelihatan jelas di tahap debugging kita sekarang. Kalau nanti muncul crash `CombineUriWithFaceIndex`/`PhysicalFontFamily` di `crash.log` kita, baru diadopsi (polanya sudah didokumentasikan di sini kalau perlu) |
| H | Hipotesis SIMD/`box64`-Reflection.Emit (env var `DOTNET_EnableHWIntrinsic=0`) | ⚠️ **Belum terverifikasi bahkan di sesi asalnya sendiri**, dan itu spesifik `box64`, sedangkan container kita **FEXCore** | **Tidak diadopsi** — spekulatif & mungkin tidak relevan untuk FEXCore. Dicatat sebagai kandidat kalau nanti ada crash trace yang nunjuk ke `System.Reflection.Emit.*` di `crash.log`/log Wine kita |

**Prinsip yang dipakai:** cuma transfer temuan yang (1) independen dari keputusan arsitektur/struktur repo yang berbeda, DAN (2) diverifikasi ulang lewat `grep` langsung ke repo kita — bukan asumsi "kalau di sana kena, di sini juga pasti kena".

## Yang Masih Perlu Perhatian Manual

0. **Fitur rename/save-as NUS3BANK wajib diuji manual.** `ChangeChunkNameAndPath`/`RepackXfbinData`/`FindChunks` di `XFBIN_LIB` saya tulis ulang karena hilang dari source yang di-upload (lihat item #16 di tabel atas) — logikanya saya bangun dari pola index-resolution & rumus ukuran section yang sudah dipakai di tempat lain pada library yang sama (bukan tebakan buta), tapi ini tetap kode baru yang belum pernah dijalankan sungguhan. Sebelum dipakai untuk mod asli, tes dulu: buka file `.nus3bank.xfbin`, ganti nama lewat "Save As", pastikan file hasilnya kebuka lagi dengan benar (baik oleh Mod Manager maupun oleh game).

1. **`YACpkTool.exe` tetap x86.** Ini adalah wrapper untuk `CpkMaker.dll` — proprietary CRI Middleware (build 2013, mixed-mode native, tidak ada source code untuk direkompilasi ke x64). Dipanggil sebagai **proses anak terpisah** (`Process.Start`), jadi tidak memblokir migrasi aplikasi utama ke x64. **Wajib diuji langsung di Winlator**: pastikan fitur build/repack CPK (`RunRepackProcess`/`RunExtractProcess`) benar-benar bisa menjalankan exe x86 ini di konfigurasi Winlator Anda (FEXCore atau WowBox64 untuk 32-bit — bukan Box86). Jika ternyata gagal, satu-satunya jalan keluar adalah menulis ulang logic pembuatan `.cpk` dengan implementasi managed (AnyCPU) — ini di luar scope migrasi UI karena menyentuh *core logic* mod-building, bukan sekadar UI.
2. **Build belum divalidasi end-to-end** (sandbox analisis ini tidak punya akses `dotnet` SDK/NuGet). Validasi sesungguhnya terjadi di GitHub Actions (`.github/workflows/build.yml`) — cek tab **Actions** setelah push pertama.
3. **`BasicDarkTheme.xaml`** adalah tema pengganti minimal (Button/ComboBox/CheckBox/TextBox). Kontrol lain masih pakai default WPF classic — poles lanjutan warna/spacing bisa dilakukan bertahap tanpa menyentuh logic apa pun.
4. **Font Bangers** dipilih karena gaya bold/comic yang paling dekat dengan font asli dan lisensinya (SIL OFL) mengizinkan bundling bebas. Kalau ingin gaya lain yang masih gratis, alternatif lain: `Luckiest Guy`, `Anton`, `Bungee` (semua di Google Fonts, semua OFL).
5. `Extended.Wpf.Toolkit` (Xceed) sengaja **dipertahankan** — dipakai wajar untuk `IntegerUpDown`/`UShortUpDown`/`ColorPicker`, bukan sumber masalah DWM/Fluent.

## Cara Build Manual (Windows, opsional)

```
dotnet restore NSC-ModManager/NSC-ModManager.csproj
dotnet publish NSC-ModManager/NSC-ModManager.csproj -c Release -r win-x64 -p:Platform=x64 --self-contained true -o out
```

## CI/CD

`.github/workflows/build.yml` otomatis build tiap push ke `main`/PR, dan bikin GitHub Release ber-zip kalau push tag `v*.*.*` (mis. `v2.1.0`).
