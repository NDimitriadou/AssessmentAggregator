using System.ComponentModel.DataAnnotations;

namespace ReadInEveryWeatherAggregator.Api.Models;

public enum SortField { Date, Title, Source }

public enum SortOrder { Desc, Asc }

/// <summary>
/// The parameters you can put in the URL of GET /api/aggregation.
/// </summary>
public sealed class AggregationQuery : IValidatableObject
{
    //Word to search for in News and Books.
    [StringLength(100)]
    public string Query { get; set; } = "dotnet";

    //City to get the weather for.
    [StringLength(100)]
    public string City { get; set; } = "Athens";

    //Which sources to include, separated by commas (e.g. "News,Books"). Leave empty for all.
    public string? Sources { get; set; }

    //Which categories to include, separated by commas (e.g. "news,books"). Leave empty for all
    public string? Category { get; set; }

    //Only items on or after this date.
    public DateTimeOffset? From { get; set; }

    //Only items on or before this date.
    public DateTimeOffset? To { get; set; }

    public SortField SortBy { get; set; } = SortField.Date;

    public SortOrder Order { get; set; } = SortOrder.Desc;

    [Range(1, 200)]
    public int Limit { get; set; } = 50;

    public IReadOnlySet<string> GetSources() => Split(Sources);

    public IReadOnlySet<string> GetCategories() => Split(Category);

    // Checks that need more than one field (e.g. From must be before To)
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (From is not null && To is not null && From > To)
        {
            yield return new ValidationResult("'From' must be earlier than 'To'.", [nameof(From), nameof(To)]);
        }
    }

    private static HashSet<string> Split(string? value) =>
        (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
