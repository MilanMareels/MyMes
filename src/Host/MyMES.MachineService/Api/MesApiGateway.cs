using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMES.Contracts.Common;
using MyMES.Contracts.Identity;
using MyMES.Contracts.Machines;
using MyMES.MachineService.Configuration;

namespace MyMES.MachineService.Api;

/// <summary>Reports machine state to the MES.</summary>
public interface IMesApiGateway
{
    /// <summary>
    /// Pushes a machine's status. Returns <c>false</c> when the MES could not be updated, which
    /// is logged and retried on the next poll rather than treated as fatal.
    /// </summary>
    Task<bool> ReportStatusAsync(
        string machineCode,
        MachineStatusDto status,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Signs in as a service account and keeps the token fresh.
/// </summary>
/// <remarks>
/// <para>
/// The worker is just another API client: it holds no database connection and gets exactly the
/// permissions its service account was granted. If the MES is down, every call here fails softly
/// and the worker keeps polling; machine communication problems must never take the API with them,
/// and the reverse holds too.
/// </para>
/// <para>
/// The machine-code to id lookup is cached because it changes rarely and the poll loop would
/// otherwise fetch the whole register every few seconds.
/// </para>
/// </remarks>
public sealed class MesApiGateway(
    HttpClient http,
    IOptions<MachineServiceOptions> options,
    ILogger<MesApiGateway> logger) : IMesApiGateway, IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly MachineServiceOptions _options = options.Value;
    private readonly SemaphoreSlim _tokenGate = new(1, 1);
    private readonly Dictionary<string, Guid> _machineIds = new(StringComparer.OrdinalIgnoreCase);

    private string? _token;
    private DateTime _tokenExpiresUtc = DateTime.MinValue;

    public async Task<bool> ReportStatusAsync(
        string machineCode,
        MachineStatusDto status,
        CancellationToken cancellationToken = default)
    {
        if (!_options.HasCredentials)
        {
            return false;
        }

        try
        {
            if (!await EnsureTokenAsync(cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            var id = await ResolveMachineIdAsync(machineCode, cancellationToken).ConfigureAwait(false);
            if (id is null)
            {
                logger.LogWarning("{MachineCode} is not registered in the MES; status not reported.", machineCode);
                return false;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, $"api/v1/machines/{id}/status")
            {
                Content = JsonContent.Create(new ChangeMachineStatusRequest { Status = status }, options: Json)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

            var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            // A rejected status change is the MES enforcing a rule, not a transport failure.
            logger.LogWarning(
                "MES refused the status change for {MachineCode}: {StatusCode}.",
                machineCode,
                response.StatusCode);

            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Could not reach the MES to report {MachineCode}.", machineCode);
            return false;
        }
    }

    private async Task<bool> EnsureTokenAsync(CancellationToken cancellationToken)
    {
        // Refreshed a minute early so a request cannot be issued with a token that expires in
        // flight.
        if (_token is not null && _tokenExpiresUtc > DateTime.UtcNow.AddMinutes(1))
        {
            return true;
        }

        await _tokenGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_token is not null && _tokenExpiresUtc > DateTime.UtcNow.AddMinutes(1))
            {
                return true;
            }

            var response = await http.PostAsJsonAsync(
                "api/v1/auth/login",
                new LoginRequest { UserName = _options.UserName!, Password = _options.Password! },
                Json,
                cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError(
                    "The MachineService account could not sign in ({StatusCode}). Check MachineService:UserName and :Password.",
                    response.StatusCode);
                return false;
            }

            var login = await response.Content
                .ReadFromJsonAsync<LoginResponse>(Json, cancellationToken)
                .ConfigureAwait(false);

            if (login is null)
            {
                return false;
            }

            _token = login.AccessToken;
            _tokenExpiresUtc = login.ExpiresAtUtc;
            logger.LogInformation("MachineService signed in as {UserName}.", login.User.UserName);
            return true;
        }
        finally
        {
            _tokenGate.Release();
        }
    }

    private async Task<Guid?> ResolveMachineIdAsync(string machineCode, CancellationToken cancellationToken)
    {
        if (_machineIds.TryGetValue(machineCode, out var cached))
        {
            return cached;
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/v1/machines?search={Uri.EscapeDataString(machineCode)}&pageSize=50");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var page = await response.Content
            .ReadFromJsonAsync<PagedResult<MachineDto>>(Json, cancellationToken)
            .ConfigureAwait(false);

        var match = page?.Items.FirstOrDefault(m =>
            string.Equals(m.MachineCode, machineCode, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            return null;
        }

        _machineIds[machineCode] = match.Id;
        return match.Id;
    }

    /// <summary>The HttpClient is owned by the factory; only the token gate is ours to release.</summary>
    public void Dispose() => _tokenGate.Dispose();
}
