namespace VeganHelper.BLL.Services;

using System.Threading;
using System.Threading.Tasks;
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.Posts;

public interface IPostService
{
    Task<long> CreatePostAsync(CreatePostRequest request, long authorId, CancellationToken cancellationToken = default);
}
