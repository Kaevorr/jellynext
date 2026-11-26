(() => {
    'use strict';

    const styleId = 'jellynext-discovery-styles';
    if (document.getElementById(styleId)) return;

    const styles = document.createElement('style');
    styles.id = styleId;
    styles.textContent = `
        .jellynext-discovery .jellynext-cards {
            display: grid; grid-auto-flow: column; grid-auto-columns: clamp(8.5rem, 15vw, 12rem);
            gap: 1rem; overflow-x: auto; padding: .25rem 0 1rem;
        }
        .jellynext-discovery article { display: flex; flex-direction: column; gap: .45rem; min-width: 0; }
        .jellynext-discovery .jellynext-poster {
            aspect-ratio: 2 / 3; position: relative; display: grid; place-items: center;
            border-radius: .4rem; overflow: hidden; background: rgba(127, 127, 127, .16);
        }
        .jellynext-discovery .jellynext-poster img {
            position: absolute; inset: 0; width: 100%; height: 100%; object-fit: cover;
        }
        .jellynext-discovery .jellynext-poster span { opacity: .5; font-size: 2.5rem; }
        .jellynext-discovery .jellynext-title { overflow-wrap: anywhere; font-weight: 600; }
        .jellynext-discovery .jellynext-year { opacity: .75; }
        .jellynext-discovery .jellynext-request { margin-top: auto; display: flex; flex-wrap: wrap; gap: .35rem; }
        .jellynext-discovery .jellynext-catalog-controls,
        .jellynext-discovery .jellynext-catalog-controls form,
        .jellynext-discovery .jellynext-pagination {
            display: flex; flex-wrap: wrap; align-items: center; gap: .5rem; margin-bottom: .8rem;
        }
        .jellynext-discovery .jellynext-catalog-controls form { margin: 0; }
        .jellynext-discovery button, .jellynext-discovery select, .jellynext-discovery input {
            min-height: 2.4em; border-radius: .3rem; padding: .3em .55em;
        }
        .jellynext-discovery input {
            border: 1px solid rgba(127, 127, 127, .6);
            background: rgba(127, 127, 127, .14);
            color: inherit;
        }
        .jellynext-discovery input[type=number] { width: 7.5em; }
        .jellynext-discovery button { cursor: pointer; }
        .jellynext-discovery button:disabled { cursor: default; opacity: .65; }
    `;
    document.head.append(styles);

    const sections = new Map();
    const feeds = new Map();
    let requests = new Map();
    let identity = '';
    let scheduled = false;

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

    function createSection(tab, type) {
        const section = element('section', 'jellynext-discovery verticalSection padded-left padded-right');
        const heading = element('h2', 'sectionTitle', type === 'Movie' ? 'Movie Discovery' : 'Show Discovery');
        const body = element('div');
        section.classList.add('padded-top');
        section.append(heading, body);
        section.hidden = true;
        tab.prepend(section);
        return { section, body };
    }

    function makeCard(client, type, item) {
        const card = element('article');
        const poster = element('div', 'jellynext-poster');
        poster.setAttribute('aria-hidden', 'true');
        poster.append(element('span', 'material-icons', type === 'Movie' ? 'movie' : 'tv'));
        if (item.posterUrl) {
            const picture = element('img');
            picture.alt = '';
            picture.loading = 'lazy';
            picture.referrerPolicy = 'no-referrer';
            picture.addEventListener('error', () => picture.remove(), { once: true });
            picture.src = item.posterUrl;
            poster.append(picture);
        }
        card.append(poster, element('div', 'jellynext-title', item.title));
        if (item.year) card.append(element('span', 'jellynext-year', item.year));
        if (!item.canRequest) return card;

        const controls = element('div', 'jellynext-request');
        const button = element('button', 'raised', type === 'Movie' ? 'Request movie' : 'Request season');
        button.type = 'button';
        const status = element('span');
        status.setAttribute('role', 'status');
        let seasons;
        if (item.seasons > 1) {
            seasons = element('select');
            seasons.setAttribute('aria-label', 'Season of ' + item.title);
            for (let number = 1; number <= item.seasons; number++) {
                const option = element('option', '', 'Season ' + number);
                option.value = number;
                seasons.append(option);
            }
            controls.append(seasons);
        }
        const requestKey = () => [type, item.id, seasons?.value || 1].join('/');
        const update = () => {
            const state = requests.get(requestKey());
            button.disabled = state === 'pending' || state === 'requested';
            status.textContent = {
                pending: 'Sending request…',
                requested: 'Request sent.',
                failed: 'Request failed. Try again or check Downloads settings.'
            }[state] || '';
        };
        seasons?.addEventListener('change', update);
        button.addEventListener('click', async () => {
            const key = requestKey();
            if (['pending', 'requested'].includes(requests.get(key))) return;
            const userRequests = requests;
            userRequests.set(key, 'pending');
            update();
            try {
                const path = type === 'Movie'
                    ? `MovieCatalog/${item.id}/Request`
                    : `Discovery/Show/${item.id}/Request?season=${seasons?.value || 1}`;
                const result = await api(client, path, 'POST');
                if (userRequests === requests) requests.set(key, result.success ? 'requested' : 'failed');
            } catch {
                if (userRequests === requests) requests.set(key, 'failed');
            }
            update();
        });
        update();
        controls.append(button, status);
        card.append(controls);
        return card;
    }

    function renderMovieCatalog(client, view) {
        const controls = element('div', 'jellynext-catalog-controls');
        const searchForm = element('form');
        const queryInput = element('input');
        queryInput.type = 'search';
        queryInput.maxLength = 100;
        queryInput.placeholder = 'Movie title';
        queryInput.setAttribute('aria-label', 'Movie title');
        searchForm.append(queryInput, element('button', 'raised', 'Search movies'));

        const yearForm = element('form');
        const yearInput = element('input');
        yearInput.type = 'number';
        yearInput.min = 1870;
        yearInput.max = 2200;
        yearInput.placeholder = 'Release year';
        yearInput.setAttribute('aria-label', 'Release year');
        yearForm.append(yearInput, element('button', 'raised', 'Browse by year'));

        const status = element('span');
        status.setAttribute('role', 'status');
        const items = element('div');
        const pagination = element('div', 'jellynext-pagination');
        const previous = element('button', 'raised', 'Previous page');
        const next = element('button', 'raised', 'Next page');
        const retry = element('button', 'raised', 'Retry');
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
            status.textContent = 'Loading movies…';

            try {
                const result = await api(client, 'MovieCatalog?' + parameters);
                if (request !== sequence || !view.section.isConnected) return;
                const label = query ? 'Search results' : year ? 'Movies from ' + year : 'Popular movies';
                status.textContent = result.items.length
                    ? `${label} · Page ${page} of ${Math.max(page, result.pageCount)}`
                    : 'No movies found.';
                const cards = element('div', 'jellynext-cards');
                for (const movie of result.items) cards.append(makeCard(client, 'Movie', movie));
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
                status.textContent = 'The movie catalog is unavailable.';
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
        if (!data.linked || !data.enabled) {
            view.body.replaceChildren(element('p', '', data.linked
                ? 'Enable these recommendations in JellyNext’s Trakt settings.'
                : 'Link your Trakt account in JellyNext to see recommendations.'));
            return;
        }

        if (!data.items.length) {
            view.body.replaceChildren(element('p', '', 'No recommendations yet. Run Sync Trakt Content in Scheduled Tasks.'));
            return;
        }

        const cards = element('div', 'jellynext-cards');
        for (const item of data.items) cards.append(makeCard(client, type, item));
        view.body.replaceChildren(cards);
    }

    function visibleMainLibraryViews() {
        const hash = window.location.hash.slice(1);
        const separator = hash.indexOf('?');
        const viewIndex = new URLSearchParams(separator < 0 ? '' : hash.slice(separator + 1)).get('tab');
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
                if (tab.getClientRects().length && !tab.classList.contains('hide')) targets.push([tab, type]);
                continue;
            }

            // A saved landing view can select Suggestions without adding a tab parameter.
            if ((viewIndex === null || viewIndex === '0') && page.querySelector(':scope > .padded-bottom-page')) {
                targets.push([page, type]);
            }
        }
        return targets;
    }

    function updatePage() {
        scheduled = false;
        const client = window.ApiClient;
        const user = client?.getCurrentUserId?.();
        const activeIdentity = user ? client.serverAddress() + '/' + user : '';
        if (identity !== activeIdentity) {
            identity = activeIdentity;
            for (const view of sections.values()) view.section.remove();
            sections.clear();
            feeds.clear();
            requests = new Map();
        }
        if (!user) return;

        const targets = new Map(visibleMainLibraryViews());
        for (const [tab, view] of sections) {
            if (!targets.has(tab) || !view.section.isConnected) {
                view.section.remove();
                sections.delete(tab);
            }
        }

        for (const [tab, type] of targets) {
            let view = sections.get(tab);
            if (!view) {
                view = createSection(tab, type);
                sections.set(tab, view);
                if (type === 'Movie') renderMovieCatalog(client, view);
            }
            if (type === 'Movie') continue;
            const feed = getFeed(client, type);
            if (view.feed === feed) continue;
            view.feed = feed;
            feed.result.then(data => {
                if (sections.get(tab) !== view || view.feed !== feed) return;
                render(client, type, view, data);
                view.section.hidden = false;
            }).catch(error => {
                if (sections.get(tab) !== view || view.feed !== feed) return;
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
            ? !record.target.closest?.('.jellynext-discovery')
            : record.target.matches('.page, .pageTabContent'))) {
            scheduleUpdate();
        }
    }).observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['class'] });
    document.addEventListener('viewshow', scheduleUpdate, true);
    window.addEventListener('hashchange', scheduleUpdate);
    window.addEventListener('focus', scheduleUpdate);
    scheduleUpdate();
})();
