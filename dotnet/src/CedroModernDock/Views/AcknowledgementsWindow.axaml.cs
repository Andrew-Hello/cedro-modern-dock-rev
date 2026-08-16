using Avalonia.Controls;
using CedroModernDock.Core.Application;
using CedroModernDock.ViewModels;

namespace CedroModernDock.Views;

public partial class AcknowledgementsWindow : Window
{
    public AcknowledgementsWindow()
    {
        InitializeComponent();
    }

    public static void Open(Window owner, LocalizationService localizationService)
    {
        var window = new AcknowledgementsWindow
        {
            DataContext = new AcknowledgementsViewModel(localizationService)
        };
        window.ShowDialog(owner);
    }
}
