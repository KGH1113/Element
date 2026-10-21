# ADOFAI-IPC source snapshot

ADOFAI-IPC 2.1.0 canonical TypeScript source, including both the typed root API and the official `adofai-ipc/transport` message API. ImplDmNote uses the transport entry point for its existing feature protocols without an npm runtime dependency. Do not edit these sources directly. Regenerate both SDK and runtime with `./scripts/run.sh ipc-sync /path/to/adofai-ipc /path/to/release-dlls` from the repository root. `source.json` records the clean upstream revision and SHA-256 of every source file; `sdk:check` verifies the complete inventory.
