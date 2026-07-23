namespace SystemCacheCleaner.Models;

public record CacheCategoryDefinition(
    string CategoryId,
    string DisplayName,
    string Description,
    IReadOnlyList<string> RootPaths,
    bool IsDefaultSelected,
    string RiskLevel
);
