namespace VeganHelper.BLL.Services;

using System.Threading;
using System.Threading.Tasks;
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.Posts;

public interface IPostService
{
    Task<long> CreatePostAsync(CreatePostRequest request, long authorId, CancellationToken cancellationToken = default);
    Task<PagedResult<PostFeedItemDto>> GetFeedAsync(GetFeedRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<PostFeedItemDto>> SearchPostsAsync(SearchPostsRequest request, CancellationToken cancellationToken = default);
    Task<PostDetailDto> GetPostDetailAsync(long postId, CancellationToken cancellationToken = default);

    Task<PagedResult<MyPostItemDto>> GetMyPostsAsync(long authorId, GetMyPostsRequest request, CancellationToken cancellationToken = default);
    Task DeletePostAsync(long postId, long authorId, CancellationToken cancellationToken = default);

    Task UpdatePostAsync(long postId, UpdatePostRequest request, long authorId, CancellationToken cancellationToken = default);

}
