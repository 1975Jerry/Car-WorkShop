using Workshop.Application.Common.Files;

namespace Workshop.Application.Tests;

internal class FakeFileStore : IFileStore
{
    private readonly Dictionary<string, byte[]> _files = new();

    public IReadOnlyDictionary<string, byte[]> Files => _files;

    public async Task<StoredFile> SaveAsync(
        string folder, string originalFileName, Stream content, string contentType,
        CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        var path = $"uploads/{folder}/{Guid.NewGuid():N}-{originalFileName}";
        _files[path] = bytes;
        return new StoredFile(path, bytes.LongLength, contentType);
    }

    public Task<Stream?> OpenReadAsync(string relativePath, CancellationToken ct = default) =>
        Task.FromResult<Stream?>(_files.TryGetValue(relativePath, out var bytes)
            ? new MemoryStream(bytes)
            : null);

    public Task DeleteAsync(string relativePath, CancellationToken ct = default)
    {
        _files.Remove(relativePath);
        return Task.CompletedTask;
    }
}
