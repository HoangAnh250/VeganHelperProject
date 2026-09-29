using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace VeganHelper.BLL.Services.Media;

public class CloudflareR2StorageService : IMediaStorageService
{
    private readonly AmazonS3Client _s3Client;
    private readonly string _bucketName;
    private readonly string _publicUrl;

    public CloudflareR2StorageService(IConfiguration config)
    {
        var r2Config = config.GetSection("CloudflareR2");
        _bucketName = r2Config["BucketName"]!;
        _publicUrl = r2Config["PublicUrl"]!;

        var awsCredentials = new Amazon.Runtime.BasicAWSCredentials(
            r2Config["AccessKey"], 
            r2Config["SecretKey"]);
            
        var awsConfig = new AmazonS3Config
        {
            ServiceURL = r2Config["ServiceUrl"],
        };

        _s3Client = new AmazonS3Client(awsCredentials, awsConfig);
    }

    public async Task<string> UploadFileAsync(IFormFile file, string folder)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("File is empty or null", nameof(file));
        }

        var fileName = $"{folder}/{Guid.NewGuid()}_{file.FileName.Replace(" ", "_")}";

        using var stream = file.OpenReadStream();
        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = fileName,
            InputStream = stream,
            ContentType = file.ContentType,
            DisablePayloadSigning = true
        };

        await _s3Client.PutObjectAsync(request);

        return $"{_publicUrl}/{fileName}";
    }
    
    public async Task DeleteFileAsync(string fileUrl)
    {
        if (string.IsNullOrEmpty(fileUrl) || !fileUrl.StartsWith(_publicUrl))
        {
            return;
        }

        var key = fileUrl.Substring(_publicUrl.Length).TrimStart('/');
        
        var request = new DeleteObjectRequest
        {
            BucketName = _bucketName,
            Key = key
        };

        await _s3Client.DeleteObjectAsync(request);
    }
}
