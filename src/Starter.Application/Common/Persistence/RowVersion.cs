using Starter.Application.Common.Exceptions;

namespace Starter.Application.Common.Persistence;

/// <summary>
/// Row versions travel to clients as base64 strings. The early comparison gives a fast, clear
/// conflict; the database concurrency token still protects the window between read and write.
/// </summary>
public static class RowVersion
{
    public static string Encode(byte[] rowVersion) => Convert.ToBase64String(rowVersion);

    public static void EnsureMatches(byte[] current, string? expected, string resource)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            throw new ConcurrencyConflictException($"A version is required to update this {resource}.");
        }

        Span<byte> buffer = stackalloc byte[16];
        if (!Convert.TryFromBase64String(expected, buffer, out var written)
            || !buffer[..written].SequenceEqual(current))
        {
            throw new ConcurrencyConflictException(
                $"The {resource} was changed by another user. Reload it and apply your changes again.");
        }
    }
}
