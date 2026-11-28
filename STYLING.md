# Style JellyNext Discovery

Open **Dashboard → Plugins → JellyNext → Settings → General → Discovery appearance → Custom CSS**. Paste your rules, click **Save Configuration**, and refresh Jellyfin Web. For server themes, you can put the same rules in **Dashboard → Branding → Custom CSS**. Clear the JellyNext field and save to remove its overrides.

JellyNext's built-in CSS is in the `jellynext` CSS layer. Put your overrides outside that layer. Use `:root` to set values for both the rows and the popup, which opens directly under the document body.

## Hide Discovery or Browse libraries

Paste whichever of these rules you want. **Browse libraries** is part of Jellyfin Cinematic, so use the second rule on the combined Movies & Shows page.

```css
/* Hide Movie and Show Discovery everywhere. */
.jellynext-discovery { --jellynext-discovery-display: none; }

/* Hide the Browse libraries control and its menu. */
.jfc-watch .jfc-watch-libraries { display: none; }
```

To hide Discovery only on the combined page, scope the first rule to `.jellynext-discovery[data-jellynext-layout="combined"]`. To keep Movie Discovery and hide Show Discovery, use `.jellynext-discovery[data-jellynext-type="shows"]`. Use both attributes to combine those choices.

## Style the category layout

Movie Discovery groups the installed IMDb catalog into horizontal genre shelves such as **Action**, **Adventure**, and **Comedy**. **See all** opens a paginated grid for that genre, with every matching movie available through its pages. Movies can appear in several genres. CSS changes the layout and visibility of the results. The catalog continues to load eight previews per genre and 24 movies at a time in **See all**.

### Pin genres and use widescreen cards

```css
.jellynext-discovery[data-jellynext-type="movies"] {
  --jellynext-card-width: clamp(9rem, 18vw, 15rem);
  --jellynext-poster-ratio: 16 / 9;
  --jellynext-genre-header-justify: space-between;
  --jellynext-genre-gap: 0.5rem;
}

.jellynext-genre[data-jellynext-genre-key="action"] { --jellynext-genre-order: -2; }
.jellynext-genre[data-jellynext-genre-key="adventure"] { --jellynext-genre-order: -1; }
.jellynext-genre[data-jellynext-genre-key="uncategorized"] { --jellynext-genre-display: none; }
```

The visual order uses the lowest number first. Shelves with the same order retain the catalog’s alphabetical order. Keyboard navigation follows the page’s original order.

### Wrap previews into grids

```css
.jellynext-discovery[data-jellynext-type="movies"] {
  --jellynext-shelf-flow: row;
  --jellynext-shelf-columns: repeat(auto-fill, minmax(min(100%, 10rem), 1fr));
  --jellynext-grid-columns: repeat(auto-fill, minmax(min(100%, 12rem), 1fr));
}
```

### Make See all a text list

```css
.jellynext-catalog-grid {
  --jellynext-grid-columns: minmax(0, 1fr);
  --jellynext-poster-display: none;
  --jellynext-card-columns: minmax(0, 1fr) auto;
  --jellynext-card-gap: 0.3rem;
  --jellynext-card-padding: 0.7rem 0.9rem;
  --jellynext-card-background: #252129;
  --jellynext-card-item-radius: 0.45rem;
}
```

## CSS variables

| Variable | Styles |
| --- | --- |
| `--jellynext-text` | Discovery and popup text |
| `--jellynext-muted` | Popup metadata and status text |
| `--jellynext-discovery-display` | Set to `none` to hide a Discovery section |
| `--jellynext-discovery-background` | Background behind a Discovery row |
| `--jellynext-combined-gutter`, `--jellynext-combined-margin` | Side padding and vertical spacing on a combined page |
| `--jellynext-genre-display`, `--jellynext-genre-order` | Visibility and visual order of each genre shelf |
| `--jellynext-genre-gap`, `--jellynext-genre-margin` | Spacing between and around genre shelves |
| `--jellynext-genre-header-justify`, `--jellynext-genre-title-size` | Alignment of the genre name and See all, and genre title size |
| `--jellynext-shelf-flow`, `--jellynext-shelf-columns` | Card flow and column sizes for preview shelves |
| `--jellynext-shelf-overflow`, `--jellynext-shelf-padding` | Horizontal overflow and padding within card groups |
| `--jellynext-grid-columns` | Column sizes for See all |
| `--jellynext-card-width` | Width of the cards in a row |
| `--jellynext-card-gap` | Space between cards |
| `--jellynext-card-columns`, `--jellynext-card-content-gap` | Columns and spacing inside each card |
| `--jellynext-card-background`, `--jellynext-card-border`, `--jellynext-card-item-radius`, `--jellynext-card-padding` | Card surface, border, corners, and padding |
| `--jellynext-card-radius` | Poster corner radius |
| `--jellynext-poster-background` | Poster placeholder background |
| `--jellynext-poster-display`, `--jellynext-poster-ratio`, `--jellynext-poster-fit` | Artwork visibility, shape, and image fit |
| `--jellynext-title-size`, `--jellynext-title-weight`, `--jellynext-year-size` | Card title and year typography |
| `--jellynext-filters-display`, `--jellynext-control-gap` | Filter visibility and spacing between controls |
| `--jellynext-input-background`, `--jellynext-input-border` | Search and year field colors |
| `--jellynext-dialog-background` | Popup background |
| `--jellynext-dialog-width` | Maximum popup width before the viewport limit |
| `--jellynext-dialog-radius` | Popup corner radius |
| `--jellynext-detail-columns`, `--jellynext-detail-columns-mobile` | Popup columns at desktop and narrow widths |
| `--jellynext-detail-gap`, `--jellynext-detail-padding` | Popup content spacing |
| `--jellynext-backdrop` | Overlay behind the popup |
| `--jellynext-border` | Popup and button borders |
| `--jellynext-button-background` | Secondary button and season selector background |
| `--jellynext-button-text` | Secondary button and season selector text |
| `--jellynext-button-radius` | Button and season selector corners |
| `--jellynext-accent` | Watch button background and border |
| `--jellynext-accent-text` | Watch button text |
| `--jellynext-focus` | Keyboard focus outline |

```css
:root {
  --jellynext-dialog-background: #191216;
  --jellynext-border: #64414b;
  --jellynext-accent: #a81531;
  --jellynext-accent-text: #fff;
  --jellynext-focus: #ff8092;
  --jellynext-card-width: clamp(8rem, 14vw, 11rem);
}

.jellynext-discovery[data-jellynext-type="shows"] {
  --jellynext-card-radius: 0.7rem;
}

.jellynext-dialog[data-jellynext-layout="combined"] {
  --jellynext-dialog-width: 52rem;
}
```

## CSS selectors

| Selector | Element |
| --- | --- |
| `.jellynext-discovery` | A movie or show Discovery section |
| `.jellynext-heading`, `.jellynext-body` | Section heading and content wrapper |
| `.jellynext-catalog-controls`, `.jellynext-catalog-status`, `.jellynext-pagination` | Catalog search and year controls, page status, and paging buttons |
| `.jellynext-title-filter`, `.jellynext-year-filter`, `.jellynext-apply-filters`, `.jellynext-clear-filters` | Individual filter controls |
| `.jellynext-previous-page`, `.jellynext-next-page`, `.jellynext-retry` | Paging and retry buttons |
| `.jellynext-cards`, `.jellynext-card-item`, `.jellynext-card` | Card group, item wrapper, and clickable card |
| `[data-jellynext-id]` | Card wrapper by IMDb ID for movies or Trakt ID for shows |
| `.jellynext-poster`, `.jellynext-poster-image`, `.jellynext-poster-icon`, `.jellynext-title`, `.jellynext-year` | Artwork, image, fallback icon, title, and year |
| `.jellynext-dialog`, `.jellynext-dialog::backdrop`, `.jellynext-detail` | Popup, overlay, and popup layout |
| `.jellynext-detail-copy`, `.jellynext-detail-title` | Popup text wrapper and title |
| `.jellynext-meta`, `.jellynext-description`, `.jellynext-status` | Popup metadata, description, and status |
| `.jellynext-actions`, `.jellynext-watch`, `.jellynext-download`, `.jellynext-season`, `.jellynext-close` | Popup actions, Download & Watch or Watch, Download, season selector, and close button |
| `.jellynext-progress`, `.jellynext-progress-bar` | Download progress container and native progress meter. The meter inherits `--jellynext-accent`. |
| `.jellynext-genre`, `.jellynext-genre-header`, `.jellynext-genre-title` | Movie genre preview, preview header, and title |
| `.jellynext-see-all`, `.jellynext-back-to-genres`, `.jellynext-catalog-grid` | Genre navigation and paginated grid |
| `.jellynext-genre-results[data-jellynext-view="genres"]`, `.jellynext-genre-results[data-jellynext-view="genre"]` | All genre shelves or one See all view |
| `[data-jellynext-genre-key="sci-fi"]` | Genre CSS selector on shelves, the selected genre view, and its movie popups |
| `[data-jellynext-genre="Sci-Fi"]` | The same elements selected by the exact IMDb label |
| `.jfc-watch .jfc-watch-libraries`, `.jfc-watch .jfc-watch-libraries > summary` | Cinematic’s Browse libraries control and clickable header |

Both `.jellynext-discovery` and `.jellynext-dialog` carry `data-jellynext-type="movies"` or `data-jellynext-type="shows"`, and `data-jellynext-layout="native"` or `data-jellynext-layout="combined"`.

## Combined Movies & Shows pages

JellyNext detects [Jellyfin Cinematic](https://github.com/Kaevorr/Jellyfin-Styleing)'s combined page automatically. It places the rows below the featured title and follows the selected filter.

| Cinematic filter | Discovery rows |
| --- | --- |
| All | Movie Discovery, then Show Discovery |
| Movies | Movie Discovery |
| Shows | Show Discovery |

Other JavaScript themes can add one visible mount element to their combined page.

```html
<div data-jellynext-discovery="all"></div>
```

When the user changes filters, set that element's `data-jellynext-discovery` value to `all`, `movies`, or `shows`. JellyNext mounts the corresponding sections inside the element. Remove or hide the element when leaving the combined view so library pages can use their normal placement.
