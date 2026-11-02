using PijacaUDzepu.API.Services.Interfaces;

namespace PijacaUDzepu.API.Services;

// Slike na disku servera (wwwroot/Resources/Images). Koristi se lokalno, kad R2 nije podešen;
// na Render-u se disk briše pri svakom deploy-u, pa tamo treba R2.
public class LocalImageStorage : IImageStorage
{
    private const string UrlPrefix = "/Resources/Images/";

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<LocalImageStorage> _logger;

    public LocalImageStorage(IWebHostEnvironment env, ILogger<LocalImageStorage> logger)
    {
        _env = env;
        _logger = logger;
    }

    public async Task<string> Save(byte[] content, string key, string contentType)
    {
        // Lokalno su sve slike u jednom folderu, kao i ranije
        var fileName = Path.GetFileName(key);
        var filePath = Path.Combine(_env.WebRootPath, "Resources", "Images", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllBytesAsync(filePath, content);
        return UrlPrefix + fileName;
    }

    public Task Delete(string? imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl) || !imageUrl.StartsWith(UrlPrefix))
            return Task.CompletedTask;

        try
        {
            var path = Path.Combine(_env.WebRootPath, "Resources", "Images", Path.GetFileName(imageUrl));
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Brisanje slike {Url} nije uspelo", imageUrl);
        }
        return Task.CompletedTask;
    }
}
