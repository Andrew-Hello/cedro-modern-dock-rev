namespace CedroModernDock.Core.Domain;

/// <summary>Port for launching executable programs (.exe).</summary>
public interface IProgramLauncher
{
    void Launch(string executablePath, string label);
}
