using System.IO;

namespace Win10WebmCut;

/// <summary>Owns only its temporary output. The UI never deletes export files.</summary>
public static class ExportTransaction
{
    private static FileRevision? ReadTarget(string path)
    {
        if (Directory.Exists(path)) throw new IOException("Cel wskazuje katalog, nie plik.");
        if (!File.Exists(path)) return null;
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Eksport nie zastępuje dowiązań/reparse points. Wybierz zwykły plik.");
        return FileRevision.Read(path);
    }

    public static async Task WriteAsync(string input, string destination, bool allowOverwrite,
        Func<string, CancellationToken, Task> writeAndVerify, CancellationToken token)
    {
        string source = Path.GetFullPath(input);
        string target = Path.GetFullPath(destination);
        if (source.Equals(target, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Plik wynikowy musi być inny niż wejściowy.");
        string directory = Path.GetDirectoryName(target)
            ?? throw new ArgumentException("Nieprawidłowy katalog docelowy.");
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory);
        FileRevision? before = ReadTarget(target);
        if (before is not null && before.SameFile(FileRevision.Read(source)))
            throw new ArgumentException("Cel i wejście wskazują ten sam plik (alias/dowiązanie).");
        if (before is not null && !allowOverwrite)
            throw new IOException("Nie udzielono zgody na nadpisanie pliku.");

        // Same directory/volume and real extension: FFmpeg selects the proper muxer.
        string temporary = Path.Combine(directory,
            $".webmcut-{Guid.NewGuid():N}.working{Path.GetExtension(target)}");
        bool ownsTemporary = false;
        try
        {
            token.ThrowIfCancellationRequested();
            using (new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
            ownsTemporary = true;
            await writeAndVerify(temporary, token).ConfigureAwait(false);
            if (!File.Exists(temporary) || new FileInfo(temporary).Length == 0)
                throw new IOException("Eksport nie utworzył niepustego wyniku.");
            if (ReadTarget(target) != before)
                throw new IOException("Plik docelowy zmienił się w trakcie eksportu. Nie został zastąpiony.");

            // Commit boundary: cancellation before here wins. Once commit starts,
            // a successful replace remains a success even if cancellation arrives.
            token.ThrowIfCancellationRequested();
            if (before is null)
                File.Move(temporary, target, overwrite: false);
            else
                File.Replace(temporary, target, destinationBackupFileName: null);
            ownsTemporary = false;
        }
        finally
        {
            if (ownsTemporary)
            {
                try { File.Delete(temporary); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    AppLog.Write("export-cleanup", ex.ToString());
                    // Never delete the destination or mask the original export failure.
                }
            }
        }
    }
}
