using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using VeganHelper.API.Middlewares;

namespace VeganHelper.UnitTests;

public sealed class ChatRequestCancellationTests
{
    [Fact]
    public async Task ClientAbortDoesNotBecomeAnInternalServerError()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = new DefaultHttpContext { RequestAborted = cancellation.Token };
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(
            _ => Task.FromCanceled(cancellation.Token), NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(499, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }
}
