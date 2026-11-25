using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMES.Application.Security;
using MyMES.Application.Dashboard;
using MyMES.PluginSystem;

namespace MyMES.Api.Controllers;

/// <summary>The platform's own inventory of what it loaded.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v1/platform")]
public sealed class PlatformController(IModuleRegistry registry, IHostEnvironment environment) : ControllerBase
{
    [HttpGet]
    [Authorize(MesPermissions.UsersRead)]
    public IActionResult Get()
    {
        var version = typeof(PlatformController).Assembly.GetName().Version?.ToString() ?? "0.0.0";

        return Ok(new PlatformInfoDto(
            version,
            environment.EnvironmentName,
            [.. registry.Modules.Select(m => new ModuleInfoDto(m.Name, m.Order, m.AssemblyName))],
            registry.Extensions));
    }
}
