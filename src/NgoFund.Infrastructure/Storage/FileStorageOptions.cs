namespace NgoFund.Infrastructure.Storage;

/// <summary>Bound from the "Storage" configuration section. Defaults to a local "storage" folder
/// for host-run dev; docker-compose.yml overrides this to "/app/storage" (the named volume mount).</summary>
public class FileStorageOptions
{
    public string RootPath { get; set; } = "storage";
}
