# Style JellyNext Discovery

Open **Dashboard → Plugins → JellyNext → Settings → General → Discovery appearance → Custom CSS**. Paste your rules, click **Save Configuration**, and refresh Jellyfin Web. For server themes, you can put the same rules in **Dashboard → Branding → Custom CSS**. Clear the JellyNext field and save to remove its overrides.

JellyNext's built-in CSS is in the `jellynext` CSS layer. Put your overrides outside that layer. Use `:root` to set values for both the rows and the popup, which opens directly under the document body.

## CSS variables

| Variable | Styles |
| --- | --- |
| `--jellynext-text` | Discovery and popup text |
| `--jellynext-muted` | Popup metadata and status text |
| `--jellynext-discovery-background` | Background behind a Discovery row |
| `--jellynext-combined-gutter` | Side padding on a combined page |
| `--jellynext-card-width` | Width of the cards in a row |
| `--jellynext-card-gap` | Space between cards |
| `--jellynext-card-radius` | Poster corner radius |
| `--jellynext-poster-background` | Poster placeholder background |
| `--jellynext-dialog-background` | Popup background |
| `--jellynext-dialog-width` | Maximum popup width before the viewport limit |
| `--jellynext-dialog-radius` | Popup corner radius |
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
| `.jellynext-heading` | Section heading |
| `.jellynext-catalog-controls`, `.jellynext-catalog-status`, `.jellynext-pagination` | Catalog search and year controls, page status, and paging buttons |
| `.jellynext-cards`, `.jellynext-card` | Horizontal card row and each clickable card |
| `.jellynext-poster`, `.jellynext-title`, `.jellynext-year` | Artwork, title, and year |
| `.jellynext-dialog`, `.jellynext-dialog::backdrop`, `.jellynext-detail` | Popup, overlay, and popup layout |
| `.jellynext-meta`, `.jellynext-description`, `.jellynext-status` | Popup metadata, description, and status |
| `.jellynext-actions`, `.jellynext-watch`, `.jellynext-download`, `.jellynext-season`, `.jellynext-close` | Popup actions, Download & Watch or Watch, Download, season selector, and close button |
| `.jellynext-progress`, `.jellynext-progress-bar` | Download progress container and native progress meter. The meter inherits `--jellynext-accent`. |
| `.jellynext-genre`, `.jellynext-genre-header`, `.jellynext-genre-title` | Movie genre preview, preview header, and title |
| `.jellynext-see-all`, `.jellynext-back-to-genres`, `.jellynext-catalog-grid` | Genre navigation and paginated grid |
| `[data-jellynext-genre]` | Preview row selector by IMDb genre name, for example `[data-jellynext-genre="Science-Fiction"]` with the exact label from the catalog |

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
