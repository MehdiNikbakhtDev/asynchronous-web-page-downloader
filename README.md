# Asynchronous Web Page Downloader

A simple C# console application that reads URLs from a seed file, downloads multiple web pages asynchronously, and saves their HTML to disk. Built with .NET 10 and the standard .NET libraries, with no external runtime packages.

## Features

- Seed-based input: one URL per line, with no command-line arguments required.
- Up to four concurrent downloads using `async`/`await` and `Task.WhenAll`.
- HTTP and HTTPS URL validation, blank-line filtering, and duplicate removal.
- Independent error handling for each download.
- Streaming to temporary files before saving completed downloads as HTML.
- Download results showing file names, byte counts, duration, and a success/failure summary.
- Cancellation with `Ctrl+C`.

## Requirements

- .NET 10 SDK

## Run

From the solution directory:

```powershell
dotnet run --project src/WebPageDownloader
```

Edit `src/WebPageDownloader/seed.txt` to choose the pages to download. Put one absolute HTTP or HTTPS URL on each line:

```text
https://example.com
https://www.iana.org/help/example-domains
```

No command-line arguments are required. The seed file is copied to the build and publish output directories, and the application reads it from beside the executable. This also works when starting the project in Visual Studio.

The downloaded pages are saved in a `Downloads` directory under the current working directory. Blank lines are ignored, duplicate URLs are removed, and invalid URLs are skipped. A missing, unreadable, or empty seed file produces an error message. Downloading the same URL again replaces its previous output file.

Press `Ctrl+C` to cancel downloads in progress.

## Build

```powershell
dotnet build WebPageDownload.slnx --configuration Release
```

## Project structure

```text
src/WebPageDownloader/
  Program.cs                       Application entry point and seed-file loading
  Services/PageDownloadService.cs  Concurrent downloads and file storage
  Helper/UrlParser.cs               HTTP and HTTPS URL validation
  Model/DownloadResult.cs           Result of an individual download
  seed.txt                         Editable list of URLs
```

## Exit codes

- `0`: all downloads succeeded.
- `1`: the seed file could not be read, no valid URLs were found, or a download failed.
- `130`: downloads were cancelled.

## Design decisions

- A single `HttpClient` instance is reused for all requests.
- Downloads run asynchronously with at most four active requests at a time.
- Each URL is handled independently, so one failed request does not stop the others.
- Responses stream directly to temporary files rather than being buffered fully in memory.
- A temporary file is renamed only after a complete download, preventing partial HTML files from being treated as successful results.
- Output names contain a short URL hash, avoiding collisions between pages with similar paths or different query strings.
