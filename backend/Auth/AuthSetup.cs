using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace backend.Auth;

/// <summary>Microsoft Entra login with a stateless cookie (see ADR 0006).</summary>
public static class AuthSetup
{
    public static IServiceCollection AddEntraLogin(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var section = configuration.GetSection("Authentication:Microsoft");
        var tenantId = Required(section, "TenantId");
        var clientId = Required(section, "ClientId");
        var clientSecret = Required(section, "ClientSecret");

        // Keys stay in memory: every redeploy and restart logs everyone out (accepted, ADR 0006).
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.ExpireTimeSpan = TimeSpan.FromDays(7);
                options.SlidingExpiration = true;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                // The API answers 401 itself; only /api/auth/login talks to Microsoft.
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            })
            .AddOpenIdConnect(
                OpenIdConnectDefaults.AuthenticationScheme,
                options =>
                {
                    options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.Authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
                    options.ClientId = clientId;
                    options.ClientSecret = clientSecret;
                    options.ResponseType = OpenIdConnectResponseType.Code;
                    options.CallbackPath = "/signin-oidc";
                    options.MapInboundClaims = false;
                    options.SaveTokens = false;
                    options.GetClaimsFromUserInfoEndpoint = false;
                    options.Scope.Clear();
                    options.Scope.Add("openid");
                    options.Scope.Add("profile");
                    options.Scope.Add("email");
                    options.TokenValidationParameters.NameClaimType = "name";
                }
            );

        services.AddAuthorization();
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            // Only the scheme is needed (https redirect URIs and cookies behind Railway's TLS proxy).
            options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
            // Railway's proxy address is not known in advance; without clearing, the headers from
            // non-loopback proxies would be ignored.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });
        return services;
    }

    public static void MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth").AllowAnonymous();

        auth.MapGet(
            "/login",
            (string? returnUrl) =>
                Results.Challenge(
                    new AuthenticationProperties { RedirectUri = SafeReturnUrl(returnUrl) },
                    [OpenIdConnectDefaults.AuthenticationScheme]
                )
        );

        // Clears only our cookie, not the Microsoft session. POST: SameSite=Lax blocks cross-site posts.
        auth.MapPost(
            "/logout",
            async (HttpContext http) =>
            {
                await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Results.NoContent();
            }
        );

        auth.MapGet(
            "/me",
            (ClaimsPrincipal user) =>
                user.Identity?.IsAuthenticated == true
                    ? Results.Ok(
                        new
                        {
                            name = user.FindFirstValue("name"),
                            email = user.FindFirstValue("email")
                                ?? user.FindFirstValue("preferred_username"),
                        }
                    )
                    : Results.Unauthorized()
        );
    }

    /// <summary>Only same-site relative paths: anything else would be an open redirect.</summary>
    public static string SafeReturnUrl(string? returnUrl) =>
        returnUrl is ['/', not ('/' or '\\'), ..] && !returnUrl.Any(char.IsControl) || returnUrl == "/"
            ? returnUrl
            : "/";

    private static string Required(IConfigurationSection section, string key) =>
        section[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException(
                $"Missing configuration 'Authentication:Microsoft:{key}' (see README, Entra)."
            );
}
