namespace MyMES.Infrastructure;

/// <summary>
/// Marks a message that crosses a process boundary.
/// </summary>
/// <remarks>
/// Domain events stay inside the application and may reference domain types. An integration event
/// is part of a contract between processes, so it carries only contract types and primitives, and
/// changing one is a breaking change for whoever is listening.
/// </remarks>
public interface IIntegrationEvent
{
    DateTime OccurredAtUtc { get; }
}
