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

REM Fix crash "Cannot find non-neutral culture related to 'en-us'" (ICU tidak
REM tersedia/rusak di Wine). Sudah di-bake permanen lewat <InvariantGlobalization>
REM di csproj, baris ini cuma jaring pengaman kalau exe kebetulan dijalankan
REM tanpa lewat launcher ini.
set DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

REM Cadangan tambahan (aktifkan/hapus REM salah satu baris di bawah
REM kalau langkah di atas SAJA belum cukup menyelesaikan hang):
REM set DOTNET_TieredPGO=0
REM set DOTNET_TC_QuickJitForLoops=0
REM set DOTNET_gcServer=0

cd /d "%~dp0"
start "" "NSC_ModManager.exe"
