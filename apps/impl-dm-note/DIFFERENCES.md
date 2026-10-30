# Differences from DM Note

ImplDmNote is based on DM Note 1.6.1 at commit
`167f7c8223d790f8d1240737293dba46bedf4753`. The exact upstream provenance and
copyright information are recorded in [`UPSTREAM.md`](UPSTREAM.md).

This document records the intentional product-level differences from that
upstream snapshot.

## Identity and storage

- The application is named **ImplDmNote**.
- The bundle identifier is `io.github.kgh1113.impldmnote`.
- Application settings are stored under the ImplDmNote-specific application
  data directory, allowing it to coexist with DM Note.
- Internal Tauri capabilities, Windows named pipes, and the raw-input window
  class use ImplDmNote-specific names.
- The `dmn` plugin API and `dmnote-local-*` asset URIs remain available for
  preset and plugin compatibility.

## ImplResourcePack integration

- ImplDmNote connects to the local ADOFAI-IPC bridge.
- The active key-viewer profile is synchronized with ImplResourcePack's key
  limiter.
- macOS modifier keys retain distinct left/right mappings when synchronized.
- The overlay can display the current song title above the combo.
- ImplResourcePack adds recording mode and judgment-text visibility settings.

## First launch

- A new install opens with ready-made hand key viewers (16K, 12K, 10K, 8K,
  4K; 16K selected) and foot key viewers (0K, 2K, 4K, 6K, 8K on F1–F8; 0K
  selected) instead of two empty tabs. They come from
  `src-tauri/initial_key_viewers.json`.
- Those key viewers use the bundled MapleStory OTF Bold font, which is copied
  into the app data folder on first launch.
- Reset Data still returns to the upstream two empty tabs.

## Defaults

- Note effects are on by default, and Reset Data turns them back on.
- OBS mode is on by default, so in-game key viewers work without a trip to
  Settings. A store without `obsModeEnabled` loads with it on.
- Saved stores keep whatever the user chose.

## Removed upstream components

- The upstream release and automatic-update channel is removed.
- The Xbox Game Bar companion is removed.
- Promotional screenshots, animations, videos, release notes, and the
  documentation website are removed.
- The custom Windows single-executable packager is removed.

## Distribution and platform behavior

- Windows releases use one portable archive named
  `ImplDmNote-v<version>-windows-x64-portable.zip`.
- The portable archive contains `impl-dm-note.exe`, the GPL, upstream
  provenance, and third-party notices. It uses the system's Evergreen WebView2
  Runtime instead of bundling a Fixed Runtime.
- macOS builds include a Dock helper and the same legal notices in the app
  bundle.
- Platform-specific build commands are exposed as `desktop:build:mac` and
  `desktop:build:windows`; `desktop:build` selects the current platform.

## Security and maintenance

- External URL opening accepts only `https`, `http`, and `mailto`.
- The Tauri content security policy explicitly permits only the local app
  facilities, localhost OBS connection, user webfonts, and trusted inline
  plugin execution needed by the application.
- JavaScript plugins remain unsandboxed and run with the current user's
  privileges; the UI warns users to install only reviewed, trusted plugins.
- Tauri and affected transitive dependencies are updated, and desktop CI
  checks frontend tests, Rust tests, Clippy, dependency audits, license
  reports, and platform packaging.

## Licensing

- ImplDmNote remains GPL-3.0-only and is excluded from the repository root's
  proprietary license.
- Original DM Note authors retain copyright in their contributions.
- The retained upstream icon and in-app logo are credited in
  [`THIRD_PARTY_NOTICES.txt`](THIRD_PARTY_NOTICES.txt).
- The bundled MapleStory OTF Bold font is NEXON's and is not covered by the
  GPL; its terms are in [`THIRD_PARTY_NOTICES.txt`](THIRD_PARTY_NOTICES.txt).
- Cargo and production npm license texts are collected in
  [`THIRD_PARTY_LICENSES.txt`](THIRD_PARTY_LICENSES.txt).
