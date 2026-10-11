using System.Net;
using WebPageDownloader;
using WebPageDownloader.Helper;

var seedFilePath = Path.Combine(AppContext.BaseDirectory, "seed.txt");
string[] seedLines;

try
{
    seedLines = await File.ReadAllLinesAsync(seedFilePath);
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"Could not read seed file '{seedFilePath}': {exception.Message}");
    return 1;
}

var urls = new List<Uri>();

foreach (var line in seedLines)
{
    var value = line.Trim();

    if (string.IsNullOrWhiteSpace(value))
    {
        continue;
    }

    if (UrlParser.TryParse(value, out var url))
    {
        urls.Add(url);
    }
    else
    {
        Console.Error.WriteLine($"Skipping invalid HTTP URL in seed file: {value}");
    }
}

urls = urls.Distinct().ToList();

if (urls.Count == 0)
{
    Console.Error.WriteLine("The seed file contains no valid HTTP or HTTPS URLs.");
    return 1;
}

using var cancellationSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationSource.Cancel();
    Console.WriteLine("Cancelling downloads...");
};

var outputDirectory = Path.Combine(Environment.CurrentDirectory, "Downloads");

using var handler = new SocketsHttpHandler
{
    AutomaticDecompression = DecompressionMethods.All,
    PooledConnectionLifetime = TimeSpan.FromMinutes(5)
};
using var httpClient = new HttpClient(handler)
{
    Timeout = TimeSpan.FromSeconds(30)
};
httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("WebPageDownloader/1.0");

var downloader = new PageDownloadService(httpClient, outputDirectory, maxConcurrency: 4);

Console.WriteLine($"Downloading {urls.Count} page(s) to {outputDirectory}");

try
{
    var results = await downloader.DownloadAllAsync(urls, cancellationSource.Token);

    foreach (var result in results)
    {
        if (result.IsSuccess)
        {
            Console.WriteLine(
                $"[OK] {result.Url} -> {Path.GetFileName(result.FilePath)} " +
                $"({result.BytesDownloaded:N0} bytes, {result.Duration.TotalMilliseconds:N0} ms)");
        }
        else
        {
            Console.Error.WriteLine($"[FAILED] {result.Url} - {result.ErrorMessage}");
        }
    }

    var successfulDownloads = results.Count(result => result.IsSuccess);
    Console.WriteLine($"Completed: {successfulDownloads} succeeded, {results.Count - successfulDownloads} failed.");

    return successfulDownloads == results.Count ? 0 : 1;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Downloads were cancelled.");
    return 130;
}
