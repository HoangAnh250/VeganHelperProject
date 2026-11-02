using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using VeganHelper.API.Middlewares;

namespace VeganHelper.UnitTests;

public sealed class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenClientAbortsRequest_DoesNotReturnInternalServerError()
    {
        using var cancellation = new CancellationTokenSource();
        var context = new DefaultHttpContext();
        context.RequestAborted = cancellation.Token;
        context.Response.Body = new MemoryStream();
        cancellation.Cancel();

        var middleware = new ExceptionHandlingMiddleware(
            _ => Task.FromException(new OperationCanceledException(cancellation.Token)),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }

    [Fact]
    public async Task InvokeAsync_WhenCancellationIsNotFromClient_StillHandlesItAsServerError()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var middleware = new ExceptionHandlingMiddleware(
            _ => Task.FromException(new OperationCanceledException("Internal operation timed out.")),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.True(context.Response.Body.Length > 0);
    }
}
