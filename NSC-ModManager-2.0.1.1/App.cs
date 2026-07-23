namespace NSC_ModManager
{
    /// <summary>
    /// The original WPF App.xaml.cs declared "public partial class App : System.Windows.Application".
    /// Many ViewModel files (~280 call sites) reference App.Current.Dispatcher.Invoke(...)
    /// using this app-specific class name rather than System.Windows.Application.Current
    /// directly. Since App itself was deleted along with App.xaml when WPF was removed,
    /// this restores just enough of it -- an empty subclass of the Application shim -- so
    /// that "App.Current" (a static member inherited from the Application shim) keeps
    /// resolving exactly like it did before.
    /// </summary>
    public class App : System.Windows.Application
    {
    }
}
