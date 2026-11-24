using Starter.Domain.Common;

namespace Starter.Domain.Projects;

/// <summary>
/// Short, human-friendly project identifier such as "OPS" or "FIN2".
/// Modelled as a value object because the same normalization and format rules apply
/// wherever a key is accepted (create, lookup, uniqueness checks).
/// </summary>
public readonly record struct ProjectKey
{
    public const int MinLength = 2;
    public const int MaxLength = 10;

    private ProjectKey(string value) => Value = value;

    public string Value { get; }

    public static ProjectKey Create(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;

        if (normalized.Length is < MinLength or > MaxLength
            || !char.IsAsciiLetterUpper(normalized[0])
            || !normalized.All(char.IsAsciiLetterOrDigit))
        {
            throw new DomainException(
                "project.key_invalid",
                $"Project key must be {MinLength}-{MaxLength} letters or digits and start with a letter.");
        }

        return new ProjectKey(normalized);
    }

    public override string ToString() => Value;
}
