using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VeganHelper.API.Configuration;
using VeganHelper.API.Infrastructure.AI;
using VeganHelper.BLL.DTOs.AI;

namespace VeganHelper.UnitTests;

public sealed class GeminiChatClientTests
{
    [Fact]
    public async Task CompleteWithPostSearchAsync_ExecutesOnlyDeclaredToolAndReturnsRealCards()
    {
        var requests = 0;
        using var http = new HttpClient(new FakeHandler(async (request, cancellationToken) =>
        {
            requests++;
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            if (requests == 1)
            {
                Assert.Equal("search_vegan_posts", body.RootElement.GetProperty("tools")[0]
                    .GetProperty("functionDeclarations")[0].GetProperty("name").GetString());
                return JsonResponse("""
                    {"candidates":[{"content":{"role":"model","parts":[{"functionCall":
                    {"name":"search_vegan_posts","id":"call-1","args":{"requiredIngredients":["mushroom"]}},
                    "thoughtSignature":"signature"}]}}],
                    "usageMetadata":{"promptTokenCount":11,"candidatesTokenCount":3}}
                    """);
            }

            var contents = body.RootElement.GetProperty("contents");
            Assert.Equal("signature", contents[contents.GetArrayLength() - 2].GetProperty("parts")[0].GetProperty("thoughtSignature").GetString());
            var toolResult = contents[contents.GetArrayLength() - 1].GetProperty("parts")[0].GetProperty("functionResponse");
            Assert.Equal("call-1", toolResult.GetProperty("id").GetString());
            Assert.Equal(42, toolResult.GetProperty("response").GetProperty("posts")[0].GetProperty("postId").GetInt64());
            Assert.False(body.RootElement.TryGetProperty("tools", out _));
            return JsonResponse("""
                {"candidates":[{"content":{"parts":[{"text":"Try this recipe."}]}}],"modelVersion":"test",
                 "usageMetadata":{"promptTokenCount":16,"candidatesTokenCount":5}}
                """);
        }));
        var client = CreateClient(http);
        var result = await client.CompleteWithPostSearchAsync(null,
            [new AiChatTurn("user", "Suggest a mushroom dish")],
            (criteria, _) =>
            {
                Assert.Equal("mushroom", Assert.Single(criteria.RequiredIngredients!));
                return Task.FromResult(new AiPostSearchResult([new AiPostCardDto(42, "Mushroom tofu", null)], false));
            });

        Assert.Equal(2, requests);
        Assert.Equal("Try this recipe.", result.Text);
        Assert.Equal(42, Assert.Single(result.RecommendedPosts).PostId);
        Assert.Equal(27, result.InputTokens);
        Assert.Equal(8, result.OutputTokens);
    }

    [Fact]
    public async Task CompleteAsync_SendsSystemInstructionHistoryAndApiKey()
    {
        using var http = new HttpClient(new FakeHandler(async (request, cancellationToken) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(
                "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash-lite:generateContent",
                request.RequestUri!.AbsoluteUri);
            Assert.Equal("test-key", Assert.Single(request.Headers.GetValues("x-goog-api-key")));

            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;
            var systemInstruction = root.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString();
            Assert.StartsWith("System instruction", systemInstruction);
            Assert.Contains("Người dùng thích món nhanh.", systemInstruction);
            Assert.Equal(256, root.GetProperty("generationConfig").GetProperty("maxOutputTokens").GetInt32());
            Assert.Equal("user", root.GetProperty("contents")[0].GetProperty("role").GetString());
            Assert.Equal("Gợi ý món chay", root.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString());
            Assert.Equal("model", root.GetProperty("contents")[1].GetProperty("role").GetString());

            return JsonResponse("""
                {
                  "candidates": [{ "content": { "parts": [{ "text": "Bạn có thể thử bún chay." }] } }],
                  "modelVersion": "gemini-2.5-flash-lite",
                  "usageMetadata": { "promptTokenCount": 21, "candidatesTokenCount": 9 }
                }
                """);
        }));

        var client = CreateClient(http);
        var result = await client.CompleteAsync(
            "Người dùng thích món nhanh.",
            [new AiChatTurn("user", "Gợi ý món chay"), new AiChatTurn("ai", "Bạn muốn món nào?")]);

        Assert.Equal("Bạn có thể thử bún chay.", result.Text);
        Assert.Equal("gemini-2.5-flash-lite", result.Model);
        Assert.Equal(21, result.InputTokens);
        Assert.Equal(9, result.OutputTokens);
    }

    [Fact]
    public async Task CompleteAsync_MapsQuotaFailureWithoutExposingProviderBody()
    {
        using var http = new HttpClient(new FakeHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("private provider detail and test-key")
            })));

        var exception = await Assert.ThrowsAsync<VeganHelper.BLL.Contracts.GeminiChatException>(() =>
            CreateClient(http).CompleteAsync(null, [new AiChatTurn("user", "Xin chào")]));

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
        var client = new GeminiChatClient(
            http,
            Options.Create(new GeminiOptions { Enabled = false, ApiKey = "test-key" }),
            new AiChatPromptDefinition { Version = "test", SystemInstruction = "System instruction" },
            NullLogger<GeminiChatClient>.Instance);

        var exception = await Assert.ThrowsAsync<VeganHelper.BLL.Contracts.GeminiChatException>(() =>
            client.CompleteAsync(null, [new AiChatTurn("user", "Xin chào")]));

        Assert.Equal("ai_disabled", exception.Code);
    }

    private static GeminiChatClient CreateClient(HttpClient http) => new(
        http,
        Options.Create(new GeminiOptions
        {
            Enabled = true,
            ApiKey = "test-key",
            Model = "gemini-2.5-flash-lite",
            MaxOutputTokens = 256,
            MaxInputCharacters = 30_000,
            TimeoutSeconds = 10
        }),
        new AiChatPromptDefinition { Version = "test", SystemInstruction = "System instruction" },
        NullLogger<GeminiChatClient>.Instance);

    private static HttpResponseMessage JsonResponse(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
    };

    private sealed class FakeHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
