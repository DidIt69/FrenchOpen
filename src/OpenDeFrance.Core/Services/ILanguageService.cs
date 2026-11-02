namespace OpenDeFrance.Core.Services;

public interface ILanguageService
{
    string CurrentLanguage { get; }
    event Action? OnLanguageChanged;
    void SetLanguage(string language);
    void ToggleLanguage();
    string this[string key] { get; }
    string Get(string key);
}
