using System.Text;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CoreServer.Models;
using CoreServer.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoreServer.Services;

public class BlobLogBackgroundService : BackgroundService
{
    private readonly ILogger<BlobLogBackgroundService> _logger;
    private readonly IChatHub _hub;
    private readonly BlobServiceClient? _blobServiceClient;
    private readonly string _logContainerName;
    private readonly TimeSpan _interval;

    private DateTimeOffset _lastTickUtc;

    public BlobLogBackgroundService(
        ILogger<BlobLogBackgroundService> logger,
        IChatHub hub,
        BlobServiceClient? blobServiceClient,
        IConfiguration config)
    {
        _logger = logger;
        _hub = hub;
        _blobServiceClient = blobServiceClient;
        _logContainerName = config["LogContainerName"] ?? "log";

        if (!int.TryParse(config["LogIntervalMinutes"], out var minutes) || minutes <= 0)
        {
            minutes = 5;
        }
        _interval = TimeSpan.FromMinutes(minutes);
        _lastTickUtc = DateTimeOffset.UtcNow;
        Console.WriteLine($"Blob log background service initialised, interval: {minutes} minutes");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_blobServiceClient is null)
        {
            _logger.LogInformation("Blob logging disabled: BlobServiceClient not configured.");
            return;
        }

        var container = _blobServiceClient.GetBlobContainerClient(_logContainerName);
        try
        {
            await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to ensure log container exists: {Container}", _logContainerName);
        }

        _logger.LogInformation("Blob logging started with interval {Interval} into container '{Container}'.", _interval, _logContainerName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                var since = _lastTickUtc;
                var now = DateTimeOffset.UtcNow;
                _lastTickUtc = now;

                // Get up to 200 recent messages and filter by timestamp
                var recent = _hub.GetHistory(200)
                    .Where(m => m.TS > since)
                    .OrderBy(m => m.TS)
                    .ToList();

                if (recent.Count == 0)
                {
                    continue; // nothing to log for this tick
                }

                var sb = new StringBuilder();
                foreach (var m in recent)
                {
                    var ts = m.TS.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.fff 'Z'");
                    var id8 = (m.ClientId ?? string.Empty).Length > 8 ? m.ClientId[..8] : (m.ClientId ?? string.Empty);
                    var type = m.Type.ToString();
                    sb.Append('[').Append(ts).Append("] ")
                      .Append(id8).Append(" (" + type + ")")
                      .Append(": ")
                      .AppendLine(m.Text ?? string.Empty);
                }

                var content = sb.ToString();
                var blobName = $"{now:yyyyMMdd}/{now:HHmmss}.log"; // virtual folder per day
                var blob = container.GetBlobClient(blobName);
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
                var headers = new BlobHttpHeaders { ContentType = "text/plain; charset=utf-8" };
                var options = new BlobUploadOptions { HttpHeaders = headers };
                await blob.UploadAsync(stream, options, stoppingToken);

                _logger.LogInformation("Uploaded log tick with {Count} message(s) to blob '{BlobName}'.", recent.Count, blobName);
            }
            catch (OperationCanceledException)
            {
                // shutting down
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during blob log tick.");
            }
        }

        _logger.LogInformation("Blob logging stopping.");
    }
}
