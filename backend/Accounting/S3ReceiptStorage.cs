using Amazon.S3;
using Amazon.S3.Model;
using backend.Invoicing;
using Microsoft.Extensions.Options;

namespace backend.Accounting;

/// <summary>S3-compatible <see cref="IReceiptStorage"/>, in the same bucket as the Invoice PDFs.</summary>
public class S3ReceiptStorage(IAmazonS3 s3, IOptions<ObjectStorageOptions> options) : IReceiptStorage
{
    private string Bucket => options.Value.Bucket;

    public async Task PutAsync(int id, byte[] pdf, CancellationToken ct = default)
    {
        using var body = new MemoryStream(pdf);
        await s3.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = Bucket,
                Key = ReceiptStorageKey.For(id),
                InputStream = body,
                ContentType = "application/pdf",
            },
            ct
        );
    }

    public async Task<byte[]?> GetAsync(int id, CancellationToken ct = default)
    {
        try
        {
            using var response = await s3.GetObjectAsync(Bucket, ReceiptStorageKey.For(id), ct);
            using var buffer = new MemoryStream();
            await response.ResponseStream.CopyToAsync(buffer, ct);
            return buffer.ToArray();
        }
        catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task DeleteAsync(int id, CancellationToken ct = default) =>
        s3.DeleteObjectAsync(Bucket, ReceiptStorageKey.For(id), ct);
}
