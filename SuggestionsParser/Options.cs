using CommandLine;
using CommandLine.Text;

namespace SuggestionsParser;

public class Options
{
    [Option('g', "gl", Required = true, HelpText = "Geolocation (see documentation for country codes)")]
    public string? Gl { get; set; }

    [Option('h', "hl", Required = true, HelpText = "Language (see documentation for supported languages)")]
    public string? Hl { get; set; }

    [Option('y', "youtube", Required = true, HelpText = "Use for YouTube suggestions (true or false)")]
    public bool IsYouTube { get; set; }
}