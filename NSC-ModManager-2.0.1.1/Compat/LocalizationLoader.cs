using System;
using System.IO;
using System.Xml.Linq;

namespace NSC_ModManager.Compat
{
    /// <summary>
    /// lang.xaml is a plain WPF ResourceDictionary of &lt;v:String x:Key="..."&gt;value&lt;/v:String&gt;
    /// entries. It's just XML, so we can read it directly with System.Xml.Linq -- no WPF/XAML
    /// parser needed. This populates the System.Windows.Application.Current.Resources shim so
    /// every existing `(string)System.Windows.Application.Current.Resources["m_xxx"]` call in
    /// the ViewModel layer keeps working unchanged.
    /// </summary>
    public static class LocalizationLoader
    {
        public static void LoadInto(System.Windows.ResourceProvider resources, string langXamlPath)
        {
            if (!File.Exists(langXamlPath))
                return;

            try
            {
                var doc = XDocument.Load(langXamlPath);
                XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

                foreach (var el in doc.Descendants())
                {
                    // Matches <v:String x:Key="m_foo">bar</v:String> regardless of which
                    // prefix the "clr-namespace:System;assembly=mscorlib" xmlns was bound to.
                    if (el.Name.LocalName != "String")
                        continue;

                    var keyAttr = el.Attribute(x + "Key");
                    if (keyAttr == null)
                        continue;

                    resources.Values[keyAttr.Value] = el.Value;
                }
            }
            catch
            {
                // If lang.xaml is missing/malformed, resource lookups just fall back to
                // "[[missing:key]]" from ResourceProvider -- not fatal.
            }
        }
    }
}
