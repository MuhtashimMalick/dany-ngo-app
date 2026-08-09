using Microsoft.Extensions.Options;
using NgoFund.Application.Abstractions;

namespace NgoFund.Infrastructure.Storage;

/// <summary>Files addressed by a UUID storage key on local disk (or a mounted volume) — never the original filename.</summary>
public class LocalFileStorage(IOptions<FileStorageOptions> options) : IFileStorage
{
    public async Task<string> SaveAsync(Stream content, CancellationToken cancellationToken)
    {
        var storageKey = Guid.CreateVersion7().ToString("N");
        var path = GetPath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var fileStream = File.Create(path);
        await content.CopyToAsync(fileStream, cancellationToken);

        return storageKey;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        Stream stream = File.OpenRead(GetPath(storageKey));
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        File.Delete(GetPath(storageKey));
        return Task.CompletedTask;
    }

    private string GetPath(string storageKey) => Path.Combine(options.Value.RootPath, storageKey);
}
