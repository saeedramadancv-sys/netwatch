namespace NetWatch.Domain.Common;

/// <summary>
/// Base for entities that are created and edited by users and therefore need audit stamps.
/// High-volume append-only rows (probe results) deliberately do not inherit from this:
/// they carry a single timestamp and never change.
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}
