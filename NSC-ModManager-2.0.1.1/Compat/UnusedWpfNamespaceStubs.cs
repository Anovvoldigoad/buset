// ============================================================================
// UnusedWpfNamespaceStubs.cs
//
// Every ViewModel file in this project carries a large, mechanically-inserted
// block of "using System.Windows.X;" statements (Controls, Controls.Primitives,
// Controls.Ribbon, Shell, Shapes, Navigation, Media.Effects, Markup, Ink,
// Documents, Media.TextFormatting, Automation, Forms.Integration) alongside two
// other obviously-bogus lines already present in every file ("using static
// System.Runtime.InteropServices.JavaScript.JSType;" and "using static
// System.Windows.Forms.VisualStyles.VisualStyleElement.Window;"). We verified
// none of these namespaces are used for an actual type anywhere in ViewModel/
// or Model/ -- they're unused imports, not real dependencies.
//
// A "using X;" directive only needs the namespace X to exist somewhere (even
// with zero members) to compile; it does not need to be used. Rather than
// hand-edit the using-block in every one of those files (high effort, no
// behavioral benefit, and real risk of introducing a typo across dozens of
// files), we just declare each namespace here, empty. That's enough for every
// existing "using System.Windows.Controls;" (etc.) line to resolve.
// ============================================================================

namespace System.Windows.Controls { }
namespace System.Windows.Controls.Primitives { }
namespace System.Windows.Controls.Ribbon { }
namespace System.Windows.Shell { }
namespace System.Windows.Shapes { }
namespace System.Windows.Navigation { }
namespace System.Windows.Media.Effects { }
namespace System.Windows.Markup { }
namespace System.Windows.Ink { }
namespace System.Windows.Documents { }
namespace System.Windows.Media.TextFormatting { }
namespace System.Windows.Automation { }
namespace System.Windows.Forms.Integration { }
