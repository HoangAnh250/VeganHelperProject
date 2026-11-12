using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VeganHelper.BLL.Contracts;
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task JwtSessionLookup_OnlyHandlesCancellationOfTheHttpRequest(bool abortRequest)
    {
        using var cancellation = new CancellationTokenSource();
        var accounts = new Mock<IAccountAccessService>(MockBehavior.Strict);
        accounts.Setup(service => service.GetSessionRoleAsync(42, 3, cancellation.Token))
            .Returns(() =>
            {
                if (abortRequest)
                {
                    cancellation.Cancel();
                    return Task.FromCanceled<string?>(cancellation.Token);
                }

                return Task.FromException<string?>(new OperationCanceledException());
            });
        using var services = new ServiceCollection().AddSingleton(accounts.Object).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services, RequestAborted = cancellation.Token };
        var scheme = new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));
        var context = new TokenValidatedContext(http, scheme, new JwtBearerOptions())
        {
            Principal = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "42"), new Claim("token_version", "3")], "Bearer"))
        };
        var validate = typeof(ExceptionHandlingMiddleware).Assembly
            .GetType("VeganHelper.API.Authorization.AccountTokenValidation", throwOnError: true)!
            .GetMethod("ValidateAsync", BindingFlags.Static | BindingFlags.NonPublic)!
            .CreateDelegate<Func<TokenValidatedContext, Task>>();

        if (abortRequest)
        {
            await validate(context);
            Assert.True(context.Result?.None);
        }
        else
        {
            await Assert.ThrowsAsync<OperationCanceledException>(() => validate(context));
            Assert.Null(context.Result);
        }
        accounts.VerifyAll();
    }
}
