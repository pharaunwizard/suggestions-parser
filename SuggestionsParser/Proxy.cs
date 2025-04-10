namespace SuggestionsParser;

public record Proxy
{
    public Uri Uri { get; }
    public string Username { get; }
    public string Password { get; }

    public Proxy(string proxy) : this(new Uri($"socks5://{proxy}")) { }

    public Proxy(Uri uri)
    {
        Uri = uri ?? throw new ArgumentException("uri is null");

        if (string.IsNullOrEmpty(uri.UserInfo))
            throw new ArgumentException($"Invalid proxy format: {uri}. (name:password@ip:port)");

        var userInfo = uri.UserInfo.Split(':');
        if (userInfo.Length != 2)
            throw new ArgumentException($"Invalid proxy format: {uri} (name:password@ip:port)");

        Username = userInfo[0];
        Password = userInfo[1];
    }
}

