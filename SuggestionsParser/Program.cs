using Serilog;
using Serilog.Events;

using System.Collections.Concurrent;
using System.Text;

namespace SuggestionsParser;

internal class Program
{
    public static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information)
            .WriteTo.File("log.txt", rollingInterval: RollingInterval.Day, encoding: Encoding.UTF8)
            .CreateLogger();


        var keys = await File.ReadAllLinesAsync(Config.InputKeysFile);
        var proxies = await Proxy.FromFileAsync(Config.ProxiesFile);

        var queries = new ConcurrentQueue<string>();
        var suggestions = new ConcurrentQueue<string>();

        foreach (var key in keys)
        {
            foreach (var query in Config.Symbols.Select(symbol => $"{key} {symbol}"))
            {
                queries.Enqueue(query);
            }
        }

        using var tokenSource = new CancellationTokenSource();

        var parsers = new List<Task>(proxies.Count);

        parsers.AddRange(proxies
            .Select(proxy => new Parser(queries, suggestions, proxy))
            .Select(parser => parser.StartAsync(tokenSource.Token)));

        Console.ReadKey();

        await tokenSource.CancelAsync();
        Log.Logger.Information("Stopping!");

        await Task.WhenAll(parsers);
        parsers.ForEach(parser => parser.Dispose());

        await File.WriteAllLinesAsync(Config.OutputKeysFile, suggestions, Encoding.UTF8);

        Log.Logger.Information($"{suggestions.Count} - All done!");

        Console.ReadKey();
    }
}

