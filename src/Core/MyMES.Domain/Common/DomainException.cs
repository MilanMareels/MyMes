namespace MyMES.Domain.Common;

/// <summary>
/// Raised when an operation would leave an aggregate in a state its invariants forbid.
/// </summary>
/// <remarks>
/// The domain throws this rather than returning a result, because reaching an invalid state is a
/// bug in the caller, not an expected outcome. The application layer checks preconditions first
/// and returns a <c>Result</c>; this exception is the backstop for anything that slips through.
/// </remarks>
public sealed class DomainException : Exception
{
    public DomainException(string code, string message) : base(message) => Code = code;

    /// <summary>Stable identifier for the violated rule, for example <c>production_order.not_released</c>.</summary>
    public string Code { get; }
}
