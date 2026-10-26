using System.IO;

namespace Win10WebmCut;

public static class ToolResolver
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, string> Selected = new(StringComparer.Ordinal);
    public static string Resolve(string name)
    {
        if (name is not ("ffmpeg" or "ffprobe")) throw new ArgumentException("Nieznane narzędzie.", nameof(name));
        string executable = name + ".exe";
        string local = Path.Combine(AppContext.BaseDirectory, "tools", executable);
        if (File.Exists(local)) return Remember(name, local);
        foreach (string part in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            string directory = Environment.ExpandEnvironmentVariables(part.Trim().Trim('"'));
            // No implicit current-directory lookup, relative PATH entries or shell aliases.
            if (!Path.IsPathFullyQualified(directory)) continue;
            string path;
            try { path = Path.Combine(directory, executable); }
            catch (ArgumentException) { continue; }
            if (File.Exists(path)) return Remember(name, path);
        }
        throw new FileNotFoundException($"Brak {executable}. Umieść go w tools obok EXE lub w bezwzględnym katalogu PATH. Katalog tools repozytorium nie jest kopiowany automatycznie.");
    }
    private static string Remember(string name, string path)
    {
        string fullPath = Path.GetFullPath(path);
        lock (Gate) Selected[name] = fullPath;
        return fullPath;
    }
    public static string Description()
    {
        lock (Gate)
            return string.Join(Environment.NewLine, new[] { "ffmpeg", "ffprobe" }.Select(name =>
                $"{name}: {(Selected.TryGetValue(name, out string? value) ? value : "jeszcze nie odnaleziono")}"));
    }
}
