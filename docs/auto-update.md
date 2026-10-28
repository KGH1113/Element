# 자동 업데이트

게임을 켤 때 모드가 GitHub의 최신 릴리스를 확인하고, 새 버전을 받아 그
실행에서 바로 적용합니다. 모드 패키지에 ImplDmNote가 들어 있으므로 앱도 함께
업데이트됩니다([모드에 포함된 ImplDmNote](bundled-impl-dm-note.md)).
**일반 → 자동 업데이트**에서 끌 수 있고, 같은 줄에 이번 실행의 결과가 표시됩니다.

## 설치된 모드 구조

```text
Mods/ImplResourcePack/
  Info.json                     UMM 진입점: 현재 버전의 런처
  Settings.xml                  모드 설정 (업데이트해도 그대로)
  UpdateSettings.json           자동 업데이트 설정 (없으면 켜짐)
  AdofaiIpc.*.dll, ipc/         다른 모드가 찾는 IPC 번들
  Runtime/state.json            Current · Previous · Trial · Failed
  Runtime/launchers/<v>/        ImplResourcePack.Launcher.dll
  Runtime/versions/<v>/         모드 DLL, 업데이터, 네이티브 라이브러리, 폰트, ImplDmNote
```

버전마다 폴더가 따로 있어서 실행 중인 파일을 덮어쓰지 않습니다. Windows의
파일 잠금도 생기지 않습니다.

## 게임을 켤 때

1. UMM이 `Info.json`의 런처를 불러옵니다. 런처는 `state.json`을 읽고, 지난
   실행이 남긴 문제를 고칩니다. 끝나지 않은 Trial은 실패로 기록하고, 망가진
   Current는 Previous로 되돌립니다.
2. 현재 버전 폴더의 업데이터가 `releases/latest/download/ImplResourcePack.update.json`을
   받습니다(5초 제한). 더 새 버전이 있으면 그 태그의 `ImplResourcePack-<os>.zip`을
   받아 크기와 SHA-256을 확인하고, 그 버전 폴더만 풀어 둡니다.
3. 시작부터 20초 안에 설치가 끝나면 그 버전을 Trial로 불러옵니다. 성공하면
   Current로 올리고(이전 버전은 Previous), `Info.json`이 새 런처를 가리키게
   합니다. 다음 실행부터 새 런처가 쓰입니다.
4. 20초를 넘기면 지금 버전으로 게임을 시작하고 다운로드는 백그라운드에서
   계속합니다. 다음 실행에서 받아 둔 버전을 바로 적용합니다.
5. 새 버전의 `Main.Load`가 실패하면 그 버전을 `Failed`에 넣고 지웁니다. 실패한
   버전은 다시 받지 않으며, 게임을 다시 켜면 이전 버전이 실행됩니다.

오프라인이면 매니페스트 요청이 5초 안에 실패하고 지금 버전으로 시작합니다.
GitHub에 릴리스가 없거나 매니페스트가 없으면 업데이트할 것이 없는 것으로
봅니다. 응답이 `github.com`·`*.githubusercontent.com` 밖으로 리디렉션되면
받지 않습니다.

## 버전 사이의 약속

새 런처는 다음 실행에서야 쓰이므로, 업데이트는 항상 **이전 버전의 런처와
업데이터**가 수행합니다. 그래서 아래 계약은 바꾸지 않습니다.

- 런처 → 업데이터: `ImplResourcePack.Updater.EntryPoint.Resolve(ModEntry, string)`에
  JSON 요청을 넘기고 JSON 결과를 받습니다.
- 런처 → 모드: 버전 폴더 `Info.json`의 `AssemblyName`·`EntryMethod`
  (`ImplResourcePack.Main.Load(ModEntry)`).
- `ImplResourcePack.update.json`의 `schemaVersion: 1` 형식과 패키지 안의
  `ImplResourcePack/Runtime/versions/<v>/` 위치.

모드는 버전 폴더(`Main.RuntimePath`)에서 폰트·네이티브 라이브러리·ImplDmNote를
읽고, 모드 루트(`Main.Path`)에는 설정만 둡니다. 시작할 때 자기 IPC 번들을
루트로 복사합니다. 다른 모드의 IPC 로더가 `Mods/<mod>/ipc/`에서 찾기 때문입니다.

## 개발 빌드

`./scripts/run.sh build`/`install`, `windows-install`로 설치한 버전에는
`.development` 표시가 붙어 스스로 업데이트하지 않습니다. 설치할 때마다
`Runtime/state.json`을 새로 쓰고 다른 버전 폴더는 지웁니다.

업데이터를 직접 시험하려면:

1. `IMPL_RESOURCEPACK_INSTALL_UPDATABLE=1 ./scripts/run.sh build`로 표시 없이 설치합니다.
2. 버전을 올려 패키지를 만들고(`./scripts/run.sh version 0.2.1`,
   `IMPL_RESOURCEPACK_PACKAGE_PLATFORMS=macos IMPL_RESOURCEPACK_SKIP_DMNOTE=1 ./scripts/run.sh package`),
   버전을 되돌립니다.
3. GitHub과 같은 경로로 로컬 서버를 엽니다:
   `<root>/releases/latest/download/ImplResourcePack.update.json`,
   `<root>/releases/download/v0.2.1/ImplResourcePack-macos.zip`.
4. 설치된 모드의 `UpdateSettings.json`에 `"Source": "http://127.0.0.1:<port>/releases/"`를
   넣고 게임을 켭니다. 루프백 HTTP 주소만 받아들입니다.
5. 끝나면 `UpdateSettings.json`을 지우고 `./scripts/run.sh install`로 되돌립니다.

## 한계

- 프리릴리스(`0.3.0-beta.1`)는 GitHub의 latest가 아니므로 자동으로 받지 않습니다.
- Linux용 패키지는 없어서 Linux에서는 확인하지 않습니다.
- ImplDmNote가 이미 실행 중이면 새 앱은 다음에 게임이 앱을 열 때 설치됩니다.
- 무결성은 같은 릴리스의 SHA-256으로 확인합니다. 별도 서명은 없습니다.
- 새 버전의 런처는 Trial에서 시험되지 않고 다음 실행부터 쓰입니다. 런처 자체가
  시작하지 못하는 릴리스는 롤백할 수 없어 다시 설치해야 하므로, 런처
  (`ImplResourcePack.Launcher`)를 바꾼 릴리스는 위의 로컬 서버 방법으로 게임에서
  두 번 실행해 확인한 뒤 게시합니다.
