using MyMES.Abstractions.Results;
using MyMES.Contracts.Common;
using MyMES.Contracts.Machines;

namespace MyMES.Application.Machines;

/// <summary>Use cases for the machine register.</summary>
public interface IMachineService
{
    Task<PagedResult<MachineDto>> GetAsync(
        PageRequest page,
        MachineStatusDto? status = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<Result<MachineDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<MachineDto>> CreateAsync(CreateMachineRequest request, CancellationToken cancellationToken = default);

    Task<Result<MachineDto>> UpdateAsync(
        Guid id,
        UpdateMachineRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<MachineDto>> ChangeStatusAsync(
        Guid id,
        ChangeMachineStatusRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<MachineDto>> DecommissionAsync(Guid id, CancellationToken cancellationToken = default);
}
