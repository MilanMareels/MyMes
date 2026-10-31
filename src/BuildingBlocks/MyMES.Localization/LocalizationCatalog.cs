using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace MyMES.Localization;

/// <summary>Which cultures the UI offers and which one it falls back to.</summary>
public sealed class LocalizationOptions
{
    public const string SectionName = "Localization";

    /// <summary>Culture used when the request does not ask for one, or asks for an unknown one.</summary>
    public string DefaultCulture { get; set; } = "en";

    /// <summary>Cultures the UI offers. A plant usually wants its own language plus English.</summary>
    public IList<string> SupportedCultures { get; set; } = ["en", "nl"];
}

/// <summary>
/// Looks up UI strings by key.
/// </summary>
/// <remarks>
/// A deliberately small abstraction over the resource mechanism. Today it is backed by an
/// in-memory table, which keeps the foundation dependency-free; swapping in .resx files or a
/// database-backed catalogue later means replacing the registration, not the call sites.
/// </remarks>
public interface ILocalizationCatalog
{
    /// <summary>The string for a key in the current culture, or the key itself when unknown.</summary>
    string this[string key] { get; }

    /// <summary>The string for a key in a specific culture.</summary>
    string GetString(string key, string culture);

    IReadOnlyList<string> SupportedCultures { get; }
}

/// <inheritdoc cref="ILocalizationCatalog"/>
public sealed class InMemoryLocalizationCatalog(IOptions<LocalizationOptions> options) : ILocalizationCatalog
{
    private readonly LocalizationOptions _options = options.Value;

    /// <summary>
    /// Seed strings for the shell. Feature modules add their own keys by replacing this
    /// implementation or by merging a further table into it.
    /// </summary>
    private static readonly Dictionary<string, Dictionary<string, string>> Strings =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = new(StringComparer.Ordinal)
            {
                ["nav.dashboard"] = "Dashboard",
                ["nav.machines"] = "Machines",
                ["nav.production"] = "Production orders",
                ["nav.products"] = "Products",
                ["nav.users"] = "Users",
                ["nav.erp"] = "ERP messages",
                ["common.loading"] = "Loading",
                ["common.empty"] = "Nothing to show",
                ["common.error"] = "Something went wrong",
                ["common.retry"] = "Try again",
                ["common.signOut"] = "Sign out"
            },
            ["nl"] = new(StringComparer.Ordinal)
            {
                ["nav.dashboard"] = "Dashboard",
                ["nav.machines"] = "Machines",
                ["nav.production"] = "Productieorders",
                ["nav.products"] = "Producten",
                ["nav.users"] = "Gebruikers",
                ["nav.erp"] = "ERP-berichten",
                ["common.loading"] = "Laden",
                ["common.empty"] = "Niets te tonen",
                ["common.error"] = "Er ging iets mis",
                ["common.retry"] = "Opnieuw proberen",
                ["common.signOut"] = "Afmelden"
            }
        };

    public string this[string key] => GetString(key, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    public IReadOnlyList<string> SupportedCultures => [.. _options.SupportedCultures];

    public string GetString(string key, string culture)
    {
        if (Strings.TryGetValue(culture, out var table) && table.TryGetValue(key, out var value))
        {
            return value;
        }

        if (Strings.TryGetValue(_options.DefaultCulture, out var fallback)
            && fallback.TryGetValue(key, out var fallbackValue))
        {
            return fallbackValue;
        }

        // Returning the key makes a missing translation obvious in the UI instead of blank.
        return key;
    }
}

public static class LocalizationServiceCollectionExtensions
{
    public static IServiceCollection AddMyMesLocalization(this IServiceCollection services)
    {
        services.TryAddSingleton<ILocalizationCatalog, InMemoryLocalizationCatalog>();
        return services;
    }
}
