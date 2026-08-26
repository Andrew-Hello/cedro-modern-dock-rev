namespace CedroModernDock.Infrastructure.Windows.Adapters;

using System.Diagnostics;
using CedroModernDock.Core.Domain;

/// <summary>
/// Launches classic executables and enhanced Windows shell targets. Shell
/// targets allow packaged apps/UWP apps and installed web apps to be pinned by
/// AppUserModelID instead of requiring a directly executable .exe path.
/// </summary>
public class WindowsProgramLauncher : IProgramLauncher
{
    public bool Launch(string executablePath, string label)
    {
        Debug.WriteLine($"{label} Clicked");

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            Debug.WriteLine($"Launch target not defined for: {label}");
            return false;
        }

        try
        {
            if (IsShellNamespaceTarget(executablePath))
                return LaunchShellNamespaceTarget(executablePath, label);

            if (IsShellHandledFile(executablePath))
                return LaunchShellHandledFile(executablePath, label);

            return ExecuteAndHandleElevation(executablePath, label);
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Failed to open: {label}");
            Debug.WriteLine($"Target: {executablePath}");
            Debug.WriteLine($"Error: {e.Message}");
            return false;
        }
    }

    private static bool IsShellNamespaceTarget(string target)
        => target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)
           || target.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase);

    private static bool IsShellHandledFile(string target)
    {
        string extension = Path.GetExtension(target);
        return extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".url", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".appref-ms", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LaunchShellNamespaceTarget(string target, string label)
    {
        // Explorer reliably resolves the AppsFolder namespace and launches the
        // registered app behind an AUMID. ms-settings: and similar protocols can
        // be handed directly to ShellExecute.
        ProcessStartInfo startInfo;
        if (target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
        {
            startInfo = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{target}\"",
                UseShellExecute = true
            };
        }
        else
        {
            startInfo = new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            };
        }

        Process.Start(startInfo);
        Debug.WriteLine($"Executing shell target: {label} -> {target}");
        return true;
    }

    private static bool LaunchShellHandledFile(string path, string label)
    {
        if (!File.Exists(path))
            return false;

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
        Debug.WriteLine($"Executing shell-handled shortcut: {label}");
        return true;
    }

    private bool ExecuteAndHandleElevation(string path, string label)
    {
        var launchCommand = ResolveLaunchCommand(path);
        if (!File.Exists(launchCommand.ExecutablePath))
        {
            Debug.WriteLine($"Executable not found: {launchCommand.ExecutablePath}");
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = launchCommand.ExecutablePath,
                UseShellExecute = false
            };
            foreach (var arg in launchCommand.Arguments)
                startInfo.ArgumentList.Add(arg);

            Process.Start(startInfo);
            Debug.WriteLine($"Executing: {label}");
            return true;
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            if (e.NativeErrorCode == 740)
            {
                Debug.WriteLine("Standard execution failed. Requesting elevation...");
                string command = BuildElevationCommand(launchCommand);
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-Command {command}",
                    UseShellExecute = false
                });
                Debug.WriteLine($"(Elevated) Executing: {label}");
                return true;
            }

            Debug.WriteLine("Error trying to execute program");
            return false;
        }
    }

    /// <summary>
    /// Resolves the actual launch command for an executable, handling
    /// Discord's Squirrel installer pattern (app runs via Update.exe --processStart).
    /// </summary>
    public static LaunchCommand ResolveLaunchCommand(string executablePath)
    {
        string normalizedPath = Path.GetFullPath(executablePath);
        if (IsDiscordExecutable(normalizedPath))
        {
            string? versionDirectory = Path.GetDirectoryName(normalizedPath);
            string? installDirectory = versionDirectory != null
                ? Path.GetDirectoryName(versionDirectory) : null;

            if (installDirectory != null)
            {
                string updateExecutable = Path.Combine(installDirectory, "Update.exe");
                if (File.Exists(updateExecutable))
                {
                    return new LaunchCommand(
                        updateExecutable,
                        new[] { "--processStart", Path.GetFileName(normalizedPath) }
                    );
                }
            }
        }

        return new LaunchCommand(normalizedPath, Array.Empty<string>());
    }

    private static bool IsDiscordExecutable(string executablePath)
    {
        string? fileName = Path.GetFileName(executablePath);
        string? versionDirectory = Path.GetDirectoryName(executablePath);
        string? installDirectory = versionDirectory != null
            ? Path.GetDirectoryName(versionDirectory) : null;

        if (fileName == null || versionDirectory == null || installDirectory == null)
            return false;

        return string.Equals(fileName, "Discord.exe", StringComparison.OrdinalIgnoreCase)
            && Path.GetFileName(versionDirectory)?.StartsWith("app-", StringComparison.OrdinalIgnoreCase) == true
            && string.Equals(Path.GetFileName(installDirectory), "Discord", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildElevationCommand(LaunchCommand launchCommand)
    {
        string escapedFilePath = EscapePowerShellArgument(launchCommand.ExecutablePath);
        if (launchCommand.Arguments.Length == 0)
            return $"Start-Process -FilePath '{escapedFilePath}' -Verb RunAs";

        string escapedArguments = string.Join(", ",
            launchCommand.Arguments.Select(a => $"'{EscapePowerShellArgument(a)}'"));

        return $"Start-Process -FilePath '{escapedFilePath}' -ArgumentList {escapedArguments} -Verb RunAs";
    }

    private static string EscapePowerShellArgument(string argument) => argument.Replace("'", "''");

    public sealed record LaunchCommand(string ExecutablePath, string[] Arguments);
}
