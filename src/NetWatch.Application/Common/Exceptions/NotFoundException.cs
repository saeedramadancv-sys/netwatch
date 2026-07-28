namespace NetWatch.Application.Common.Exceptions;

/// <summary>
/// The requested entity does not exist. Translated to 404 by the API's exception handler,
/// so services can simply state the fact instead of every caller re-checking for null.
/// </summary>
public class NotFoundException(string entity, object key)
    : Exception($"{entity} '{key}' was not found.")
{
    public string Entity { get; } = entity;

    public object Key { get; } = key;
}
