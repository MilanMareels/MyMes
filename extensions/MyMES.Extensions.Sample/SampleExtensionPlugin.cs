using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyMES.Abstractions.Results;
using MyMES.Application.Machines;
using MyMES.Contracts.Common;
using MyMES.Contracts.Machines;
using MyMES.PluginSystem;

namespace MyMES.Extensions.Sample;

/// <summary>
/// A worked example of a third-party extension.
/// </summary>
/// <remarks>
/// <para>
/// Ships in the solution so the extension seam is covered by the build and the tests rather than
/// only by documentation. It demonstrates all three mechanisms at once:
/// </para>
/// <list type="bullet">
///   <item><description><strong>Decorate</strong> — wraps <see cref="IMachineService"/> to audit
///   every status change without touching the Machines module.</description></item>
///   <item><description><strong>Extend</strong> — adds an endpoint of its own.</description></item>
///   <item><description><strong>Replace</strong> — shown in <c>ConfigureServices</c> as a
///   commented example, because replacing a service in a sample would change the product's
///   behaviour for everyone who builds it.</description></item>
/// </list>
/// <para>
/// Disabled unless <c>Extensions:Sample:Enabled</c> is true, so a stock build is unaffected.
/// </para>
/// </remarks>
public sealed class SampleExtensionPlugin : IMyMesPlugin, IEndpointModule
{
    private bool _enabled;

    public string Name => "Sample extension";

    public string Version => "1.0.0";

    public string CompatiblePlatformVersion => "0.1";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        _enabled = configuration.GetValue<bool>("Extensions:Sample:Enabled");
        if (!_enabled)
        {
            return;
        }

        // Decorate: the audit wrapper receives the real service as a constructor parameter and
        // passes everything through, so the platform behaviour is unchanged.
        services.DecorateService<IMachineService, AuditingMachineService>();

        // Replace: this is how a customer would substitute their own implementation outright.
        // services.ReplaceService<IMachineService, CustomerMachineService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        if (!_enabled)
        {
            return;
        }

        // Extend: a route the platform knows nothing about.
        endpoints.MapGet("/extensions/sample/status", () => Results.Ok(new
            {
                Extension = "Sample extension",
                Version = "1.0.0",
                Note = "Loaded from extensions/MyMES.Extensions.Sample."
            }))
            .WithTags("Extensions")
            .WithName("GetSampleExtensionStatus")
            .WithSummary("Proves that an extension can add its own endpoint.")
            .RequireAuthorization();
    }
}

/// <summary>
/// Logs every machine status change, then delegates to the real service.
/// </summary>
/// <remarks>
/// A decorator takes the service it wraps as a constructor parameter. It must pass every member
/// through: swallowing one would silently remove platform behaviour.
/// </remarks>
internal sealed class AuditingMachineService(
    IMachineService inner,
    ILogger<AuditingMachineService> logger) : IMachineService
{
    public Task<PagedResult<MachineDto>> GetAsync(
        PageRequest page,
        MachineStatusDto? status = null,
        string? search = null,
        CancellationToken cancellationToken = default) =>
        inner.GetAsync(page, status, search, cancellationToken);

    public Task<Result<MachineDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        inner.GetByIdAsync(id, cancellationToken);

    public Task<Result<MachineDto>> CreateAsync(
        CreateMachineRequest request,
        CancellationToken cancellationToken = default) =>
        inner.CreateAsync(request, cancellationToken);

    public Task<Result<MachineDto>> UpdateAsync(
        Guid id,
        UpdateMachineRequest request,
        CancellationToken cancellationToken = default) =>
        inner.UpdateAsync(id, request, cancellationToken);

    public async Task<Result<MachineDto>> ChangeStatusAsync(
        Guid id,
        ChangeMachineStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await inner.ChangeStatusAsync(id, request, cancellationToken).ConfigureAwait(false);

        if (result.IsSuccess)
        {
            logger.LogInformation(
                "[sample-extension] {MachineCode} moved to {Status}.",
                result.Value.MachineCode,
                result.Value.Status);
        }
        else
        {
            logger.LogInformation(
                "[sample-extension] status change for machine {MachineId} refused: {Code}.",
                id,
                result.Error!.Value.Code);
        }

        return result;
    }

    public async Task<Result<MachineDto>> DecommissionAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await inner.DecommissionAsync(id, cancellationToken).ConfigureAwait(false);

        if (result.IsSuccess)
        {
            logger.LogInformation(
                "[sample-extension] {MachineCode} decommissioned.",
                result.Value.MachineCode);
        }

        return result;
    }
}
