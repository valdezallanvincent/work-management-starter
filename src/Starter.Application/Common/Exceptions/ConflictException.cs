namespace Starter.Application.Common.Exceptions;

/// <summary>
/// The request conflicts with the current state of a resource, for example a duplicate unique value.
/// </summary>
public sealed class ConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
