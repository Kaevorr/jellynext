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

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyNextLibraryController"/> class.
    /// </summary>
    /// <param name="authorization">Jellyfin authorization context.</param>
    /// <param name="cache">The content cache.</param>
    /// <param name="downloads">The download provider factory.</param>
    /// <param name="trakt">The Trakt client.</param>
    /// <param name="publicPages">Cache for the first pages of the public catalog.</param>
    /// <param name="logger">The controller logger.</param>
    public JellyNextLibraryController(
        IAuthorizationContext authorization,
        ContentCacheService cache,
        DownloadProviderFactory downloads,
        TraktApi trakt,
        IMemoryCache publicPages,
        ILogger<JellyNextLibraryController> logger)
    {
        _authorization = authorization;
        _cache = cache;
        _downloads = downloads;
        _trakt = trakt;
        _publicPages = publicPages;
        _logger = logger;
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
    public async Task<IActionResult> GetMovieCatalog(
        CancellationToken cancellationToken,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, StringLength(100)] string? query = null,
        [FromQuery, Range(1870, 2200)] int? year = null)
    {
        var authorization = await _authorization.GetAuthorizationInfo(HttpContext);
        if (!CanBrowseCatalog(authorization))
        {
            return Forbid();
        }

        try
        {
            var (movies, pageCount) = string.IsNullOrWhiteSpace(query) && !year.HasValue && page <= 25
                ? await _publicPages.GetOrCreateAsync(
                    ("JellyNext.MovieCatalog", page),
                    entry =>
                    {
                        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                        return _trakt.GetMovieCatalogAsync(page, null, null, cancellationToken);
                    })
                : await _trakt.GetMovieCatalogAsync(page, query?.Trim(), year, cancellationToken);
            var canRequest = CanRequestCatalog(authorization) && IsDownloadConfigured(ContentType.Movie);
            return Ok(new
            {
                page,
                pageCount,
                items = movies.Where(movie => movie.Ids?.Trakt > 0).Select(movie =>
                {
                    var item = TraktApi.ToMovieContent(movie, "catalog");
                    return new
                    {
                        id = item.TraktId,
                        title = item.Title,
                        year = item.Year,
                        posterUrl = item.PosterUrl,
                        canRequest = canRequest && CanRequest(item)
                    };
                })
            });
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            _logger.LogWarning(exception, "Could not load the public movie catalog");
            return StatusCode(503);
        }
    }

    /// <summary>Requests a movie from the public catalog.</summary>
    /// <param name="traktId">The movie's Trakt ID.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The download request outcome.</returns>
    [HttpPost("MovieCatalog/{traktId:int:min(1)}/Request")]
    public async Task<IActionResult> RequestCatalogMovie(int traktId, CancellationToken cancellationToken)
    {
        var authorization = await _authorization.GetAuthorizationInfo(HttpContext);
        if (!CanBrowseCatalog(authorization) || !CanRequestCatalog(authorization))
        {
            return Forbid();
        }

        if (!IsDownloadConfigured(ContentType.Movie))
        {
            return BadRequest();
        }

        try
        {
            var movie = await _trakt.GetCatalogMovieAsync(traktId, cancellationToken);
            if (movie?.Ids?.Trakt != traktId)
            {
                return NotFound();
            }

            var item = TraktApi.ToMovieContent(movie, "catalog");
            if (!CanRequest(item))
            {
                return BadRequest();
            }

            var result = await _downloads.GetProvider().RequestMovieAsync(item, authorization.UserId.ToString());
            return Ok(new { success = result.Success });
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            _logger.LogWarning(exception, "Could not request catalog movie {TraktId}", traktId);
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

    private static bool CanBrowseCatalog(AuthorizationInfo authorization)
        => authorization.IsAuthenticated && authorization.User is { MaxParentalRatingScore: null };

    private static bool CanRequestCatalog(AuthorizationInfo authorization)
        => authorization.User?.HasPermission(PermissionKind.IsAdministrator) == true
            || UserHelper.GetTraktUser(authorization.UserId, authorized: true)?.SyncMovieRecommendations == true;

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

    private static bool IsDownloadConfigured(ContentType type)
    {
        var config = Plugin.Instance?.Configuration;
        return config?.DownloadIntegration switch
        {
            DownloadIntegrationType.Jellyseerr => HasValues(config.JellyseerrUrl, config.JellyseerrApiKey),
            DownloadIntegrationType.Webhook => HasValues(type == ContentType.Movie ? config.WebhookMovieUrl : config.WebhookShowUrl),
            DownloadIntegrationType.Native when type == ContentType.Movie =>
                config.RadarrQualityProfileId.HasValue && HasValues(config.RadarrUrl, config.RadarrApiKey, config.RadarrRootFolderPath),
            DownloadIntegrationType.Native =>
                config.SonarrQualityProfileId.HasValue && HasValues(config.SonarrUrl, config.SonarrApiKey, config.SonarrRootFolderPath),
            _ => false
        };
    }

    private static bool CanRequest(ContentItem item)
        => item.TraktId > 0 && Plugin.Instance?.Configuration.DownloadIntegration switch
        {
            DownloadIntegrationType.Jellyseerr => item.TmdbId is > 0,
            DownloadIntegrationType.Webhook => true,
            _ => item.Type == ContentType.Movie ? item.TmdbId is > 0 : item.TvdbId is > 0
        };

    private static bool HasValues(params string[] values)
        => values.All(value => !string.IsNullOrWhiteSpace(value));
}
