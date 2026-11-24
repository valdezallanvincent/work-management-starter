namespace Starter.Application.Common.Exceptions;

/// <summary>
/// The resource was changed by someone else after the client read it.
/// </summary>
public sealed class ConcurrencyConflictException(string message) : Exception(message);
