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
