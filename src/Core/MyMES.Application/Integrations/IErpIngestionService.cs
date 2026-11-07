using System;
using System.Collections.Generic;
using System.Text;
using MyMES.Application.Results;
using MyMES.Application.Integrations;

namespace MyMES.Application.Integrations;

public interface IErpIngestionService
{
    Task<Result<IncomingErpMessageDto>> ReceiveAsync(ReceiveErpProductRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<IncomingErpMessageDto>> GetAsync(IncomingMessageStatusDto? status = null, CancellationToken ct = default);
    Task<int> ProcessPendingAsync(CancellationToken ct = default);
}
