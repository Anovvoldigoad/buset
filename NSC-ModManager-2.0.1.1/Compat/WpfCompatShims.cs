// ============================================================================
// WpfCompatShims.cs
//
// This project used to reference WPF (PresentationCore/PresentationFramework/
// WindowsBase). WPF has been removed entirely (UseWPF=false, UseWindowsForms=true)
// because it isn't the goal here -- the goal is running reliably under Wine/Winlator.
//
// The ViewModel layer (~46,000 lines across ~40 files) was written assuming those
// WPF assemblies were present: it uses System.Windows.Visibility, System.Windows.
// Application.Current.Resources[...] for localized strings, System.Windows.
// Application.Current.Dispatcher.Invoke(...) for thread marshaling, System.Windows.
// MessageBox/MessageBoxButton/MessageBoxImage, System.Windows.Data.CollectionViewSource,
// System.Windows.Data.IValueConverter, and System.Windows.Media.Imaging.BitmapImage.
//
// Rather than touching every one of those hundreds of call sites (high risk of
// introducing typos across files this codebase's author -- not us -- never
// intended us to rewrite), we declare drop-in replacements *in the same
// namespaces* the original WPF types lived in. As long as nothing else in the
// project references real PresentationFramework types, these shims satisfy the
// compiler with the same call syntax the ViewModel code already uses.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;

namespace System.Windows
{
    /// <summary>Drop-in replacement for WPF's System.Windows.Visibility.</summary>
    public enum Visibility
    {
        Visible = 0,
        Hidden = 1,
        Collapsed = 2
    }

    public enum MessageBoxButton
    {
        OK = 0,
        OKCancel = 1,
        YesNoCancel = 3,
        YesNo = 4
    }

    public enum MessageBoxImage
    {
        None = 0,
        Hand = 16,
        Stop = 16,
        Error = 16,
        Question = 32,
        Exclamation = 48,
        Warning = 48,
        Asterisk = 64,
        Information = 64
    }

    public enum MessageBoxResult
    {
        None = 0,
        OK = 1,
        Cancel = 2,
        Yes = 6,
        No = 7
    }

    /// <summary>
    /// Drop-in replacement for WPF's System.Windows.MessageBox, backed by the real
    /// System.Windows.Forms.MessageBox now that this is a WinForms app.
    /// </summary>
    public static class MessageBox
    {
        public static MessageBoxResult Show(string text)
            => Map(System.Windows.Forms.MessageBox.Show(text));

        public static MessageBoxResult Show(string text, string caption)
            => Map(System.Windows.Forms.MessageBox.Show(text, caption));

        public static MessageBoxResult Show(string text, string caption, MessageBoxButton button)
            => Map(System.Windows.Forms.MessageBox.Show(text, caption, MapButton(button)));

        public static MessageBoxResult Show(string text, string caption, MessageBoxButton button, MessageBoxImage icon)
            => Map(System.Windows.Forms.MessageBox.Show(text, caption, MapButton(button), MapIcon(icon)));

        private static System.Windows.Forms.MessageBoxButtons MapButton(MessageBoxButton b) => b switch
        {
            MessageBoxButton.OKCancel => System.Windows.Forms.MessageBoxButtons.OKCancel,
            MessageBoxButton.YesNoCancel => System.Windows.Forms.MessageBoxButtons.YesNoCancel,
            MessageBoxButton.YesNo => System.Windows.Forms.MessageBoxButtons.YesNo,
            _ => System.Windows.Forms.MessageBoxButtons.OK
        };

        private static System.Windows.Forms.MessageBoxIcon MapIcon(MessageBoxImage i) => i switch
        {
            MessageBoxImage.Error => System.Windows.Forms.MessageBoxIcon.Error,
            MessageBoxImage.Question => System.Windows.Forms.MessageBoxIcon.Question,
            MessageBoxImage.Warning => System.Windows.Forms.MessageBoxIcon.Warning,
            MessageBoxImage.Information => System.Windows.Forms.MessageBoxIcon.Information,
            _ => System.Windows.Forms.MessageBoxIcon.None
        };

        private static MessageBoxResult Map(System.Windows.Forms.DialogResult r) => r switch
        {
            System.Windows.Forms.DialogResult.OK => MessageBoxResult.OK,
            System.Windows.Forms.DialogResult.Cancel => MessageBoxResult.Cancel,
            System.Windows.Forms.DialogResult.Yes => MessageBoxResult.Yes,
            System.Windows.Forms.DialogResult.No => MessageBoxResult.No,
            _ => MessageBoxResult.None
        };
    }

    /// <summary>
    /// Drop-in replacement for WPF's System.Windows.Application.Current, exposing just
    /// the two members the ViewModel layer actually calls: .Resources["key"] (localized
    /// strings, originally sourced from Resources/Localization/lang.xaml) and
    /// .Dispatcher.Invoke(...)/.CheckAccess() (UI-thread marshaling).
    /// Wired up once from Program.cs at startup.
    /// </summary>
    public sealed class Application
    {
        private static Application _current;
        public static Application Current => _current ??= new Application();

        public ResourceProvider Resources { get; } = new ResourceProvider();
        public DispatcherProvider Dispatcher { get; } = new DispatcherProvider();
    }

    public sealed class ResourceProvider
    {
        internal readonly Dictionary<string, object> Values = new Dictionary<string, object>(StringComparer.Ordinal);

        public object this[object key]
        {
            get
            {
                var k = key?.ToString() ?? string.Empty;
                return Values.TryGetValue(k, out var v) ? v : $"[[missing:{k}]]";
            }
        }
    }

    public sealed class DispatcherProvider
    {
        /// <summary>Set once by Program.cs after the main form exists.</summary>
        public static Action<Action> InvokeAction;
        public static Func<bool> CheckAccessFunc;

        public void Invoke(Action action)
        {
            if (InvokeAction != null) InvokeAction(action);
            else action();
        }

        public bool CheckAccess() => CheckAccessFunc?.Invoke() ?? true;
    }
}

namespace System.Windows.Input
{
    /// <summary>
    /// Drop-in replacement for WPF's System.Windows.Input.CommandManager. WPF used
    /// RequerySuggested to auto-refresh CanExecute on UI events; there's no WPF input
    /// routing anymore, so this is just a plain static event that RelayCommand hooks
    /// into. Call InvalidateRequerySuggested() manually after an action that changes
    /// whether a command can run, if a button's enabled state needs to refresh.
    /// </summary>
    public static class CommandManager
    {
        public static event EventHandler RequerySuggested;

        public static void InvalidateRequerySuggested()
            => RequerySuggested?.Invoke(null, EventArgs.Empty);
    }
}

namespace System.Windows.Data
{
    /// <summary>Drop-in replacement for WPF's System.Windows.Data.IValueConverter.
    /// Nothing calls these anymore (WinForms has no XAML bindings/converters), but the
    /// existing Converter/*.cs classes implement this interface, so it needs to exist
    /// for them to keep compiling.</summary>
    public interface IValueConverter
    {
        object Convert(object value, Type targetType, object parameter, CultureInfo culture);
        object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture);
    }

    public interface IMultiValueConverter
    {
        object Convert(object[] values, Type targetType, object parameter, CultureInfo culture);
        object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture);
    }

    /// <summary>
    /// WPF ViewModels called CollectionViewSource.GetDefaultView(list).MoveCurrentTo(item)
    /// purely to nudge the WPF DataGrid's visual selection to match a "Selected___"
    /// property. The WinForms views now sync selection directly (DataGridView/ListBox
    /// selection + property binding), so this is a safe no-op shim.
    /// </summary>
    public static class CollectionViewSource
    {
        public static NoOpCollectionView GetDefaultView(object collection) => new NoOpCollectionView();
    }

    public sealed class NoOpCollectionView
    {
        public void MoveCurrentTo(object item) { /* no-op: WinForms view handles selection directly */ }
    }
}

namespace System.Windows.Media
{
    /// <summary>Minimal base so BitmapImage below has somewhere to inherit from,
    /// matching the original System.Windows.Media.ImageSource hierarchy.</summary>
    public abstract class ImageSource { }
}

namespace System.Windows.Media.Imaging
{
    public enum BitmapCacheOption
    {
        Default = 0,
        OnLoad = 1,
        OnDemand = 2,
        None = 3
    }

    /// <summary>
    /// WPF's BitmapImage actually derives from BitmapSource. Some ViewModel
    /// properties (e.g. TitleViewModel.ModIconPreview) are typed as BitmapSource
    /// rather than BitmapImage, so we need the same shape here for assignment
    /// compatibility. UnderlyingImage lives here so it's reachable regardless of
    /// which of the two types a call site uses.
    /// </summary>
    public abstract class BitmapSource : System.Windows.Media.ImageSource
    {
        public System.Drawing.Image UnderlyingImage { get; protected set; }
    }

    /// <summary>
    /// Drop-in replacement for WPF's BitmapImage, backed by System.Drawing.Image
    /// (GDI+, available via WinForms). Supports exactly the members this codebase
    /// actually uses: parameterless ctor, Uri ctor, BeginInit/EndInit, UriSource,
    /// StreamSource, CacheOption, Freeze().
    /// </summary>
    public class BitmapImage : BitmapSource, IDisposable
    {
        public Uri UriSource { get; set; }
        public System.IO.Stream StreamSource { get; set; }
        public BitmapCacheOption CacheOption { get; set; }

        public BitmapImage() { }

        public BitmapImage(Uri uri)
        {
            UriSource = uri;
            Load();
        }

        public void BeginInit() { /* no-op, kept for call-site compatibility */ }

        public void EndInit() => Load();

        /// <summary>WPF used this for thread-safety when sharing across threads.
        /// GDI+ images aren't freeze-able the same way; no-op is safe here since the
        /// WinForms view only ever reads UnderlyingImage on the UI thread.</summary>
        public void Freeze() { }

        private void Load()
        {
            try
            {
                if (StreamSource != null)
                {
                    // Copy the stream since the caller may dispose it right after EndInit().
                    var ms = new System.IO.MemoryStream();
                    StreamSource.Position = 0;
                    StreamSource.CopyTo(ms);
                    ms.Position = 0;
                    UnderlyingImage = System.Drawing.Image.FromStream(ms);
                }
                else if (UriSource != null)
                {
                    var path = UriSource.IsAbsoluteUri ? UriSource.LocalPath : UriSource.ToString();
                    if (System.IO.File.Exists(path))
                        UnderlyingImage = System.Drawing.Image.FromFile(path);
                }
            }
            catch
            {
                UnderlyingImage = null;
            }
        }

        public void Dispose() => UnderlyingImage?.Dispose();
    }

    /// <summary>
    /// Drop-in replacement for WPF's BitmapFrame.Create(stream), used once in
    /// CharacterSelectParamModel.cs to load a character icon into a BitmapSource.
    /// </summary>
    public static class BitmapFrame
    {
        public static BitmapImage Create(System.IO.Stream stream)
        {
            var img = new BitmapImage();
            img.StreamSource = stream;
            img.EndInit();
            return img;
        }
    }
}

namespace System.Windows.Media.Animation
{
    /// <summary>
    /// Drop-in replacement for WPF's System.Windows.Media.Animation.RepeatBehavior,
    /// used by TitleViewModel.MeouchEffectRepeat (a cosmetic effect-repeat setting).
    /// Only the two members actually used are implemented: the Forever static
    /// property and the double-count constructor.
    /// </summary>
    public struct RepeatBehavior
    {
        public double Count { get; }
        public bool IsForever { get; }

        public RepeatBehavior(double count)
        {
            Count = count;
            IsForever = false;
        }

        private RepeatBehavior(bool forever)
        {
            Count = 0;
            IsForever = forever;
        }

        public static RepeatBehavior Forever => new RepeatBehavior(true);
    }
}
