# Bundled ADOFAI-IPC v2

Release DLLs built from the revision in source.json. Contracts and Loader retain assembly identity 1.0.0.0; product version is 2.1.0 and wire protocol is 3. SHA256SUMS pins the complete runtime bundle. Consumer mods reference only Contracts and Loader. Copy all four files together and restart the game after replacing a bundle.

Regenerate with `./scripts/run.sh ipc-sync /path/to/adofai-ipc /path/to/release-dlls` after building upstream with its `./scripts/run.sh package`. Sync records provenance and hashes; it does not certify upstream test results. At revision `51949b2`, Release builds and Unity compatibility checks passed, while the upstream typed-file partial-export rollback regression failed. ImplResourcePack uses the message transport API rather than typed-file export.
