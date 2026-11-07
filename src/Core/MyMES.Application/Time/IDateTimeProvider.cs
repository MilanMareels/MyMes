namespace MyMES.Abstractions.Time;

/// <summary>
/// Supplies the current time. Everything that stamps a record goes through this so that the
/// behaviour can be frozen in tests instead of depending on the wall clock.
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>The current instant in UTC. All persisted timestamps are UTC.</summary>
    DateTime UtcNow { get; }
}
