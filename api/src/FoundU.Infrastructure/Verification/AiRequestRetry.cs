using System.Net;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace FoundU.Infrastructure.Verification;

/// <summary>
/// Small retry boundary for idempotent, recommendation-only AI requests. It deliberately does
/// not inspect or log request/response bodies and never retries validation/authentication errors.
/// </summary>
internal static class AiRequestRetry
{
    internal const int MaxRetries = 2;

    internal static async Task<(HttpResponseMessage Response, int RetryCount)> SendAsync(
        Func<int, Task<HttpResponseMessage>> send,
        ILogger? logger,
        string agentName,
        string correlationId,
        CancellationToken cancellationToken)
    {
        for (var retry = 0; ; retry++)
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                var response = await send(retry);
                if (!IsTransient(response.StatusCode) || retry == MaxRetries)
                {
                    logger?.LogInformation("agent_request_completed AgentName={AgentName} CorrelationId={CorrelationId} Attempt={Attempt} StatusCode={StatusCode} DurationMs={DurationMs}", agentName, correlationId, retry + 1, (int)response.StatusCode, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                    return (response, retry);
                }

                logger?.LogWarning("agent_retry_scheduled AgentName={AgentName} CorrelationId={CorrelationId} Attempt={Attempt} MaxAttempts={MaxAttempts} StatusCode={StatusCode}", agentName, correlationId, retry + 1, MaxRetries + 1, (int)response.StatusCode);
                response.Dispose();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException) when (retry == MaxRetries)
            {
                logger?.LogWarning("agent_request_timed_out AgentName={AgentName} CorrelationId={CorrelationId} Attempts={Attempts}", agentName, correlationId, retry + 1);
                throw;
            }
            catch (HttpRequestException) when (retry == MaxRetries)
            {
                logger?.LogWarning("agent_request_failed AgentName={AgentName} CorrelationId={CorrelationId} FailureCategory=network_unavailable Attempts={Attempts}", agentName, correlationId, retry + 1);
                throw;
            }
            catch (HttpRequestException)
            {
                logger?.LogWarning("agent_retry_scheduled AgentName={AgentName} CorrelationId={CorrelationId} Attempt={Attempt} MaxAttempts={MaxAttempts} FailureCategory=network_unavailable", agentName, correlationId, retry + 1, MaxRetries + 1);
            }

            logger?.LogInformation("agent_retry_attempt AgentName={AgentName} CorrelationId={CorrelationId} Attempt={Attempt} MaxAttempts={MaxAttempts}", agentName, correlationId, retry + 2, MaxRetries + 1);
            await Task.Delay(TimeSpan.FromMilliseconds(75 * (retry + 1)), cancellationToken);
        }
    }

    internal static bool IsTransient(HttpStatusCode status) => status is HttpStatusCode.RequestTimeout
        or (HttpStatusCode)429
        or HttpStatusCode.BadGateway
        or HttpStatusCode.ServiceUnavailable
        or HttpStatusCode.GatewayTimeout;
}
