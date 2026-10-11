namespace WebPageDownloader.Helper;

public static class UrlParser
{
    public static bool TryParse(string value, out Uri url)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var parsedUrl) &&
            (parsedUrl.Scheme == Uri.UriSchemeHttp || parsedUrl.Scheme == Uri.UriSchemeHttps))
        {
            url = parsedUrl;
            return true;
        }

        url = null!;
        return false;
    }
}
