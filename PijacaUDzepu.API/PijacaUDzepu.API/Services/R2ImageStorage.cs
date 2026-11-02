using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using PijacaUDzepu.API.Services.Interfaces;

namespace PijacaUDzepu.API.Services;

// Slike u Cloudflare R2 (S3 API). Uključuje se kad su podešene sve R2__* env varijable.
public class R2ImageStorage : IImageStorage
{
    private readonly AmazonS3Client _client;
    private readonly string _bucket;
    private readonly string _publicUrl;
    private readonly ILogger<R2ImageStorage> _logger;

    public R2ImageStorage(IConfiguration config, ILogger<R2ImageStorage> logger)
    {
        _logger = logger;
        _bucket = config["R2:Bucket"]!;
        _publicUrl = config["R2:PublicUrl"]!.TrimEnd('/');

        var s3Config = new AmazonS3Config
        {
            ServiceURL = $"https://{config["R2:AccountId"]}.r2.cloudflarestorage.com",
            AuthenticationRegion = "auto",
            ForcePathStyle = true,
            // R2 ne podržava sve checksum zaglavlja koja novi AWS SDK šalje podrazumevano
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
        };
        _client = new AmazonS3Client(
            new BasicAWSCredentials(config["R2:AccessKeyId"], config["R2:SecretAccessKey"]), s3Config);
    }

    public static bool IsConfigured(IConfiguration config) =>
        new[] { "AccountId", "AccessKeyId", "SecretAccessKey", "Bucket", "PublicUrl" }
            .All(k => !string.IsNullOrWhiteSpace(config[$"R2:{k}"]));

    public async Task<string> Save(byte[] content, string key, string contentType)
    {
        using var stream = new MemoryStream(content);
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = stream,
            ContentType = contentType,
            // Ime fajla ima GUID, pa se sadržaj pod istim URL-om nikad ne menja
            Headers = { CacheControl = "public, max-age=31536000, immutable" },
            DisablePayloadSigning = true
        });
        return $"{_publicUrl}/{key}";
    }

    public async Task Delete(string? imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl) || !imageUrl.StartsWith(_publicUrl + "/"))
            return;

        try
        {
            await _client.DeleteObjectAsync(_bucket, imageUrl[(_publicUrl.Length + 1)..]);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Brisanje slike {Url} iz R2 nije uspelo", imageUrl);
        }
    }
}
