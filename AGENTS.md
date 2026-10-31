# Before modifying the code...
- **DO NOT MODIFY THE CODE UNTIL I SAYS TO.**
- You are not the only one who modifies the code. Almost every code is modified by me. So please read the code often that I wrote.

# README.md rule
- IF you are new to this project 'cause the session started right before, read README.md to understand this project.
- If there is outdated thing in README.md, edit README.md to bring up to date.
- README.md is a landing page for people, not agent memory or a changelog. Keep it short and visual: banner, showcase videos, feature highlights, quick start, shortcuts, structure diagram, platform support, license.
- Write for someone who has never seen the project: say what a feature does for the player, not why it was built that way. No development-process reasoning or comparisons with tools the reader didn't ask about (e.g. "no need to run OBS" in a feature description, "works without JALib"). No emoji.
- **DO NOT** add implementation details, internal behavior specs, fixed-bug notes, migration notes, or "how it works in code" paragraphs to README.md. Put them in a file under `docs/` and link from README.md at most one line.
- A new feature gets at most one short bullet or card in README.md. Don't grow a section into a paragraph; if it needs more than two lines, it belongs in `docs/`.
- Keep README.md media in `docs/media/`. GitHub strips `<video>` tags that point at repository files, so showcase videos are embedded with GitHub-uploaded `https://github.com/user-attachments/assets/...` URLs; keep the source mp4 (H.264, under 10MB, no GIFs) in `docs/media/showcase/` so it can be re-uploaded. GitHub shows the uploaded file name above each video, so name the files by what they show (`Tweaks.mp4`). Each settings-section clip starts on the Settings home and shows entering that tab.
- Version, mod loader and platform are drawn in the banner, not in a badge row. After a version bump, rerun `node docs/media/src/render-banner.mjs <playwright>/index.mjs` (it reads `Element/Info.json`).

# Useful dev tool usage
- You can use `ilspycmd` and `assetripper(headless)` to inspect ADOFAI's code. Use it unless you really don't need to.
- Use `./scripts/run.sh` for building, packaging, checking, ext. instead of manually executing dotnet commands. Also maintain the scripts.
- After modifying the mod, complete the relevant checks and run `./scripts/run.sh install` to install the verified build into the user's mod folder.

# UI/UX
- ElementNote desktop app (`apps/element-note`, React): use shadcn mcp server actively. and use shadcn components.
- In-game UI (Unity UGUI under `Element/Presentation`): shadcn components can't run there, so follow shadcn's card/button/switch/slider patterns as the existing settings UI does.
- Read apple-design skill.
- [Toss tech blog: 좋은 에러 메시지를 만드는 6가지 원칙](https://toss.tech/article/21021) read this blog for better error ux.

# Extre rules
- Always think about the performance and do not make ADOFAI laggy.
- Read this if you are doing something with ADOFAI-IPC: [AdofaiIpc-Documentation](https://github.com/KGH1113/adofai-ipc/tree/main/docs)
- Always think about SOLID principles
