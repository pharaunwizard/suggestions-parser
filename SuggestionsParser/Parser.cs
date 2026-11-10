using Serilog;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace SuggestionsParser;

internal class Parser : IDisposable
{
    private readonly string _defaultUrl = "https://www.google.com/complete/search?client=chrome";

    // Calculated empirically to avoid a ban.
    private readonly TimeSpan _delay = TimeSpan.FromMilliseconds(400);

    private readonly StringBuilder _currentUrl = new();
    private readonly HttpClient _client = new();

    private readonly Options _options;
    private readonly ConcurrentQueue<QueryItem> _queries;
    private readonly ConcurrentQueue<Suggestion> _suggestions;

    public Parser(Options options, ConcurrentQueue<QueryItem> queries, ConcurrentQueue<Suggestion> suggestions, Proxy proxy)
        : this(options, queries, suggestions)
    {
        _client = Utils.CreateHttpClient(proxy);
    }

    public Parser(Options options, ConcurrentQueue<QueryItem> queries, ConcurrentQueue<Suggestion> suggestions)
    {
        _options = options;
        _queries = queries;
        _suggestions = suggestions;
        _defaultUrl += $"&hl={options.Hl}&gl={options.Gl}";
    }

    public Task StartAsync(CancellationToken token) => Task.Run(async () =>
    {
        while (!_queries.IsEmpty)
        {
            if (token.IsCancellationRequested)
                return;

            if (!_queries.TryDequeue(out var item))
                continue;

            Log.Logger.Information("Query: {Result}", item.Text);

            foreach (var suggestion in await ParseSuggestionsAsync(item))
            {
                _suggestions.Enqueue(suggestion);
            }

            await Task.Delay(_delay, CancellationToken.None);
        }
    }, CancellationToken.None);

    private async Task<List<Suggestion>> ParseSuggestionsAsync(QueryItem item)
    {
        _currentUrl.Clear();
        _currentUrl.Append(_defaultUrl);
        if (_options.IsYouTube)
        {
            _currentUrl.Append("&ds=yt");
        }

        _currentUrl.Append($"&q={HttpUtility.UrlEncode(item.Text)}");

        var requestUrl = _currentUrl.ToString();

        try
        {
            using var message = new HttpRequestMessage();
            message.Method = HttpMethod.Get;
            message.RequestUri = new Uri(requestUrl);

            using var response = await _client.SendAsync(message);
            var responseText = await response.Content.ReadAsStringAsync();

            responseText = Regex.Unescape(responseText);

            return ExtractSuggestions(responseText);
        }
        catch (Exception ex)
        {
            Log.Logger.Warning("Request failed for '{Query}': {Message}", item.Text, ex.Message);
            return [];
        }
    }

    private static List<Suggestion> ExtractSuggestions(string response)
    {
        try
        {
            using var document = JsonDocument.Parse(response);
            var suggestions = document.RootElement[1]
                .EnumerateArray()
                .Select((element, index) => new Suggestion(element.GetString()?.Trim() ?? string.Empty, index))
                .Where(s => !string.IsNullOrEmpty(s.Text))
                .ToList();
            return suggestions;
        }
        catch
        {
            return [];
        }
    }

    public void Dispose() => _client.Dispose();
}
