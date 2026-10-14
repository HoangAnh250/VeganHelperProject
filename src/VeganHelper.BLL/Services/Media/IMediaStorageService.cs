using Microsoft.AspNetCore.Http;

namespace VeganHelper.BLL.Services.Media;

public interface IMediaStorageService
{
    Task<string> UploadFileAsync(IFormFile file, string folder);
    Task DeleteFileAsync(string fileUrl);
}
