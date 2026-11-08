using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace backend.Invoicing;

/// <summary>
/// Bound from the env vars Railway injects for a bucket (BUCKET, ENDPOINT, ACCESS_KEY_ID,
/// SECRET_ACCESS_KEY, REGION).
/// </summary>
public class ObjectStorageOptions
{
    public const string SectionName = "ObjectStorage";

    public string Bucket { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string AccessKeyId { get; set; } = "";
    public string SecretAccessKey { get; set; } = "";
    public string Region { get; set; } = "auto";
}

/// <summary>S3-compatible <see cref="IInvoiceStorage"/> (Railway bucket, MinIO).</summary>
public class S3InvoiceStorage(IAmazonS3 s3, IOptions<ObjectStorageOptions> options) : IInvoiceStorage
{
    private string Bucket => options.Value.Bucket;

    public async Task PutAsync(int number, byte[] pdf, CancellationToken ct = default)
    {
        using var body = new MemoryStream(pdf);
        await s3.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = Bucket,
                Key = InvoiceStorageKey.For(number),
                InputStream = body,
                ContentType = "application/pdf",
            },
            ct
        );
    }

    public async Task<byte[]?> GetAsync(int number, CancellationToken ct = default)
    {
        try
        {
            using var response = await s3.GetObjectAsync(Bucket, InvoiceStorageKey.For(number), ct);
            using var buffer = new MemoryStream();
            await response.ResponseStream.CopyToAsync(buffer, ct);
            return buffer.ToArray();
        }
        catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}

public static class ObjectStorageSetup
{
    public static IServiceCollection AddObjectStorage(this IServiceCollection services, IConfiguration config)
    {
        var section = config.GetSection(ObjectStorageOptions.SectionName);
        services.Configure<ObjectStorageOptions>(section);
        // Railway injects the bucket variables unprefixed; they override the section.
        services.PostConfigure<ObjectStorageOptions>(o =>
        {
            o.Bucket = config["BUCKET"] ?? o.Bucket;
            o.Endpoint = config["ENDPOINT"] ?? o.Endpoint;
            o.AccessKeyId = config["ACCESS_KEY_ID"] ?? o.AccessKeyId;
            o.SecretAccessKey = config["SECRET_ACCESS_KEY"] ?? o.SecretAccessKey;
            o.Region = config["REGION"] ?? o.Region;
        });
        services.AddSingleton<IAmazonS3>(sp =>
        {
            var o = sp.GetRequiredService<IOptions<ObjectStorageOptions>>().Value;
            return new AmazonS3Client(
                new Amazon.Runtime.BasicAWSCredentials(o.AccessKeyId, o.SecretAccessKey),
                new AmazonS3Config
                {
                    ServiceURL = o.Endpoint,
                    AuthenticationRegion = o.Region,
                    ForcePathStyle = true,
                    RequestChecksumCalculation = Amazon.Runtime.RequestChecksumCalculation.WHEN_REQUIRED,
                    ResponseChecksumValidation = Amazon.Runtime.ResponseChecksumValidation.WHEN_REQUIRED,
                }
            );
        });
        services.AddSingleton<IInvoiceStorage, S3InvoiceStorage>();
        return services;
    }

    /// <summary>Development only: creates the (private) bucket when missing.</summary>
    public static async Task EnsureBucketAsync(this IServiceProvider services)
    {
        var s3 = services.GetRequiredService<IAmazonS3>();
        var bucket = services.GetRequiredService<IOptions<ObjectStorageOptions>>().Value.Bucket;
        var buckets = await s3.ListBucketsAsync();
        if (buckets.Buckets is null || !buckets.Buckets.Any(b => b.BucketName == bucket))
        {
            await s3.PutBucketAsync(new PutBucketRequest { BucketName = bucket });
        }
    }
}
