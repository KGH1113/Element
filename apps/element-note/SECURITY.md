# Security

Report suspected vulnerabilities privately to the repository owner before public disclosure. Do not include secrets, personal data, or exploit payloads in a public issue.

## Plugin trust boundary

ElementNote custom JavaScript plugins are not sandboxed. They run inside the application with the current user's privileges and can access the compatibility APIs exposed to the renderer. Users must review plugin source and install only trusted code.

## RustSec audit status

The 2026-09-03 release audit reports zero known vulnerabilities after upgrading the Tauri stack and locking `bytes 1.12.1`, `time 0.3.55`, `anyhow 1.0.104`, `event-listener 5.4.2`, and `quick-xml 0.41.0`.

`cargo audit` also reports informational warnings that are not suppressed in CI:

- `RUSTSEC-2024-0411` through `RUSTSEC-2024-0420`, plus `RUSTSEC-2024-0370`: Tauri 2.11's WRY/tray stack retains unmaintained GTK3 crates and `proc-macro-error` for Linux targets. ElementNote releases only for macOS and Windows, so these target-specific crates are not linked into supported binaries.
- `RUSTSEC-2024-0429`: the affected `glib::VariantStrIter` implementation arrives through the same Linux-only GTK3 graph and is not reachable in supported macOS or Windows binaries.
- `RUSTSEC-2025-0075`, `RUSTSEC-2025-0080`, `RUSTSEC-2025-0081`, `RUSTSEC-2025-0098`, and `RUSTSEC-2025-0100`: unmaintained `unic-*` crates enter through `urlpattern -> tauri-utils`. These advisories report maintenance status rather than a known vulnerability. The dependency remains until the supported Tauri line replaces it; URL input controlled by the application is additionally scheme-validated where it can launch an external handler.

Dependency paths are reviewed whenever Tauri or the advisory database changes. CI runs RustSec without an ignore list so newly reported vulnerabilities fail the gate.
