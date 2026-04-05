using Microsoft.Extensions.Diagnostics.HealthChecks;
using Muxarr.Core.Utilities;

namespace Muxarr.Web.HealthChecks.Checks;

public class FFmpegHealthCheck(ILogger<FFmpegHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await ProcessExecutor.ExecuteProcessAsync("ffmpeg", "-version", TimeSpan.FromSeconds(5));

            if (result.ExitCode == 0)
            {
                return HealthCheckResult.Healthy();
            }

            return HealthCheckResult.Unhealthy($"ffmpeg exited with code {result.ExitCode}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Health check failed for ffmpeg");
            return HealthCheckResult.Unhealthy("ffmpeg is not available", ex);
        }
    }
}
