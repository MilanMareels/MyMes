using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MyMES.Contracts.Common;
using MyMES.Contracts.Dashboard;
using MyMES.Contracts.Identity;
using MyMES.Contracts.Machines;
using MyMES.Contracts.Products;
using Shouldly;

namespace MyMES.IntegrationTests;

/// <summary>
/// End-to-end checks through the real HTTP pipeline: authentication, authorisation, module
/// routing and the error contract.
/// </summary>
public sealed class ApiEndpointTests(MyMesApiFactory factory) : IClassFixture<MyMesApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task The_liveness_probe_is_open_and_healthy()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_anonymous_caller_is_refused()
    {
        // Authorisation is enforced by the API, not by hiding links in the UI.
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/v1/machines", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Bad_credentials_are_refused_without_saying_which_half_was_wrong()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest { UserName = "nobody", Password = "Wrong-Password-1" },
            Json);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotContain("unknown");
        body.ShouldNotContain("inactive");
    }

    [Fact]
    public async Task The_seeded_administrator_can_sign_in_and_read_machines()
    {
        using var client = await SignedInClientAsync();

        var response = await client.GetAsync(new Uri("/api/v1/machines", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<MachineDto>>(Json);
        page.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_machine_can_be_created_and_read_back()
    {
        using var client = await SignedInClientAsync();
        var code = $"CNC-{Random.Shared.Next(1000, 9999)}";

        var created = await client.PostAsJsonAsync(
            "/api/v1/machines",
            new CreateMachineRequest { MachineCode = code, Name = "Integration lathe" },
            Json);

        created.StatusCode.ShouldBe(HttpStatusCode.Created);

        var machine = await created.Content.ReadFromJsonAsync<MachineDto>(Json);
        machine.ShouldNotBeNull();
        machine.MachineCode.ShouldBe(code);

        var fetched = await client.GetAsync(new Uri($"/api/v1/machines/{machine.Id}", UriKind.Relative));
        fetched.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_duplicate_machine_code_returns_a_conflict_problem_document()
    {
        using var client = await SignedInClientAsync();
        var code = $"DUP-{Random.Shared.Next(1000, 9999)}";
        var request = new CreateMachineRequest { MachineCode = code, Name = "First" };

        await client.PostAsJsonAsync("/api/v1/machines", request, Json);
        var second = await client.PostAsJsonAsync("/api/v1/machines", request, Json);

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // The machine-readable code is what a client should branch on.
        var problem = await second.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().ShouldBe("machine.code_in_use");
    }

    [Fact]
    public async Task An_unknown_machine_returns_a_not_found_problem_document()
    {
        using var client = await SignedInClientAsync();

        var response = await client.GetAsync(
            new Uri($"/api/v1/machines/{Guid.CreateVersion7()}", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_product_revision_can_be_added_and_released_through_the_api()
    {
        using var client = await SignedInClientAsync();
        var code = $"FG-{Random.Shared.Next(1000, 9999)}";

        var createdResponse = await client.PostAsJsonAsync(
            "/api/v1/products",
            new CreateProductRequest
            {
                ProductCode = code,
                Name = "Integration pump",
                UnitOfMeasure = "pcs",
                InitialVersionNumber = "A"
            },
            Json);

        createdResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var product = (await createdResponse.Content.ReadFromJsonAsync<ProductDto>(Json))!;
        product.Versions.Count.ShouldBe(1);
        product.ReleasedVersionNumber.ShouldBeNull();

        var versionId = product.Versions[0].Id;
        var released = await client.PostAsJsonAsync(
            $"/api/v1/products/{product.Id}/versions/{versionId}/release",
            new { },
            Json);

        released.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = (await released.Content.ReadFromJsonAsync<ProductDto>(Json))!;
        updated.ReleasedVersionNumber.ShouldBe("A");
    }

    [Fact]
    public async Task The_dashboard_reports_figures_for_a_signed_in_user()
    {
        using var client = await SignedInClientAsync();

        var response = await client.GetAsync(new Uri("/api/v1/dashboard/summary", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var summary = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(Json);
        summary.ShouldNotBeNull();
        summary.GeneratedAtUtc.ShouldBeGreaterThan(DateTime.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task Every_module_registered_its_routes()
    {
        // Proves the module mechanism actually wired the host, not just that it compiled.
        using var client = await SignedInClientAsync();

        var response = await client.GetAsync(new Uri("/api/v1/platform", UriKind.Relative));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var platform = (await response.Content.ReadFromJsonAsync<PlatformInfoDto>(Json))!;
        platform.Modules.Select(m => m.Name).ShouldBe(
            ["Machines", "Production", "Quality", "Inventory"],
            ignoreOrder: true);
    }

    [Fact]
    public async Task A_loaded_extension_contributes_its_own_endpoint()
    {
        using var client = await SignedInClientAsync();

        var response = await client.GetAsync(new Uri("/api/v1/extensions/sample/status", UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<HttpClient> SignedInClientAsync()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest
            {
                UserName = MyMesApiFactory.AdministratorUserName,
                Password = MyMesApiFactory.AdministratorPassword
            },
            Json);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, "the bootstrap administrator should be seeded");

        var login = (await response.Content.ReadFromJsonAsync<LoginResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        return client;
    }
}
