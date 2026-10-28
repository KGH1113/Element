# 모드에 포함된 ImplDmNote

모드 릴리스 패키지는 OS별로 나뉘고, 각 패키지에 그 OS용 ImplDmNote가 들어
있습니다. **일반 → ImplDmNote 자동 열기/닫기**(기본값 켜짐)를 켜 두면 게임을
켤 때 앱이 열리고, 게임을 끄면 앱도 닫힙니다.

| 패키지 | 들어 있는 것 |
|---|---|
| `ImplResourcePack-macos.zip` | 모드 DLL, macOS 네이티브 라이브러리, `Mac` 폰트, `ImplDmNote/macos/ImplDmNote.app.zip` |
| `ImplResourcePack-windows.zip` | 모드 DLL, `ImplGameWebView.dll`(스모크 테스트 통과본), `Windows` 폰트, `ImplDmNote/windows/` |

모두 버전 폴더 `Runtime/versions/<버전>/` 안에 있습니다([자동 업데이트](auto-update.md)).
모드가 업데이트되면 새 버전 폴더의 앱이 다음에 게임이 앱을 열 때 설치됩니다.

## 게임을 켤 때

`DmNoteAppLauncher`가 게임 스레드 밖에서 다음을 처리합니다.

1. 이미 실행 중이면 아무것도 하지 않습니다. macOS는 `pgrep -x impl-dm-note`,
   Windows는 `impl-dm-note` 프로세스 이름으로 확인합니다.
2. 실행 중인 버전 폴더의 앱을 고정된 위치로 복사합니다. 모드 빌드가 바뀌었을 때만
   다시 복사하며(`<설치 경로>.stamp`의 크기·수정 시각 비교), 새 폴더를 다 만든
   뒤 바꿔 끼웁니다.
   - macOS: `~/Library/Application Support/ImplResourcePack/ImplDmNote.app`.
     `ditto -x -k --noqtn`으로 풀어서 실행 권한과 서명이 유지됩니다.
   - Windows: `%LOCALAPPDATA%\ImplResourcePack\ImplDmNote`
3. `--launched-by-game`으로 실행합니다. macOS는 `open -g`라 게임이 앞에
   남습니다. Windows Release는 관리자 권한을 요구하므로 ShellExecute로 실행하고
   UAC 확인 창이 뜹니다. 거절하면 경고만 남기고 넘어갑니다.

모드 폴더에서 바로 실행하지 않는 이유: Windows는 실행 중인 exe가 잠겨 모드
업데이트를 막고, macOS는 일반 zip 해제에서 실행 권한이 빠집니다. 위치가
고정되어 있어 손쉬운 사용·입력 모니터링 권한과 앱 설정도 그대로 유지됩니다.

## 앱 쪽 동작

`--launched-by-game`으로 실행된 앱은:

- 창 없이 트레이(Windows)·메뉴 막대(macOS) 아이콘으로 시작합니다. 저장된 창
  상태는 바꾸지 않습니다. macOS는 Dock 아이콘을 눌러도 설정 창이 열립니다.
- 다시 실행 요청을 받아도(단일 인스턴스) 창을 띄우지 않습니다.
- 게임 연결이 한 번 된 뒤 5초 넘게 끊기면 스스로 종료합니다. 게임 재시작이나
  모드 재로드는 그 안에 다시 연결됩니다.

앱은 게임이 모드를 불러오는 중에 연결되므로, 게임이 키 프로필에 5초 안에
답하지 못하면 같은 프로필을 5초마다 다시 보냅니다(모드는 같은 revision을
duplicate로 확인).

Windows에서 앱 위치가 새로 바뀐 뒤 처음 OBS 모드를 켜면 Windows 방화벽이 네트워크
접근을 묻습니다. 게임 내 키뷰어와 같은 PC의 OBS는 localhost만 쓰므로 취소해도
됩니다. 다른 PC의 OBS에서 볼 때만 허용이 필요합니다.

사용자가 직접 연 앱은 게임을 꺼도 닫히지 않습니다. 옵션을 꺼도 이미 게임이 연
앱은 게임과 함께 닫힙니다.

게임에 연결됐을 때 OBS 모드가 꺼져 있고 "얼불춤 안에 표시"가 켜져 있으면,
앱이 실행될 때마다 한 번 OBS 모드를 켤지 묻습니다(켜기/무시). 게임이 연 앱이라면
이때 창을 띄웁니다.

## 패키징

```bash
./scripts/run.sh windows-webview-check   # Windows 게임 내 키뷰어 DLL (데스크톱 스모크 테스트)
./scripts/run.sh dmnote-windows-build    # Windows PC에서 ImplDmNote 포터블 빌드 (입력 조작 없음)
./scripts/run.sh package                 # macOS에서 실행, macOS 앱은 직접 빌드
```

- `package`는 macOS에서만 실행합니다. macOS 앱은 지금 앱 소스(커밋하지 않은
  변경 포함)로 만든 빌드가 있으면 재사용하고, 없으면
  `npm run desktop:build:mac`으로 빌드합니다.
- Windows 앱은 `dmnote-windows-build`가 커밋된 `apps/impl-dm-note`,
  `package.json`, `package-lock.json`만 `git archive`로 보내 Windows PC에서
  `npm run desktop:build:windows`로 빌드합니다. 커밋하지 않은 앱 변경이 있으면
  거부합니다. ASIO 오디오 백엔드가 bindgen을 쓰므로 그 PC에 LLVM이 필요합니다
  (`winget install LLVM.LLVM` 한 번, 또는 `LIBCLANG_PATH`). Node 22는 없으면
  빌드 캐시에 받아 씁니다.
- 릴리스(`./scripts/run.sh release`)는 두 OS 앱 모두 main의 CD 빌드를
  씁니다([릴리스](releasing.md)).
- CD는 Windows 포터블 zip을 `impl-dm-note-windows-portable` 아티팩트로
  올립니다. 로컬 `package`에서 이를 쓰려면 `IMPL_RESOURCEPACK_DMNOTE_WINDOWS_ZIP`,
  앱 없이 패키징하려면 `IMPL_RESOURCEPACK_SKIP_DMNOTE=1`을 지정합니다.
- `IMPL_RESOURCEPACK_PACKAGE_PLATFORMS=macos`처럼 OS를 지정하면 그 OS의
  패키지만 만듭니다(테스트용).
- 로컬 `install`/`build`는 macOS 앱도 함께 설치합니다. 앱 소스가 바뀐 뒤 처음
  설치할 때만 다시 빌드합니다(몇 분). 앱이 바뀌지 않았으면 모드 폴더의 zip도
  그대로 두어, 게임이 앱을 다시 설치하지 않습니다.
- `windows-install`은 Windows PC에 접속하지 않고
  `build/ImplResourcePack-windows-install.zip`을 만듭니다. 커밋된 앱 소스로
  CD가 만든 Windows 앱(main merge나 `gh workflow run cd.yml --ref dev`)을
  `gh`로 받아 넣습니다. 그런 빌드가 아직 없으면 앱 없이 만들고, 그 PC의
  `Install.ps1`이 이미 있던 앱을 유지합니다. Windows 게임 내 키뷰어도 이
  Mac에 현재 소스의 빌드가 없으면 CD에서 받습니다.
- `IMPL_RESOURCEPACK_SKIP_DMNOTE=1`이면 `install`과 `windows-install`도 앱을
  새로 넣지 않습니다.

Windows 앱은 Fixed WebView2 런타임을 넣지 않고 시스템의 Evergreen 런타임을
씁니다. 게임 내 키뷰어도 같은 런타임을 사용합니다.

## 라이선스

ImplDmNote는 GPL-3.0-only입니다. 모드 패키지에는 별개 프로그램으로 함께 들어
있으며, 앱 안에 라이선스와 고지 파일이 있습니다(macOS는 `Contents/Resources`,
Windows는 `ImplDmNote/windows/`). 소스는 이 저장소의 `apps/impl-dm-note`입니다.
