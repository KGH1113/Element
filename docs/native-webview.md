# 게임 내 네이티브 키뷰어

게임 내 키뷰어는 macOS 11 이상에서 네이티브 웹뷰로 렌더링합니다.
기존 이미지 미러는 제거했으며 Windows와 Linux는 아직 지원하지 않습니다.

## 동작

ImplDmNote의 OBS 브릿지가 손·발 페이지의 인증된 loopback URL과 원본 논리
뷰포트 크기를 제공합니다. 게임 모드가 네이티브 라이브러리를 로드하고 실제
Unity 창의 `contentView` 안에 투명한 WKWebView 두 개를 붙입니다. 별도 앱 창은
생성하지 않습니다. 웹뷰는 입력과 포커스를 받지 않아 게임 조작을 통과시킵니다.

이 모드에서는 게임이 이미지 캡처를 요청하지 않습니다. 파일 읽기·쓰기와
Unity 텍스처 업로드도 없습니다. 2초마다 URL·크기·뷰어 구성을 확인하고,
화면 애니메이션은 웹뷰가 직접 그립니다. 실제 성능 개선 폭은 측정 전입니다.
같은 URL을 다시 받아도 페이지를 재로드하지 않습니다. OBS가 꺼지거나 앱 연결이
끊기면 웹뷰를 제거하고, 준비되면 다시 연결합니다.

손·발 페이지는 각 뷰어에서 선택한 탭을 따로 사용합니다. 게임 좌우 아래에
16포인트 간격으로 배치하고 최대 430×270포인트에 맞춥니다. 원본 CSS 뷰포트를
보존하도록 페이지 배율을 적용하며 창이 작아지면 서로 겹치지 않게 축소합니다.

## 빌드와 설치

저장소 루트에서 실행합니다.

```bash
./scripts/run.sh mod-check
./scripts/run.sh native-webview-check
./scripts/run.sh desktop-frontend-check
./scripts/run.sh desktop-native-check
./scripts/run.sh install
./scripts/run.sh desktop-bundle --release
```

모드 설치 후 게임을 재시작해야 새 DLL을 로드합니다. 실행 중인 게임은 기존
버전을 계속 사용합니다. 네이티브 라이브러리는 모드 폴더의
`native/libImplGameWebView.dylib`에 포함됩니다.

테스트 앱은
`apps/impl-dm-note/src-tauri/target/release/bundle/macos/ImplDmNote.app`입니다.
압축본은 `Release/ImplDmNote-Renderer-macos-arm64.zip`입니다.
기존 ImplDmNote 인스턴스를 종료한 뒤 이 앱을 실행합니다. 기존 앱 데이터와
프리셋을 사용하며 테스트를 위해 초기화하지 않습니다.

Release 앱을 사용해야 합니다. Debug 앱의 OBS 브릿지는
`http://localhost:3400/obs/index.html` 개발 서버로 리다이렉트하지만,
게임 웹뷰는 제공받은 OBS 서버와 같은 origin의 탐색만 허용합니다.
Release 빌드는 내장 프론트엔드를 OBS 서버에서 직접 제공하므로 Vite가 필요 없습니다.

## 사용하기

1. 업데이트된 ImplDmNote에서 손·발 뷰어의 원하는 탭·모양을 선택합니다.
2. 앱 설정에서 OBS 모드를 켭니다. OBS Studio 자체를 실행할 필요는 없습니다.
3. 게임의 UnityModManager에서 Impl Resource Pack 옵션을 엽니다.
4. **Show hand and foot viewers in game**을 켭니다.
5. **Show viewer preview**를 누릅니다. 플레이를 시작하지 않아도 표시할 수 있습니다.

게임 내 표시는 기본으로 켜져 있습니다. 이전 `NativeDmNoteInGame` 설정은
더 이상 사용하지 않으며, `ShowDmNoteInGame`에 저장한 표시 여부는 유지합니다.

설정 아래 상태 메시지로 연결과 로드 실패 원인을 확인할 수 있습니다.
`Native viewers attached`는 네이티브 뷰 부착과 탐색 요청이 성공했다는 뜻이며,
페이지 전체 로드나 캡처 성공까지 확인한 메시지는 아닙니다.
Record mode와 협동 모드에서는 표시하지 않습니다.

## 직접 검증할 항목

| 항목 | 확인 방법 | 통과 기준 |
|---|---|---|
| 게임 창 내부 표시 | 메뉴 미리보기를 켜고 손·발 입력 | 두 뷰어가 각자 선택한 탭을 표시하고 입력에 반응 |
| 창 이동·크기 | 창 이동, 크기 변경, 다른 배율의 모니터로 이동 | 창 안 좌우 아래에 유지, 비율 유지, 서로 겹치지 않음 |
| 전체 화면·복원 | 전체 화면 진입·종료, 최소화·복원 | 복원 후 정상 표시, 별도 떠 있는 창 없음 |
| 게임 입력 | 키뷰어 영역 위를 클릭하고 게임 키 입력 | 키뷰어가 포커스를 빼앗거나 게임 입력을 막지 않음 |
| 앱 연결 | OBS 끄기·켜기, 앱 종료·재실행 | 오래된 화면을 제거하고 재연결 |
| 모드 수명 | 미리보기 끄기, 모드 비활성화·다시 활성화 | 웹뷰 제거, 중복 창·중복 연결 없이 다시 표시 |
| Discord | 공유 대상에서 **얼불춤 앱만** 선택, 수신 화면 확인 | 게임과 두 키뷰어가 함께 보이고 갱신됨 |
| OBS | macOS 화면 캡처 소스를 **윈도우 캡처**로 설정하고 **얼불춤 창만** 선택 | 미리보기와 로컬 녹화에 게임·키뷰어가 함께 포함 |

화면 전체 캡처나 별도 브라우저 소스 추가로 보인 결과는 위 캡처 조건의 통과로
기록하지 않습니다. Discord는 실제 수신 화면까지 확인해야 합니다. 방송·통화 시작은
사용자가 직접 진행합니다. 캡처 앱의 버전과 OS 버전, 선택한 캡처 방식을 함께 기록하세요.

실제 캡처에서 웹뷰가 빠진다면 네이티브 자식 뷰가 해당 캡처 경로에 포함되지 않는
경우입니다. 이때는 게임 렌더링에 합성하는 방식이 필요합니다. 기본 렌더링
방식의 전환 자체가 Discord·OBS 캡처 호환성을 검증한 것은 아닙니다.

## 표시 끄기

**Hide viewer preview**를 누르고 **Show hand and foot viewers in game**을
끄면 게임 내 키뷰어를 표시하지 않습니다. 기존 이미지 미러로 전환하는 옵션은 없습니다.

## 검증 범위

2026-10-22 기본 방식 전환 후 검증 결과:

| 검사 | 결과 |
|---|---|
| `mod-check` | 빌드 성공, 경고·오류 0개, C# 테스트 17개 통과 |
| `desktop-frontend-check` | SDK·타입·린트·포맷 검사, Vitest 154개, Node 테스트 7개, 프로덕션 빌드 통과 |
| `desktop-native-check` | Cargo check·test·Clippy 통과, Rust 테스트 42개를 lib/bin 각각 실행 |
| `native-webview-check` | universal 라이브러리·서명·실제 WebKit 독립 프로브 통과 |
| `check` | 셸 스크립트 26개 검사 통과 |
| 이전 경로 제거 | 이미지 미러·프레임/종료 명령·캐시 파일 세션·Unity 이미지 텍스처 경로와 전환 옵션 제거 |

기존 이미지 캡처에만 해당하는 테스트는 제거했고, 네이티브 요청의 연결 해제·
중복 요청·늦은 응답 차단과 오류에서 OBS 토큰을 노출하지 않는 동작을 검증합니다.
이 전환 검증에서는 실행 중인 게임과 앱을 교체하거나 재시작하지 않았습니다.
실제 게임 표시와 앱 번들에 대한 아래 기록은 최초 네이티브 구현의 검증입니다.

최초 실제 게임 테스트에서 `The Unity game window was not found`가 표시되는 문제를
확인했습니다. 설치된 UnityPlayer는 `PlayerWindow`·`PlayerWindowView`를 사용하지만
초기 탐색기는 `UnityView` 계열만 인식한 것이 원인이었습니다. 해당 실제 클래스
이름을 인식하도록 수정하고, 독립 프로브도 같은 클래스 이름을 사용하도록
보완했습니다. 이 수정 라이브러리를 적용하려면 게임을 완전히 종료하고 다시
실행해야 합니다. 모드 끄기·켜기만으로는 이미 로드된 네이티브 코드가 바뀌지 않습니다.

2026-10-22 최초 네이티브 구현의 로컬 검증 결과:

| 검사 | 결과 |
|---|---|
| `mod-check` | 빌드 성공, 경고·오류 0개, C# 테스트 17개 통과 |
| `desktop-frontend-check` | SDK·타입·린트·포맷 검사, Vitest 158개, Node 테스트 7개, 프로덕션 프론트엔드 빌드 통과 |
| `desktop-native-check` | Cargo check·test·Clippy 통과, Rust 테스트 43개를 lib/bin 각각 실행 |
| `native-webview-check` | arm64·x86_64 라이브러리·서명·실제 WebKit 독립 프로브 통과 |
| `check` | 셸 스크립트 26개 검사 통과 |
| `desktop-bundle --release` | 최적화 앱 빌드·앱 번들·로컬 서명 검증·ZIP 생성 완료 |
| 설치 | 수정된 네이티브 라이브러리를 `Mods/ImplResourcePack`에 설치하고 게임 완전 종료·재실행 완료, 설치본 해시 일치 확인 |
| 실제 얼불춤 | 수정 후 에디터 화면의 왼쪽 아래 손 뷰어·오른쪽 아래 발 뷰어 표시 확인 |
| Discord·OBS | 미검증 |

`native-webview-check`는 실제 엔진과 같은 클래스 이름의 합성 PlayerWindow/
PlayerWindowView 창과 실제 WKWebView, 로컬 HTTP fixture로
ABI·부착·배율·투명 배경·입력 통과·창 크기 조절·URL 재사용·제거·재부착을 검증합니다.
이 검사는 실제 게임의 뷰 계층이나 Discord·OBS 캡처 포함 여부를 입증하지 않습니다.
실제 게임 창 내부의 최초 표시까지는 별도로 확인했으며, 나머지 게임 동작과
Discord·OBS 캡처 검증 항목은 추가로 확인해야 합니다.
