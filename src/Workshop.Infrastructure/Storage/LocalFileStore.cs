using Microsoft.Extensions.Configuration;
using Workshop.Application.Common.Files;

namespace Workshop.Infrastructure.Storage;

/// <summary>
/// Local filesystem implementation of <see cref="IFileStore"/>.
/// Writes under {root}/{folder}/{yyyy}/{MM}/{hash}{ext}. The relative path returned
/// is prefixed with the public prefix (default "uploads") so static-file middleware
/// in Workshop.Web can serve it directly.
///
/// Configuration:
///   FileStore:Root          absolute path; required (Workshop.Web sets it to
///                           &lt;ContentRoot&gt;/wwwroot/uploads at startup).
///   FileStore:PublicPrefix  public URL prefix; default "uploads".
/// </summary>
public class LocalFileStore : IFileStore
{
    private readonly string _root;
    private readonly string _publicPrefix;

    public LocalFileStore(IConfiguration config)
    {
        var rootOverride = config["FileStore:Root"];
        if (!string.IsNullOrWhiteSpace(rootOverride))
            _root = rootOverride;
        else
            // Fallback for hosts that didn't set the config (e.g. tests creating the
            // service directly). Use AppContext.BaseDirectory so we don't blow up.
            _root = Path.Combine(AppContext.BaseDirectory, "uploads");

        _publicPrefix = config["FileStore:PublicPrefix"] ?? "uploads";
        Directory.CreateDirectory(_root);
    }

    public async Task<StoredFile> SaveAsync(
        string folder, string originalFileName, Stream content, string contentType,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(folder)) folder = "misc";
        folder = SanitizeFolder(folder);
        var ext = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".bin";

        var now = DateTime.UtcNow;
        var relDir = Path.Combine(folder, now.ToString("yyyy"), now.ToString("MM"));
        var absDir = Path.Combine(_root, relDir);
        Directory.CreateDirectory(absDir);

        var fileName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var absPath = Path.Combine(absDir, fileName);

        long size;
        using (var fs = File.Create(absPath))
        {
            await content.CopyToAsync(fs, ct);
            size = fs.Length;
        }

        var relPath = Path.Combine(_publicPrefix, relDir, fileName).Replace('\\', '/');
        return new StoredFile(relPath, size, contentType);
    }

    public Task<Stream?> OpenReadAsync(string relativePath, CancellationToken ct = default)
    {
        var abs = ResolveAbsolute(relativePath);
        if (abs is null || !File.Exists(abs)) return Task.FromResult<Stream?>(null);
        return Task.FromResult<Stream?>(File.OpenRead(abs));
    }

    public Task DeleteAsync(string relativePath, CancellationToken ct = default)
    {
        var abs = ResolveAbsolute(relativePath);
        if (abs is not null && File.Exists(abs)) File.Delete(abs);
        return Task.CompletedTask;
    }

    private string? ResolveAbsolute(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return null;

        // Strip leading "uploads/" (public prefix) if present so we don't double it.
        var trimmed = relativePath.Replace('\\', '/');
        if (trimmed.StartsWith(_publicPrefix + "/", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[(_publicPrefix.Length + 1)..];

        var combined = Path.GetFullPath(Path.Combine(_root, trimmed));
        // Path traversal guard.
        var rootFull = Path.GetFullPath(_root);
        return combined.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase) ? combined : null;
    }

    private static string SanitizeFolder(string folder)
    {
        var bad = Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\' }).ToArray();
        var parts = folder.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return string.Join('/', parts.Select(p => new string(p.Where(c => !bad.Contains(c)).ToArray())));
    }
}
