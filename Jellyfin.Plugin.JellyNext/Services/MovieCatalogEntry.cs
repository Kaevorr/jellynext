namespace Jellyfin.Plugin.JellyNext.Services;

internal sealed record MovieCatalogEntry(string Id, string Title, string? OriginalTitle, int? Year, string[] Genres);
