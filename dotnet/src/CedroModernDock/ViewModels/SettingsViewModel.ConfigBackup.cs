namespace CedroModernDock.ViewModels;

public partial class SettingsViewModel
{
    public string ConfigBackupTitle => T("settings.general.configBackup.title");
    public string ConfigBackupHelper => T("settings.general.configBackup.helper");
    public string OpenBackupFolderText => T("settings.general.configBackup.openFolder");
    public string ExportConfigText => T("settings.general.configBackup.export");
    public string ImportConfigText => T("settings.general.configBackup.import");
    public string ExportDialogTitle => T("settings.general.configBackup.exportDialogTitle");
    public string ImportDialogTitle => T("settings.general.configBackup.importDialogTitle");
    public string ConfigFileTypeText => T("settings.general.configBackup.fileType");
    public string ExportSuccessText => T("settings.general.configBackup.exportSuccess");
    public string ExportFailedText => T("settings.general.configBackup.exportFailed");
    public string ImportFailedText => T("settings.general.configBackup.importFailed");
    public string ImportSuccessRestartText => T("settings.general.configBackup.importSuccessRestart");
}
