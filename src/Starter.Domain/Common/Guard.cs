namespace Starter.Domain.Common;

internal static class Guard
{
    public static string RequiredText(string? value, int maxLength, string code, string fieldName)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainException(code, $"{fieldName} is required.");
        }

        if (trimmed.Length > maxLength)
        {
            throw new DomainException(code, $"{fieldName} must be {maxLength} characters or fewer.");
        }

        return trimmed;
    }

    public static string? OptionalText(string? value, int maxLength, string code, string fieldName)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            throw new DomainException(code, $"{fieldName} must be {maxLength} characters or fewer.");
        }

        return trimmed;
    }
}
