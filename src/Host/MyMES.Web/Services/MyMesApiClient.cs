using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MyMES.Contracts.Common;
using MyMES.Contracts.Dashboard;
using MyMES.Contracts.Identity;
using MyMES.Contracts.Machines;
using MyMES.Contracts.Production;
using MyMES.Contracts.Products;

namespace MyMES.Web.Services;

/// <summary>Raised when the API refuses a call; carries the message meant for the user.</summary>
public sealed class ApiException(string message, HttpStatusCode statusCode) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    /// <summary>True when the session is no longer valid and the user has to sign in again.</summary>
    public bool IsUnauthorised => StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
}

/// <summary>
/// The only way the Blazor app reaches data.
/// </summary>
/// <remarks>
/// The UI has no database access at all: this client is the application boundary. Every page
/// depends on it rather than on an <c>HttpClient</c>, so the URLs and the error translation live
/// in one place.
/// </remarks>
public sealed class MyMesApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    // -- Authentication ------------------------------------------------------

    /// <summary>
    /// Exchanges credentials for a token. Returns <c>null</c> when they are rejected, because a
    /// failed sign-in is an expected outcome rather than an error.
    /// </summary>
    public async Task<LoginResponse?> LoginAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync(
            "api/v1/auth/login",
            new LoginRequest { UserName = userName, Password = password },
            Json,
            cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Locked)
        {
            return null;
        }

        await ThrowIfFailedAsync(response, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<LoginResponse>(Json, cancellationToken).ConfigureAwait(false);
    }

    // -- Dashboard ------------------------------------------------------------

    public Task<DashboardSummaryDto?> GetDashboardAsync(CancellationToken cancellationToken = default) =>
        GetAsync<DashboardSummaryDto>("api/v1/dashboard/summary", cancellationToken);

    public Task<PlatformInfoDto?> GetPlatformAsync(CancellationToken cancellationToken = default) =>
        GetAsync<PlatformInfoDto>("api/v1/platform", cancellationToken);

    // -- Machines --------------------------------------------------------------

    public Task<PagedResult<MachineDto>?> GetMachinesAsync(
        int page = 1,
        int pageSize = 25,
        MachineStatusDto? status = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = $"api/v1/machines?page={page}&pageSize={pageSize}";
        if (status is not null)
        {
            query += $"&status={status}";
        }
        
        if (!string.IsNullOrWhiteSpace(search))
        {
            query += $"&search={Uri.EscapeDataString(search)}";
        }
        return GetAsync<PagedResult<MachineDto>>(query, cancellationToken);
    }

    public Task<MachineDto?> CreateMachineAsync(
        CreateMachineRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<CreateMachineRequest, MachineDto>("api/v1/machines", request, cancellationToken);

    public Task<MachineDto?> ChangeMachineStatusAsync(
        Guid id,
        MachineStatusDto status,
        CancellationToken cancellationToken = default) =>
        PostAsync<ChangeMachineStatusRequest, MachineDto>(
            $"api/v1/machines/{id}/status",
            new ChangeMachineStatusRequest { Status = status },
            cancellationToken);

    // -- Production orders ------------------------------------------------------

    public Task<PagedResult<ProductionOrderDto>?> GetProductionOrdersAsync(
        int page = 1,
        int pageSize = 25,
        ProductionOrderStatusDto? status = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = $"api/v1/production-orders?page={page}&pageSize={pageSize}";
        if (status is not null)
        {
            query += $"&status={status}";
        }
        
        if (!string.IsNullOrWhiteSpace(search))
        {
            query += $"&search={Uri.EscapeDataString(search)}";
        }
        return GetAsync<PagedResult<ProductionOrderDto>>(query, cancellationToken);
    }

    public Task<ProductionOrderDto?> CreateProductionOrderAsync(
        CreateProductionOrderRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<CreateProductionOrderRequest, ProductionOrderDto>(
            "api/v1/production-orders", request, cancellationToken);

    public Task<ProductionOrderDto?> TransitionOrderAsync(
        Guid id,
        string action,
        CancellationToken cancellationToken = default) =>
        PostAsync<object, ProductionOrderDto>($"api/v1/production-orders/{id}/{action}", new { }, cancellationToken);

    public Task<ProductionOrderDto?> ReportProductionAsync(
        Guid id,
        decimal quantity,
        CancellationToken cancellationToken = default) =>
        PostAsync<ReportProductionRequest, ProductionOrderDto>(
            $"api/v1/production-orders/{id}/report",
            new ReportProductionRequest { Quantity = quantity },
            cancellationToken);

    // -- Products ----------------------------------------------------------------

    public Task<PagedResult<ProductSummaryDto>?> GetProductsAsync(
        int page = 1,
        int pageSize = 25,
        bool? activeOnly = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = $"api/v1/products?page={page}&pageSize={pageSize}";
        if (activeOnly is not null)
        {
            query += $"&activeOnly={activeOnly.Value.ToString().ToLowerInvariant()}";
        }
        
        if (!string.IsNullOrWhiteSpace(search))
        {
            query += $"&search={Uri.EscapeDataString(search)}";
        }
        return GetAsync<PagedResult<ProductSummaryDto>>(query, cancellationToken);
    }

    public Task<ProductDto?> GetProductAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync<ProductDto>($"api/v1/products/{id}", cancellationToken);

    public Task<ProductDto?> CreateProductAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<CreateProductRequest, ProductDto>("api/v1/products", request, cancellationToken);

    public Task<ProductVersionDto?> AddProductVersionAsync(
        Guid productId,
        AddProductVersionRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<AddProductVersionRequest, ProductVersionDto>(
            $"api/v1/products/{productId}/versions", request, cancellationToken);

    public Task<ProductDto?> ReleaseProductVersionAsync(
        Guid productId,
        Guid versionId,
        CancellationToken cancellationToken = default) =>
        PostAsync<object, ProductDto>(
            $"api/v1/products/{productId}/versions/{versionId}/release", new { }, cancellationToken);

    // -- Users ---------------------------------------------------------------------

    public Task<List<UserDto>?> GetUsersAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<UserDto>>("api/v1/users", cancellationToken);

    public Task<UserDto?> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<CreateUserRequest, UserDto>("api/v1/users", request, cancellationToken);

    public Task DecommissionMachineAsync(Guid id, CancellationToken cancellationToken = default) =>
        PostAsync<object, MachineDto>($"api/v1/machines/{id}/decommission", new { }, cancellationToken);

    public Task<ProductDto?> DeactivateProductAsync(Guid id, CancellationToken cancellationToken = default) =>
        PostAsync<object, ProductDto>($"api/v1/products/{id}/deactivate", new { }, cancellationToken);

    public Task DeactivateUserAsync(string id, CancellationToken cancellationToken = default) =>
        PostAsync<object, object>($"api/v1/users/{id}/deactivate", new { }, cancellationToken);

    public Task ActivateUserAsync(string id, CancellationToken cancellationToken = default) =>
        PostAsync<object, object>($"api/v1/users/{id}/activate", new { }, cancellationToken);

    // -- Plumbing ---------------------------------------------------------------------

    private async Task<T?> GetAsync<T>(string url, CancellationToken cancellationToken)
    {
        var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        await ThrowIfFailedAsync(response, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken).ConfigureAwait(false);
    }

    private async Task<TOut?> PostAsync<TIn, TOut>(string url, TIn body, CancellationToken cancellationToken)
    {
        var response = await http.PostAsJsonAsync(url, body, Json, cancellationToken).ConfigureAwait(false);
        await ThrowIfFailedAsync(response, cancellationToken).ConfigureAwait(false);

        return response.StatusCode == HttpStatusCode.NoContent
            ? default
            : await response.Content.ReadFromJsonAsync<TOut>(Json, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Turns the API's ProblemDetails into a message a person can act on.</summary>
    private static async Task ThrowIfFailedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string? detail = null;
        try
        {
            var problem = await response.Content
                .ReadFromJsonAsync<ProblemResponse>(Json, cancellationToken)
                .ConfigureAwait(false);
            detail = problem?.Detail ?? problem?.Title;
        }
        catch (Exception)
        {
            // Not a ProblemDetails body; the status code below is all we have.
        }

        throw new ApiException(
            detail ?? $"The API returned {(int)response.StatusCode} {response.ReasonPhrase}.",
            response.StatusCode);
    }

    private sealed record ProblemResponse(string? Title, string? Detail, int? Status, string? Code);
}

/// <summary>
/// Attaches the signed-in user's API token to every outgoing request.
/// </summary>
/// <remarks>
/// The token is carried in the authentication cookie and read back here, so the UI acts strictly
/// on behalf of the user: an operator's browser session cannot call anything their own permissions
/// do not allow.
/// </remarks>
public sealed class ApiTokenHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var token = accessor.HttpContext?.User.FindFirst(WebAuthenticationDefaults.ApiTokenClaim)?.Value;

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}

