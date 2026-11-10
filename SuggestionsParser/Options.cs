using CommandLine;

namespace SuggestionsParser;

public enum ParseMode
{
    /// <summary>Классический побуквенный перебор (key + a-z), один проход, плоский вывод.</summary>
    Simple,

    /// <summary>Умный сбор: модификаторы (префикс+суффикс) + a-z перебор + частотные тиры + trend-diff. Один проход без рекурсии.</summary>
    Smart
}

public class Options
{
    [Option('g', "gl", Required = true, HelpText = "Geolocation (see documentation for country codes)")]
    public string? Gl { get; set; }

    [Option('h', "hl", Required = true, HelpText = "Language (see documentation for supported languages)")]
    public string? Hl { get; set; }

    [Option('y', "youtube", Required = true, HelpText = "Use for YouTube suggestions (true or false)")]
    public bool IsYouTube { get; set; }

    [Option('m', "mode", Required = false, Default = "simple",
        HelpText = "Mode: 'simple' (classic a-z brute force, flat output) or 'smart' (modifiers prefix+suffix + a-z + frequency tiers + trend diff).")]
    public string Mode { get; set; } = "simple";

    [Option('t', "trend", Required = false, Default = false,
        HelpText = "[smart only] Trend mode: also write only the rising suggestions (new since last run) to trends.txt.")]
    public bool TrendMode { get; set; }
}
