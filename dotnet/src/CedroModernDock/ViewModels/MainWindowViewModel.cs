using CommunityToolkit.Mvvm.ComponentModel;

namespace CedroModernDock.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    /// <summary>
    /// Status line for the Phase 0 spike — shows the dock's HWND, shell-hook
    /// registration result, and Win+D defense events.
    /// </summary>
    [ObservableProperty]
    private string _statusText = "Initializing…";
}
