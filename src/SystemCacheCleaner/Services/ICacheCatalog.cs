using SystemCacheCleaner.Models;

namespace SystemCacheCleaner.Services;

public interface ICacheCatalog
{
    IReadOnlyList<CacheCategoryDefinition> GetCategories();
}
