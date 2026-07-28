using System.Net;
using System.Text.RegularExpressions;

namespace NetWatch.Domain.Common;

/// <summary>
/// Small guard-clause helpers so entity constructors read as a list of invariants
/// instead of a wall of if-throw blocks.
/// </summary>
public static partial class Guard
{
    public static string AgainstNullOrWhiteSpace(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{field} is required.");
        }

        return value.Trim();
    }

    public static string AgainstTooLong(string value, int maxLength, string field)
    {
        if (value.Length > maxLength)
        {
            throw new DomainException($"{field} must be at most {maxLength} characters.");
        }

        return value;
    }

    public static int AgainstOutOfRange(int value, int min, int max, string field)
    {
        if (value < min || value > max)
        {
            throw new DomainException($"{field} must be between {min} and {max}.");
        }

        return value;
    }

    /// <summary>
    /// Accepts either a literal IP address or a syntactically valid DNS hostname.
    /// Resolution is not attempted here — a device can legitimately be registered
    /// before its DNS record exists, and a failed lookup is a probe result, not a
    /// validation error.
    /// </summary>
    public static string AgainstInvalidHost(string? value, string field)
    {
        var host = AgainstNullOrWhiteSpace(value, field);
        AgainstTooLong(host, 253, field);

        if (IPAddress.TryParse(host, out _))
        {
            return host;
        }

        if (!HostnamePattern().IsMatch(host))
        {
            throw new DomainException($"{field} must be a valid IP address or hostname.");
        }

        return host;
    }

    [GeneratedRegex(
        @"^(?=.{1,253}$)([a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)(\.[a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex HostnamePattern();
}
