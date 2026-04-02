<p align="center">
  <img src="docs/logo.png" alt="Muxarr" width="120"/><br/>
  <a href="https://github.com/KirovAir/muxarr/actions/workflows/build-and-deploy.yml"><img src="https://github.com/KirovAir/muxarr/actions/workflows/build-and-deploy.yml/badge.svg" alt="Build and Deploy"/></a>
  <a href="https://github.com/KirovAir/muxarr/pkgs/container/muxarr"><img src="https://img.shields.io/badge/ghcr.io-kirovair%2Fmuxarr-blue?logo=docker" alt="Docker Image"/></a>
  <a href="https://www.gnu.org/licenses/gpl-3.0"><img src="https://img.shields.io/badge/License-GPLv3-blue.svg" alt="License: GPL v3"/></a>
</p>

# Muxarr

Muxarr cleans up your media files by removing redundant audio and subtitle tracks and standardizing track metadata. It uses **mkvmerge** to remux files - not re-encode them. Remuxing copies streams into a new container without decoding, so there is **zero quality loss** and it runs fast even on low-end hardware like a NAS or Raspberry Pi.

Integrates with your existing media management services for original language detection and automatic processing via webhooks.

### Quick Start

```yaml
services:
  muxarr:
    image: ghcr.io/kirovair/muxarr:latest
    container_name: muxarr
    environment:
      - TZ=Europe/Amsterdam
      - PUID=1000
      - PGID=1000
    volumes:
      - /path/to/data:/data
      - /path/to/media:/media
    ports:
      - 8183:8183
    restart: unless-stopped
```

## Features

- **Lossless track removal** - strip redundant audio tracks (commentary, foreign dubs) and subtitles (SDH, foreign). A typical 4GB file processes in about a minute depending on disk speed, saving up to 10% in file size.
- **Original language detection** - integrates with your media manager so foreign films and shows always keep the correct audio track
- **Automatic processing** - webhook support to process new imports as they arrive
- **Per-directory profiles** - different language and track rules for different collections (e.g. anime vs western media)
- **Smart metadata fixes** - cleans up encoder tags and codec dumps from track names. Uses mkvpropedit for metadata-only changes (instant, no remux needed)
- **Safe by default** - validates the output file before replacing the original. If anything fails, the original is untouched.
- **Library overview** - browse your library with codec, resolution, and language breakdowns

## Screenshots

<p align="center">
  <img src="https://raw.githubusercontent.com/KirovAir/muxarr/master/docs/screenshots/conversion.png" alt="Conversion Details" width="800"/><br/>
  <em>Conversion detail: 4.48 GB to 4.02 GB (10.3% saved) by removing redundant audio and subtitle tracks</em>
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/KirovAir/muxarr/master/docs/screenshots/before.png" alt="Before" width="600"/><br/>
  ⬇️<br/>
  <img src="https://raw.githubusercontent.com/KirovAir/muxarr/master/docs/screenshots/after.png" alt="After" width="600"/><br/>
  <em>Before and after metadata cleanup</em>
</p>

<details>
<summary>More screenshots</summary>
<br/>
<p align="center">
  <img src="https://raw.githubusercontent.com/KirovAir/muxarr/master/docs/screenshots/filedetails.png" alt="File Details" width="800"/><br/>
  <em>File details with track preview</em>
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/KirovAir/muxarr/master/docs/screenshots/dashboard.png" alt="Dashboard" width="800"/><br/>
  <em>Dashboard</em>
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/KirovAir/muxarr/master/docs/screenshots/settings.png" alt="Profile Settings" width="800"/><br/>
  <em>Profile settings</em>
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/KirovAir/muxarr/master/docs/screenshots/statistics.png" alt="Statistics" width="800"/><br/>
  <em>Statistics</em>
</p>
</details>

## Installation

### Docker Compose

```yaml
services:
  muxarr:
    image: ghcr.io/kirovair/muxarr:latest
    container_name: muxarr
    environment:
      - TZ=Europe/Amsterdam
      - PUID=1000
      - PGID=1000
    volumes:
      - /path/to/data:/data
      # Media paths should match those used by your existing services
      # so Muxarr can find files by the same paths they report.
      - /path/to/media:/media
    ports:
      - 8183:8183
    restart: unless-stopped
```

### Docker Run

```bash
docker run -d \
  --name=muxarr \
  -e TZ=Europe/Amsterdam \
  -e PUID=1000 \
  -e PGID=1000 \
  -p 8183:8183 \
  -v /path/to/data:/data \
  -v /path/to/media:/media \
  --restart unless-stopped \
  ghcr.io/kirovair/muxarr:latest
```

## Configuration

### Environment Variables

| Variable | Description | Default |
|---|---|---|
| `TZ` | Timezone | `UTC` |
| `PUID` | User ID for file permissions | `1000` |
| `PGID` | Group ID for file permissions | `1000` |
| `ConnectionStrings__DefaultConnection` | SQLite connection string | `Data Source=/data/muxarr.db` |

### Volumes

| Path | Description |
|---|---|
| `/data` | Database and configuration |
| `/media` | Media files (use multiple `-v` mounts as needed) |

### Setup

1. Open `http://your-ip:8183`
2. Create a profile with your media directories and language rules
3. Optionally connect your media manager for original language detection and webhook automation
4. Scan your library, preview the changes, and queue files for processing

## Built With

- [.NET 10](https://dotnet.microsoft.com/) / Blazor
- [MKVToolNix](https://mkvtoolnix.download/) (mkvmerge, mkvpropedit)
- [FFmpeg](https://ffmpeg.org/)

## License

GPL-3.0 - see [LICENSE](LICENSE.md).

Muxarr is not affiliated with Sonarr, Radarr, or any other *arr projects.
