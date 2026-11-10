namespace SuggestionsParser;

public record QueryItem(string Text);

public record Suggestion(string Text, int Position)
{
    public int WordCount => Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    public int FreqScore => Position * 2 + WordCount;

    public string ToOutputLine() => $"{Text}\t{FreqScore}\t{Position}\t{WordCount}";
}
