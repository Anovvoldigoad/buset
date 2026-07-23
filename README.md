# NSC Mod Manager — WinForms rewrite (Winlator-focused)

This is `NSC-ModManager-2.0.1.1` with **WPF removed entirely** and replaced with
WinForms, plus its `XFBIN_LIB` project dependency, wired up to build
automatically via GitHub Actions.

## Why

The original WPF build crashed instantly under Wine/Winlator because ModernWpfUI
called into WinRT APIs Wine doesn't implement. Rather than patch just that one
spot, this goes further: no WPF, no ModernWpf, no WinRT-adjacent anything —
WinForms only, which has a much longer track record running cleanly under Wine.

## What changed

- `UseWPF` → `UseWindowsForms` in the `.csproj`; ModernWpfUI, Extended.Wpf.Toolkit,
  NodeNetwork/NodeNetworkToolkit, and WpfAnimatedGif package references removed
  (none of them were load-bearing for anything WinForms still needs).
- `App.xaml`/`App.xaml.cs` → `Program.cs` (plain WinForms entry point, same
  vcredist/MSVCP100.dll check as before).
- `View/TitleView.xaml(.cs)` → `View/TitleForm.cs`: same `TitleViewModel`, same
  commands, same properties — new plain-WinForms layout (menu + tabs for
  Mods/Options/Credits instead of animated overlay panels; a few cosmetic
  simplifications are called out in code comments, e.g. the screenshot
  carousel swaps instantly instead of cross-fading).
- `View/CharacterRosterEditorView.xaml(.cs)` and the NS4 variant →
  `CharacterRosterEditorForm.cs` / `CharacterRosterEditorNS4Form.cs`: same
  `CharacterRosterEditorViewModel`/`NS4ViewModel` and the same slot-reordering
  logic (`ReplaceSlots`, `ConvertCostumeToSlot`, etc.), with drag-and-drop
  reimplemented using WinForms' `ListBox.IndexFromPoint` instead of WPF's
  `VisualTreeHelper` hit-testing.
- `Controls/LoadingControl` and `Controls/KuramaControl` → simplified WinForms
  UserControls (spinner instead of storyboard pulse; static mascot image
  instead of tail-wiggle animation). Purely decorative, no functionality lost.
- **`Compat/WpfCompatShims.cs` + `Compat/UnusedWpfNamespaceStubs.cs`**: the
  ~46,000-line ViewModel layer was written assuming WPF assemblies were
  present (`System.Windows.Visibility`, `Application.Current.Resources[...]`
  for localized strings, `Application.Current.Dispatcher.Invoke(...)`,
  `MessageBox`/`MessageBoxButton`/`MessageBoxImage`, `CollectionViewSource`,
  `IValueConverter`, `BitmapImage`/`BitmapSource`/`BitmapFrame`, plus a large
  block of unused `using System.Windows.*` imports present in nearly every
  file). Instead of hand-editing hundreds of call sites across dozens of
  files, these two files declare drop-in replacements in the *same*
  namespaces, so that existing ViewModel/Model code compiles unchanged.
  `Compat/LocalizationLoader.cs` parses the existing `lang.xaml` (plain XML)
  into that shim at startup so every `Resources["m_xxx"]` lookup still works.
- Fully-dead code after the rewrite (only ever reachable from removed XAML)
  was deleted rather than shimmed: the whole `Converter/` folder (16
  `IValueConverter` implementations), `AnimatedVisibility.cs`, and
  `Resources/Styles/LightStyle.xaml`.

## How to build

1. Create a new empty repository on GitHub.
2. Push the contents of this folder to it, keeping the top-level layout exactly as-is:
   ```
   .github/workflows/build.yml
   NSC-ModManager-2.0.1.1/
   XFBIN_LIB/
   ```
   ```bash
   cd this-folder
   git init
   git add .
   git commit -m "Initial commit: WinForms rewrite of NSC Mod Manager"
   git branch -M main
   git remote add origin <your-repo-url>
   git push -u origin main
   ```
3. Pushing to `main` triggers the workflow automatically. You can also trigger it
   manually from the repo's **Actions** tab ("Run workflow").
4. When the run finishes, open it and download the **NSC-ModManager-build** artifact
   under "Artifacts" — that's the built app folder (`NSC_ModManager.exe` and everything
   it needs, framework-dependent, x86).
5. Copy that folder into your Winlator container and run `NSC_ModManager.exe`.

## Known limitation

This was patched and verified by reading the source and grepping for stray WPF
references, not by compiling it locally — WPF's XAML build step only runs on
Windows and this environment has neither Windows nor a usable .NET SDK/network
to install one. WinForms itself doesn't have that restriction (no XAML build
step), so the GitHub Actions run above is the first real compiler check this
code gets. If it reports errors, paste them back and they'll get fixed.

## Update log

- **First CI run**: 2 errors (missing `DynamicData` package reference lost as
  a side effect of removing `NodeNetworkToolkit`; a missed `RepeatBehavior`
  usage in `TitleViewModel.cs`). Both fixed.
- **Second CI run**: 754 raw error lines, 3 real root causes: (1) the new
  `Program.cs` entry-point class shadowed a pre-existing
  `NSC_ModManager.Properties.Program` utility class ~40 ViewModels depend on
  for static data lists — renamed to `EntryPoint`; (2) ~280 call sites use
  `App.Current.Dispatcher...` (the WPF-era `App : Application` subclass
  specifically, not just `Application.Current`) — restored as a minimal `App.cs`;
  (3) **`XFBIN_LIB` itself was an older/mismatched version** — `NUS3BANKViewModel.cs`
  calls `XFBIN_READER.FindChunks`/`XFBIN_WRITER.RepackXfbinData`/
  `ChangeChunkNameAndPath`, none of which existed in the uploaded `XFBIN_LIB-main`
  source. These were added to `XFBIN_LIB` directly (not shimmed) based on the
  existing page/chunk/chunk-table data model and the byte-writing logic already
  present in `RepackXFBIN`. See the `nsc-modmanager-winlator` skill for full detail.

See the `nsc-modmanager-winlator` skill for the full diagnosis history (the
original ModernWpf/WinRT crash) leading up to this rewrite.
