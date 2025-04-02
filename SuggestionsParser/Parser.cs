using Serilog;

using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace SuggestionsParser;

internal class Parser(ConcurrentQueue<string> queries, ConcurrentQueue<string> suggestions, Proxy proxy) : IDisposable
{
    private const string Url = "https://www.google.com/complete/search?client=chrome&ds=yt";

    private readonly HttpClient _client = Utils.CreateHttpClient(proxy);
    private readonly TimeSpan _delay = TimeSpan.FromMilliseconds(400);

    private readonly StringBuilder _url = new();

    public Task StartAsync(CancellationToken token) => Task.Run(async () =>
    {
        while (!queries.IsEmpty)
        {
            if (token.IsCancellationRequested)
                return;

            if (!queries.TryDequeue(out var query)) continue;

            Log.Logger.Information($"{Environment.CurrentManagedThreadId} Query: {query}");
            foreach (var suggestion in await ParseSuggestionsAsync(query))
            {
                suggestions.Enqueue(suggestion);
            }

            await Task.Delay(_delay, CancellationToken.None);
        }

        Log.Logger.Information("Done!");
    }, CancellationToken.None);

    private async Task<List<string>> ParseSuggestionsAsync(string query)
    {
        _url.Clear();
        _url.Append(Url);
        _url.Append($"&hl={Config.UserInterfaceLanguage}");
        _url.Append($"&q={HttpUtility.UrlEncode(query)}");

        var requestUrl = _url.ToString();

        using var message = new HttpRequestMessage();
        message.Method = HttpMethod.Get;
        message.RequestUri = new Uri(requestUrl);

        using var response = await _client.SendAsync(message);
        var responseText = await response.Content.ReadAsStringAsync();

        responseText = Regex.Unescape(responseText);

        return ExtractSuggestions(responseText);
    }

    private static List<string> ExtractSuggestions(string response)
    {
        var suggestions = new List<string>();
        var openingBrackets = response.Count(symbol => symbol == '[');
        var closedBrackets = response.Count(symbol => symbol == ']');

        if (openingBrackets != closedBrackets || openingBrackets == 0)
            return suggestions;

        var startIndex = response.IndexOf('[', 1) + 1;
        var endIndex = response.IndexOf(']', startIndex);

        var suggestionString = response[startIndex..endIndex];
        if (suggestionString.Length > 0)
        {
            suggestions = [.. suggestionString.Split(',').Select(suggestion => suggestion.Replace("\"", string.Empty, StringComparison.Ordinal).Trim())];
        }

        return suggestions;
    }

    public void Dispose() => _client.Dispose();
}

