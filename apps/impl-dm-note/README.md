# ImplDmNote

ImplDmNote는 리듬 게임용 키 입력 시각화 데스크톱 앱입니다. DM Note v1.6.1을 기반으로 하며 ImplResourcePack과 localhost ADOFAI-IPC로 현재 키 리미터 프로필을 동기화합니다.

## 주요 기능

- 손·발 오버레이와 키 입력 횟수 표시
- 프리셋, 키음, 커스텀 CSS, 신뢰된 JavaScript 플러그인
- OBS 브라우저 소스 연결
- 노래 제목, 녹화 모드, 판정 표시 설정
- Windows ASIO 출력과 macOS Dock helper
- TUFReplay-Renderer의 자동 렌더 세션과 투명 프레임 출력
- ImplResourcePack 게임 화면에 기존 손·발 창을 표시하는 실시간 미러

## 게임 화면 키뷰어

macOS 11 이상에서 ImplResourcePack 모드와 ImplDmNote를 함께 실행하고 앱의 OBS 모드를 켜면, 게임 플레이 중 손·발 키뷰어가 게임 창 양쪽 아래에 표시됩니다. 모드의 **Show hand and foot viewers in game**은 기본으로 켜져 있으며 **Show viewer preview**로 메뉴에서도 확인할 수 있습니다. OBS Studio를 실행할 필요는 없습니다. 각 뷰어에서 선택한 탭을 따로 사용하고 최대 430×270포인트에 맞춰 배치합니다.

게임 창 내부의 WKWebView가 OBS 페이지를 직접 렌더링합니다. 프레임 캡처·임시 파일 전송·Unity 텍스처 업로드와 기존 이미지 미러는 제거했습니다. Windows·Linux의 게임 내 키뷰어는 아직 지원하지 않습니다. 리플레이 렌더링 중에는 게임 내 뷰어를 잠시 숨기고, 끝나면 페이지를 다시 불러오지 않고 표시합니다. Discord·OBS에서 얼불춤 창만 선택해도 키뷰어가 함께 캡처됩니다. 앱은 OBS 모드·토큰·포트나 리플레이 렌더링 상태가 바뀔 때만 게임에 키뷰어 정보를 보냅니다(`mirror.native.changed`·`mirror.native.unavailable`).

`./scripts/run.sh desktop-bundle --release`로 만든 앱을 사용하세요. Debug 앱은 OBS 페이지를 Vite 개발 서버로 리다이렉트하므로 독립 실행 검증에 적합하지 않습니다. [게임·Discord·OBS 검증 절차](../../docs/native-webview.md)를 참고하세요.

## 리플레이 렌더링

렌더 기능이 포함된 ImplDmNote와 TUFReplay-Renderer를 실행하면 앱이 ADOFAI-IPC 2.0.0의 localhost WebSocket에 자동 연결합니다. 웹에서 ImplDmNote 포함을 선택하고 Render를 누르면 현재 프리셋, 손·발 탭, CSS, JavaScript 플러그인과 패널 배치를 한 번 고정합니다. 별도 OBS 캡처나 Node, Chrome, 소스 경로, 수동 스냅샷 설정은 필요하지 않습니다.

렌더 세션 동안 실제 키보드·마우스·HID 입력과 전역 단축키는 키뷰어 입력 경로에서 제외됩니다. 기록된 키는 숨겨진 전용 화면에서만 재생하며 앱의 누적 카운터나 플러그인 저장소에는 저장하지 않습니다. 렌더 카운터는 기록에 포함된 입력을 0부터 셉니다. 종료·취소 시 실제 입력으로 돌아오며, 연결이 끊어지면 최대 15초 안에 앱 자체의 실시간 감시가 세션을 정리합니다. 세션 경계에서는 눌림 표시를 해제하여 키가 계속 눌린 채 남지 않게 합니다.

macOS는 앱에 포함된 WKWebView, Windows는 WebView2의 전체 화면 캡처를 사용합니다. 키·글자·카운터·통계·DOM 플러그인과 WebGL 노트가 함께 출력됩니다. 정상적인 플러그인 키/통계 API와 타이머는 렌더 시간에 맞춰 실행되며, 별도 Worker 시계나 외부 프로그램, 새 네트워크 데이터에 의존하는 플러그인은 별도 재생 계약이 필요합니다.

자동 배치는 Render를 누를 때 열려 있는 손·발 오버레이 창을 모두 포함합니다. 각 창의 선택된 탭과 실제 위치·크기를 따로 고정하고, 같은 기록 입력과 프레임 시간으로 재생합니다. 두 창 사이의 빈 공간은 캡처하지 않습니다. 창을 숨기면 해당 키뷰어는 포함되지 않으며, 발 창만 열어도 렌더링할 수 있습니다. 이 기능은 ADOFAI-IPC 2.0.0, 렌더러와 ImplDmNote를 함께 최신 렌더 빌드로 업데이트해야 합니다.

앱은 `dmnote.attach`로 렌더 기능을 알리고, Renderer가 동일 연결에서 `dmnote.begin/reset/frame/end` 명령을 직접 전달합니다. 앱은 이름이 있는 완료·오류 이벤트로 응답합니다. 프레임은 하나씩 처리하고 픽셀은 기존 세션 디렉터리의 RGBA/PNG 파일로 전달합니다. 이전 `hello/poll/reply` 또는 HTTP transport는 사용하지 않습니다. SDK 원본과 라이선스는 `vendor/adofai-ipc`에 포함되며 npm 패키지가 필요하지 않습니다.

기본 위치는 실제 오버레이 창과 게임 창의 화면 영역에서 계산하며, 영상 해상도에 맞춰 위치와 크기를 유지합니다. macOS는 최종 영상에 필요한 크기로 캡처한 RGBA 픽셀을 전달하여 프레임별 TIFF/PNG 변환을 생략합니다. Windows는 PNG 경로를 사용합니다. 수동 위치가 필요하면 렌더러의 `dmNote.automaticPlacement`를 `false`로 설정하고 `dmNote.viewerKind`로 손(`hand`) 또는 발(`foot`) 키뷰어 하나를 선택합니다.

개발 및 검증은 저장소 루트에서 실행합니다. 다음 명령은 게임에 설치하지 않습니다.

```bash
./scripts/run.sh desktop-check
./scripts/run.sh desktop-render-test
./scripts/run.sh desktop-build
./scripts/run.sh desktop-bundle
```

`desktop-render-test`는 배포용 프론트엔드를 빌드한 뒤 macOS의 격리된 비영구 WKWebView에서 합성 입력만 사용합니다. Tauri와 동일하게 읽기 전용 전역 속성을 설치하여 네이티브 연결을 보존하는지 확인합니다. 패키지에서 Tauri가 적용하는 CSP nonce 조건까지 재현하여 전역·탭 custom CSS와 고정한 로컬 폰트가 첫 프레임부터 적용되는지 검사합니다. 기존 앱이나 사용자 데이터, 입력 장치를 열지 않습니다. `desktop-bundle`은 개발 프로필의 앱에 프론트엔드를 내장하고 macOS에서는 로컬 서명을 확인한 뒤 `Release/ImplDmNote-Renderer-macos-<arch>.zip`을 생성합니다. Windows 캡처 코드는 포함되지만 macOS에서 Windows 실행 검증은 하지 않습니다.

## 개발

저장소 루트에서 Node.js 22와 stable Rust를 준비한 뒤 실행합니다.

```bash
npm ci
npm run desktop:dev
npm run desktop:check
cd apps/impl-dm-note/src-tauri
cargo test --locked
cargo clippy --locked --all-targets -- -D warnings
```

현재 운영체제의 릴리스 빌드는 `npm run desktop:build`로 만들 수 있습니다. 명시적인 플랫폼 명령은 `desktop:build:mac`과 `desktop:build:windows`입니다. Windows 공식 산출물은 `ImplDmNote-v<version>-windows-x64-portable.zip`이며 WebView2 Fixed Runtime과 법적 고지를 포함합니다.

## 데이터와 호환성

- Windows: `%APPDATA%/io.github.kgh1113.impldmnote/store.json`
- macOS: `~/Library/Application Support/io.github.kgh1113.impldmnote/store.json`

기존 프리셋을 위한 `dmn` 플러그인 API와 `dmnote-local-*` URI는 공개 호환 인터페이스로 유지됩니다. 이전 설정 파일의 알 수 없는 필드는 무시됩니다.

## 원본 DM Note와의 차이

기준 원본과 ImplDmNote 사이의 제품·배포·보안 차이는 [`DIFFERENCES.md`](DIFFERENCES.md)에 한곳으로 정리되어 있습니다. 정확한 원본 태그와 커밋, 저작권 정보는 [`UPSTREAM.md`](UPSTREAM.md)를 확인하세요.

## 보안

외부 링크는 `https`, `http`, `mailto` 스킴만 열 수 있습니다. 커스텀 JavaScript 플러그인은 샌드박스되지 않고 현재 사용자 권한으로 실행되므로, 내용을 검토한 신뢰할 수 있는 플러그인만 설치하세요. OBS 연결은 사용자가 시작한 로컬 브리지에 한정하고 세션 토큰을 사용합니다.

## 라이선스와 원본

ImplDmNote는 저장소 루트의 독점 라이선스 범위에서 제외된 독립적인 GPL-3.0-only 구성요소입니다. GPL 전문은 [`LICENSE`](LICENSE), 포함 자산과 SDK 고지는 [`THIRD_PARTY_NOTICES.txt`](THIRD_PARTY_NOTICES.txt) 및 [`THIRD_PARTY_LICENSES.txt`](THIRD_PARTY_LICENSES.txt)를 확인하세요.

Copyright (C) 2024 lee-sihun and DM Note contributors. 2026 ImplDmNote modifications Copyright (C) 2026 KGH1113.
