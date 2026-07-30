namespace CedroModernDock.Infrastructure.Windows.Adapters;

using System.Diagnostics;
using CedroModernDock.Core.Domain;

/// <summary>
/// Direct port of DefaultProgramLauncher.java. Launches executables,
/// with special handling for Discord (Squirrel-installed apps) and
/// automatic elevation (UAC) when the standard launch fails with error=740.
/// </summary>
public class WindowsProgramLauncher : IProgramLauncher
{
    public void Launch(string executablePath, string label)
    {
        Debug.WriteLine($"{label} Clicked");

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            Debug.WriteLine($"Executable path not defined for: {label}");
            return;
        }

        try
        {
            ExecuteAndHandleElevation(executablePath, label);
        }
        catch (Exception e) when (e is not System.ComponentModel.Win32Exception)
        {
            Debug.WriteLine($"Failed to open: {label}");
            Debug.WriteLine($"Path: {executablePath}");
            Debug.WriteLine($"Error: {e.Message}");
        }
    }

    private void ExecuteAndHandleElevation(string path, string label)
    {
        var launchCommand = ResolveLaunchCommand(path);
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
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            // error=740 means elevation is required (ERROR_ELEVATION_REQUIRED)
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
            }
            else
            {
                Debug.WriteLine("Error trying to execute program");
                throw;
            }
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

    /// <summary>Direct port of the Java LaunchCommand record.</summary>
    public sealed record LaunchCommand(string ExecutablePath, string[] Arguments);
}
