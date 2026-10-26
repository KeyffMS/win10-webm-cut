using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Win10WebmCut;

public static class AppLog
{
    private const long MaxFileBytes = 1024 * 1024;
    private const int Backups = 3;
    private const int EntryCharacters = 65536;
    private static readonly object Gate = new();
    private static readonly AsyncLocal<string?> Operation = new();
    public static IDisposable BeginOperation(string kind)
    {
        string? previous = Operation.Value;
        Operation.Value = kind + ":" + Guid.NewGuid().ToString("N");
        return new OperationScope(previous);
    }
    private sealed class OperationScope(string? previous) : IDisposable
    {
        public void Dispose() => Operation.Value = previous;
    }
    private static string _tools = "Wersje FFmpeg/FFprobe: jeszcze nie odczytano (preflight przy eksporcie).";
    private static string _writeFailure = "";
    public static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Win10WebmCut", "Logs");
    public static string FilePath => Path.Combine(DirectoryPath, "app.log");

    public static void Write(string operation, string message)
    {
        try
        {
            if (message.Length > EntryCharacters) message = message[..EntryCharacters] + "\n[limit wpisu]";
            string entry = $"{DateTimeOffset.UtcNow:O} [{operation}] [{Operation.Value ?? "app"}] {message}{Environment.NewLine}";
            lock (Gate)
            {
                Directory.CreateDirectory(DirectoryPath);
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length + Encoding.UTF8.GetByteCount(entry) > MaxFileBytes)
                {
                    for (int i = Backups; i >= 1; i--)
                    {
                        string source = i == 1 ? FilePath : FilePath + "." + (i - 1);
                        if (File.Exists(source)) File.Move(source, FilePath + "." + i, overwrite: true);
                    }
                }
                File.AppendAllText(FilePath, entry, new UTF8Encoding(false));
                _writeFailure = "";
            }
        }
        catch (Exception ex)
        {
            // Diagnostics must not cause a second crash. Never recursively log this failure.
            Volatile.Write(ref _writeFailure, ex.Message);
            Debug.WriteLine("Win10WebmCut log unavailable: " + ex.Message);
        }
    }

    public static void SetToolsDescription(string text) { lock (Gate) _tools = text; }
    public static string Summary()
    {
        string version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "nie odczytano";
        string tools;
        lock (Gate) tools = _tools;
        return $"Win10WebmCut {version}\n{RuntimeInformation.FrameworkDescription}\n{RuntimeInformation.OSDescription}\n" +
            $"Proces: {RuntimeInformation.ProcessArchitecture}; system: {RuntimeInformation.OSArchitecture}\n" +
            $"{tools}\n{ToolResolver.Description()}\nLog lokalny: {FilePath}\n" +
            (string.IsNullOrEmpty(_writeFailure) ? "" : "Log niedostępny: " + _writeFailure + "\n") +
            "SDK nie jest wymagane do uruchomienia paczki self-contained. Wersja SDK nie jest odpytywana przez aplikację.";
    }

    public static string ForSharing(string text)
    {
        // Best-effort redaction, not a promise of automatic anonymization.
        string redacted = Regex.Replace(text, @"(?i)(?:[a-z]:[\\/]|\\\\)[^\r\n<>""']+", "[ścieżka usunięta]",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        return "Sprawdź treść przed udostępnieniem — nazwy/fragmenty danych mogą wymagać ręcznego usunięcia.\n\n" + redacted;
    }
}
