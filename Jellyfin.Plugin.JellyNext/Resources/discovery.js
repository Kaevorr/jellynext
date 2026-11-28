(() => {
    'use strict';

    const styleId = 'jellynext-discovery-styles';
    if (document.getElementById(styleId)) return;

    const styles = document.createElement('style');
    styles.id = styleId;
    styles.textContent = `
        @layer jellynext {
        .jellynext-discovery {
            display: var(--jellynext-discovery-display, block);
            color: var(--jellynext-text, inherit);
            background: var(--jellynext-discovery-background, transparent);
        }
        .jellynext-discovery[hidden], .jellynext-discovery [hidden], .jellynext-dialog [hidden] { display: none; }
        .jellynext-discovery[data-jellynext-layout="combined"] {
            padding-inline: var(--jellynext-combined-gutter, var(--jfc-watch-gutter, clamp(1rem, 4vw, 5rem)));
            margin-block: var(--jellynext-combined-margin, 1.5rem);
        }
        .jellynext-discovery .jellynext-cards {
            display: grid;
            grid-auto-flow: var(--jellynext-shelf-flow, column);
            grid-auto-columns: var(--jellynext-card-width, clamp(8.5rem, 15vw, 12rem));
            grid-template-columns: var(--jellynext-shelf-columns, none);
            gap: var(--jellynext-card-gap, 1rem);
            overflow-x: var(--jellynext-shelf-overflow, auto);
            padding: var(--jellynext-shelf-padding, .25rem 0 1rem);
        }
        .jellynext-discovery .jellynext-card-item { min-width: 0; }
        .jellynext-discovery .jellynext-card {
            display: grid; grid-template-columns: var(--jellynext-card-columns, minmax(0, 1fr));
            gap: var(--jellynext-card-content-gap, .45rem);
            text-align: start; width: 100%; color: inherit;
            background: var(--jellynext-card-background, transparent);
            border: var(--jellynext-card-border, 0);
            border-radius: var(--jellynext-card-item-radius, 0);
            padding: var(--jellynext-card-padding, 0);
        }
        .jellynext-discovery .jellynext-card:focus-visible {
            outline: 2px solid var(--jellynext-focus, var(--jfc-crimson, #00a4dc)); outline-offset: 3px;
        }
        .jellynext-discovery .jellynext-poster, .jellynext-dialog .jellynext-poster {
            aspect-ratio: var(--jellynext-poster-ratio, 2 / 3);
            position: relative; display: var(--jellynext-poster-display, grid); place-items: center;
            border-radius: var(--jellynext-card-radius, .4rem); overflow: hidden;
            background: var(--jellynext-poster-background, rgba(127, 127, 127, .16));
        }
        .jellynext-poster img {
            position: absolute; inset: 0; width: 100%; height: 100%; object-fit: var(--jellynext-poster-fit, cover);
        }
        .jellynext-poster span { opacity: .5; font-size: 2.5rem; }
        .jellynext-discovery .jellynext-title {
            overflow-wrap: anywhere; font-size: var(--jellynext-title-size, inherit); font-weight: var(--jellynext-title-weight, 600);
        }
        .jellynext-discovery .jellynext-year { opacity: .75; font-size: var(--jellynext-year-size, inherit); }
        .jellynext-dialog {
            color: var(--jellynext-text, var(--jfc-white, #f1f6fa));
            background: var(--jellynext-dialog-background, #18232d);
            border: 1px solid var(--jellynext-border, #3c5265);
            border-radius: var(--jellynext-dialog-radius, 1rem);
            width: min(var(--jellynext-dialog-width, 46rem), calc(100% - 2rem)); max-height: calc(100% - 2rem); padding: 0;
            box-shadow: 0 1rem 4rem #0009; overflow: auto;
        }
        .jellynext-dialog[data-jellynext-layout="combined"] {
            background: var(--jellynext-dialog-background, #191216);
            border-color: var(--jellynext-border, #53323b);
        }
        .jellynext-dialog::backdrop { background: var(--jellynext-backdrop, #070e16cc); }
        .jellynext-dialog .jellynext-detail {
            position: relative; display: grid;
            grid-template-columns: var(--jellynext-detail-columns, minmax(8rem, 13rem) minmax(0, 1fr));
            align-items: start; gap: var(--jellynext-detail-gap, 1.5rem); padding: var(--jellynext-detail-padding, clamp(1rem, 4vw, 2rem));
        }
        .jellynext-dialog .jellynext-detail-title { margin: 0 2.8rem .55rem 0; overflow-wrap: anywhere; }
        .jellynext-dialog .jellynext-description { line-height: 1.55; }
        .jellynext-dialog .jellynext-meta, .jellynext-dialog .jellynext-status { color: var(--jellynext-muted, #bbccd9); }
        .jellynext-dialog .jellynext-actions { display: flex; gap: .6rem; flex-wrap: wrap; align-items: center; }
        .jellynext-dialog button, .jellynext-dialog select, .jellynext-discovery button:not(.jellynext-card) {
            font: inherit; color: var(--jellynext-button-text, inherit);
            background: var(--jellynext-button-background, #2a3d4d);
            border: 1px solid var(--jellynext-border, #546e83);
            border-radius: var(--jellynext-button-radius, .5rem);
            padding: .55em .8em; min-height: 2.35rem;
        }
        [data-jellynext-layout="combined"] :where(button:not(.jellynext-card), select) {
            background: var(--jellynext-button-background, #281a20);
            border-color: var(--jellynext-border, #64414b);
        }
        .jellynext-dialog button { cursor: pointer; }
        .jellynext-dialog button:disabled { cursor: default; opacity: .55; }
        .jellynext-dialog button:focus-visible, .jellynext-dialog select:focus-visible {
            outline: 2px solid var(--jellynext-focus, var(--jfc-crimson, #7ddfff)); outline-offset: 2px;
        }
        .jellynext-dialog .jellynext-watch {
            background: var(--jellynext-accent, var(--jfc-crimson, #79def7));
            color: var(--jellynext-accent-text, var(--jfc-white, #071820));
            border-color: var(--jellynext-accent, var(--jfc-crimson, #79def7));
        }
        .jellynext-dialog .jellynext-progress-bar {
            display: block; width: 100%; height: .65rem;
            accent-color: var(--jellynext-accent, var(--jfc-crimson, #79def7));
        }
        .jellynext-dialog .jellynext-close {
            position: absolute; right: .75rem; top: .75rem; width: 2.45rem; padding: 0; font-size: 1.35rem;
        }
        @media (max-width: 36rem) {
            .jellynext-dialog .jellynext-detail {
                grid-template-columns: var(--jellynext-detail-columns-mobile, 1fr);
                gap: var(--jellynext-detail-gap, 1rem);
            }
            .jellynext-dialog .jellynext-poster { width: min(8rem, 38vw); }
        }
        .jellynext-discovery .jellynext-catalog-controls,
        .jellynext-discovery .jellynext-catalog-controls form,
        .jellynext-discovery .jellynext-pagination {
            display: flex; flex-wrap: wrap; align-items: center;
            gap: var(--jellynext-control-gap, .5rem); margin-bottom: .8rem;
        }
        .jellynext-discovery .jellynext-catalog-controls { display: var(--jellynext-filters-display, flex); }
        .jellynext-discovery .jellynext-catalog-controls form { margin: 0; }
        .jellynext-discovery button, .jellynext-discovery select, .jellynext-discovery input {
            min-height: 2.4em; border-radius: .3rem; padding: .3em .55em;
        }
        .jellynext-discovery input {
            border: 1px solid var(--jellynext-input-border, rgba(127, 127, 127, .6));
            background: var(--jellynext-input-background, rgba(127, 127, 127, .14));
            color: inherit;
        }
        .jellynext-discovery input[type=number] { width: 7.5em; }
        .jellynext-discovery button { cursor: pointer; }
        .jellynext-discovery button:disabled { cursor: default; opacity: .65; }
        .jellynext-discovery .jellynext-genre-results[data-jellynext-view="genres"] {
            display: flex; flex-direction: column; gap: var(--jellynext-genre-gap, 0);
        }
        .jellynext-discovery .jellynext-genre {
            display: var(--jellynext-genre-display, block); min-width: 0;
            order: var(--jellynext-genre-order, 0); margin-block: var(--jellynext-genre-margin, 1rem);
        }
        .jellynext-discovery .jellynext-genre-header {
            display: flex; align-items: center; flex-wrap: wrap;
            justify-content: var(--jellynext-genre-header-justify, flex-start); gap: .85rem; margin-bottom: .4rem;
        }
        .jellynext-discovery .jellynext-genre-title { margin: 0; font-size: var(--jellynext-genre-title-size, 1.17em); }
        .jellynext-discovery .jellynext-catalog-grid {
            grid-auto-flow: row; grid-auto-columns: auto;
            grid-template-columns: var(--jellynext-grid-columns, repeat(auto-fill, minmax(min(100%, var(--jellynext-card-width, 9rem)), 1fr)));
        }
        .jellynext-discovery .jellynext-pagination[hidden] { display: none; }
        }
    `;
    document.head.append(styles);

    const sections = new Map();
    const feeds = new Map();
    let requests = new Map();
    let identity = '';
    let scheduled = false;
    let activeDialog;
    const customStyles = document.createElement('style');
    customStyles.id = 'jellynext-custom-css';
    document.head.append(customStyles);

    function element(tag, className, content) {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (content !== undefined) node.textContent = content;
        return node;
    }

    async function api(client, path, method = 'GET') {
        const response = await client.fetch({ type: method, url: client.getUrl('JellyNextLibrary/' + path) });
        if (!response.ok) throw response;
        return response.json();
    }

    function getFeed(client, type) {
        const cached = feeds.get(type);
        if (cached && (cached.pending || Date.now() < cached.expires)) return cached;
        const feed = { pending: true };
        feed.result = api(client, 'Discovery/' + type).finally(() => {
            feed.pending = false;
            feed.expires = Date.now() + 30000;
        });
        feeds.set(type, feed);
        return feed;
    }

    function createSection(tab, type, layout) {
        const section = element('section', 'jellynext-discovery verticalSection padded-left padded-right');
        section.dataset.jellynextType = type === 'Movie' ? 'movies' : 'shows';
        section.dataset.jellynextLayout = layout;
        const heading = element('h2', 'sectionTitle jellynext-heading', type === 'Movie' ? 'Movie Discovery' : 'Show Discovery');
        const body = element('div', 'jellynext-body');
        section.classList.add('padded-top');
        section.append(heading, body);
        section.hidden = true;
        if (layout === 'combined' && tab.matches('.jfc-watch')) {
            const preceding = Array.from(tab.children).filter(child => child.matches('.jfc-watch-feature, .jellynext-discovery')).at(-1);
            if (preceding) preceding.after(section);
            else tab.append(section);
        } else if (layout === 'combined') {
            tab.append(section);
        } else {
            tab.prepend(section);
        }
        return { section, body, tab, layout };
    }

    function makePoster(type, posterUrl) {
        const poster = element('span', 'jellynext-poster');
        poster.setAttribute('aria-hidden', 'true');
        poster.append(element('span', 'material-icons jellynext-poster-icon', type === 'Movie' ? 'movie' : 'tv'));
        if (posterUrl) setPoster(poster, posterUrl);
        return poster;
    }

    function setPoster(poster, url) {
        const picture = element('img', 'jellynext-poster-image');
        picture.alt = '';
        picture.loading = 'lazy';
        picture.referrerPolicy = 'no-referrer';
        picture.addEventListener('error', () => picture.remove(), { once: true });
        picture.src = url;
        poster.append(picture);
        poster.dataset.loaded = 'true';
    }

    async function playOnThisDevice(client, media) {
        const deviceId = client.deviceId();
        const ownId = client.getCurrentUserId().replaceAll('-', '').toLowerCase();
        const response = await client.fetch({ type: 'GET', url: client.getUrl('Sessions', { deviceId }) });
        if (!response.ok) throw response;
        const session = (await response.json()).find(candidate => candidate.DeviceId === deviceId
            && candidate.UserId?.replaceAll('-', '').toLowerCase() === ownId && candidate.SupportsRemoteControl);
        if (!session) throw new Error('Jellyfin Web session is unavailable');

        const parameters = { playCommand: 'PlayNow', itemIds: media.watchItemId };
        if (media.positionTicks > 0) parameters.startPositionTicks = media.positionTicks;
        const play = await client.fetch({
            type: 'POST',
            url: client.getUrl(`Sessions/${encodeURIComponent(session.Id)}/Playing`, parameters)
        });
        if (!play.ok) throw play;
    }

    function openTitleDetails(client, type, item, opener) {
        activeDialog?.close();
        const dialog = element('dialog', 'jellynext-dialog');
        const source = opener.closest('.jellynext-discovery');
        dialog.dataset.jellynextType = source.dataset.jellynextType;
        dialog.dataset.jellynextLayout = source.dataset.jellynextLayout;
        const genre = opener.closest('[data-jellynext-genre]');
        if (genre) {
            dialog.dataset.jellynextGenre = genre.dataset.jellynextGenre;
            dialog.dataset.jellynextGenreKey = genre.dataset.jellynextGenreKey;
        }
        const detail = element('div', 'jellynext-detail');
        const copy = element('div', 'jellynext-detail-copy');
        const title = element('h2', 'jellynext-detail-title', item.title);
        title.id = 'jellynext-detail-title';
        dialog.setAttribute('aria-labelledby', title.id);
        const close = element('button', 'jellynext-close', '×');
        close.type = 'button';
        close.setAttribute('aria-label', 'Close details');
        close.addEventListener('click', () => dialog.close());
        const metadata = element('p', 'jellynext-meta', [item.year, type === 'Movie' ? 'Movie' : 'Series'].filter(Boolean).join(' · '));
        const description = element('p', 'jellynext-description', 'Loading details…');
        const controls = element('div', 'jellynext-actions');
        const watch = element('button', 'jellynext-watch', 'Download & Watch');
        const download = element('button', 'jellynext-download', 'Download');
        watch.type = download.type = 'button';
        controls.append(watch, download);
        const status = element('p', 'jellynext-status');
        status.setAttribute('role', 'status');
        const progress = element('div', 'jellynext-progress');
        const bar = element('progress', 'jellynext-progress-bar');
        bar.max = 100;
        bar.setAttribute('aria-label', 'Download progress');
        progress.append(bar);
        const poster = makePoster(type, item.posterUrl);
        let details;
        let loadVersion = 0;
        let checkTimer;
        let loadFailed = false;
        let playing = false;
        let autoWatchKey;
        const tracked = new Set();
        let seasons;
        function addSeasonSelector(numbers) {
            if (seasons || !numbers?.length || (numbers.length === 1 && numbers[0] === 1)) return false;
            seasons = element('select', 'jellynext-season');
            seasons.setAttribute('aria-label', 'Season of ' + item.title);
            for (const number of numbers) {
                const option = element('option', '', 'Season ' + number);
                option.value = number;
                seasons.append(option);
            }
            seasons.addEventListener('change', () => {
                autoWatchKey = undefined;
                details = undefined;
                update();
                refresh();
            });
            controls.append(seasons);
            return true;
        }
        if (item.seasons > 1) {
            addSeasonSelector(Array.from({ length: item.seasons }, (_, index) => index + 1));
        }
        copy.append(title, metadata, description, controls, status, progress);
        detail.append(poster, copy, close);
        dialog.append(detail);
        document.body.append(dialog);
        activeDialog = dialog;
        dialog.showModal();
        close.focus();

        const requestKey = () => [type, item.id, seasons?.value || 1].join('/');
        const activeQueue = () => ['queued', 'downloading', 'paused', 'importing', 'warning']
            .includes(details?.downloadProgress?.state);

        function update() {
            const key = requestKey();
            const state = requests.get(key);
            const canDownload = (details?.canDownload ?? item.canRequest) && !(type === 'Show' && item.catalog && !details);
            const available = !!details?.watchItemId;
            const armed = autoWatchKey === key;
            const queued = activeQueue();
            const queue = details?.downloadProgress;
            if (queued) tracked.add(key);
            watch.textContent = available && !armed ? 'Watch' : 'Download & Watch';
            watch.disabled = playing || armed || (!available && (state === 'pending' || (!canDownload && !queued)));
            download.disabled = !canDownload || queued || state === 'pending' || state === 'requested'
                || (available && type === 'Movie');
            progress.hidden = !queued || !Number.isFinite(queue.percent);
            if (!progress.hidden) bar.value = queue.percent;

            let message;
            if (playing) {
                message = 'Starting playback…';
            } else if (available && armed) {
                message = 'Ready to watch. Return to this tab to start playback.';
            } else if (state === 'pending') {
                message = 'Sending download request…';
            } else if (queued) {
                const label = {
                    queued: 'Queued',
                    downloading: 'Downloading',
                    paused: 'Download paused',
                    importing: 'Waiting for import',
                    warning: 'Download needs attention'
                }[queue.state];
                message = [label, !progress.hidden ? queue.percent + '%' : '', queue.source].filter(Boolean).join(' · ');
            } else if (available) {
                message = details.watchTitle ? 'Ready to watch · ' + details.watchTitle : 'Ready to watch in your library.';
            } else if (state === 'failed') {
                message = 'Download request failed. Try again or check Downloads settings.';
            } else if (armed || state === 'requested') {
                if (queue?.state === 'unavailable') {
                    message = 'Download requested. Could not read the ' + queue.source + ' queue.';
                } else if (queue?.source && !tracked.has(key)) {
                    message = 'Download requested. Waiting for ' + queue.source + ' to find a download.';
                } else {
                    message = 'Waiting for Jellyfin to find the downloaded media.';
                }
            } else if (loadFailed) {
                message = 'Could not check whether this title is in your library.';
            } else {
                message = details
                    ? (canDownload ? 'Available to watch after download and import.' : details.downloadUnavailableReason)
                    : 'Checking your library…';
            }
            if (armed && !available && !playing) message += ' Playback will start in this tab when ready.';
            if (status.textContent !== message) status.textContent = message;
        }

        async function refresh() {
            clearTimeout(checkTimer);
            const version = ++loadVersion;
            const parameters = new URLSearchParams();
            if (item.catalog && !item.imdb) parameters.set('catalog', 'true');
            if (type === 'Show') parameters.set('season', seasons?.value || 1);
            parameters.set('progress', 'true');
            try {
                const titleId = item.imdb ? 'IMDb/' + item.id : item.id;
                const result = await api(client, `Details/${type}/${titleId}?${parameters}`);
                if (!dialog.open || version !== loadVersion) return;
                details = result;
                loadFailed = false;
                description.textContent = details.overview || 'No description available.';
                metadata.textContent = [details.year, type === 'Movie' ? 'Movie' : 'Series', ...(details.genres || []).slice(0, 3)]
                    .filter(Boolean).join(' · ');
                if (!poster.dataset.loaded && details.posterUrl) setPoster(poster, details.posterUrl);
                if (addSeasonSelector(details.seasons)) {
                    details.watchItemId = null;
                    update();
                    refresh();
                    return;
                }
            } catch {
                if (!dialog.open || version !== loadVersion) return;
                loadFailed = true;
                description.textContent = 'Details are temporarily unavailable.';
            }

            update();
            const key = requestKey();
            const state = requests.get(key);
            if (autoWatchKey === key && state !== 'pending' && !loadFailed && details?.watchItemId
                && document.visibilityState === 'visible') {
                startPlayback();
            } else if (autoWatchKey === key || activeQueue()
                || state === 'pending' || (state === 'requested' && (!details?.watchItemId || (type === 'Show' && !tracked.has(key))))) {
                checkTimer = setTimeout(refresh, 8000);
            }
        }

        async function requestDownload(autoWatch) {
            const key = requestKey();
            const state = requests.get(key);
            if (state === 'pending') return;
            if (autoWatch) autoWatchKey = key;
            if (state === 'requested' || activeQueue()) {
                update();
                if (autoWatch) refresh();
                return;
            }
            const userRequests = requests;
            userRequests.set(key, 'pending');
            update();
            try {
                const path = type === 'Movie'
                    ? `MovieCatalog/${item.imdb ? 'IMDb/' : ''}${item.id}/Request`
                    : `${item.catalog ? 'ShowCatalog' : 'Discovery/Show'}/${item.id}/Request?season=${seasons?.value || 1}`;
                const result = await api(client, path, 'POST');
                if (userRequests === requests) requests.set(key, result.success ? 'requested' : 'failed');
            } catch {
                if (userRequests === requests) requests.set(key, 'failed');
            }
            if (!dialog.open || requestKey() !== key || userRequests !== requests) return;
            if (requests.get(key) === 'failed' && autoWatchKey === key) autoWatchKey = undefined;
            update();
            if (requests.get(key) === 'requested') refresh();
        }

        async function startPlayback() {
            const media = details;
            if (!dialog.open || !media?.watchItemId || playing) return;
            playing = true;
            update();
            try {
                await playOnThisDevice(client, media);
                if (dialog.open) dialog.close();
            } catch {
                if (!dialog.open) return;
                dialog.close();
                window.location.hash = '#/details?id=' + encodeURIComponent(media.watchItemId)
                    + '&serverId=' + encodeURIComponent(client.serverId());
            }
        }
        const onVisibilityChange = () => {
            if (document.visibilityState === 'visible' && autoWatchKey === requestKey() && !playing) refresh();
        };
        document.addEventListener('visibilitychange', onVisibilityChange);
        download.addEventListener('click', () => requestDownload(false));
        watch.addEventListener('click', () => {
            if (details?.watchItemId) startPlayback();
            else requestDownload(true);
        });
        dialog.addEventListener('click', event => {
            if (event.target === dialog) dialog.close();
        });
        dialog.addEventListener('close', () => {
            clearTimeout(checkTimer);
            document.removeEventListener('visibilitychange', onVisibilityChange);
            dialog.remove();
            if (activeDialog === dialog) activeDialog = undefined;
            if (opener.isConnected) opener.focus();
        }, { once: true });
        update();
        refresh();
    }

    function makeCard(client, type, item, observePoster) {
        const card = element('article', 'jellynext-card-item');
        card.dataset.jellynextId = item.id;
        const open = element('button', 'jellynext-card');
        open.type = 'button';
        const poster = makePoster(type, item.posterUrl);
        open.append(poster, element('span', 'jellynext-title', item.title));
        if (!item.posterUrl) observePoster?.(poster, item);
        if (item.year) open.append(element('span', 'jellynext-year', item.year));
        open.addEventListener('click', () => openTitleDetails(client, type, item, open));
        card.append(open);
        return card;
    }

    function watchMoviePosters(client) {
        const items = new WeakMap();
        const retried = new WeakSet();
        const queued = new Set();
        const lookups = new Map();
        let active = 0;
        const observer = new IntersectionObserver(entries => {
            for (const entry of entries) {
                if (entry.isIntersecting) queued.add(entry.target);
                else queued.delete(entry.target);
            }
            fill();
        }, { rootMargin: '180px' });

        function fill() {
            for (const poster of queued) {
                if (active >= 4) break;
                queued.delete(poster);
                observer.unobserve(poster);
                if (!poster.isConnected) continue;
                const item = items.get(poster);
                if (!lookups.has(item.id)) {
                    if (lookups.size >= 256) lookups.delete(lookups.keys().next().value);
                    const lookup = api(client, `MovieCatalog/Poster/${encodeURIComponent(item.id)}`).catch(error => {
                        lookups.delete(item.id);
                        throw error;
                    });
                    lookups.set(item.id, lookup);
                }
                active++;
                lookups.get(item.id).then(result => {
                    if (result.posterUrl && poster.isConnected) {
                        item.posterUrl = result.posterUrl;
                        setPoster(poster, result.posterUrl);
                    }
                }).catch(error => {
                    if ([429, 503].includes(error?.status) && !retried.has(poster)) {
                        retried.add(poster);
                        setTimeout(() => {
                            if (poster.isConnected) observer.observe(poster);
                        }, 15000);
                    }
                }).finally(() => {
                    active--;
                    fill();
                });
            }
        }

        return {
            observe(poster, item) {
                items.set(poster, item);
                observer.observe(poster);
            },
            clear() {
                queued.clear();
                observer.disconnect();
            }
        };
    }

    function renderMovieCatalog(client, view) {
        view.catalog = true;
        const controls = element('form', 'jellynext-catalog-controls');
        const title = element('input', 'jellynext-title-filter');
        title.type = 'search';
        title.maxLength = 100;
        title.placeholder = 'Movie title';
        title.setAttribute('aria-label', 'Movie title');
        const year = element('input', 'jellynext-year-filter');
        year.type = 'number';
        year.min = 1870;
        year.max = 2200;
        year.placeholder = 'Release year';
        year.setAttribute('aria-label', 'Release year');
        const clear = element('button', 'raised jellynext-clear-filters', 'Clear filters');
        clear.type = 'button';
        controls.append(title, year, element('button', 'raised jellynext-apply-filters', 'Apply filters'), clear);
        const back = element('button', 'raised jellynext-back-to-genres', 'All genres');
        back.type = 'button';
        const status = element('p', 'jellynext-catalog-status');
        status.setAttribute('role', 'status');
        const results = element('div', 'jellynext-genre-results');
        const pagination = element('div', 'jellynext-pagination');
        const previous = element('button', 'raised jellynext-previous-page', 'Previous page');
        const next = element('button', 'raised jellynext-next-page', 'Next page');
        const retry = element('button', 'raised jellynext-retry', 'Retry');
        previous.type = next.type = retry.type = 'button';
        pagination.append(previous, next);
        const credit = element('p', 'jellynext-catalog-credit');
        const source = element('a', '', 'IMDb');
        source.href = 'https://www.imdb.com';
        credit.append('Information courtesy of ', source, '. Used with permission.');
        view.body.append(controls, back, status, results, pagination, retry, credit);

        let genre = '';
        let page = 1;
        let queryFilter = '';
        let yearFilter = '';
        let sequence = 0;
        let catalogTimer;
        const posters = watchMoviePosters(client);
        view.dispose = () => {
            clearTimeout(catalogTimer);
            posters.clear();
        };

        function setGenre(node, name) {
            node.dataset.jellynextGenre = name;
            node.dataset.jellynextGenreKey = name.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
        }

        function cards(items, grid = false) {
            const row = element('div', 'jellynext-cards' + (grid ? ' jellynext-catalog-grid' : ''));
            for (const item of items) row.append(makeCard(client, 'Movie', { ...item, catalog: true, imdb: true }, posters.observe));
            return row;
        }

        async function load(focusHeading = false) {
            clearTimeout(catalogTimer);
            if (!view.section.isConnected) return;
            const request = ++sequence;
            const parameters = new URLSearchParams();
            if (queryFilter) parameters.set('query', queryFilter);
            if (yearFilter) parameters.set('year', yearFilter);
            if (genre) {
                parameters.set('genre', genre);
                parameters.set('page', page);
                setGenre(results, genre);
            } else {
                delete results.dataset.jellynextGenre;
                delete results.dataset.jellynextGenreKey;
            }
            results.dataset.jellynextView = genre ? 'genre' : 'genres';
            back.hidden = !genre;
            pagination.hidden = retry.hidden = credit.hidden = true;
            posters.clear();
            results.replaceChildren();
            status.textContent = genre ? `Loading ${genre} movies…` : 'Loading movie genres…';
            try {
                const response = await api(client, (genre ? 'MovieCatalog' : 'MovieCatalog/Genres') + '?' + parameters);
                if (request !== sequence || !view.section.isConnected) return;
                if (response.preparing) {
                    view.section.hidden = false;
                    status.textContent = 'Preparing the movie catalog. The first download may take a few minutes.';
                    catalogTimer = setTimeout(() => load(focusHeading), 2500);
                    return;
                }
                if (genre) {
                    const heading = element('h3', 'jellynext-genre-title', genre);
                    heading.tabIndex = -1;
                    results.append(heading, cards(response.items, true));
                    status.textContent = response.items.length
                        ? `Page ${page} of ${response.pageCount} · Title A–Z`
                        : 'No movies match these filters.';
                    previous.disabled = page <= 1;
                    next.disabled = page >= response.pageCount;
                    pagination.hidden = response.pageCount <= 1;
                    if (focusHeading) {
                        heading.focus({ preventScroll: true });
                        heading.scrollIntoView({ block: 'nearest' });
                    }
                } else {
                    status.textContent = response.length ? '' : 'No movies match these filters.';
                    for (const entry of response) {
                        const shelf = element('section', 'jellynext-genre');
                        setGenre(shelf, entry.name);
                        const header = element('div', 'jellynext-genre-header');
                        const seeAll = element('button', 'raised jellynext-see-all', 'See all');
                        seeAll.type = 'button';
                        seeAll.setAttribute('aria-label', 'See all ' + entry.name + ' movies');
                        seeAll.addEventListener('click', () => {
                            genre = entry.name;
                            page = 1;
                            load(true);
                        });
                        header.append(element('h3', 'jellynext-genre-title', entry.name), seeAll);
                        shelf.append(header, cards(entry.items));
                        results.append(shelf);
                    }
                    if (focusHeading) title.focus({ preventScroll: true });
                }
                credit.hidden = false;
                view.section.hidden = false;
            } catch (error) {
                if (request !== sequence || !view.section.isConnected) return;
                if ([401, 403, 404].includes(error?.status)) {
                    view.section.hidden = true;
                    return;
                }
                view.section.hidden = false;
                retry.hidden = false;
                status.textContent = error?.status === 424
                    ? 'Movie Discovery is waiting for setup. Ask your server administrator to enable the automatic movie catalog in JellyNext settings.'
                    : 'Movie Discovery could not load the catalog. Try again.';
            }
        }

        function applyFilters() {
            queryFilter = title.value.trim();
            yearFilter = year.value;
            page = 1;
            load();
        }
        controls.addEventListener('submit', event => {
            event.preventDefault();
            applyFilters();
        });
        clear.addEventListener('click', () => {
            title.value = year.value = '';
            applyFilters();
        });
        back.addEventListener('click', () => {
            genre = '';
            page = 1;
            load(true);
        });
        previous.addEventListener('click', () => {
            page--;
            load(true);
        });
        next.addEventListener('click', () => {
            page++;
            load(true);
        });
        retry.addEventListener('click', () => load());
        load();
    }

    function renderCatalog(client, type, view) {
        view.catalog = true;
        const movie = type === 'Movie';
        const kind = movie ? 'movies' : 'shows';
        const controls = element('div', 'jellynext-catalog-controls');
        const searchForm = element('form');
        const queryInput = element('input', 'jellynext-title-filter');
        queryInput.type = 'search';
        queryInput.maxLength = 100;
        queryInput.placeholder = movie ? 'Movie title' : 'Show title';
        queryInput.setAttribute('aria-label', queryInput.placeholder);
        searchForm.append(queryInput, element('button', 'raised jellynext-apply-filters', 'Search ' + kind));

        const yearForm = element('form');
        const yearInput = element('input', 'jellynext-year-filter');
        yearInput.type = 'number';
        yearInput.min = 1870;
        yearInput.max = 2200;
        yearInput.placeholder = 'Release year';
        yearInput.setAttribute('aria-label', 'Release year');
        yearForm.append(yearInput, element('button', 'raised jellynext-apply-filters', 'Browse by year'));

        const status = element('span', 'jellynext-catalog-status');
        status.setAttribute('role', 'status');
        const items = element('div');
        const pagination = element('div', 'jellynext-pagination');
        const previous = element('button', 'raised jellynext-previous-page', 'Previous page');
        const next = element('button', 'raised jellynext-next-page', 'Next page');
        const retry = element('button', 'raised jellynext-retry', 'Retry');
        previous.type = next.type = retry.type = 'button';
        pagination.append(previous, next, retry);
        controls.append(searchForm, yearForm);
        view.body.append(controls, status, items, pagination);

        let query = '';
        let year = '';
        let page = 1;
        let sequence = 0;
        async function load() {
            const request = ++sequence;
            const parameters = new URLSearchParams({ page });
            if (query) parameters.set('query', query);
            if (year) parameters.set('year', year);
            previous.disabled = next.disabled = true;
            retry.hidden = true;
            items.replaceChildren();
            status.textContent = 'Loading ' + kind + '…';

            try {
                const result = await api(client, type + 'Catalog?' + parameters);
                if (request !== sequence || !view.section.isConnected) return;
                const label = query ? 'Search results'
                    : year ? (movie ? 'Movies from ' : 'Shows from ') + year : 'Popular ' + kind;
                status.textContent = result.items.length
                    ? `${label} · Page ${page} of ${Math.max(page, result.pageCount)}`
                    : 'No ' + kind + ' found.';
                const cards = element('div', 'jellynext-cards');
                for (const item of result.items) cards.append(makeCard(client, type, { ...item, catalog: true }));
                items.replaceChildren(cards);
                previous.disabled = page <= 1;
                next.disabled = page >= result.pageCount;
                view.section.hidden = false;
            } catch (error) {
                if (request !== sequence || !view.section.isConnected) return;
                if ([401, 403, 404].includes(error?.status)) {
                    view.section.hidden = true;
                    return;
                }
                view.section.hidden = false;
                status.textContent = 'The ' + (movie ? 'movie' : 'show') + ' catalog is unavailable.';
                retry.hidden = false;
            }
        }

        searchForm.addEventListener('submit', event => {
            event.preventDefault();
            query = queryInput.value.trim();
            year = yearInput.value = '';
            page = 1;
            load();
        });
        yearForm.addEventListener('submit', event => {
            event.preventDefault();
            year = yearInput.value;
            query = queryInput.value = '';
            page = 1;
            load();
        });
        previous.addEventListener('click', () => {
            page--;
            load();
        });
        next.addEventListener('click', () => {
            page++;
            load();
        });
        retry.addEventListener('click', load);
        load();
    }

    function render(client, type, view, data) {
        if (!data.linked || !data.enabled || !data.items.length) {
            view.body.replaceChildren();
            renderCatalog(client, type, view);
            return;
        }

        const cards = element('div', 'jellynext-cards');
        for (const item of data.items) cards.append(makeCard(client, type, item));
        view.body.replaceChildren(cards);
    }

    function visibleMainLibraryViews() {
        const hash = window.location.hash.slice(1);
        const separator = hash.indexOf('?');
        const parameters = new URLSearchParams(separator < 0 ? '' : hash.slice(separator + 1));
        const customHost = Array.from(document.querySelectorAll('[data-jellynext-discovery]')).find(host =>
            host.getClientRects().length && ['all', 'movies', 'shows'].includes(host.dataset.jellynextDiscovery));
        if (customHost) return combinedTargets(customHost, customHost.dataset.jellynextDiscovery);

        if (parameters.get('jfc') === 'watch') {
            const panel = document.querySelector('.jfc-watch-active > .jfc-watch');
            return panel?.getClientRects().length ? combinedTargets(panel, parameters.get('filter')) : [];
        }

        const viewIndex = parameters.get('tab');
        const targets = [];
        for (const [pageId, mainTab, type] of [
            ['moviesPage', 'moviesTab', 'Movie'],
            ['tvshowsPage', 'seriesTab', 'Show'],
            ['tvRecommendedPage', 'seriesTab', 'Show']
        ]) {
            const page = document.getElementById(pageId);
            if (!page?.getClientRects().length || page.classList.contains('hide')) continue;
            const tab = page.querySelector('#' + mainTab);
            if (tab) {
                if (tab.getClientRects().length && !tab.classList.contains('hide')) targets.push([type, { tab, layout: 'native' }]);
                continue;
            }

            // A saved landing view can select Suggestions without adding a tab parameter.
            if ((viewIndex === null || viewIndex === '0') && page.querySelector(':scope > .padded-bottom-page')) {
                targets.push([type, { tab: page, layout: 'native' }]);
            }
        }
        return targets;
    }

    function combinedTargets(tab, filter) {
        const target = { tab, layout: 'combined' };
        if (filter === 'movies') return [['Movie', target]];
        if (filter === 'shows' || filter === 'tvshows') return [['Show', target]];
        return [['Movie', target], ['Show', target]];
    }

    function updatePage() {
        scheduled = false;
        const client = window.ApiClient;
        const user = client?.getCurrentUserId?.();
        const activeIdentity = user ? client.serverAddress() + '/' + user : '';
        if (identity !== activeIdentity) {
            activeDialog?.close();
            identity = activeIdentity;
            for (const view of sections.values()) {
                view.dispose?.();
                view.section.remove();
            }
            sections.clear();
            feeds.clear();
            requests = new Map();
            customStyles.textContent = '';
            if (user) {
                api(client, 'Appearance').then(appearance => {
                    if (identity === activeIdentity) customStyles.textContent = appearance.css || '';
                }).catch(() => {});
            }
        }
        if (!user) return;

        const targets = new Map(visibleMainLibraryViews());
        for (const [type, view] of sections) {
            const target = targets.get(type);
            if (target?.tab !== view.tab || !view.section.isConnected) {
                view.dispose?.();
                view.section.remove();
                sections.delete(type);
            }
        }

        for (const [type, { tab, layout }] of targets) {
            let view = sections.get(type);
            if (!view) {
                view = createSection(tab, type, layout);
                sections.set(type, view);
                if (type === 'Movie') renderMovieCatalog(client, view);
            }
            if (view.catalog) continue;
            const feed = getFeed(client, type);
            if (view.feed === feed) continue;
            view.feed = feed;
            feed.result.then(data => {
                if (sections.get(type) !== view || view.feed !== feed) return;
                render(client, type, view, data);
                view.section.hidden = false;
            }).catch(error => {
                if (sections.get(type) !== view || view.feed !== feed) return;
                if ([401, 403, 404].includes(error?.status)) {
                    view.section.hidden = true;
                    return;
                }
                const retry = element('button', 'raised', 'Retry JellyNext discovery');
                retry.type = 'button';
                retry.addEventListener('click', () => {
                    feeds.delete(type);
                    scheduleUpdate();
                });
                view.body.replaceChildren(retry);
                view.section.hidden = false;
            });
        }
    }

    function scheduleUpdate() {
        if (scheduled) return;
        scheduled = true;
        requestAnimationFrame(updatePage);
    }

    new MutationObserver(records => {
        if (records.some(record => record.type === 'childList'
            ? !record.target.closest?.('.jellynext-discovery, .jellynext-dialog')
            : record.attributeName === 'data-jellynext-discovery' || record.target.matches('.page, .pageTabContent'))) {
            scheduleUpdate();
        }
    }).observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['class', 'data-jellynext-discovery'] });
    document.addEventListener('viewshow', scheduleUpdate, true);
    window.addEventListener('hashchange', () => {
        activeDialog?.close();
        scheduleUpdate();
    });
    window.addEventListener('focus', scheduleUpdate);
    scheduleUpdate();
})();
