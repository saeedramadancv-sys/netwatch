namespace NetWatch.Application.Common.Exceptions;

/// <summary>
/// The request is well formed but conflicts with existing state, e.g. registering a
/// hostname that is already monitored. Translated to 409 rather than 400: the client
/// sent nothing invalid, the server just cannot accept it right now.
/// </summary>
public class ConflictException(string message) : Exception(message);
