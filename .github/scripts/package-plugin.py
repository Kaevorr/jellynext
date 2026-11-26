"""Build the JellyNext release archive and Jellyfin catalog from a Release build."""

import argparse
import hashlib
import json
import re
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    parser.add_argument("--repository", default="Kaevorr/jellynext")
    parser.add_argument("--ref", default="main")
    args = parser.parse_args()

    root = Path(__file__).resolve().parents[2]
    project = root / "Jellyfin.Plugin.JellyNext"
    project_xml = ET.parse(project / "Jellyfin.Plugin.JellyNext.csproj")
    version = project_xml.findtext("./PropertyGroup/AssemblyVersion")
    framework = project_xml.findtext("./PropertyGroup/TargetFramework")
    target_abi = project_xml.find("./ItemGroup/PackageReference[@Include='Jellyfin.Controller']").attrib["Version"]
    notes = (root / "CHANGELOG.md").read_text(encoding="utf-8")
    section = re.search(rf"^## v{re.escape(version)}\s*\n(.*?)(?=^## |\Z)", notes, re.MULTILINE | re.DOTALL)
    changelog = section[1].strip() if section else f"Release v{version}"

    args.output.mkdir(parents=True, exist_ok=True)
    archive = args.output / f"jellynext-v{version}.zip"
    assembly = project / "bin" / "Release" / framework / "Jellyfin.Plugin.JellyNext.dll"
    if not assembly.is_file():
        parser.error(f"Release assembly missing. Build the solution in Release mode first: {assembly}")
    with ZipFile(archive, "w", compression=ZIP_DEFLATED) as package:
        package.write(assembly, assembly.name)
    with archive.open("rb") as package:
        checksum = hashlib.file_digest(package, "md5").hexdigest()

    manifest = [{
        "guid": "a4df60c5-6ab4-412a-8f79-2cab93fb2bc5",
        "name": "JellyNext",
        "description": "Personalized Trakt recommendations, next seasons, trending movies, and watchlist downloads.",
        "overview": "Trakt-powered discovery for Jellyfin.",
        "owner": args.repository.split("/")[0],
        "category": "MoviesAndShows",
        "imageUrl": f"https://raw.githubusercontent.com/{args.repository}/{args.ref}/images/jellynext_logo_full.png",
        "versions": [{
            "version": version,
            "changelog": changelog,
            "targetAbi": target_abi,
            "sourceUrl": f"https://github.com/{args.repository}/releases/download/v{version}/{archive.name}",
            "checksum": checksum,
            "timestamp": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        }],
    }]
    (args.output / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
