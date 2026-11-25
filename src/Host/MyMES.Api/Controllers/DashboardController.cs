using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMES.Application.Dashboard;

namespace MyMES.Api.Controllers;

/// <summary>The figures behind the dashboard KPI cards.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v1/dashboard")]
public sealed class DashboardController(IDashboardService dashboard) : ControllerBase
{
    [HttpGet("summary")]
    [Authorize]
    public async Task<IActionResult> Summary(CancellationToken cancellationToken) =>
        Ok(await dashboard.GetSummaryAsync(cancellationToken));
}
