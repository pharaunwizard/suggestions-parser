using CommandLine;
using CommandLine.Text;
using Serilog;
using Serilog.Events;
using System.Collections.Concurrent;
using System.Text;

namespace SuggestionsParser;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information)
            .WriteTo.File("log.txt", rollingInterval: RollingInterval.Day, encoding: Encoding.UTF8)
            .CreateLogger();

        try
        {
            var options = ParseOptions(args)!;

            var (success, keys) = await TryReadLinesAsync(Config.InputKeysFile);
            if (!success)
                throw new FileNotFoundException($"{Config.InputKeysFile} not found or empty!");

            var queries = new ConcurrentQueue<string>();
            var suggestions = new ConcurrentQueue<string>();

            var symbols = await LoadSymbolsAsync();

            foreach (var key in keys)
            {
                foreach (var query in symbols.Select(symbol => $"{key} {symbol}"))
                {
                    queries.Enqueue(query);
                }
            }
            
            var parsers = await CreateParsersAsync(options, queries, suggestions);

            using var tokenSource = new CancellationTokenSource();
            
            var tasks = parsers.Select(p => p.StartAsync(tokenSource.Token)).ToArray();

            var keyPressTask = Task.Run(() =>
            {
                Console.WriteLine("Press any key to stop.");
                Console.ReadKey(true);
                return Task.CompletedTask;
            });

            await Task.WhenAny(Task.WhenAll(tasks), keyPressTask);

            await tokenSource.CancelAsync();
            Log.Logger.Information("Stopping!");

            await Task.WhenAll(tasks);

            parsers.ForEach(parser => parser.Dispose());

            await File.WriteAllLinesAsync(Config.OutputKeysFile, suggestions, Encoding.UTF8);

            Log.Logger.Information("{SuggestionsCount} - All done!", suggestions.Count);

            Console.ReadKey();
        }
        catch (Exception ex)
        {
            Log.Logger.Fatal(ex, ex.Message);
        }

        await Log.CloseAndFlushAsync();
    }


    private static async Task<List<Parser>> CreateParsersAsync(Options options, ConcurrentQueue<string> queries,
        ConcurrentQueue<string> suggestions)
    {
        var parsers = new List<Parser>();

        var (success, lines) = await TryReadLinesAsync(Config.ProxiesFile);

        if (success)
        {
            parsers.AddRange(lines.Select(proxy => new Parser(options, queries, suggestions, new Proxy(proxy))));
        }
        else
        {
            Log.Logger.Information("proxies.txt not found. Parsing will be performed without proxies!");
            parsers.Add(new Parser(options, queries, suggestions));
        }

        return parsers;
    }

    private static async Task<string> LoadSymbolsAsync()
    {
        var (success, lines) = await TryReadLinesAsync(Config.SymbolsFile);
        if (success)
            return lines[0];
        
        Log.Logger.Information($"symbols.txt not found. Using default symbols: {Config.DefaultSymbols}");
        return Config.DefaultSymbols;
    }

    private static async Task<(bool Success, string[] Lines)> TryReadLinesAsync(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return (false, []);
            }

            var lines = await File.ReadAllLinesAsync(path);
            return (lines.Length > 0, lines);
        }
        catch (IOException)
        {
            return (false, []);
        }
    }

    //ToDo: refactoring
    private static Options? ParseOptions(string[] args)
    {
        Options? options = null;

        var parser = new CommandLine.Parser();
        var parserResult = parser.ParseArguments<Options>(args);

        parserResult
            .WithParsed(opts => options = opts)
            .WithNotParsed(errors =>
            {
                var helpText = HelpText.AutoBuild(parserResult);
                Console.WriteLine(helpText);
                Environment.Exit(1);
            });

        return options;
    }
}