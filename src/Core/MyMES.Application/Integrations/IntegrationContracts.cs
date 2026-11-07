using System;
using System.Collections.Generic;
using System.Text;

namespace MyMES.Contracts.Integrations;

public sealed record IncomingErpMessageDto(Guid Id, string ExternalReference, string ProductCode, IncomingMessageStatusDto Status, string? Error, DateTime ReceivedAtUtc, DateTime? ProcessedAtUtc);

public enum IncomingMessageStatusDto { New = 0, Processed = 1, Failed = 2 }

public sealed record ReceiveErpProductRequest(string ExternalReference, string ProductCode, string Payload);
