using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VeganHelper.API.Configuration;
using VeganHelper.API.Infrastructure.AI;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs.AI;

namespace VeganHelper.UnitTests;

public sealed class DeepSeekChatClientTests
{
    [Fact]
    public async Task CompleteAsync_SendsSystemHistoryAndBearerKey()
    {
        using var http = new HttpClient(new FakeHandler(async (request, ct) =>
        {
            Assert.Equal("https://api.deepseek.com/chat/completions", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("test-key", request.Headers.Authorization.Parameter);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var root = body.RootElement;
            Assert.Equal("deepseek-flash", root.GetProperty("model").GetString());
            Assert.Equal("disabled", root.GetProperty("thinking").GetProperty("type").GetString());
            Assert.Equal("system", root.GetProperty("messages")[0].GetProperty("role").GetString());
            Assert.Contains("likes quick meals", root.GetProperty("messages")[0].GetProperty("content").GetString());
            Assert.Equal("user", root.GetProperty("messages")[1].GetProperty("role").GetString());
            Assert.Equal("assistant", root.GetProperty("messages")[2].GetProperty("role").GetString());
            return JsonResponse("""
                {"model":"deepseek-flash","choices":[{"message":{"role":"assistant","content":"Try tofu and mushrooms."},"finish_reason":"stop"}],
                 "usage":{"prompt_tokens":21,"completion_tokens":9}}
                """);
        }));
        var result = await CreateClient(http).CompleteAsync("User likes quick meals",
            [new AiChatTurn("user", "Lunch idea?"), new AiChatTurn("ai", "Sure")]);
        Assert.Equal("Try tofu and mushrooms.", result.Text);
        Assert.Equal("deepseek-flash", result.Model);
        Assert.Equal(21, result.InputTokens);
        Assert.Equal(9, result.OutputTokens);
    }

    [Fact]
    public async Task CompleteWithPostSearchAsync_CallsOnlyDeclaredToolAndReturnsRealCards()
    {
        var requests = 0;
        using var http = new HttpClient(new FakeHandler(async (request, ct) =>
        {
            requests++;
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var root = body.RootElement;
            if (requests == 1)
            {
                Assert.Equal("search_vegan_posts", root.GetProperty("tools")[0].GetProperty("function").GetProperty("name").GetString());
                return JsonResponse("""
                    {"choices":[{"message":{"role":"assistant","content":null,"tool_calls":[{"id":"call-1","type":"function",
                    "function":{"name":"search_vegan_posts","arguments":"{\"requiredIngredients\":[\"mushroom\"]}"}}]},"finish_reason":"tool_calls"}],
                    "usage":{"prompt_tokens":11,"completion_tokens":3}}
                    """);
            }
            Assert.False(root.TryGetProperty("tools", out _));
            var messages = root.GetProperty("messages");
            Assert.Equal("assistant", messages[messages.GetArrayLength() - 2].GetProperty("role").GetString());
            var toolResult = messages[messages.GetArrayLength() - 1];
            Assert.Equal("call-1", toolResult.GetProperty("tool_call_id").GetString());
            Assert.Contains("Mushroom tofu", toolResult.GetProperty("content").GetString());
            return JsonResponse("""
                {"choices":[{"message":{"role":"assistant","content":"Try this recipe."},"finish_reason":"stop"}],
                 "usage":{"prompt_tokens":16,"completion_tokens":5}}
                """);
        }));
        var result = await CreateClient(http).CompleteWithPostSearchAsync(null,
            [new AiChatTurn("user", "Suggest a mushroom dish")],
            (criteria, _) =>
            {
                Assert.Equal("mushroom", Assert.Single(criteria.RequiredIngredients!));
                return Task.FromResult(new AiPostSearchResult([new AiPostCardDto(42, "Mushroom tofu", null)], false));
            });
        Assert.Equal(2, requests);
        Assert.Equal(42, Assert.Single(result.RecommendedPosts).PostId);
        Assert.Equal(27, result.InputTokens);
        Assert.Equal(8, result.OutputTokens);
    }

    [Fact]
    public async Task CompleteAsync_MapsQuotaFailureWithoutLeakingProviderBody()
    {
        using var http = new HttpClient(new FakeHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            { Content = new StringContent("private provider detail and test-key") })));
        var exception = await Assert.ThrowsAsync<AiChatProviderException>(() =>
            CreateClient(http).CompleteAsync(null, [new AiChatTurn("user", "Hello")]));
        Assert.Equal("provider_quota", exception.Code);
        Assert.Equal(429, exception.StatusCode);
        Assert.DoesNotContain("test-key", exception.ToString());
        Assert.DoesNotContain("private provider detail", exception.ToString());
    }

    [Fact]
    public async Task CompleteAsync_DoesNotCallProviderWhenDisabled()
    {
        using var http = new HttpClient(new FakeHandler((_, _) =>
            throw new Xunit.Sdk.XunitException("disabled provider was called")));
        var client = new DeepSeekChatClient(http,
            Options.Create(new DeepSeekChatOptions { Enabled = false, ApiKey = "test-key" }),
            new AiChatPromptDefinition { Version = "test", SystemInstruction = "System instruction" },
            NullLogger<DeepSeekChatClient>.Instance);
        var exception = await Assert.ThrowsAsync<AiChatProviderException>(() =>
            client.CompleteAsync(null, [new AiChatTurn("user", "Hello")]));
        Assert.Equal("ai_disabled", exception.Code);
    }

    private static DeepSeekChatClient CreateClient(HttpClient http) => new(http,
        Options.Create(new DeepSeekChatOptions { ApiKey = "test-key", Model = "deepseek-flash" }),
        new AiChatPromptDefinition { Version = "test", SystemInstruction = "System instruction" },
        NullLogger<DeepSeekChatClient>.Instance);

    private static HttpResponseMessage JsonResponse(string body) => new(HttpStatusCode.OK)
    { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            send(request, ct);
    }
}
