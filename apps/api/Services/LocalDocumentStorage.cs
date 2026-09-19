using Microsoft.Extensions.Options;
using SubClear.Api.Options;

namespace SubClear.Api.Services;

public interface IDocumentStorage
{
    Task<string> SaveAsync(
        Guid tenantId,
        Guid documentId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default);
}

public sealed class LocalDocumentStorage : IDocumentStorage
{
    private readonly IWebHostEnvironment _environment;
    private readonly StorageOptions _options;

    public LocalDocumentStorage(IWebHostEnvironment environment, IOptions<StorageOptions> options)
    {
        _environment = environment;
        _options = options.Value;
    }

    public async Task<string> SaveAsync(
        Guid tenantId,
        Guid documentId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var root = _options.RootPath;
        if (!Path.IsPathRooted(root))
        {
            root = Path.Combine(_environment.ContentRootPath, root);
        }

        var folder = Path.Combine(root, tenantId.ToString("N"), documentId.ToString("N"));
        Directory.CreateDirectory(folder);
        var safe = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = "upload.bin";
        }

        var path = Path.Combine(folder, safe);
        await using var file = File.Create(path);
        await content.CopyToAsync(file, cancellationToken);
        return Path.GetRelativePath(root, path).Replace('\\', '/');
    }
}
