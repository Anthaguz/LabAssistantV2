using LabAssistant.Models.Configuration;

namespace LabAssistant.Models.Configuration;

public interface IAppSettingsStore
{
    AppSettings Settings { get; }
    string SettingsPath { get; }
    void LoadOrCreate();
    void Reload();
    void Save();
    void ResetToDefault();
    void SetTemplateFolder(string path);
    void SetLogFolder(string path);
    void SetVmBasePath(string path);
    void SetDifferencingDiskBasePath(string path);
    void SetTheme(AppTheme theme);
}
