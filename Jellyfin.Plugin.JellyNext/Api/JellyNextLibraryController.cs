using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.JellyNext.Configuration;
using Jellyfin.Plugin.JellyNext.Helpers;
using Jellyfin.Plugin.JellyNext.Models.Common;
using Jellyfin.Plugin.JellyNext.Services;
using Jellyfin.Plugin.JellyNext.Services.DownloadProviders;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyNext.Api;

/// <summary>
/// Exposes JellyNext discovery for the current Jellyfin user.
/// </summary>
[ApiController]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("[controller]")]
public class JellyNextLibraryController : ControllerBase
{
    private readonly IAuthorizationContext _authorization;
    private readonly ContentCacheService _cache;
    private readonly DownloadProviderFactory _downloads;
    private readonly TraktApi _trakt;
    private readonly IMemoryCache _publicPages;
    private readonly ILogger<JellyNextLibraryController> _logger;
    private readonly LocalLibraryService _library;
    private readonly IUserDataManager _userData;
    private readonly ArrQueueService _queue;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyNextLibraryController"/> class.
    /// </summary>
    /// <param name="authorization">Jellyfin authorization context.</param>
    /// <param name="cache">The content cache.</param>
    /// <param name="downloads">The download provider factory.</param>
    /// <param name="trakt">The Trakt client.</param>
    /// <param name="publicPages">Cache for the first pages of the public catalog.</param>
    /// <param name="logger">The controller logger.</param>
    /// <param name="library">The local library lookup service.</param>
    /// <param name="userData">The Jellyfin playback history service.</param>
    /// <param name="queue">The Radarr and Sonarr queue reader.</param>
    public JellyNextLibraryController(
        IAuthorizationContext authorization,
        ContentCacheService cache,
        DownloadProviderFactory downloads,
        TraktApi trakt,
        IMemoryCache publicPages,
        ILogger<JellyNextLibraryController> logger,
        LocalLibraryService library,
        IUserDataManager userData,
        ArrQueueService queue)
    {
        _authorization = authorization;
        _cache = cache;
        _downloads = downloads;
        _trakt = trakt;
        _publicPages = publicPages;
        _logger = logger;
        _library = library;
        _userData = userData;
        _queue = queue;
    }

    /// <summary>Gets the styles configured by the Jellyfin administrator.</summary>
    /// <returns>The JellyNext stylesheet.</returns>
    [HttpGet("Appearance")]
    public IActionResult GetAppearance()
        => Ok(new { css = Plugin.Instance?.Configuration.DiscoveryCustomCss ?? string.Empty });

    /// <summary>Gets title details and playback availability for the viewing user.</summary>
    /// <param name="type">Movie or show.</param>
    /// <param name="traktId">The title's Trakt ID.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <param name="season">Optional season to check for playback.</param>
    /// <param name="catalog">Whether to use the public show catalog.</param>
    /// <param name="progress">Whether to include the current provider queue status.</param>
    /// <returns>Title details, request permissions, and the playable item when available.</returns>
    [HttpGet("Details/{type}/{traktId:int:min(1)}")]
    public async Task<IActionResult> GetDetails(
        ContentType type,
        int traktId,
        CancellationToken cancellationToken,
        [FromQuery, Range(1, int.MaxValue)] int? season = null,
        [FromQuery] bool catalog = false,
        [FromQuery] bool progress = false)
    {
        var authorization = await _authorization.GetAuthorizationInfo(HttpContext);
        if (!authorization.IsAuthenticated || authorization.User is not { } viewingUser || !Enum.IsDefined(type))
        {
            return Forbid();
        }

        ContentItem? item;
        var seasons = Array.Empty<int>();
        var publicTitle = type == ContentType.Movie || catalog;
        if (publicTitle)
        {
            if (!CanBrowseCatalog(authorization))
            {
                return Forbid();
            }

            try
            {
                item = await GetPublicTitleAsync(type, traktId, cancellationToken);
                if (item is not null && type == ContentType.Show)
                {
                    seasons = await GetPublicSeasonsAsync(traktId, cancellationToken);
                }
            }
            catch (Exception exception) when (exception is HttpRequestException or JsonException)
            {
                _logger.LogWarning(exception, "Could not load catalog details for {Type} {TraktId}", type, traktId);
                return StatusCode(503);
            }
        }
        else
        {
            item = GetItems(authorization.UserId, type).FirstOrDefault(candidate => candidate.TraktId == traktId);
        }

        if (item is null)
        {
            return NotFound();
        }

        var playable = _library.FindPlayableContent(item, viewingUser, season, _userData);
        var unavailable = !publicTitle || CanRequestCatalog(authorization, type)
            ? GetDownloadConfigurationIssue(type) ?? GetDownloadIdentifierIssue(item)
            : "Ask an administrator to enable Discovery downloads for your Jellyfin user.";
        var queue = progress && unavailable is null && (type == ContentType.Show || playable.Item is null)
            ? await _queue.GetProgressAsync(item, season ?? 1, cancellationToken)
            : null;
        return Ok(new
        {
            title = item.Title,
            year = item.Year,
            posterUrl = item.PosterUrl,
            overview = item.Overview,
            genres = item.Genres,
            seasons,
            canDownload = unavailable is null,
            downloadUnavailableReason = unavailable,
            downloadProgress = queue is null ? null : new { source = queue.Source, state = queue.State, percent = queue.Percent },
            watchItemId = playable.Item?.Id,
            watchTitle = type == ContentType.Show ? playable.Item?.Name : null,
            positionTicks = playable.PositionTicks
        });
    }

    /// <summary>Gets the current user's movie recommendations.</summary>
    /// <param name="startIndex">Optional start index.</param>
    /// <param name="limit">Optional page size.</param>
    /// <returns>Movie recommendations.</returns>
    [HttpGet("Recommendations/Movies")]
    public Task<ActionResult<QueryResult<ContentItem>>> GetMovieRecommendations(int? startIndex, int? limit)
        => GetRecommendations(ContentType.Movie, startIndex, limit);

    /// <summary>Gets the current user's movie and show recommendations.</summary>
    /// <param name="startIndex">Optional start index.</param>
    /// <param name="limit">Optional page size.</param>
    /// <returns>Movie and show recommendations.</returns>
    [HttpGet("Recommendations")]
    public Task<ActionResult<QueryResult<ContentItem>>> GetAllRecommendations(int? startIndex, int? limit)
        => GetRecommendations(null, startIndex, limit);

    /// <summary>Gets one of the current user's recommendations by TMDB ID.</summary>
    /// <param name="tmdbId">The TMDB ID.</param>
    /// <returns>The matching recommendation.</returns>
    [HttpGet("Item/{tmdbId:int}")]
    public async Task<ActionResult<ContentItem>> GetItem(int tmdbId)
    {
        var userId = await GetUserId();
        var item = GetItems(userId, null).FirstOrDefault(candidate => candidate.TmdbId == tmdbId);
        return item is null ? NotFound() : Ok(item);
    }

    /// <summary>Browses or searches the public movie catalog.</summary>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <param name="page">One-based result page.</param>
    /// <param name="query">Optional movie title to search.</param>
    /// <param name="year">Optional release year when browsing.</param>
    /// <returns>One page of public movie listings.</returns>
    [HttpGet("MovieCatalog")]
    public Task<IActionResult> GetMovieCatalog(
        CancellationToken cancellationToken,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, StringLength(100)] string? query = null,
        [FromQuery, Range(1870, 2200)] int? year = null)
        => GetCatalog(ContentType.Movie, page, query, year, cancellationToken);

    /// <summary>Browses or searches the public TV catalog.</summary>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <param name="page">One-based result page.</param>
    /// <param name="query">Optional title search.</param>
    /// <param name="year">Optional year filter.</param>
    /// <returns>One page of public TV shows.</returns>
    [HttpGet("ShowCatalog")]
    public Task<IActionResult> GetShowCatalog(
        CancellationToken cancellationToken,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, StringLength(100)] string? query = null,
        [FromQuery, Range(1870, 2200)] int? year = null)
        => GetCatalog(ContentType.Show, page, query, year, cancellationToken);

    private async Task<IActionResult> GetCatalog(ContentType type, int page, string? query, int? year, CancellationToken cancellationToken)
    {
        var authorization = await _authorization.GetAuthorizationInfo(HttpContext);
        if (!CanBrowseCatalog(authorization))
        {
            return Forbid();
        }

        try
        {
            var (items, pageCount) = string.IsNullOrWhiteSpace(query) && !year.HasValue && page <= 25
                ? await _publicPages.GetOrCreateAsync(
                    ("JellyNext.Catalog", type, page),
                    entry =>
                    {
                        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                        return _trakt.GetCatalogAsync(type, page, null, null, cancellationToken);
                    })
                : await _trakt.GetCatalogAsync(type, page, query?.Trim(), year, cancellationToken);
            var canRequest = CanRequestCatalog(authorization, type) && IsDownloadConfigured(type);
            return Ok(new
            {
                page,
                pageCount,
                items = items.Select(item => new
                {
                    id = item.TraktId,
                    title = item.Title,
                    year = item.Year,
                    posterUrl = item.PosterUrl,
                    canRequest = canRequest && CanRequest(item)
                })
            });
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            _logger.LogWarning(exception, "Could not load the public {Type} catalog", type);
            return StatusCode(503);
        }
    }

    /// <summary>Requests a movie from the public catalog.</summary>
    /// <param name="traktId">The movie's Trakt ID.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The download request outcome.</returns>
    [HttpPost("MovieCatalog/{traktId:int:min(1)}/Request")]
    public Task<IActionResult> RequestCatalogMovie(int traktId, CancellationToken cancellationToken)
        => RequestCatalog(ContentType.Movie, traktId, 1, cancellationToken);

    /// <summary>Requests a season of a public catalog TV show.</summary>
    /// <param name="traktId">The show's Trakt ID.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <param name="season">The season number.</param>
    /// <returns>The download request outcome.</returns>
    [HttpPost("ShowCatalog/{traktId:int:min(1)}/Request")]
    public Task<IActionResult> RequestCatalogShow(int traktId, CancellationToken cancellationToken, [Range(1, int.MaxValue)] int season = 1)
        => RequestCatalog(ContentType.Show, traktId, season, cancellationToken);

    private async Task<IActionResult> RequestCatalog(ContentType type, int traktId, int season, CancellationToken cancellationToken)
    {
        var authorization = await _authorization.GetAuthorizationInfo(HttpContext);
        if (!CanBrowseCatalog(authorization) || !CanRequestCatalog(authorization, type))
        {
            return Forbid();
        }

        if (!IsDownloadConfigured(type))
        {
            return BadRequest();
        }

        try
        {
            var item = await GetPublicTitleAsync(type, traktId, cancellationToken);
            if (item is null)
            {
                return NotFound();
            }

            if (!CanRequest(item) || (type == ContentType.Show && !(await GetPublicSeasonsAsync(traktId, cancellationToken)).Contains(season)))
            {
                return BadRequest();
            }

            var provider = _downloads.GetProvider();
            var result = type == ContentType.Movie
                ? await provider.RequestMovieAsync(item, authorization.UserId.ToString())
                : await provider.RequestShowAsync(
                    item,
                    season,
                    authorization.UserId.ToString(),
                    item.Genres?.Contains("anime", StringComparer.OrdinalIgnoreCase) == true);
            return Ok(new { success = result.Success });
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            _logger.LogWarning(exception, "Could not request catalog {Type} {TraktId}", type, traktId);
            return StatusCode(503);
        }
    }

    /// <summary>Gets discovery cards for a single media type.</summary>
    /// <param name="type">The media type.</param>
    /// <returns>The current user's discovery cards and availability.</returns>
    [HttpGet("Discovery/{type}")]
    public async Task<IActionResult> GetDiscovery(ContentType type)
    {
        var userId = await GetUserId();
        if (userId == Guid.Empty || !Enum.IsDefined(type))
        {
            return BadRequest();
        }

        var user = UserHelper.GetTraktUser(userId, authorized: true);
        var enabled = user is not null && (type == ContentType.Movie ? user.SyncMovieRecommendations : user.SyncShowRecommendations);
        var canRequest = IsDownloadConfigured(type);
        var items = enabled ? GetItems(userId, type) : Array.Empty<ContentItem>();

        return Ok(new
        {
            linked = user is not null,
            enabled,
            items = items.Select(item => new
            {
                id = item.TraktId,
                title = item.Title,
                year = item.Year,
                posterUrl = item.PosterUrl,
                canRequest = canRequest && CanRequest(item),
                seasons = type == ContentType.Show && user?.LimitShowsToSeasonOne == false
                    ? Math.Clamp(item.AiredSeasonCount ?? 1, 1, 10)
                    : 1
            })
        });
    }

    /// <summary>Requests one of the current user's recommendations.</summary>
    /// <param name="type">The media type.</param>
    /// <param name="traktId">The recommendation's Trakt ID.</param>
    /// <param name="season">The season to request for a show.</param>
    /// <returns>The request outcome.</returns>
    [HttpPost("Discovery/{type}/{traktId:int}/Request")]
    public async Task<IActionResult> RequestDiscovery(ContentType type, int traktId, [FromQuery] int season = 1)
    {
        var userId = await GetUserId();
        var user = UserHelper.GetTraktUser(userId, authorized: true);
        if (user is null)
        {
            return Forbid();
        }

        if (!Enum.IsDefined(type) || (type == ContentType.Movie ? !user.SyncMovieRecommendations : !user.SyncShowRecommendations))
        {
            return NotFound();
        }

        var item = GetItems(userId, type).FirstOrDefault(candidate => candidate.TraktId == traktId);
        if (item is null)
        {
            return NotFound();
        }

        var maxSeason = user.LimitShowsToSeasonOne ? 1 : Math.Clamp(item.AiredSeasonCount ?? 1, 1, 10);
        if (!IsDownloadConfigured(type) || !CanRequest(item) || season < 1 || season > maxSeason)
        {
            return BadRequest();
        }

        var provider = _downloads.GetProvider();
        var result = type == ContentType.Movie
            ? await provider.RequestMovieAsync(item, userId.ToString())
            : await provider.RequestShowAsync(
                item,
                season,
                userId.ToString(),
                item.Genres?.Contains("anime", StringComparer.OrdinalIgnoreCase) == true);

        return Ok(new { success = result.Success });
    }

    private async Task<Guid> GetUserId()
    {
        var authorization = await _authorization.GetAuthorizationInfo(HttpContext);
        return authorization.IsAuthenticated ? authorization.UserId : Guid.Empty;
    }

    private Task<ContentItem?> GetPublicTitleAsync(ContentType type, int traktId, CancellationToken cancellationToken)
        => _publicPages.GetOrCreateAsync(
            ("JellyNext.CatalogDetails", type, traktId),
            entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                return _trakt.GetCatalogTitleAsync(type, traktId, cancellationToken);
            });

    private static bool CanBrowseCatalog(AuthorizationInfo authorization)
        => authorization.IsAuthenticated && authorization.User is { MaxParentalRatingScore: null };

    private static bool CanRequestCatalog(AuthorizationInfo authorization, ContentType type)
    {
        var linked = UserHelper.GetTraktUser(authorization.UserId, authorized: true);
        return authorization.User?.HasPermission(PermissionKind.IsAdministrator) == true
            || Plugin.Instance?.Configuration.CatalogRequestUsers?.Contains(authorization.UserId) == true
            || (type == ContentType.Movie ? linked?.SyncMovieRecommendations == true : linked?.SyncShowRecommendations == true);
    }

    private async Task<int[]> GetPublicSeasonsAsync(int traktId, CancellationToken cancellationToken)
    {
        var seasons = await _publicPages.GetOrCreateAsync(
            ("JellyNext.CatalogSeasons", traktId),
            entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                return _trakt.GetCatalogSeasonsAsync(traktId, cancellationToken);
            });
        return seasons is { Length: > 0 } ? seasons : new[] { 1 };
    }

    private ContentItem[] GetItems(Guid userId, ContentType? type)
    {
        if (UserHelper.GetTraktUser(userId, authorized: true) is not { } user)
        {
            return Array.Empty<ContentItem>();
        }

        return _cache.GetCachedContent(userId, "recommendations")
            .Where(item => (type is null || item.Type == type)
                && (item.Type == ContentType.Movie ? user.SyncMovieRecommendations : user.SyncShowRecommendations))
            .ToArray();
    }

    private async Task<ActionResult<QueryResult<ContentItem>>> GetRecommendations(ContentType? type, int? startIndex, int? limit)
    {
        var items = GetItems(await GetUserId(), type);
        return Ok(new QueryResult<ContentItem>
        {
            Items = items.Skip(startIndex ?? 0).Take(limit ?? items.Length).ToArray(),
            TotalRecordCount = items.Length
        });
    }

    private static bool IsDownloadConfigured(ContentType type) => GetDownloadConfigurationIssue(type) is null;

    private static string? GetDownloadConfigurationIssue(ContentType type)
    {
        var config = Plugin.Instance?.Configuration;
        return config?.DownloadIntegration switch
        {
            DownloadIntegrationType.Jellyseerr => HasValues(config.JellyseerrUrl, config.JellyseerrApiKey)
                ? null : "Set the Jellyseerr URL and API key in JellyNext Settings > Downloads.",
            DownloadIntegrationType.Webhook => HasValues(type == ContentType.Movie ? config.WebhookMovieUrl : config.WebhookShowUrl)
                ? null : "Set the webhook URL for this media type in JellyNext Settings > Downloads.",
            DownloadIntegrationType.Native when type == ContentType.Movie =>
                config.RadarrQualityProfileId is > 0 && HasValues(config.RadarrUrl, config.RadarrApiKey, config.RadarrRootFolderPath)
                    ? null : "Set the Radarr URL, API key, quality profile, and root folder in JellyNext Settings > Downloads.",
            DownloadIntegrationType.Native =>
                config.SonarrQualityProfileId is > 0 && HasValues(config.SonarrUrl, config.SonarrApiKey, config.SonarrRootFolderPath)
                    ? null : "Set the Sonarr URL, API key, quality profile, and root folder in JellyNext Settings > Downloads.",
            _ => "Select a provider in JellyNext Settings > Downloads."
        };
    }

    private static bool CanRequest(ContentItem item) => GetDownloadIdentifierIssue(item) is null;

    private static string? GetDownloadIdentifierIssue(ContentItem item)
    {
        if (item.TraktId <= 0)
        {
            return "This title is missing its catalog ID.";
        }

        return Plugin.Instance?.Configuration.DownloadIntegration switch
        {
            DownloadIntegrationType.Webhook => null,
            DownloadIntegrationType.Native when item.Type == ContentType.Show =>
                item.TvdbId is > 0 ? null : "Sonarr needs a TVDB ID for this show.",
            _ => item.TmdbId is > 0 ? null : "Your download provider needs a TMDB ID for this title."
        };
    }

    private static bool HasValues(params string[] values)
        => values.All(value => !string.IsNullOrWhiteSpace(value));
}
