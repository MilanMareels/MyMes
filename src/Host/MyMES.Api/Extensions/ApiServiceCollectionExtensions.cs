using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using MyMES.Api.Security;
using MyMES.Identity;
using MyMES.Persistence;

namespace MyMES.Api.Extensions;

internal static class ApiServiceCollectionExtensions
{
    /// <summary>Wires ASP.NET Core Identity onto the shared database context.</summary>
    /// <remarks>
    /// <c>AddIdentityCore</c> rather than the full <c>AddIdentity</c>: the API issues bearer
    /// tokens and has no use for Identity's cookie handlers or UI, and registering them would add
    /// a second authentication scheme that could shadow the bearer one.
    /// </remarks>
    public static IServiceCollection AddMyMesIdentity(this IServiceCollection services)
    {
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                // A shop-floor password policy: long enough to resist guessing, without the
                // character-class rules that push operators towards writing passwords down.
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 4;

                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;

                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<MyMesDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        return services;
    }

    /// <summary>Configures bearer authentication against the tokens this API issues.</summary>
    public static IServiceCollection AddMyMesAuthentication(
        this IServiceCollection services,
        JwtOptions jwtOptions,
        string signingKey)
    {
        services.AddSingleton(SigningCredentialsProvider.Create(signingKey));
        services.AddScoped<ITokenService, TokenService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    // The default five-minute grace makes expiry hard to reason about; a token is
                    // either valid or it is not.
                    ClockSkew = TimeSpan.Zero
                };

                // A browser cannot set headers on a WebSocket handshake, so SignalR passes the
                // token in the query string. Accept it for hub paths only.
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }

    /// <summary>
    /// Declares API version 1.0.
    /// </summary>
    /// <remarks>
    /// The version is part of the path (<c>/api/v1/...</c>) rather than a header, because a URL
    /// that names its version is self-describing in logs, proxies and Swagger. Version 2 becomes
    /// a sibling group mapped from the same modules.
    /// </remarks>
    public static IServiceCollection AddMyMesVersioning(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            // Without this the versions are enforced but never described, so Swagger cannot show
            // which version an endpoint belongs to.
            .AddApiExplorer();

        return services;
    }

    /// <summary>Configures Swagger, including the bearer-token input.</summary>
    public static IServiceCollection AddMyMesSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "MyMES API",
                Version = "v1",
                Description = "Manufacturing Execution System: machines, production orders, products and revisions."
            });

            options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste the token from POST /api/v1/auth/login."
            });

            // Swashbuckle 10 takes a factory so the requirement can reference the document it
            // is being added to.
            options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme)] = []
            });

            // Include the XML docs so endpoint summaries reach the generated page.
            var xml = Path.Combine(AppContext.BaseDirectory, "MyMES.Api.xml");
            if (File.Exists(xml))
            {
                options.IncludeXmlComments(xml);
            }
        });

        return services;
    }
}
