using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMES.Abstractions.Results;
using MyMES.Abstractions.Time;
using MyMES.Application.Abstractions;
using MyMES.Application.Mapping;
using MyMES.Contracts.Common;
using MyMES.Contracts.Machines;
using MyMES.Domain.Common;
using MyMES.Domain.Machines;

namespace MyMES.Application.Machines;

/// <inheritdoc cref="IMachineService"/>
public sealed class MachineService(
    IApplicationDbContext db,
    IDateTimeProvider clock,
    ILogger<MachineService> logger) : IMachineService
{
    public async Task<PagedResult<MachineDto>> GetAsync(
        PageRequest page,
        MachineStatusDto? status = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var paging = page.Normalised();
        var query = db.Machines.AsNoTracking();

        if (status is { } wanted)
        {
            var domainStatus = wanted.ToDomain();
            query = query.Where(m => m.Status == domainStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(m => m.MachineCode.Contains(term) || m.Name.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await query
            .OrderBy(m => m.MachineCode)
            .Skip((paging.Page - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<MachineDto>(
            [.. items.Select(m => m.ToDto())],
            paging.Page,
            paging.PageSize,
            total);
    }

    public async Task<Result<MachineDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var machine = await db.Machines
            .AsNoTracking()
            .SingleOrDefaultAsync(m => m.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return machine is null
            ? Result.Failure<MachineDto>(NotFound(id))
            : Result.Success(machine.ToDto());
    }

    public async Task<Result<MachineDto>> CreateAsync(
        CreateMachineRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = request.MachineCode.Trim().ToUpperInvariant();

        // Checked up front so the caller gets a clean 409 instead of a unique-index violation.
        var exists = await db.Machines
            .AnyAsync(m => m.MachineCode == code, cancellationToken)
            .ConfigureAwait(false);

        if (exists)
        {
            return Result.Failure<MachineDto>(Error.Conflict(
                "machine.code_in_use",
                $"Machine code {code} is already registered."));
        }

        try
        {
            var machine = Machine.Create(request.MachineCode, request.Name, request.Description, clock.UtcNow);
            db.Machines.Add(machine);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Machine {MachineCode} registered with id {MachineId}.", machine.MachineCode, machine.Id);
            return Result.Success(machine.ToDto());
        }
        catch (DomainException ex)
        {
            return Result.Failure<MachineDto>(Error.Validation(ex.Code, ex.Message));
        }
    }

    public async Task<Result<MachineDto>> UpdateAsync(
        Guid id,
        UpdateMachineRequest request,
        CancellationToken cancellationToken = default)
    {
        var machine = await db.Machines
            .SingleOrDefaultAsync(m => m.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (machine is null)
        {
            return Result.Failure<MachineDto>(NotFound(id));
        }

        try
        {
            machine.UpdateDetails(request.Name, request.Description);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(machine.ToDto());
        }
        catch (DomainException ex)
        {
            return Result.Failure<MachineDto>(Error.Validation(ex.Code, ex.Message));
        }
    }

    public async Task<Result<MachineDto>> ChangeStatusAsync(
        Guid id,
        ChangeMachineStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var machine = await db.Machines
            .SingleOrDefaultAsync(m => m.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (machine is null)
        {
            return Result.Failure<MachineDto>(NotFound(id));
        }

        try
        {
            machine.ChangeStatus(request.Status.ToDomain(), clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            logger.LogInformation(
                "Machine {MachineCode} moved to {Status}.",
                machine.MachineCode,
                machine.Status);

            return Result.Success(machine.ToDto());
        }
        catch (DomainException ex)
        {
            return Result.Failure<MachineDto>(Error.Conflict(ex.Code, ex.Message));
        }
    }

    public async Task<Result<MachineDto>> DecommissionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var machine = await db.Machines
            .SingleOrDefaultAsync(m => m.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (machine is null)
        {
            return Result.Failure<MachineDto>(NotFound(id));
        }

        try
        {
            machine.Decommission(clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Machine {MachineCode} decommissioned.", machine.MachineCode);
            return Result.Success(machine.ToDto());
        }
        catch (DomainException ex)
        {
            return Result.Failure<MachineDto>(Error.Conflict(ex.Code, ex.Message));
        }
    }

    private static Error NotFound(Guid id) =>
        Error.NotFound("machine.not_found", $"No machine with id {id}.");
}
