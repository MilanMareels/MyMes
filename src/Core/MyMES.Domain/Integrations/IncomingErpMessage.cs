using System;
using MyMES.Domain.Common;

namespace MyMES.Domain.Integrations;

public enum IncomingMessageStatus { New = 0, Processed = 1, Failed = 2 }

public sealed class IncomingErpMessage : AuditableEntity
{
    private IncomingErpMessage() { }

    private IncomingErpMessage(Guid id, string externalReference, string productCode, string payload) : base(id)
    {
        ExternalReference = externalReference;
        ProductCode = productCode;
        Payload = payload;
        Status = IncomingMessageStatus.New;
    }

    public string ExternalReference { get; private set; } = string.Empty;
    public string ProductCode { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public IncomingMessageStatus Status { get; private set; }
    public string? Error { get; private set; }
    public DateTime ReceivedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }

    public static IncomingErpMessage Create(string externalReference, string productCode, string payload, DateTime utcNow)
        => new(NewId(), externalReference.Trim(), productCode.Trim().ToUpperInvariant(), payload)
        { ReceivedAtUtc = utcNow };

    public void MarkProcessed(DateTime utcNow)
    {
        Status = IncomingMessageStatus.Processed;
        ProcessedAtUtc = utcNow;
    }

    public void MarkFailed(string error, DateTime utcNow)
    {
        Status = IncomingMessageStatus.Failed;
        Error = error;
        ProcessedAtUtc = utcNow;
    }
}
