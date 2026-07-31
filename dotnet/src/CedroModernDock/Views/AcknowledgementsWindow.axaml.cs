using Avalonia.Controls;

namespace CedroModernDock.Views;

public partial class AcknowledgementsWindow : Window
{
    public AcknowledgementsWindow()
    {
        InitializeComponent();
    }

    public static void Open(Window owner)
    {
        var window = new AcknowledgementsWindow();
        window.ShowDialog(owner);
    }
}
