using Microsoft.AspNetCore.Routing;

namespace MyMES.PluginSystem;

/// <summary>
/// Contributes HTTP endpoints to the host.
/// </summary>
/// <remarks>
/// Implemented alongside <c>IMyMesModule</c> by a module that has an API surface, and by a plugin
/// that adds endpoints of its own. The host calls every implementation once while building the
/// request pipeline, so a module owns its routes without the host having to know about them.
/// </remarks>
public interface IEndpointModule
{
    /// <summary>
    /// Maps this module's routes.
    /// </summary>
    /// <param name="endpoints">
    /// The route builder, already scoped to the API's versioned group by the host.
    /// </param>
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
