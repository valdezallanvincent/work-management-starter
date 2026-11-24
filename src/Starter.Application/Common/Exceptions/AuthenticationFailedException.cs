namespace Starter.Application.Common.Exceptions;

/// <summary>
/// Deliberately carries no detail about why authentication failed, to avoid account enumeration.
/// </summary>
public sealed class AuthenticationFailedException() : Exception("Authentication failed.");
