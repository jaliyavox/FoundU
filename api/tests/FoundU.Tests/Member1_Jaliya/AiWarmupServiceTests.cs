using System.Net;
using FoundU.Infrastructure.Verification;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FoundU.Tests;

/// <summary>The API wakes the AI service as it starts, so the first question does not time out.</summary>
[Trait("Member", "Member1-Jaliya")]
public sealed class AiWarmupServiceTests
{
    [Fact]
    public async Task TheAiServiceIsPingedAsTheApiStarts()
    {
        var handler = new CountingHandler();
        var service = new AiWarmupService(new Factory(handler),
            Options.Create(new AiServiceOptions { BaseUrl = "https://foundu-ai.example" }),
            NullLogger<AiWarmupService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await handler.FirstPing.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await service.StopAsync(CancellationToken.None);

        Assert.Equal("https://foundu-ai.example/health", handler.Requests.Single());
    }

    [Fact]
    public async Task NothingIsPingedWhenKeepWarmIsOff()
    {
        var handler = new CountingHandler();
        var service = new AiWarmupService(new Factory(handler),
            Options.Create(new AiServiceOptions { BaseUrl = "https://foundu-ai.example", KeepWarm = false }),
            NullLogger<AiWarmupService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await service.StopAsync(CancellationToken.None);

        Assert.Empty(handler.Requests);
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        public TaskCompletionSource FirstPing { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.ToString());
            FirstPing.TrySetResult();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
