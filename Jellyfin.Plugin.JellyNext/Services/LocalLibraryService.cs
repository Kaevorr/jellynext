using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.JellyNext.Models.Common;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyNext.Services;

/// <summary>
/// Service for querying the local Jellyfin library.
/// </summary>
public class LocalLibraryService
{
    private readonly ILogger<LocalLibraryService> _logger;
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalLibraryService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="libraryManager">The library manager.</param>
    public LocalLibraryService(ILogger<LocalLibraryService> logger, ILibraryManager libraryManager)
    {
        _logger = logger;
        _libraryManager = libraryManager;
    }

    /// <summary>
    /// Finds a TV series in the local library by TVDB ID.
    /// </summary>
    /// <param name="tvdbId">The TVDB ID.</param>
    /// <returns>The series if found, null otherwise.</returns>
    public Series? FindSeriesByTvdbId(int tvdbId)
    {
        var allItems = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Series },
            HasTvdbId = true,
            Recursive = true
        });

        return allItems
            .OfType<Series>()
            .Where(s => !s.Path?.Contains("jellynext-virtual", StringComparison.OrdinalIgnoreCase) ?? true)
            .FirstOrDefault(s => s.GetProviderId(MetadataProvider.Tvdb) == tvdbId.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Finds a movie in the local library by TMDB ID.
    /// </summary>
    /// <param name="tmdbId">The TMDB ID.</param>
    /// <returns>True if the movie exists in the library, false otherwise.</returns>
    public bool DoesMovieExist(int tmdbId)
    {
        var tmdbIdString = tmdbId.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var allItems = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Movie },
            HasAnyProviderId = new Dictionary<string, string>
            {
                { MetadataProvider.Tmdb.ToString(), tmdbIdString }
            },
            Recursive = true
        });

        return allItems
            .Where(m => !m.Path?.Contains("jellynext-virtual", StringComparison.OrdinalIgnoreCase) ?? true)
            .Any();
    }

    /// <summary>
    /// Finds a playable copy visible to the viewing user.
    /// </summary>
    /// <param name="content">The catalog or recommendation item.</param>
    /// <param name="user">The Jellyfin user.</param>
    /// <param name="season">Optional season to look up for shows.</param>
    /// <param name="userData">Playback position and history.</param>
    /// <returns>The playable movie or episode and its resume position.</returns>
    public (BaseItem? Item, long PositionTicks) FindPlayableContent(
        ContentItem content,
        User user,
        int? season,
        IUserDataManager userData)
    {
        var ids = new Dictionary<string, string>();
        if (content.TmdbId is > 0)
        {
            ids.Add(MetadataProvider.Tmdb.ToString(), content.TmdbId.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (content.Type == ContentType.Show && content.TvdbId is > 0)
        {
            ids.Add(MetadataProvider.Tvdb.ToString(), content.TvdbId.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrWhiteSpace(content.ImdbId))
        {
            ids.Add(MetadataProvider.Imdb.ToString(), content.ImdbId);
        }

        if (ids.Count == 0)
        {
            return default;
        }

        var movie = content.Type == ContentType.Movie;
        var matches = _libraryManager.GetItemList(new InternalItemsQuery(user)
        {
            IncludeItemTypes = new[] { movie ? BaseItemKind.Movie : BaseItemKind.Series },
            HasAnyProviderId = ids,
            IsVirtualItem = false,
            Recursive = true
        }).Where(item => IsLocal(item) && item.IsVisibleStandalone(user));

        var candidates = movie
            ? matches
            : matches.OfType<Series>()
                .SelectMany(series => series.GetEpisodes(user, new DtoOptions(false), shouldIncludeMissingEpisodes: false))
                .OfType<Episode>()
                .Where(episode => (!season.HasValue || episode.ParentIndexNumber == season)
                    && IsLocal(episode)
                    && episode.IsVisibleStandalone(user))
                .OrderBy(episode => episode.ParentIndexNumber)
                .ThenBy(episode => episode.IndexNumber);
        var playable = candidates
            .Where(item => item.GetPlayAccess(user) == PlayAccess.Full)
            .Select(item => (Item: item, Data: userData.GetUserData(user, item)))
            .OrderByDescending(result => (result.Data?.PlaybackPositionTicks ?? 0) > 0)
            .ThenBy(result => result.Data?.Played == true)
            .FirstOrDefault();
        return (playable.Item, playable.Data?.PlaybackPositionTicks ?? 0);
    }

    private static bool IsLocal(BaseItem item)
        => !item.IsVirtualItem && item.Path is { Length: > 0 } path
            && !path.Contains("jellynext-virtual", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the season numbers that exist locally for a series.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>Set of season numbers that exist locally.</returns>
    public HashSet<int> GetLocalSeasons(Series series)
    {
        var seasons = new HashSet<int>();

        var seasonItems = _libraryManager.GetItemList(new InternalItemsQuery
        {
            ParentId = series.Id,
            IncludeItemTypes = new[] { BaseItemKind.Season },
            Recursive = false
        });

        foreach (var item in seasonItems.OfType<Season>())
        {
            // Skip virtual items created by this plugin
            if (item.Path?.Contains("jellynext-virtual", StringComparison.OrdinalIgnoreCase) == true)
            {
                continue;
            }

            if (item.IndexNumber.HasValue)
            {
                seasons.Add(item.IndexNumber.Value);
            }
        }

        return seasons;
    }

    /// <summary>
    /// Checks if a specific season exists locally for a series.
    /// </summary>
    /// <param name="tvdbId">The TVDB ID of the series.</param>
    /// <param name="seasonNumber">The season number to check.</param>
    /// <returns>True if the season exists locally, false otherwise.</returns>
    public bool DoesSeasonExist(int tvdbId, int seasonNumber)
    {
        var series = FindSeriesByTvdbId(tvdbId);
        if (series == null)
        {
            return false;
        }

        var localSeasons = GetLocalSeasons(series);
        return localSeasons.Contains(seasonNumber);
    }
}
