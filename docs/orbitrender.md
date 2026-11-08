# OrbitRender 렌더

[OrbitRender](https://github.com/imnyang/OrbitRender)로 영상을 만들 때 Element의
오버레이와 ElementNote 키뷰어가 함께 들어갑니다.

## 영상에 들어가는 것

| | 리플레이 렌더 (TUFReplay에서 시작) | 일반 렌더 |
|---|---|---|
| 오버레이 (텍스트·도형) | 들어감 | OrbitRender의 **Mod Overlay**를 켜면 들어감 |
| ElementNote 키뷰어 | 게임에서 보이는 손·발 키뷰어가 같은 자리에 들어감 | 들어가지 않음 (기록된 키 입력이 없음) |
| 시도 횟수 | 늘지 않음. 기록한 플레이의 시도 횟수를 보여 줌 | 늘지 않음 (`Time.captureFramerate`로 렌더 중임을 앎) |

- 키뷰어의 키 카운트는 TUFReplay가 그 기록과 함께 저장한 카운트에서 이어집니다
  ([TUFReplay 기록](tufreplay.md)). 저장된 값이 없으면 렌더를 시작할 때 ElementNote에 쌓여 있던
  값에서 이어집니다. 렌더 중에 눌린 횟수는 영상에만 쓰이고 ElementNote의 카운트에는 더해지지 않습니다.
- 녹화 모드나 협동 모드에서는 게임처럼 키뷰어를 넣지 않습니다.
- 직접 옮긴 오버레이 요소는 영상에서도 고정한 기준점(위·아래·모서리)에서 같은 거리에 놓입니다.
  게임 창과 영상의 화면비가 달라도(예: 16:10 화면, 16:9 영상) 가장자리에 붙인 요소가 영상 밖으로
  밀려나지 않습니다.
- 렌더 중에는 화면이 영상보다 느리게 돌고 레벨이 에디터에서 재생되므로, 아래 값은 화면 대신
  영상을 따릅니다. 나머지 값(진행도·정확도·판정·콤보·BPM·시도 횟수)은 렌더가 프레임마다
  재생하는 게임 상태를 그대로 읽어서 따로 바꾸지 않습니다.

  | 값 | 렌더 중 |
  |---|---|
  | FPS | `-`. 렌더 중 화면의 프레임레이트는 영상과 관계가 없어서 숫자를 쓰지 않습니다. 이 값을 쓰는 계산식은 비어 보입니다 |
  | 음악 시간 | 영상 프레임마다 OrbitRender가 맞춘 지휘자(conductor) 위치로 계산 |
  | 곡 제목 | 게임 플레이 화면에 나오는 레벨 이름. 에디터는 이 글자를 쓰지 않아서 직접 만듭니다 |
- 다음 경우에는 키뷰어 없이 렌더링하고, 렌더가 끝나면 이유와 해결 방법을 알림으로 보여줍니다.
  - ElementNote가 꺼져 있거나 **Show inside ADOFAI**가 꺼져 있을 때
  - ElementNote가 렌더 프로토콜 2보다 오래된 버전일 때 (ElementNote 업데이트 안내)
  - TUFReplay나 OrbitRender가 키뷰어 영상을 덧씌우지 못하는 버전일 때 (둘 다 업데이트 안내)
  - ElementNote가 키뷰어 영상을 끝까지 만들지 못했을 때. 이때는 영상 전체에 키뷰어가 없습니다.

## 동작

게임 안 키뷰어는 Unity 화면 위에 얹은 네이티브 웹뷰라서 OrbitRender가 찍지
못합니다. 그래서 리플레이 렌더에서는 ElementNote가 키뷰어를 따로 그려 투명한 영상으로
저장하고, OrbitRender가 완성된 영상 위에 그 영상을 덧씌웁니다(웹캠을 합성하는 FFmpeg 단계).
ElementNote는 게임과 나란히 그리므로 게임은 키뷰어를 기다리지 않습니다.

리플레이 렌더는 TUFReplay가 주도합니다. TUFReplay가 기록한 입력과 판정을 재생하고
모드들에게 알리며, OrbitRender는 프레임을 찍고 영상으로 인코딩·합성만 합니다.

1. Element는 TUFReplay의 `ReplayApi`에 `Element.Render`로 리플렉션 등록합니다
   ([TUFReplay 기록](tufreplay.md)의 다른 두 provider와 함께). TUFReplay가 없어도 Element는
   그대로 동작합니다.
   - 녹화된 키는 `OnReplayKey`로, 영상 시각은 `OnReplayClock`으로 받습니다.
   - 영상에 찍을 오버레이 캔버스는 `GetReplayCanvases`로 넘깁니다. 키뷰어 캔버스는 없습니다.
   - 키뷰어 영상은 렌더가 끝난 뒤 `AddReplayOverlayVideos`로 넘깁니다.
   - 게임 안 리플레이에서는 이 provider가 아무 일도 하지 않습니다. ElementNote가 자기 창에 보여 줍니다.
   - Unity 키 이름이 없고 네이티브 키 코드만 있는 키는, 녹화한 플랫폼을 알 수 없어 키뷰어에 보이지 않습니다.
   - 일반 렌더에서는 OrbitRender의 Mod Overlay가 `Element Overlay` 캔버스를 스스로 찾습니다.
     Element는 OrbitRender에 등록하지 않습니다.
2. 영상이 처음 쓰일 때(첫 캔버스 요청이나 첫 영상 시각) Element가 켜져 있는 키뷰어마다
   영상 속 위치와 크기를 계산합니다. TUFReplay는 provider를 차례로 시작하므로, 이때까지
   기다려야 기록한 키 카운트가 들어와 있고 영상 크기도 정해져 있습니다. 게임 화면 대비 영상 크기 비율 `sqrt((영상 너비 / 화면 너비) × (영상 높이 / 화면 높이))`로
   크기와 여백을 함께 늘리고, 키뷰어가 고정된 꼭짓점은 그대로 둡니다.
   - 이때 `ReplayApi.RenderTakesOverlayVideos`가 참이고 `RenderFfmpegPath`가 있어야 합니다.
     없으면 키뷰어 없이 렌더링합니다.
3. Element가 ElementNote에 `render.begin`을 보냅니다. ElementNote는 현재 프리셋·탭·CSS·플러그인을
   숨겨진 창에 고정하고, 페이지가 실제로 배치한 뷰포트와 여백을 알려줍니다. 첫 프레임만 이 답을
   기다립니다(최대 30초).
4. 영상 프레임을 찍기 직전마다(`OnBeforeFrameCapture`) Element가 그동안 눌린 키와 프레임 번호·시각을
   `render.frame`으로 보내고 바로 넘어갑니다. ElementNote는 받은 순서대로 손·발 페이지를 각자
   그려서 페이지마다 FFmpeg로 인코딩합니다.
5. 렌더가 끝나면 OrbitRender가 합성 전에 TUFReplay를 거쳐 `AddReplayOverlayVideos`를 부릅니다.
   Element는 `render.finish`를 보내고 ElementNote가 모든 프레임을 다 쓸 때까지 기다린 뒤,
   키뷰어마다 영상 경로와 두 사각형을 넘깁니다.
   - 원본(source): 페이지 영상에서 여백을 정수 픽셀로 잘라낸 부분
   - 대상(target): 렌더 영상 안의 위치와 크기(정수 픽셀). 페이지 배율로 그린 키뷰어는 한 픽셀이
     한 픽셀에 들어가고, 같은 페이지를 쓰는 더 작은 키뷰어는 OrbitRender가 줄여서 넣습니다.
6. OrbitRender가 합성을 마친 뒤 TUFReplay가 `OnReplayEnd`를 부르면 Element가 `render.end`로
   세션을 닫습니다. ElementNote는 영상을 지우고 실제 키 입력을 다시 받습니다. 렌더가 취소되거나
   실패하면 `AddReplayOverlayVideos` 없이 바로 이 단계로 옵니다.

ElementNote가 게임보다 느리면 마지막 5단계에서 그만큼 기다립니다. 영상의 프레임이나 소리는
어긋나지 않습니다.

## 메시지

`element` 채널(ADOFAI-IPC)을 씁니다. 렌더 프로토콜 2입니다.

| 방향 | 이름 | 내용 |
|---|---|---|
| 앱 → 게임 이벤트 | `render.available` | `{ protocolVersion: 2, recordedCounters: true }`. 연결될 때마다 보냄. Element는 2만 받음 |
| 게임 → 앱 명령 | `render.begin` | `sessionId`(32자리 hex), `surfaces[]`(`viewerKind`, `scale`: 영상 픽셀/CSS 픽셀, `surfaceWidth`·`surfaceHeight`: 뷰포트 추정값), `fps`(영상 fps), `ffmpegPath`(TUFReplay가 렌더러와 같이 쓰는 FFmpeg), `initialOutputTimeUs`, `counterBaseline`: `"live"` 또는 `{ mode: "recorded", counters }`(기록에 없는 슬롯은 지금 값) |
| 게임 → 앱 명령 | `render.frame` | `sessionId`, `frameIndex`(출력 영상의 프레임 번호), `outputTimeUs`, `replayTimeUs`, `events[]`(`key`: Unity KeyCode 이름, `down`, `sequence`, `outputTimeUs`, `replayTimeUs`). 답하지 않음 |
| 게임 → 앱 명령 | `render.finish` | `sessionId`. 보낸 프레임을 모두 인코딩한 뒤 답함 |
| 게임 → 앱 명령 | `render.pulse` · `render.end` | `sessionId`. 답하지 않음 |
| 앱 → 게임 명령 | `render.reply` | `requestId`(위 명령의 id), `ok`, `result` 또는 `error{code,message}`. `render.begin`과 `render.finish`에만 |

- 답은 이벤트가 아니라 게임의 `render.reply` 명령으로 갑니다. 게임 쪽 IPC 이벤트는 게임
  스레드에서 처리되는데, Element는 begin과 finish를 기다리는 동안 그 스레드를 막고 있기 때문입니다.
  `render.reply`는 백그라운드 스레드에서 처리됩니다.
- `render.begin`의 답에는 화면마다 `width`·`height`(픽셀), `viewport{width,height,padding}`(CSS 픽셀)이
  있습니다. 캡처 형식은 ElementNote가 플랫폼에 맞게 고릅니다.
- `frameIndex`는 `round(영상 시각 × fps)`입니다. TUFReplay의 영상 시각 0이 출력 영상의 첫 프레임입니다.
  번호는 늘어나기만 하고 건너뛸 수 있습니다. ElementNote는 건너뛴 번호에 앞 그림을 반복하고, 첫 요청
  전 번호에는 첫 그림을 씁니다. 한 출력 프레임 안에서 두 번 찍히면 Element는 두 번째를 보내지 않고
  그 키를 다음 프레임에 붙입니다.
- `render.finish`의 답에는 `videos[]`(`viewerKind`, `path`, `width`, `height`, `frames`)가 페이지마다
  하나씩 있습니다. 영상은 `qtrle`(투명도 포함, 무손실) `.mov`이고, `render.end`가 올 때까지 남습니다.
  세션이 중간에 실패하면 이후 프레임은 무시되고, finish가 그 이유를 답합니다.
- Element는 finish를 `2분 + 프레임 수 × 0.5초`(최대 3시간)까지 기다립니다. ElementNote가 끊기면
  바로 그만둡니다.
- 영상에는 페이지 영상의 픽셀 하나가 렌더 영상 픽셀 하나에 맞게 들어갑니다. Element는 `padding`을 정수
  픽셀로 잘라내고 키뷰어를 정수 픽셀 위치·크기에 둡니다. macOS의 ElementNote는 WebKit
  스냅숏이 Retina(2x)에서 1픽셀 작게 와도 늘리지 않고 그대로 복사합니다. 어느 한쪽이라도
  어긋나면 매 프레임을 다시 샘플링해서 글자가 흐려집니다.
- 페이지 시계는 뒤로 가지 않고 시작할 때 100ms를 쓰므로, Element는 모든 시각에 1초를 더해 보냅니다.
- Element는 세션이 열려 있는 동안 타이머 스레드에서 1초마다 `render.pulse`를 보냅니다. finish를
  기다리느라 게임 스레드가 막혀 있어도 세션이 닫히지 않습니다. 게임이 30초 동안 아무 명령도 보내지
  않으면 ElementNote가 세션을 닫고 실제 입력을 되돌립니다. 게임 연결이 끊겨도 바로 닫습니다.
- ElementNote의 렌더 서비스는 한 번에 한 세션만 받습니다. 다른 렌더가 쓰고 있으면 `render-busy`를 답합니다.
- 키뷰어가 표시할 수 없는 키(예: 게임패드 버튼)는 건너뜁니다.

## 확인할 것

- macOS: `./scripts/run.sh mod-check`(배치·시각·세션·영상 사각형 테스트), `./scripts/run.sh desktop-check`,
  `./scripts/run.sh desktop-render-test`.
- Windows 캡처(WebView2 `CapturePreview`, PNG)는 아직 실제 Windows에서 확인하지 않았습니다.
  ElementNote가 남기는 단계별 시간 로그(`[render]`)로 어느 단계가 느린지 확인해야 합니다.
- 게임에서 TUFReplay로 리플레이를 렌더링해 키뷰어 위치·크기·카운트와 렌더 속도를 확인하는 단계는
  아직 하지 않았습니다. TUFReplay의 `ReplayApi.AddReplayOverlayVideos`와 OrbitRender의
  `OverlayVideoVersion` 1이 필요합니다.
