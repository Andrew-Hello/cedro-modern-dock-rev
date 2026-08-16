namespace CedroModernDock.Core.Domain;

/// <summary>Port for resolving program/folder icons from the OS.</summary>
public interface IIconGateway
{
    string? ResolveProgramIcon(string executablePath);
    string? ResolveFolderIcon(string folderPath);

    void CacheProgramIcon(string executablePath);
    void CacheFolderIcon(string folderPath);
}
