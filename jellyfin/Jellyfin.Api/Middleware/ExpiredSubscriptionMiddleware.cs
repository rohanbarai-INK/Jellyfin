using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Jellyfin.Api.Constants;
using Jellyfin.Api.Extensions;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Extensions;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Api.Middleware;

/// <summary>
/// Restricts API access for expired, non-admin users.
/// </summary>
public class ExpiredSubscriptionMiddleware
{
    private const string SubscriptionRedirectPath = "/web/#/subscription";
    private static readonly HashSet<string> _getWhitelist = new(StringComparer.OrdinalIgnoreCase)
    {
        "/Users/Me",
        "/System/Info",
        "/System/Info/Public",
        "/System/Configuration/subscription",
        "/System/Endpoint",
        "/Playback/BitrateTest",
        "/DisplayPreferences/usersettings",
        "/Branding/Configuration",
        "/QuickConnect/Enabled",
        "/UserViews",
        "/SyncPlay/List"
    };

    private static readonly HashSet<string> _postWhitelist = new(StringComparer.OrdinalIgnoreCase)
    {
        "/Sessions/Capabilities",
        "/Sessions/Capabilities/Full",
        "/Sessions/Logout",
        "/Keys/Redeem",
        "/Users/AuthenticateByName"
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExpiredSubscriptionMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpiredSubscriptionMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next delegate in the pipeline.</param>
    /// <param name="logger">The logger.</param>
    public ExpiredSubscriptionMiddleware(
        RequestDelegate next,
        ILogger<ExpiredSubscriptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Executes the middleware action.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="userManager">The user manager.</param>
    /// <returns>The async task.</returns>
    public async Task Invoke(HttpContext httpContext, IUserManager userManager)
    {
        var request = httpContext.Request;
        if (HttpMethods.IsOptions(request.Method)
            || IsWebSocketPath(request.Path))
        {
            await _next(httpContext).ConfigureAwait(false);
            return;
        }

        var principal = httpContext.User;
        if (principal.Identity?.IsAuthenticated != true)
        {
            await _next(httpContext).ConfigureAwait(false);
            return;
        }

        if (principal.GetIsApiKey()
            || principal.IsInRole(UserRoles.Administrator))
        {
            await _next(httpContext).ConfigureAwait(false);
            return;
        }

        var userId = principal.GetUserId();
        if (userId.IsEmpty())
        {
            await _next(httpContext).ConfigureAwait(false);
            return;
        }

        var user = userManager.GetUserById(userId);
        if (user is null || user.Status != UserStatus.Expired)
        {
            await _next(httpContext).ConfigureAwait(false);
            return;
        }

        if (IsWhitelistedRequest(request.Method, request.Path, userId))
        {
            await _next(httpContext).ConfigureAwait(false);
            return;
        }

        _logger.LogInformation(
            "Blocking request for expired user {UserId}. Method: {Method}, Path: {Path}.",
            userId,
            request.Method,
            request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
        httpContext.Response.ContentType = "application/json";

        await httpContext.Response.WriteAsJsonAsync(
            new
            {
                code = "SubscriptionExpired",
                message = "Subscription has expired. Only subscription renewal and logout endpoints are available.",
                redirectUrl = SubscriptionRedirectPath
            },
            httpContext.RequestAborted).ConfigureAwait(false);
    }

    private static bool IsWhitelistedRequest(string method, PathString path, Guid userId)
    {
        var normalizedPath = NormalizePath(path);
        if (string.IsNullOrEmpty(normalizedPath))
        {
            return false;
        }

        if (HttpMethods.IsGet(method))
        {
            return _getWhitelist.Contains(normalizedPath)
                || IsCurrentUserRoute(normalizedPath, userId);
        }

        if (HttpMethods.IsPost(method))
        {
            return _postWhitelist.Contains(normalizedPath);
        }

        return false;
    }

    private static bool IsWebSocketPath(PathString path)
    {
        var value = NormalizePath(path);
        return !string.IsNullOrEmpty(value)
               && value.StartsWith("/socket", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCurrentUserRoute(string normalizedPath, Guid userId)
    {
        if (string.IsNullOrEmpty(normalizedPath)
            || !normalizedPath.StartsWith("/Users/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segment = normalizedPath.AsSpan("/Users/".Length);
        var slashIndex = segment.IndexOf('/');
        if (slashIndex >= 0)
        {
            return false;
        }

        return Guid.TryParse(segment, out var routeUserId)
               && routeUserId.Equals(userId);
    }

    private static string NormalizePath(PathString path)
    {
        var value = path.Value;
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length > 1 && value.EndsWith('/')
            ? value.TrimEnd('/')
            : value;
    }
}
