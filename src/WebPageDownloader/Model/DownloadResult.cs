namespace WebPageDownloader;

public sealed record DownloadResult(
    Uri Url,
    bool IsSuccess,
    string? FilePath,
    long BytesDownloaded,
    TimeSpan Duration,
    string? ErrorMessage);
