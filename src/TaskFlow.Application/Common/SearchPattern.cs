namespace TaskFlow.Application.Common;

/// <summary>
/// Builds safe LIKE patterns for case-insensitive "contains" searches.
/// Queries use <c>lower(column) LIKE pattern</c>, which PostgreSQL serves from
/// pg_trgm GIN expression indexes (see the SearchIndexes migration).
/// </summary>
public static class SearchPattern
{
    public const string EscapeCharacter = "\\";
    private const int MaxTerms = 5;

    public static IReadOnlyList<string> Terms(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return [];

        return search
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(MaxTerms)
            .Select(Contains)
            .ToArray();
    }

    public static string Contains(string term)
    {
        var escaped = term.ToLowerInvariant()
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
        return $"%{escaped}%";
    }
}
