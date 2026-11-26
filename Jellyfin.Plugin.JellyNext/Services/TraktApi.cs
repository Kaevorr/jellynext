using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.JellyNext.Models.Common;
using Jellyfin.Plugin.JellyNext.Models.Radarr;
using Jellyfin.Plugin.JellyNext.Models.Sonarr;
using Jellyfin.Plugin.JellyNext.Models.Trakt;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyNext.Services;

/// <summary>
/// Service for interacting with the Trakt API.
/// </summary>
public class TraktApi
{
    private const string TraktApiBaseUrl = "https://api.trakt.tv";
    private const string DeviceCodeEndpoint = "/oauth/device/code";
    private const string DeviceTokenEndpoint = "/oauth/device/token";
    private const string RefreshTokenEndpoint = "/oauth/token";

    private const string TraktClientId = "2c2621eef7a2c82a221f7a03c65bfa0088555699ebeb4cefe1ee2490c8245864";
    private const string TraktClientSecret = "d2fa3baeafd861a7e024cbb53b0b9bf2451db8f56e8ca8ee650324963c2c967c";

    private readonly ILogger<TraktApi> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="TraktApi"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    public TraktApi(ILogger<TraktApi> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
    }

    /// <summary>
    /// Initiates the OAuth device authorization flow.
    /// </summary>
    /// <param name="traktUser">The Trakt user configuration.</param>
    /// <returns>The user code to display to the user.</returns>
    public async Task<string> AuthorizeDevice(TraktUser traktUser)
    {
        var request = new { client_id = TraktClientId };

        using var httpClient = _httpClientFactory.CreateClient(NamedClient.Default);
        httpClient.BaseAddress = new Uri(TraktApiBaseUrl);
        httpClient.DefaultRequestHeaders.Add("trakt-api-version", "2");
        httpClient.DefaultRequestHeaders.Add("trakt-api-key", TraktClientId);

        var response = await httpClient.PostAsJsonAsync(DeviceCodeEndpoint, request);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Trakt API error: Status={Status}, Content={Content}", response.StatusCode, errorContent);
        }

        response.EnsureSuccessStatusCode();

        var deviceCode = await response.Content.ReadFromJsonAsync<TraktDeviceCode>(_jsonOptions);
        if (deviceCode == null)
        {
            throw new InvalidOperationException("Failed to obtain device code from Trakt");
        }

        // Start background polling task and track it
        var pollingTask = Task.Run(() => PollForAccessToken(deviceCode, traktUser));
        Plugin.Instance?.PollingTasks.TryAdd(traktUser.LinkedMbUserId, pollingTask);

        return deviceCode.UserCode;
    }

    /// <summary>
    /// Polls Trakt for access token completion.
    /// </summary>
    /// <param name="deviceCode">The device code from the initial authorization.</param>
    /// <param name="traktUser">The Trakt user configuration.</param>
    /// <returns>True if authorization succeeded, false otherwise.</returns>
    public async Task<bool> PollForAccessToken(TraktDeviceCode deviceCode, TraktUser traktUser)
    {
        var request = new
        {
            code = deviceCode.DeviceCode,
            client_id = TraktClientId,
            client_secret = TraktClientSecret
        };

        var pollingInterval = deviceCode.Interval;
        var expiresAt = DateTime.UtcNow.AddSeconds(deviceCode.ExpiresIn);

        using var httpClient = _httpClientFactory.CreateClient(NamedClient.Default);
        httpClient.BaseAddress = new Uri(TraktApiBaseUrl);
        httpClient.DefaultRequestHeaders.Add("trakt-api-version", "2");
        httpClient.DefaultRequestHeaders.Add("trakt-api-key", TraktClientId);

        while (DateTime.UtcNow < expiresAt)
        {
            try
            {
                var response = await httpClient.PostAsJsonAsync(DeviceTokenEndpoint, request);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var accessToken = await response.Content.ReadFromJsonAsync<TraktUserAccessToken>(_jsonOptions);
                    if (accessToken != null)
                    {
                        traktUser.AccessToken = accessToken.AccessToken;
                        traktUser.RefreshToken = accessToken.RefreshToken;
                        traktUser.AccessTokenExpiration = DateTime.Now.AddSeconds(accessToken.ExpirationWithBuffer);

                        Plugin.Instance?.SaveConfiguration();
                        _logger.LogInformation("Successfully authorized Trakt user {UserId}", traktUser.LinkedMbUserId);
                        return true;
                    }
                }
                else if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    // Still pending user authorization
                    _logger.LogDebug("Waiting for user to authorize device...");
                }
                else if (response.StatusCode == (HttpStatusCode)418)
                {
                    // User denied authorization
                    _logger.LogWarning("User denied Trakt authorization");
                    return false;
                }
                else if (response.StatusCode == HttpStatusCode.Gone)
                {
                    // Device code expired
                    _logger.LogWarning("Trakt device code expired");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling for Trakt access token");
            }

            await Task.Delay(pollingInterval * 1000);
        }

        _logger.LogWarning("Trakt authorization timed out");
        return false;
    }

    /// <summary>
    /// Refreshes the user's access token using the refresh token.
    /// </summary>
    /// <param name="traktUser">The Trakt user configuration.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RefreshUserAccessToken(TraktUser traktUser)
    {
        if (string.IsNullOrWhiteSpace(traktUser.RefreshToken))
        {
            _logger.LogError("Attempted to refresh Trakt token but no refresh token was available");
            return;
        }

        var request = new TraktUserRefreshTokenRequest
        {
            RefreshToken = traktUser.RefreshToken,
            ClientId = TraktClientId,
            ClientSecret = TraktClientSecret
        };

        try
        {
            using var httpClient = _httpClientFactory.CreateClient(NamedClient.Default);
            httpClient.BaseAddress = new Uri(TraktApiBaseUrl);
            httpClient.DefaultRequestHeaders.Add("trakt-api-version", "2");
            httpClient.DefaultRequestHeaders.Add("trakt-api-key", TraktClientId);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "JellyNext/1.0");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            var response = await httpClient.PostAsJsonAsync(RefreshTokenEndpoint, request);
            response.EnsureSuccessStatusCode();

            var accessToken = await response.Content.ReadFromJsonAsync<TraktUserAccessToken>(_jsonOptions);
            if (accessToken != null)
            {
                traktUser.AccessToken = accessToken.AccessToken;
                traktUser.RefreshToken = accessToken.RefreshToken;
                traktUser.AccessTokenExpiration = DateTime.Now.AddSeconds(accessToken.ExpirationWithBuffer);

                Plugin.Instance?.SaveConfiguration();
                _logger.LogInformation("Successfully refreshed Trakt access token for user {UserId}", traktUser.LinkedMbUserId);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to refresh Trakt access token");
        }
    }

    /// <summary>
    /// Ensures the user's access token is valid, refreshing if necessary.
    /// </summary>
    /// <param name="traktUser">The Trakt user configuration.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task EnsureValidAccessToken(TraktUser traktUser)
    {
        if (DateTime.Now >= traktUser.AccessTokenExpiration)
        {
            traktUser.AccessToken = string.Empty;
            await RefreshUserAccessToken(traktUser);
        }
    }

    /// <summary>
    /// Creates an HTTP client with proper Trakt API headers.
    /// </summary>
    /// <param name="traktUser">The Trakt user configuration (optional, for authenticated requests).</param>
    /// <returns>Configured HTTP client.</returns>
    public async Task<HttpClient> CreateTraktClient(TraktUser? traktUser = null)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(TraktApiBaseUrl);
        httpClient.DefaultRequestHeaders.Add("trakt-api-version", "2");
        httpClient.DefaultRequestHeaders.Add("trakt-api-key", TraktClientId);
        httpClient.DefaultRequestHeaders.Add("User-Agent", "JellyNext/1.0");
        httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

        if (traktUser != null)
        {
            await EnsureValidAccessToken(traktUser);

            if (!string.IsNullOrEmpty(traktUser.AccessToken))
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", traktUser.AccessToken);
            }
        }

        return httpClient;
    }

    /// <summary>Browses or searches the public movie or show catalog.</summary>
    /// <param name="type">Movie or show.</param>
    /// <param name="page">One-based result page.</param>
    /// <param name="query">Title to search for, or null to browse.</param>
    /// <param name="year">Release year to browse.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Catalog items and the number of available pages.</returns>
    public async Task<(ContentItem[] Items, int PageCount)> GetCatalogAsync(
        ContentType type,
        int page,
        string? query,
        int? year,
        CancellationToken cancellationToken)
    {
        const int pageSize = 24;
        var movie = type == ContentType.Movie;
        var parameters = $"page={page}&limit={pageSize}&extended=images";
        var searching = !string.IsNullOrWhiteSpace(query);
        var path = searching
            ? $"/search/{(movie ? "movie" : "show")}?{parameters}&fields=title&query={Uri.EscapeDataString(query!)}"
            : $"/{(movie ? "movies" : "shows")}/popular?{parameters}";
        if (!searching && year.HasValue)
        {
            path += $"&years={year.Value}";
        }

        using var client = await CreateTraktClient();
        using var response = await client.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();

        ContentItem[] items;
        if (searching)
        {
            var results = await response.Content.ReadFromJsonAsync<TraktCatalogSearchResult[]>(_jsonOptions, cancellationToken)
                ?? Array.Empty<TraktCatalogSearchResult>();
            items = movie
                ? results.Where(result => result.Movie?.Ids?.Trakt > 0).Select(result => ToMovieContent(result.Movie!, "catalog")).ToArray()
                : results.Where(result => result.Show?.Ids?.Trakt > 0).Select(result => ToShowContent(result.Show!, "catalog")).ToArray();
        }
        else if (movie)
        {
            var movies = await response.Content.ReadFromJsonAsync<TraktMovie[]>(_jsonOptions, cancellationToken)
                ?? Array.Empty<TraktMovie>();
            items = movies.Where(item => item.Ids?.Trakt > 0).Select(item => ToMovieContent(item, "catalog")).ToArray();
        }
        else
        {
            var shows = await response.Content.ReadFromJsonAsync<TraktShow[]>(_jsonOptions, cancellationToken)
                ?? Array.Empty<TraktShow>();
            items = shows.Where(item => item.Ids?.Trakt > 0).Select(item => ToShowContent(item, "catalog")).ToArray();
        }

        var pageCount = response.Headers.TryGetValues("X-Pagination-Page-Count", out var values)
            && int.TryParse(values.FirstOrDefault(), out var count)
            ? count
            : page;
        return (items, Math.Max(0, pageCount));
    }

    /// <summary>Gets a public catalog title by its Trakt ID.</summary>
    /// <param name="type">Movie or show.</param>
    /// <param name="traktId">Title's Trakt ID.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The requested title, if found.</returns>
    public async Task<ContentItem?> GetCatalogTitleAsync(ContentType type, int traktId, CancellationToken cancellationToken)
    {
        var movie = type == ContentType.Movie;
        using var client = await CreateTraktClient();
        using var response = await client.GetAsync($"/{(movie ? "movies" : "shows")}/{traktId}?extended=full", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        if (movie)
        {
            var result = await response.Content.ReadFromJsonAsync<TraktMovie>(_jsonOptions, cancellationToken);
            return result?.Ids?.Trakt == traktId ? ToMovieContent(result, "catalog") : null;
        }

        var show = await response.Content.ReadFromJsonAsync<TraktShow>(_jsonOptions, cancellationToken);
        return show?.Ids?.Trakt == traktId ? ToShowContent(show, "catalog") : null;
    }

    /// <summary>Gets season numbers for a public catalog show.</summary>
    /// <param name="traktId">The show's Trakt ID.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>Numbered seasons, excluding specials.</returns>
    public async Task<int[]> GetCatalogSeasonsAsync(int traktId, CancellationToken cancellationToken)
    {
        using var client = await CreateTraktClient();
        using var response = await client.GetAsync($"/shows/{traktId}/seasons", cancellationToken);
        response.EnsureSuccessStatusCode();
        var seasons = await response.Content.ReadFromJsonAsync<TraktSeason[]>(_jsonOptions, cancellationToken);
        return seasons?.Where(season => season.Number > 0).Select(season => season.Number).Distinct().Order().ToArray()
            ?? Array.Empty<int>();
    }

    /// <summary>
    /// Gets personalized movie recommendations for a user.
    /// </summary>
    /// <param name="traktUser">The Trakt user configuration.</param>
    /// <param name="ignoreCollected">Whether to ignore collected movies.</param>
    /// <param name="ignoreWatchlisted">Whether to ignore watchlisted movies.</param>
    /// <param name="limit">Maximum number of recommendations to return (default: 10, max: 100).</param>
    /// <returns>List of recommended movies.</returns>
    public async Task<TraktMovie[]> GetMovieRecommendations(
        TraktUser traktUser,
        bool ignoreCollected = true,
        bool ignoreWatchlisted = false,
        int limit = 10)
    {
        var queryParams = $"?limit={limit}&extended=full,images";
        if (ignoreCollected)
        {
            queryParams += "&ignore_collected=true";
        }

        if (ignoreWatchlisted)
        {
            queryParams += "&ignore_watchlisted=true";
        }

        using var httpClient = await CreateTraktClient(traktUser);
        var response = await httpClient.GetAsync($"/recommendations/movies{queryParams}");

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Failed to get movie recommendations: Status={Status}, Content={Content}",
                response.StatusCode,
                errorContent);
            return Array.Empty<TraktMovie>();
        }

        var movies = await response.Content.ReadFromJsonAsync<TraktMovie[]>(_jsonOptions);
        return movies ?? Array.Empty<TraktMovie>();
    }

    /// <summary>
    /// Gets personalized show recommendations for a user.
    /// </summary>
    /// <param name="traktUser">The Trakt user configuration.</param>
    /// <param name="ignoreCollected">Whether to ignore collected shows.</param>
    /// <param name="ignoreWatchlisted">Whether to ignore watchlisted shows.</param>
    /// <param name="limit">Maximum number of recommendations to return (default: 10, max: 100).</param>
    /// <returns>List of recommended shows.</returns>
    public async Task<TraktShow[]> GetShowRecommendations(
        TraktUser traktUser,
        bool ignoreCollected = true,
        bool ignoreWatchlisted = false,
        int limit = 10)
    {
        var queryParams = $"?limit={limit}&extended=full,images";
        if (ignoreCollected)
        {
            queryParams += "&ignore_collected=true";
        }

        if (ignoreWatchlisted)
        {
            queryParams += "&ignore_watchlisted=true";
        }

        using var httpClient = await CreateTraktClient(traktUser);
        var response = await httpClient.GetAsync($"/recommendations/shows{queryParams}");

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Failed to get show recommendations: Status={Status}, Content={Content}",
                response.StatusCode,
                errorContent);
            return Array.Empty<TraktShow>();
        }

        var shows = await response.Content.ReadFromJsonAsync<TraktShow[]>(_jsonOptions);
        return shows ?? Array.Empty<TraktShow>();
    }

    /// <summary>
    /// Gets the user's watched shows with season/episode progress.
    /// </summary>
    /// <param name="traktUser">The Trakt user configuration.</param>
    /// <returns>List of watched shows with progress information.</returns>
    public async Task<TraktWatchedShow[]> GetWatchedShows(TraktUser traktUser)
    {
        using var httpClient = await CreateTraktClient(traktUser);
        var response = await httpClient.GetAsync("/sync/watched/shows?extended=full");

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Failed to get watched shows: Status={Status}, Content={Content}",
                response.StatusCode,
                errorContent);
            return Array.Empty<TraktWatchedShow>();
        }

        var watchedShows = await response.Content.ReadFromJsonAsync<TraktWatchedShow[]>(_jsonOptions);
        return watchedShows ?? Array.Empty<TraktWatchedShow>();
    }

    /// <summary>
    /// Gets all seasons for a show by Trakt ID.
    /// </summary>
    /// <param name="traktUser">The Trakt user configuration.</param>
    /// <param name="traktId">The Trakt show ID.</param>
    /// <returns>List of seasons for the show.</returns>
    public async Task<TraktSeason[]> GetShowSeasons(TraktUser traktUser, int traktId)
    {
        using var httpClient = await CreateTraktClient(traktUser);
        var response = await httpClient.GetAsync($"/shows/{traktId}/seasons?extended=full");

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Failed to get show seasons for Trakt ID {TraktId}: Status={Status}, Content={Content}",
                traktId,
                response.StatusCode,
                errorContent);
            return Array.Empty<TraktSeason>();
        }

        var responseContent = await response.Content.ReadAsStringAsync();

        TraktSeason[]? seasons = null;
        try
        {
            seasons = System.Text.Json.JsonSerializer.Deserialize<TraktSeason[]>(responseContent, _jsonOptions);
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize Trakt seasons for show {TraktId}", traktId);
            return Array.Empty<TraktSeason>();
        }

        return seasons ?? Array.Empty<TraktSeason>();
    }

    /// <summary>
    /// Gets trending movies.
    /// </summary>
    /// <param name="traktUser">The Trakt user configuration (for auth).</param>
    /// <param name="limit">Maximum number of trending movies to return (default: 10, max: 100).</param>
    /// <returns>List of trending movies.</returns>
    public async Task<TraktMovie[]> GetTrendingMovies(TraktUser traktUser, int limit = 10)
    {
        var queryParams = $"?limit={limit}&extended=full";

        using var httpClient = await CreateTraktClient(traktUser);
        var response = await httpClient.GetAsync($"/movies/trending{queryParams}");

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Failed to get trending movies: Status={Status}, Content={Content}",
                response.StatusCode,
                errorContent);
            return Array.Empty<TraktMovie>();
        }

        // Trending endpoint returns an array of objects with a "movie" property
        var trendingItems = await response.Content.ReadFromJsonAsync<TraktTrendingMovieItem[]>(_jsonOptions);
        if (trendingItems == null)
        {
            return Array.Empty<TraktMovie>();
        }

        // Extract the movie objects from the trending items
        return trendingItems.Select(item => item.Movie).ToArray();
    }

    /// <summary>
    /// Gets the user's movie watchlist.
    /// </summary>
    /// <param name="traktUser">The Trakt user configuration.</param>
    /// <returns>List of watchlisted movies.</returns>
    public async Task<TraktMovie[]> GetMovieWatchlist(TraktUser traktUser)
    {
        using var httpClient = await CreateTraktClient(traktUser);
        var response = await httpClient.GetAsync("/sync/watchlist/movies?extended=full");

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Failed to get movie watchlist: Status={Status}, Content={Content}",
                response.StatusCode,
                errorContent);
            return Array.Empty<TraktMovie>();
        }

        var watchlistItems = await response.Content.ReadFromJsonAsync<TraktWatchlistMovieItem[]>(_jsonOptions);
        if (watchlistItems == null)
        {
            return Array.Empty<TraktMovie>();
        }

        return watchlistItems.Select(item => item.Movie).ToArray();
    }

    /// <summary>
    /// Gets the user's show watchlist.
    /// </summary>
    /// <param name="traktUser">The Trakt user configuration.</param>
    /// <returns>List of watchlisted shows.</returns>
    public async Task<TraktShow[]> GetShowWatchlist(TraktUser traktUser)
    {
        using var httpClient = await CreateTraktClient(traktUser);
        var response = await httpClient.GetAsync("/sync/watchlist/shows?extended=full");

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Failed to get show watchlist: Status={Status}, Content={Content}",
                response.StatusCode,
                errorContent);
            return Array.Empty<TraktShow>();
        }

        var watchlistItems = await response.Content.ReadFromJsonAsync<TraktWatchlistShowItem[]>(_jsonOptions);
        if (watchlistItems == null)
        {
            return Array.Empty<TraktShow>();
        }

        return watchlistItems.Select(item => item.Show).ToArray();
    }

    /// <summary>
    /// Gets watch history for shows with automatic pagination and date filtering.
    /// Fetches all pages automatically until no more results are available.
    /// </summary>
    /// <param name="traktUser">The Trakt user configuration.</param>
    /// <param name="startAt">Start of history window (ISO 8601, optional).</param>
    /// <param name="endAt">End of history window (ISO 8601, optional).</param>
    /// <param name="limit">Number of items per page (default: 100, max: 100).</param>
    /// <returns>List of all watch history items across all pages.</returns>
    public async Task<TraktHistoryItem[]> GetShowWatchHistory(
        TraktUser traktUser,
        DateTime? startAt = null,
        DateTime? endAt = null,
        int limit = 100)
    {
        var allHistoryItems = new List<TraktHistoryItem>();
        var page = 1;
        var hasMorePages = true;

        while (hasMorePages)
        {
            var queryParams = $"?page={page}&limit={limit}&extended=full";

            if (startAt.HasValue)
            {
                // Format as ISO 8601 with Z suffix
                queryParams += $"&start_at={startAt.Value.ToUniversalTime():yyyy-MM-ddTHH:mm:ss.fffZ}";
            }

            if (endAt.HasValue)
            {
                // Format as ISO 8601 with Z suffix
                queryParams += $"&end_at={endAt.Value.ToUniversalTime():yyyy-MM-ddTHH:mm:ss.fffZ}";
            }

            using var httpClient = await CreateTraktClient(traktUser);
            var response = await httpClient.GetAsync($"/sync/history/shows{queryParams}");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Failed to get show watch history (page {Page}): Status={Status}, Content={Content}",
                    page,
                    response.StatusCode,
                    errorContent);
                break;
            }

            var historyItems = await response.Content.ReadFromJsonAsync<TraktHistoryItem[]>(_jsonOptions);
            if (historyItems == null || historyItems.Length == 0)
            {
                // No more results
                hasMorePages = false;
            }
            else
            {
                allHistoryItems.AddRange(historyItems);
                _logger.LogDebug(
                    "Fetched {Count} history items from page {Page}",
                    historyItems.Length,
                    page);

                // If we got fewer items than the limit, we've reached the last page
                if (historyItems.Length < limit)
                {
                    hasMorePages = false;
                }
                else
                {
                    page++;
                }
            }
        }

        _logger.LogInformation(
            "Fetched {TotalCount} total history items across {PageCount} page(s)",
            allHistoryItems.Count,
            page);

        return allHistoryItems.ToArray();
    }

    internal static ContentItem ToMovieContent(TraktMovie movie, string providerName)
        => new ContentItem
        {
            Type = ContentType.Movie,
            Title = movie.Title,
            Year = movie.Year,
            Overview = movie.Overview,
            PosterUrl = GetPosterUrl(movie.Images),
            TmdbId = movie.Ids.Tmdb,
            ImdbId = movie.Ids.Imdb,
            TraktId = movie.Ids.Trakt,
            ProviderName = providerName,
            Genres = movie.Genres
        };

    internal static ContentItem ToShowContent(TraktShow show, string providerName)
        => new ContentItem
        {
            Type = ContentType.Show,
            Title = show.Title,
            Year = show.Year,
            Overview = show.Overview,
            PosterUrl = GetPosterUrl(show.Images),
            TmdbId = show.Ids.Tmdb,
            ImdbId = show.Ids.Imdb,
            TvdbId = show.Ids.Tvdb,
            TraktId = show.Ids.Trakt,
            ProviderName = providerName,
            Genres = show.Genres
        };

    private static string? GetPosterUrl(Dictionary<string, string[]>? images)
    {
        if (images is null || !images.TryGetValue("poster", out var posters))
        {
            return null;
        }

        foreach (var poster in posters)
        {
            if (string.IsNullOrWhiteSpace(poster))
            {
                continue;
            }

            var url = poster.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? poster : "https://" + poster;
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && uri.Host.EndsWith(".trakt.tv", StringComparison.OrdinalIgnoreCase)
                && uri.UserInfo.Length == 0)
            {
                return uri.AbsoluteUri;
            }
        }

        return null;
    }

    private sealed class TraktCatalogSearchResult
    {
        [JsonPropertyName("movie")]
        public TraktMovie? Movie { get; set; }

        [JsonPropertyName("show")]
        public TraktShow? Show { get; set; }
    }
}
