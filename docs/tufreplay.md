# TUFReplay 기록

[TUFReplay](https://github.com/KGH1113/TUFReplay)가 기록한 플레이를 다시 보거나 영상으로 만들면,
Element는 그 플레이를 할 때의 값을 보여 줍니다.

| | 게임 안 리플레이 | OrbitRender 렌더 (TUFReplay에서 시작) |
|---|---|---|
| 시도 횟수 (Attempt, Full attempt) | 기록한 플레이의 시도 횟수 | 기록한 플레이의 시도 횟수 |
| ElementNote 키 카운트 | 기록을 시작할 때의 카운트에서 이어짐 | 기록을 시작할 때의 카운트에서 이어짐 |
| 내 시도 횟수 | 늘지 않음 | 늘지 않음 |
| 내 키 카운트 | 리플레이가 누른 키는 더해지지 않음 | 리플레이가 누른 키는 더해지지 않음 |

**값이 없는 기록**
- 이 기능이 생기기 전에 녹화한 기록에는 저장된 값이 없습니다.
- 시도 횟수는 `-`로 보입니다. 시도 횟수를 쓰는 계산식은 비어 보입니다.
- 키 카운트는 지금 쌓여 있는 값에서 이어집니다.

**키 카운트의 단위**
- 키 카운트는 ElementNote의 탭과 키 슬롯마다 저장합니다.
- 기록한 뒤에 새로 만든 키는 지금 값에서 이어집니다.

**ElementNote가 꺼져 있었다면**
- 기록할 때 ElementNote가 꺼져 있었으면 그 기록에는 키 카운트가 저장되지 않습니다.

## 저장하는 때

TUFReplay가 플레이를 기록하기 시작할 때 저장합니다. 카운트다운이나 체크포인트 시작 직후, 첫 입력을 기록하기 전입니다.

**시도 횟수**
- 이번 플레이를 센 뒤의 값입니다. 오버레이가 그 플레이 동안 보여 준 값과 같습니다.

**키 카운트**
- Element가 ElementNote에 물어서 받습니다.
- ElementNote는 다른 프로그램이라 답이 조금 늦게 옵니다. 플레이가 끝나기 전에 온 답만 저장합니다.

**저장되지 않는 플레이**
- TUFReplay가 저장하지 않는 플레이에는 값도 저장되지 않습니다. 진행이 없는 플레이가 그렇습니다.
- 기록을 지우면 값도 함께 지워집니다.

## 리플레이하는 동안

**게임 안 리플레이**
- TUFReplay는 OS 키 입력으로 리플레이를 재생합니다. 그래서 ElementNote는 리플레이가 누른 키와 내가 누른 키를 구별하지 못합니다.
- Element가 리플레이를 시작하고 끝낼 때 ElementNote에 알립니다. 그 사이의 키는 기록된 카운트에서 따로 셉니다.
- ElementNote에 저장된 내 카운트에는 더하지 않습니다. 리플레이가 끝나면 내 카운트가 다시 보입니다.

**리플레이 모드가 끝나는 경우**
- 게임과의 연결이 끊기면 ElementNote가 리플레이 모드를 스스로 끝냅니다.
- Element를 끄면 Element가 끝냅니다.

**리플레이 중 카운트 변경**
- 리플레이 중에 ElementNote에서 카운트를 초기화하거나 키를 편집하면, 변경은 내 카운트에 적용됩니다.
- 화면에는 리플레이가 끝난 뒤에 보입니다.

**타임라인 이동**
- TUFReplay 타임라인에서 앞뒤로 이동하면, 건너뛴 구간의 키는 카운트에 들어가지 않습니다.

## 동작

1. Element는 TUFReplay의 `RunStateApi`에 리플렉션으로 등록합니다. 상태 id는 `Element.Attempts`와 `Element.KeyCounts`입니다.
   - TUFReplay가 없어도 Element는 그대로 동작합니다.
   - TUFReplay가 나중에 로드되거나 껐다 켜져도 2초 안에 다시 등록합니다.
   - 저장 형식은 `AttemptRunState`와 `KeyCountRunState`의 public 필드입니다. 필드 이름이나 타입을 바꾸려면 provider에 `UpgradeRunState`가 있어야 합니다. 자세한 내용은 TUFReplay의 `docs/run-state-api.md`에 있습니다.
2. 녹화가 시작되면 TUFReplay가 `CaptureRunState`를 부릅니다.
   - 시도 횟수는 바로 돌려줍니다.
   - 키 카운트는 `counters.read`의 답을 `Task`로 돌려줍니다.
3. 리플레이나 렌더를 시작하기 직전에 TUFReplay가 `OnReplayBegin(기록한 값, render)`을 부릅니다. 레벨이 시작되기 전입니다.
   - 오버레이의 시도 횟수는 기록한 값으로 바뀝니다.
   - 게임 안 리플레이면 Element가 ElementNote에 `counters.replay.begin`을 보냅니다.
   - 렌더면 `render.begin`의 `counterBaseline`에 기록한 카운트를 담습니다.
4. 끝나면 TUFReplay가 `OnReplayEnd`를 부릅니다.
   - 오버레이는 지금 값으로 돌아갑니다.
   - Element가 `counters.replay.end`를 보냅니다.

## 메시지

`element` 채널(ADOFAI-IPC)을 씁니다.

| 방향 | 이름 | 내용 |
|---|---|---|
| 앱 → 게임 이벤트 | `counters.available` | `{ protocolVersion: 1 }`. 연결될 때마다 보냄 |
| 게임 → 앱 명령 | `counters.read` | `{}`. 앱은 같은 id로 `counters.read.result` 이벤트를 답함: `{ ok: true, counters }` 또는 `{ ok: false, code, message }` |
| 게임 → 앱 명령 | `counters.replay.begin` | `{ counters }`. 기록한 카운트(탭 id → 키 슬롯 → 횟수) 또는 `null`. 답하지 않음 |
| 게임 → 앱 명령 | `counters.replay.end` | `{}`. 답하지 않음 |

- 카운트는 0 이상의 정수만 받습니다. 아닌 값이 있으면 그 답이나 기록 전체를 쓰지 않습니다.
- 리플레이 중에 ElementNote가 새로 연결되면 Element가 `counters.replay.begin`을 다시 보냅니다.
- ElementNote의 Rust 쪽은 `keys_replay_begin`·`keys_replay_end` 명령으로 리플레이 카운트를 따로 둡니다.
  - 리플레이 중에는 창들(`keys:counter`, `keys:counters`, 부트스트랩, `keys_get_counters`)이 리플레이 카운트를 받습니다.
  - store.json에 저장되는 `keyCounters`는 바뀌지 않습니다.

## 확인할 것

- `./scripts/run.sh mod-check`: 시도 횟수 표시, provider 계약, 카운트 응답 해석, 렌더 시작 메시지.
- `./scripts/run.sh desktop-check`:
  - 리플레이 카운트가 저장된 카운트에 더해지지 않는지
  - 리플레이 중 초기화
  - 연결이 끊길 때 끝나는지
  - 렌더의 기록 카운트
- 게임에서 TUFReplay로 기록하고, 게임 안 리플레이와 렌더로 시도 횟수와 키 카운트를 확인하는 단계는 아직 하지 않았습니다.
