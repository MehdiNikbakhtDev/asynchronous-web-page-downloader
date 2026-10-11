using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace WebPageDownloader;

public sealed class PageDownloadService
{
    private readonly HttpClient _httpClient;
    private readonly string _outputDirectory;
    private readonly SemaphoreSlim _concurrencyLimiter;

    public PageDownloadService(HttpClient httpClient, string outputDirectory, int maxConcurrency = 4)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxConcurrency);

        _httpClient = httpClient;
        _outputDirectory = outputDirectory;
        _concurrencyLimiter = new SemaphoreSlim(maxConcurrency, maxConcurrency);
    }

    public async Task<IReadOnlyList<DownloadResult>> DownloadAllAsync(
        IEnumerable<Uri> urls,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(urls);
        Directory.CreateDirectory(_outputDirectory);

        var downloadTasks = urls.Select(url => DownloadWithLimitAsync(url, cancellationToken));
        return await Task.WhenAll(downloadTasks);
    }

    private async Task<DownloadResult> DownloadWithLimitAsync(Uri url, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);
        await _concurrencyLimiter.WaitAsync(cancellationToken);

        try
        {
            return await DownloadAsync(url, cancellationToken);
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    private async Task<DownloadResult> DownloadAsync(Uri url, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        string? temporaryFilePath = null;

        try
        {
            using var response = await _httpClient.GetAsync(
                url,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var filePath = Path.Combine(_outputDirectory, BuildFileName(url));
            temporaryFilePath = $"{filePath}.{Guid.NewGuid():N}.tmp";

            await using (var outputStream = new FileStream(
                             temporaryFilePath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 81920,
                             useAsync: true))
            {
                await response.Content.CopyToAsync(outputStream, cancellationToken);
            }

            File.Move(temporaryFilePath, filePath, overwrite: true);
            temporaryFilePath = null;
            stopwatch.Stop();

            return new DownloadResult(
                url,
                IsSuccess: true,
                filePath,
                new FileInfo(filePath).Length,
                stopwatch.Elapsed,
                ErrorMessage: null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException)
        {
            stopwatch.Stop();
            return new DownloadResult(
                url,
                IsSuccess: false,
                FilePath: null,
                BytesDownloaded: 0,
                stopwatch.Elapsed,
                exception.Message);
        }
        finally
        {
            if (temporaryFilePath is not null)
            {
                File.Delete(temporaryFilePath);
            }
        }
    }

    private static string BuildFileName(Uri url)
    {
        var pageName = url.AbsolutePath.Trim('/').Replace('/', '_');
        pageName = string.IsNullOrWhiteSpace(pageName) ? "index" : pageName;

        var invalidCharacters = Path.GetInvalidFileNameChars();
        pageName = new string(pageName
            .Select(character => invalidCharacters.Contains(character) ? '_' : character)
            .ToArray());

        if (pageName.Length > 60)
        {
            pageName = pageName[..60];
        }

        var urlHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(url.AbsoluteUri)))[..8].ToLowerInvariant();

        return $"{url.Host}_{pageName}_{urlHash}.html";
    }
}
