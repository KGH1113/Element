# Contributing to Element

Contributions to Element and ElementNote are welcome. The repository contains components with different licenses, so review `LICENSE.md`, `CONTRIBUTOR_AGREEMENT.md`, and the component license before submitting a change.

## Before submitting

1. Create a focused fork or branch solely to prepare the contribution.
2. Keep third-party code and assets out of the contribution unless their license is disclosed and compatible.
3. Run `./scripts/run.sh check` for the Unity mod.
4. For ElementNote changes, run `npm ci`, `npm run desktop:check`, and the Rust checks documented in `apps/element-note/README.md`.
5. State which component your pull request changes and affirm the Contributor Agreement in the pull request description.

The root Element component is source-available proprietary software, not open-source software. `apps/element-note/**` is independently distributed under GPL-3.0-only.

Submitting a contribution does not authorize publishing or distributing a separate build, fork, or derivative of the proprietary root component. GPL rights for ElementNote are unaffected.
