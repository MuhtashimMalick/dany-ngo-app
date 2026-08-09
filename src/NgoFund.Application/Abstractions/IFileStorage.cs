namespace NgoFund.Application.Abstractions;

/// <summary>
/// Where document bytes actually live — a Docker named volume mounted at <c>/app/storage</c> in
/// the API container (see docker-compose.yml). Files are addressed by a UUID storage key, never
/// the original filename, and metadata (owner, content type, hash) lives in the <c>documents</c>
/// table, not here.
/// </summary>
public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}
