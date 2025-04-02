using System.Net;

namespace SuggestionsParser;

public static class Utils
{
    public static HttpClient CreateHttpClient(Proxy proxy)
    {
        var webProxy = new WebProxy
        {
            Address = proxy.Uri,
            Credentials = new NetworkCredential(proxy.Username, proxy.Password)
        };

        var handler = new SocketsHttpHandler()
        {
            UseProxy = true,
            Proxy = webProxy,
        };

        return new HttpClient(handler, disposeHandler: true);
    }
}

