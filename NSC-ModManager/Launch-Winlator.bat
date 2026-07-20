@echo off
REM ============================================================
REM  Launcher untuk NSC Mod Manager di Winlator (FEXCore/WowBox64)
REM ============================================================
REM  .NET 6+ mengaktifkan proteksi memori W^X (Write-XOR-Execute)
REM  untuk kode hasil JIT secara default. Ini bikin CoreCLR bolak-
REM  balik mprotect() RW <-> RX tiap kali JIT compile method baru.
REM  Di bawah emulator CPU (FEXCore/Box64/QEMU-user), toggle ini
REM  sering deadlock dengan translation cache si emulator -> proses
REM  kelihatan jalan (CPU terpakai) tapi window tidak pernah muncul.
REM  Ini bukan bug NSC Mod Manager, murni interaksi CoreCLR x emulator.
REM
REM  Set DOTNET_EnableWriteXorExecute=0 mematikan proteksi itu
REM  (memori JIT jadi RWX sekaligus, sedikit kurang aman tapi jauh
REM  lebih kompatibel di lingkungan emulasi seperti ini).
REM ============================================================

set DOTNET_EnableWriteXorExecute=0

REM ------------------------------------------------------------------
REM  DOTNET_SYSTEM_GLOBALIZATION_INVARIANT SENGAJA DIHAPUS DARI SINI.
REM  Riwayat: sempat dicoba sebagai fix untuk "Cannot find non-neutral
REM  culture" (lihat README poin #19), lalu csproj-nya sudah direvert
REM  karena teorinya salah arah — TAPI baris env var di launcher ini
REM  kelewat kehapus, jadi Invariant Globalization tetap aktif diam-diam
REM  di semua percobaan berikutnya lewat launcher ini.
REM
REM  Ternyata WPF SAMA SEKALI TIDAK BISA jalan di Invariant Globalization
REM  Mode: subsistem font/text-rendering-nya sendiri (MS.Internal.FontCache.
REM  MajorLanguages) hardcode manggil `new CultureInfo("en")` di static
REM  constructor-nya, yang PASTI throw CultureNotFoundException di mode
REM  invariant (cuma invariant culture "" yang valid di mode itu). Crash ini
REM  muncul begitu ada TextBlock APAPUN yang di-render — jadi fatal total,
REM  bukan soal satu Binding tertentu lagi.
REM  JANGAN AKTIFKAN LAGI ENV VAR INI UNTUK APLIKASI WPF.
REM ------------------------------------------------------------------

REM Cadangan tambahan (aktifkan/hapus REM salah satu baris di bawah
REM kalau langkah di atas SAJA belum cukup menyelesaikan hang):
REM set DOTNET_TieredPGO=0
REM set DOTNET_TC_QuickJitForLoops=0
REM set DOTNET_gcServer=0

cd /d "%~dp0"
start "" "NSC_ModManager.exe"
