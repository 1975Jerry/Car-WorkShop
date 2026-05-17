namespace Workshop.Application.Common.Files;

/// <summary>
/// Storage abstraction for uploaded photos and documents.
/// Returns/accepts a relative path string that is the canonical address of the file
/// inside the chosen store. The Photo/Document entity persists this path as-is.
/// </summary>
public interface IFileStore
{
    /// <summary>
    /// Save a file stream under <paramref name="folder"/> with a stable, collision-free
    /// name derived from <paramref name="originalFileName"/>. Returns the relative path
    /// suitable for persisting (e.g. "uploads/photos/case-abc/2026/03/05/a1b2.jpg").
    /// </summary>
    Task<StoredFile> SaveAsync(
        string folder,
        string originalFileName,
        Stream content,
        string contentType,
        CancellationToken ct = default);

    /// <summary>Open the file for reading. Returns null if the file no longer exists.</summary>
    Task<Stream?> OpenReadAsync(string relativePath, CancellationToken ct = default);

    /// <summary>Delete the file. No-op if it doesn't exist.</summary>
    Task DeleteAsync(string relativePath, CancellationToken ct = default);
}

public record StoredFile(string RelativePath, long SizeBytes, string ContentType);
