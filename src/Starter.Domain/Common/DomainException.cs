namespace Starter.Domain.Common;

/// <summary>
/// Raised when an operation would violate a business rule.
/// <see cref="Code"/> is a stable identifier that clients can rely on; the message is for humans.
/// </summary>
public sealed class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
