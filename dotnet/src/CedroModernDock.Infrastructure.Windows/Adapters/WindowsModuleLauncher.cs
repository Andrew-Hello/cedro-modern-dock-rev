namespace CedroModernDock.Infrastructure.Windows.Adapters;

using System.Diagnostics;
using CedroModernDock.Core.Domain;

/// <summary>
/// Direct port of DefaultWindowsModuleLauncher.java. Opens built-in Windows
/// surfaces (This PC, Recycle Bin, Control Panel, Settings) via shell GUIDs/commands.
/// </summary>
public class WindowsModuleLauncher : IWindowsModuleLauncher
{
    public void Launch(string module, string label)
    {
        try
        {
            switch (module)
            {
                case "mypc":
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
                        UseShellExecute = true
                    });
                    break;
                case "trash":
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = "::{645FF040-5081-101B-9F08-00AA002F954E}",
                        UseShellExecute = true
                    });
                    break;
                case "ctrlpnl":
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "control.exe",
                        UseShellExecute = true
                    });
                    break;
                case "pconfig":
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c start ms-settings:",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    break;
            }
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"Failed to launch Windows module '{module}'", e);
        }

        Debug.WriteLine($"{label} Clicked");
    }
}
