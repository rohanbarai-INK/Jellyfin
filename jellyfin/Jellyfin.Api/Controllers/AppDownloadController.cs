using System.Threading.Tasks;
using Jellyfin.Api.Extensions;
using Jellyfin.Api.Models.AppDownloadDtos;
using Jellyfin.Extensions;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.AppDownload;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Api.Controllers;

/// <summary>
/// App download configuration API controller.
/// </summary>
[Route("AppDownload")]
public class AppDownloadController : BaseJellyfinApiController
{
    private readonly IAppDownloadService _appDownloadService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppDownloadController"/> class.
    /// </summary>
    /// <param name="appDownloadService">App download service.</param>
    public AppDownloadController(IAppDownloadService appDownloadService)
    {
        _appDownloadService = appDownloadService;
    }

    /// <summary>
    /// Gets the current app download configuration.
    /// </summary>
    /// <returns>App download config.</returns>
    [HttpGet("Config")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<AppDownloadConfigDto>> GetConfig()
    {
        var info = await _appDownloadService.GetConfigAsync().ConfigureAwait(false);
        return ToDto(info);
    }

    /// <summary>
    /// Saves the app download configuration. Admin only.
    /// </summary>
    /// <param name="request">Updated config values.</param>
    /// <returns>Saved config.</returns>
    [HttpPost("Config")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AppDownloadConfigDto>> SaveConfig([FromBody] SaveAppDownloadConfigRequest request)
    {
        if (request is null)
        {
            return BadRequest("Request payload is required.");
        }

        var actorUserId = User.GetUserId();
        if (actorUserId.IsEmpty())
        {
            return BadRequest("User is not authenticated.");
        }

        var info = new AppDownloadConfigInfo
        {
            MobileApkUrl = request.MobileApkUrl,
            MobileApkFileName = request.MobileApkFileName,
            MobileIsNew = request.MobileIsNew,
            TvApkUrl = request.TvApkUrl,
            TvApkFileName = request.TvApkFileName,
            TvIsNew = request.TvIsNew,
            MaxNewInteractions = request.MaxNewInteractions > 0 ? request.MaxNewInteractions : 3
        };

        var saved = await _appDownloadService.SaveConfigAsync(info, actorUserId).ConfigureAwait(false);
        return ToDto(saved);
    }

    private static AppDownloadConfigDto ToDto(AppDownloadConfigInfo info)
        => new()
        {
            MobileApkUrl = info.MobileApkUrl,
            MobileApkFileName = info.MobileApkFileName,
            MobileIsNew = info.MobileIsNew,
            TvApkUrl = info.TvApkUrl,
            TvApkFileName = info.TvApkFileName,
            TvIsNew = info.TvIsNew,
            MaxNewInteractions = info.MaxNewInteractions,
            UpdatedAtUtc = info.UpdatedAtUtc,
            UpdatedByUsername = info.UpdatedByUsername
        };
}
