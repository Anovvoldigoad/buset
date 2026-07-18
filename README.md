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

**Yang TIDAK disentuh sama sekali** (sesuai permintaan awal): `Model/*.cs`, seluruh logic parsing di `ViewModel/*.cs` (di luar baris dialog/MessageBox yang disebut eksplisit di atas), `XfbinParser.cs`, `BinaryReader.cs`, `Converter/*.cs`, `Controls/KuramaControl.xaml`/`LoadingControl.xaml` (memang tidak pernah pakai ModernWpf), seluruh proyek `XFBIN_LIB`.

## Yang Masih Perlu Perhatian Manual

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
