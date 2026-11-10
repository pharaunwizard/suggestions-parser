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

            var symbols = await LoadSymbolsAsync();
            var (_, proxyLines) = await TryReadLinesAsync(Config.ProxiesFile);
            if (proxyLines.Length == 0)
                Log.Logger.Information("proxies.txt not found. Parsing will be performed without proxies!");

            using var tokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                tokenSource.Cancel();
                Log.Logger.Information("Stopping!");
            };

            var mode = Enum.TryParse<ParseMode>(options.Mode, ignoreCase: true, out var parsedMode)
                ? parsedMode
                : ParseMode.Simple;
            Log.Logger.Information("Mode: {Mode}", mode);

            if (mode == ParseMode.Simple)
                await RunSimpleAsync(options, keys, symbols, proxyLines, tokenSource.Token);
            else
                await RunSmartAsync(options, keys, symbols, proxyLines, tokenSource.Token);
        }
        catch (Exception ex)
        {
            Log.Logger.Fatal(ex, ex.Message);
        }

        await Log.CloseAndFlushAsync();
    }

    // ───────────────────────── SIMPLE MODE (классический побуквенный перебор) ─────────────────────────
    // key + каждый символ из symbols.txt (по умолчанию a-z), один проход, плоский вывод (как было изначально).
    private static async Task RunSimpleAsync(Options options, string[] keys, string symbols, string[] proxyLines,
        CancellationToken token)
    {
        var queries = new ConcurrentQueue<QueryItem>();
        foreach (var key in keys)
        {
            foreach (var symbol in symbols)
                queries.Enqueue(new QueryItem($"{key} {symbol}"));
        }

        Log.Logger.Information("Simple: {Queries} queries from {Keys} keys", queries.Count, keys.Length);

        var suggestions = new ConcurrentQueue<Suggestion>();
        await RunLevelAsync(options, queries, suggestions, proxyLines, token);

        var result = suggestions.Select(s => s.Text).Distinct().OrderBy(t => t).ToList();
        await File.WriteAllLinesAsync(Config.OutputKeysFile, result, Encoding.UTF8);

        Log.Logger.Information("{SuggestionsCount} - All done!", result.Count);
    }

    // ───────────────────────── SMART MODE (модификаторы + тиры + тренд) ─────────────────────────
    private static async Task RunSmartAsync(Options options, string[] keys, string symbols, string[] proxyLines,
        CancellationToken token)
    {
        var modifiers = await LoadModifiersAsync();

        var queries = BuildSmartQueries(keys, modifiers);
        Log.Logger.Information("Smart: {Queries} queries from {Seeds} seeds ({Modifiers} modifiers)", 
            queries.Count, keys.Length, modifiers.Length);

        var suggestions = new ConcurrentQueue<Suggestion>();
        await RunLevelAsync(options, queries, suggestions, proxyLines, token);

        var all = new Dictionary<string, Suggestion>();
        foreach (var s in suggestions)
        {
            if (!all.TryGetValue(s.Text, out var existing) || s.FreqScore < existing.FreqScore)
                all[s.Text] = s;
        }

        var final = all.Values.OrderBy(s => s.FreqScore).ToList();
        Log.Logger.Information("Smart done: {Total} unique suggestions", final.Count);

        if (options.TrendMode)
        {
                var previous = await LoadLatestSnapshotAsync();
                var newOnes = final.Where(s => !previous.Contains(s.Text)).ToList();
                await File.WriteAllLinesAsync(Config.TrendsFile, newOnes.Select(s => s.ToOutputLine()), Encoding.UTF8);
                Log.Logger.Information("Trend: {New} rising suggestions since last run -> {File}",
                    newOnes.Count, Config.TrendsFile);
        }

            await SaveSnapshotAsync(final.Select(s => s.Text));
            await File.WriteAllLinesAsync(Config.OutputKeysFile, final.Select(s => s.ToOutputLine()), Encoding.UTF8);

            var n = final.Count;
            var threshold1 = n > 0 ? final[n / 3].FreqScore : 0;
            var threshold2 = n > 0 ? final[2 * n / 3].FreqScore : 0;

            var hf = final.Where(s => s.FreqScore <= threshold1).Select(s => s.Text).ToList();
            var mf = final.Where(s => s.FreqScore > threshold1 && s.FreqScore <= threshold2).Select(s => s.Text).ToList();
            var lf = final.Where(s => s.FreqScore > threshold2).Select(s => s.Text).ToList();

            await File.WriteAllLinesAsync(Config.HfKeysFile, hf, Encoding.UTF8);
            await File.WriteAllLinesAsync(Config.MfKeysFile, mf, Encoding.UTF8);
            await File.WriteAllLinesAsync(Config.LfKeysFile, lf, Encoding.UTF8);

            Log.Logger.Information("{Total} keys -> HF {Hf} / MF {Mf} / LF {Lf} - All done!",
                n, hf.Count, mf.Count, lf.Count);
    }

    private static ConcurrentQueue<QueryItem> BuildSmartQueries(IReadOnlyList<string> seeds, string[] modifiers)
    {
        var queries = new ConcurrentQueue<QueryItem>();
        foreach (var seed in seeds)
        {
            foreach (var modifier in modifiers)
            {
                queries.Enqueue(new QueryItem($"{seed} {modifier}"));
                queries.Enqueue(new QueryItem($"{modifier} {seed}"));
            }
        }

        return queries;
    }

    // ───────────────────────── общее ─────────────────────────
    private static async Task RunLevelAsync(Options options, ConcurrentQueue<QueryItem> queries,
        ConcurrentQueue<Suggestion> suggestions, string[] proxyLines, CancellationToken token)
    {
        var parsers = proxyLines.Length > 0
            ? proxyLines.Select(proxy => new Parser(options, queries, suggestions, new Proxy(proxy))).ToList()
            : [new Parser(options, queries, suggestions)];

        var tasks = parsers.Select(p => p.StartAsync(token)).ToArray();
        await Task.WhenAll(tasks);

        parsers.ForEach(parser => parser.Dispose());
    }

    private static async Task<string> LoadSymbolsAsync()
    {
        var (success, lines) = await TryReadLinesAsync(Config.SymbolsFile);
        if (success)
            return lines[0];

        Log.Logger.Information($"symbols.txt not found. Using default symbols: {Config.DefaultSymbols}");
        return Config.DefaultSymbols;
    }

    private static async Task<string[]> LoadModifiersAsync()
    {
        var (success, lines) = await TryReadLinesAsync(Config.ModifiersFile);
        if (success)
            return lines.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim()).ToArray();

        Log.Logger.Information("modifiers.txt not found. Smart mode will use a-z symbols only.");
        return [];
    }

    private static async Task<HashSet<string>> LoadLatestSnapshotAsync()
    {
        if (!Directory.Exists(Config.HistoryDirectory))
            return [];

        var latest = Directory.GetFiles(Config.HistoryDirectory, "*.txt")
            .OrderBy(f => f)
            .LastOrDefault();

        if (latest is null)
            return [];

        var lines = await File.ReadAllLinesAsync(latest);
        return lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToHashSet();
    }

    private static async Task SaveSnapshotAsync(IEnumerable<string> texts)
    {
        Directory.CreateDirectory(Config.HistoryDirectory);
        var path = Path.Combine(Config.HistoryDirectory, $"{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        await File.WriteAllLinesAsync(path, texts, Encoding.UTF8);
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
