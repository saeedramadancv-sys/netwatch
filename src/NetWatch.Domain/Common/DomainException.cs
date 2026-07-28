namespace NetWatch.Domain.Common;

/// <summary>
/// Raised when an operation would leave an entity in a state the domain forbids.
/// The API translates these into 400 responses rather than 500s.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}
