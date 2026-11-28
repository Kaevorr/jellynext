"""Exercise Movie Discovery against an isolated Jellyfin 12.4 test server."""

import argparse
import gzip
import json
import os
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from threading import Thread
from urllib.error import HTTPError
from urllib.parse import parse_qs, urlsplit
from urllib.request import Request, urlopen
from uuid import uuid4


HEADER = "tconst\ttitleType\tprimaryTitle\toriginalTitle\tisAdult\tstartYear\tendYear\truntimeMinutes\tgenres\n"
CONFIG = "Plugins/ba9826c427b44a2b8c06a515523f914c/Configuration"
GENRES = "JellyNextLibrary/MovieCatalog/Genres"
MOVIES = "JellyNextLibrary/MovieCatalog"
DETAILS = "JellyNextLibrary/Details/Movie/IMDb"
MATRIX = "tt0133093"
UNPOPULAR = "tt900000029"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--server", default="http://127.0.0.1:18096")
    parser.add_argument("--data", required=True, type=Path, help="Isolated Jellyfin server data directory")
    parser.add_argument("--browser", action="store_true", help="Run the UI checks with Playwright and Chrome")
    parser.add_argument("--theme", type=Path, help="Optional Jellyfin Cinematic checkout for browser checks")
    parser.add_argument("--automatic", action="store_true", help="Download the official snapshot on an isolated personal-use server")
    args = parser.parse_args()
    if urlsplit(args.server).hostname not in ("localhost", "127.0.0.1"):
        parser.error("Use a local test server.")

    username = os.environ["JELLYNEXT_TEST_USER"]
    password = os.environ["JELLYNEXT_TEST_PASSWORD"]
    snapshot = args.data / "plugins" / "Jellyfin.Plugin.JellyNext" / "title.basics.tsv.gz"
    if snapshot.exists():
        parser.error("The isolated server must start without a movie snapshot.")

    def api(path, method="GET", body=None, token=None, expected=200):
        auth = ('MediaBrowser Client="JellyNext Integration Test", Device="Local", '
                'DeviceId="JellyNextIntegrationTest", Version="1.0"')
        if token:
            auth += f', Token="{token}"'
        data = json.dumps(body).encode() if body is not None else None
        request = Request(args.server.rstrip("/") + "/" + path, data, method=method,
                          headers={"Authorization": auth, "Content-Type": "application/json"})
        try:
            with urlopen(request, timeout=30) as response:
                payload = response.read()
                assert response.status in ((expected,) if isinstance(expected, int) else expected), (path, response.status, expected)
                return json.loads(payload) if payload else None
        except HTTPError as error:
            assert error.code == expected, (path, error.code, expected, error.read())
            return None

    def login(name, secret):
        return api("Users/AuthenticateByName", "POST", {"Username": name, "Pw": secret})["AccessToken"]

    admin = login(username, password)
    config = api(CONFIG, token=admin)
    if args.automatic:
        try:
            api(CONFIG, "POST", {**config, "AutomaticMovieCatalog": False}, admin, expected=204)
            api(GENRES, token=admin, expected=424)
            api(CONFIG, "POST", {**config, "AutomaticMovieCatalog": True}, admin, expected=204)
            assert api(GENRES, token=admin, expected=202)["preparing"]
            for _ in range(120):
                rows = api(GENRES, token=admin, expected=(200, 202))
                if isinstance(rows, list):
                    break
                assert rows["preparing"]
                time.sleep(2)
            assert isinstance(rows, list) and {"Action", "Adventure"}.issubset(row["name"] for row in rows)
            assert snapshot.is_file() and not snapshot.with_suffix(".gz.download").exists()
            assert api(MOVIES + "?genre=Action&query=matrix&year=1999", token=admin)["items"]
            print(f"Official IMDb snapshot downloaded, cached, and indexed automatically across {len(rows)} genre rows.")
        finally:
            api(CONFIG, "POST", config, admin, expected=204)
            snapshot.unlink(missing_ok=True)
        return

    api(CONFIG, "POST", {**config, "AutomaticMovieCatalog": False}, admin, expected=204)
    users = []
    requests = []
    request_user = None

    class Providers(BaseHTTPRequestHandler):
        def reply(self, body):
            data = json.dumps(body).encode()
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.end_headers()
            self.wfile.write(data)

        def do_GET(self):
            path = urlsplit(self.path)
            if path.path == "/radarr/api/v3/movie/lookup/imdb" and parse_qs(path.query) == {"imdbId": [UNPOPULAR]}:
                self.reply({"tmdbId": 7654321, "imdbId": UNPOPULAR})
            elif path.path == "/radarr/api/v3/movie" and parse_qs(path.query) == {"tmdbId": ["7654321"]}:
                self.reply([{"id": 41, "tmdbId": 7654321}] if requests[-1][0] == "/radarr/api/v3/movie" else [])
            elif path.path == "/radarr/api/v3/queue/details" and parse_qs(path.query) == {"movieId": ["41"]}:
                self.reply([{"id": 1, "movieId": 41, "size": 100, "sizeleft": 63, "status": "downloading"}])
            elif path.path == "/api/v1/user":
                self.reply({"results": [{"id": 9, "jellyfinUserId": request_user}], "pageInfo": {"pages": 1}})
            else:
                self.send_error(404)

        def do_POST(self):
            if self.headers.get("Transfer-Encoding") == "chunked":
                chunks = []
                while size := int(self.rfile.readline().split(b";", 1)[0], 16):
                    chunks.append(self.rfile.read(size))
                    self.rfile.read(2)
                payload = json.loads(b"".join(chunks))
            else:
                payload = json.loads(self.rfile.read(int(self.headers["Content-Length"])))
            requests.append((self.path, payload))
            if self.path == "/radarr/api/v3/movie":
                self.reply({"id": 41, "tmdbId": payload["tmdbId"]})
                return
            if self.path == "/api/v1/request":
                self.reply({"id": 42})
                return
            self.send_response(204)
            self.end_headers()

    def create_user():
        name = "MovieDiscoveryTest-" + uuid4().hex[:8]
        secret = uuid4().hex
        user = api("Users/New", "POST", {"Name": name, "Password": secret}, admin)
        users.append(user["Id"])
        return user, login(name, secret)

    with ThreadingHTTPServer(("127.0.0.1", 0), Providers) as providers:
        thread = Thread(target=providers.serve_forever, daemon=True)
        thread.start()
        try:
            api(GENRES, token=admin, expected=424)
            snapshot.parent.mkdir(parents=True, exist_ok=True)
            with gzip.open(snapshot, "xt", encoding="utf-8") as out:
                out.write(HEADER)
                for i in reversed(range(1, 30)):
                    genre = "Drama,Action,Action" if i == 1 else "Drama"
                    title = "Zebra Outside Popular" if i == 29 else f"Drama {i:02d}"
                    release = 2020 if i == 1 else 2021
                    out.write(f"tt900000{i:03d}\tmovie\t{title}\t{title}\t0\t{release}\t\\N\t90\t{genre}\n")
                out.write(f"{MATRIX}\tmovie\tThe Matrix\tThe Matrix\t0\t1999\t\\N\t136\tAction,Sci-Fi\n")
                out.write("tt900000001\tmovie\tDrama 01\tDrama 01\t0\t2020\t\\N\t90\tAction\n")
            rows = api(GENRES, token=admin, expected=(200, 202))
            if isinstance(rows, dict):
                time.sleep(1)
                rows = api(GENRES, token=admin)
            assert [row["name"] for row in rows] == ["Action", "Drama", "Sci-Fi"], rows
            assert len(rows[1]["items"]) == 8
            assert rows[0]["items"][0] == rows[1]["items"][0]
            first = api(MOVIES + "?genre=Drama&page=1", token=admin)
            second = api(MOVIES + "?genre=Drama&page=2", token=admin)
            assert first["pageCount"] == second["pageCount"] == 2
            ids = [movie["id"] for movie in first["items"] + second["items"]]
            assert len(ids) == len(set(ids)) == 29 and ids[-1] == UNPOPULAR
            assert api(MOVIES + "?genre=Drama&page=1", token=admin) == first
            filtered = api(MOVIES + "?genre=Action&query=drama&year=2020", token=admin)
            assert [movie["title"] for movie in filtered["items"]] == ["Drama 01"]
            assert not api(MOVIES + "?genre=Sci-Fi&query=drama&year=2020", token=admin)["items"]
            api(GENRES, expected=401)

            regular, regular_token = create_user()
            request_user = regular["Id"]
            restricted, restricted_token = create_user()
            api(f"Users/{restricted['Id']}/Policy", "POST",
                {**restricted["Policy"], "MaxParentalRating": 10}, admin, expected=204)
            url = f"http://127.0.0.1:{providers.server_port}"
            changed = {**config, "AutomaticMovieCatalog": False, "DownloadIntegration": "Webhook",
                       "RadarrUrl": "", "WebhookMovieUrl": url + "/movie", "WebhookShowUrl": url + "/show",
                       "WebhookMoviePayload": '{"tmdbId":"{tmdbId}","imdbId":"{imdbId}"}',
                       "TraktUsers": [], "CatalogRequestUsers": [restricted["Id"]]}
            api(CONFIG, "POST", changed, admin, expected=204)
            request_path = f"{MOVIES}/IMDb/{MATRIX}/Request"
            assert api(GENRES, token=regular_token)
            assert not api(f"{DETAILS}/{MATRIX}", token=regular_token)["canDownload"]
            api(request_path, "POST", token=regular_token, expected=403)
            api(GENRES, token=restricted_token, expected=403)
            api(MOVIES + "?genre=Drama", token=restricted_token, expected=403)
            api(f"{DETAILS}/{MATRIX}", token=restricted_token, expected=403)
            api(request_path, "POST", token=restricted_token, expected=403)
            assert not requests, requests
            changed["CatalogRequestUsers"].append(regular["Id"])
            api(CONFIG, "POST", changed, admin, expected=204)
            details = api(f"{DETAILS}/{MATRIX}", token=regular_token)
            assert details["canDownload"] and details["overview"], details
            assert api(request_path, "POST", token=regular_token)["success"]
            assert str(requests[-1][1]["tmdbId"]) == "603", requests
            assert api(f"{DETAILS}/{UNPOPULAR}", token=regular_token)["title"] == "Zebra Outside Popular"
            unknown_request = f"{MOVIES}/IMDb/{UNPOPULAR}/Request"
            assert api(unknown_request, "POST", token=regular_token)["success"]
            assert requests[-1] == ("/movie", {"imdbId": UNPOPULAR, "tmdbId": ""}), requests
            native = {**changed, "DownloadIntegration": "Native", "RadarrUrl": url + "/radarr",
                      "RadarrApiKey": "fixture", "RadarrRootFolderPath": "/movies", "RadarrQualityProfileId": 7}
            api(CONFIG, "POST", native, admin, expected=204)
            assert api(f"{DETAILS}/{UNPOPULAR}", token=regular_token)["canDownload"]
            assert api(unknown_request, "POST", token=regular_token)["success"]
            assert requests[-1][0] == "/radarr/api/v3/movie"
            assert requests[-1][1]["tmdbId"] == 7654321 and requests[-1][1]["addOptions"]["searchForMovie"]
            assert api(f"{DETAILS}/{UNPOPULAR}?progress=true", token=regular_token)["downloadProgress"]["percent"] == 37
            seerr = {**native, "DownloadIntegration": "Jellyseerr", "JellyseerrUrl": url,
                     "JellyseerrApiKey": "fixture", "UseJellyseerrRadarrDefaults": True}
            api(CONFIG, "POST", seerr, admin, expected=204)
            assert api(unknown_request, "POST", token=regular_token)["success"]
            assert requests[-1][0] == "/api/v1/request" and requests[-1][1]["mediaId"] == 7654321
            api(CONFIG, "POST", changed, admin, expected=204)
            api(f"{DETAILS}/tt111111111", token=admin, expected=404)
            assert api("JellyNextLibrary/ShowCatalog", token=admin)["items"]
            print("Movie Discovery API passed catalog, permissions, IMDb-only webhook, Radarr lookup and progress, and Jellyseerr request checks.")

            if args.browser:
                verify_browser(args.server, username, password, args.theme)
        finally:
            api(CONFIG, "POST", config, admin, expected=204)
            for user in users:
                api(f"Users/{user}", "DELETE", token=admin, expected=204)
            snapshot.unlink(missing_ok=True)
            providers.shutdown()


def verify_browser(server, username, password, theme):
    from playwright.sync_api import expect, sync_playwright

    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(channel="chrome", headless=True)
        page = browser.new_page()
        page.goto(server + "/web/#/login")
        page.locator("input[type=text]").first.fill(username)
        page.locator("input[type=password]").first.fill(password)
        page.locator("button[type=submit]").first.click()
        page.wait_for_url(lambda url: "login" not in url)
        page.goto(server + "/web/#/home")
        if theme:
            page.add_style_tag(content=(theme / "theme.css").read_text(encoding="utf-8"))
            page.add_script_tag(content=(theme / "js" / "enhancements.js").read_text(encoding="utf-8"))
            page.locator(".MuiAppBar-root .jfc-watch-tab").click()
        else:
            page.evaluate("""() => {
                location.hash = '#/home?jfc=watch';
                const host = document.createElement('div');
                host.className = 'jfc-watch-active';
                host.style.cssText = 'position:fixed;inset:5rem 0 0;overflow:auto;z-index:10;background:#111';
                host.innerHTML = '<div class="jfc-watch"><div class="jfc-watch-feature"></div></div>';
                document.body.append(host);
            }""")
        mount = page.locator(".jfc-watch-active > .jfc-watch")
        movies = mount.locator('.jellynext-discovery[data-jellynext-type="movies"]')
        shows = mount.locator('.jellynext-discovery[data-jellynext-type="shows"]')
        expect(shows).to_be_visible()
        expect(movies.locator(".jellynext-genre-title")).to_have_text(["Action", "Drama", "Sci-Fi"])
        movies.get_by_role("button", name="See all Drama movies").click()
        expect(movies.locator(".jellynext-catalog-status")).to_contain_text("Page 1 of 2")
        movies.get_by_role("button", name="Next page").click()
        expect(movies.locator(".jellynext-catalog-status")).to_contain_text("Page 2 of 2")
        expect(movies.get_by_role("button", name="Zebra Outside Popular", exact=False)).to_be_visible()
        movies.get_by_role("searchbox", name="Movie title").fill("drama")
        movies.get_by_role("spinbutton", name="Release year").fill("2020")
        movies.get_by_role("button", name="Apply filters").click()
        expect(movies.locator(".jellynext-catalog-grid .jellynext-card")).to_have_count(1)
        movies.get_by_role("button", name="All genres").click()
        expect(movies.locator(".jellynext-genre-title")).to_have_text(["Action", "Drama"])
        movies.get_by_role("button", name="Clear filters").click()
        movies.get_by_role("button", name="The Matrix", exact=False).first.click()
        dialog = page.get_by_role("dialog", name="The Matrix")
        expect(dialog.get_by_role("button", name="Download & Watch")).to_be_enabled()
        expect(dialog.get_by_role("button", name="Download", exact=True)).to_be_enabled()
        expect(dialog).to_have_attribute("data-jellynext-genre-key", "action")
        dialog.get_by_role("button", name="Close details").click()

        libraries = mount.locator(".jfc-watch-libraries")
        has_browse_libraries = libraries.is_visible()
        overrides = page.add_style_tag(content="""
            .jellynext-discovery[data-jellynext-type="movies"] {
                --jellynext-shelf-flow: row;
                --jellynext-shelf-columns: repeat(2, minmax(0, 1fr));
                --jellynext-poster-ratio: 16 / 9;
            }
            .jellynext-genre[data-jellynext-genre-key="drama"] { --jellynext-genre-order: -1; }
            .jellynext-genre[data-jellynext-genre-key="sci-fi"] { --jellynext-genre-display: none; }
            .jellynext-catalog-grid { --jellynext-grid-columns: minmax(0, 1fr); --jellynext-poster-display: none; }
            .jellynext-discovery[data-jellynext-type="shows"] { --jellynext-discovery-display: none; }
            .jfc-watch .jfc-watch-libraries { display: none; }
        """)
        drama = movies.locator('.jellynext-genre[data-jellynext-genre-key="drama"]')
        action = movies.locator('.jellynext-genre[data-jellynext-genre-key="action"]')
        expect(movies.locator('.jellynext-genre[data-jellynext-genre-key="sci-fi"]')).to_be_hidden()
        assert drama.bounding_box()["y"] < action.bounding_box()["y"]
        expect(drama.locator(".jellynext-cards")).to_have_css("grid-auto-flow", "row")
        expect(drama.locator(".jellynext-poster").first).to_have_css("aspect-ratio", "16 / 9")
        expect(shows).to_be_hidden()
        if has_browse_libraries:
            expect(libraries).to_be_hidden()
        drama.get_by_role("button", name="See all Drama movies").click()
        expect(movies.locator(".jellynext-catalog-grid .jellynext-poster").first).to_be_hidden()
        expect(movies.locator(".jellynext-catalog-grid .jellynext-card").first).to_be_visible()
        overrides.evaluate("style => style.remove()")
        expect(shows).to_be_visible()
        if has_browse_libraries:
            expect(libraries).to_be_visible()
        if theme:
            mount.locator(".jfc-watch-filters").get_by_role("link", name="Shows", exact=True).click()
            expect(shows).to_be_visible()
            expect(movies).to_have_count(0)
            mount.locator(".jfc-watch-filters").get_by_role("link", name="Movies", exact=True).click()
            expect(movies).to_be_visible()
            expect(shows).to_have_count(0)
        browser.close()
        print("Jellyfin Web passed the Cinematic mount, genre navigation, filters, title popup, and CSS overrides.")


if __name__ == "__main__":
    main()
