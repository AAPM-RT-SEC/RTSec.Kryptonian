using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace RTSec.Kryptonian.Infrastructure.Data;

/// <summary>
/// Value comparers for EF Core to properly detect changes in complex types stored as JSON.
/// Without these, in-place mutations to collections/dictionaries may not be detected.
/// </summary>
public static class ValueComparers
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    /// <summary>
    /// Value comparer for Collection&lt;string&gt; properties.
    /// Compares by sequence equality rather than reference equality.
    /// </summary>
    public static ValueComparer<Collection<string>> StringListComparer { get; } = new(
        (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2) || c1 == null && c2 == null,
        c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
        c => c == null ? null! : new Collection<string>(c.ToList()));

    /// <summary>
    /// Value comparer for Dictionary&lt;string, object&gt; properties.
    /// Uses JSON serialization for deep comparison.
    /// </summary>
    public static ValueComparer<Dictionary<string, object>?> DictionaryComparer { get; } = new(
        (d1, d2) => SerializeDict(d1) == SerializeDict(d2),
        d => SerializeDict(d).GetHashCode(),
        d => CloneDict(d));

    private static string SerializeDict(Dictionary<string, object>? d)
    {
        return d == null ? string.Empty : JsonSerializer.Serialize(d, JsonOptions);
    }

    private static Dictionary<string, object>? CloneDict(Dictionary<string, object>? d)
    {
        if (d == null) return null;
        var json = JsonSerializer.Serialize(d, JsonOptions);
        return JsonSerializer.Deserialize<Dictionary<string, object>>(json, JsonOptions);
    }
}
