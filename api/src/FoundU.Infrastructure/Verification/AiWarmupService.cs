using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FoundU.Infrastructure.Verification;

/// <summary>
/// Keeps the AI service awake while the API is awake. A ping on start wakes it alongside the
/// API (a sleeping free-plan service takes about half a minute, longer than a request waits),
/// and a ping every ten minutes keeps it from dozing off between questions. /health needs no
/// key and changes nothing; a failed ping is only logged.
/// </summary>
public sealed class AiWarmupService(
    IHttpClientFactory httpClientFactory,
    IOptions<AiServiceOptions> options,
    ILogger<AiWarmupService> logger) : BackgroundService
{
    internal static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.KeepWarm || !Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var baseUrl))
            return;

        var health = new Uri(baseUrl, "/health");
        while (!stoppingToken.IsCancellationRequested)
        {
            await PingAsync(health, stoppingToken);
            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task PingAsync(Uri health, CancellationToken stoppingToken)
    {
        try
        {
            using var client = httpClientFactory.CreateClient(nameof(AiWarmupService));
            // Long enough to ride out a cold start, which is the point of the ping.
            client.Timeout = TimeSpan.FromSeconds(90);
            using var response = await client.GetAsync(health, stoppingToken);
            logger.LogInformation("AI service warm-up: {Status}.", (int)response.StatusCode);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning("AI service warm-up failed: {Reason}.", exception.GetType().Name);
        }
    }
}
