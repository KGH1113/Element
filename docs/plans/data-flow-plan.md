# 데이터 흐름 정리 (MVVM)

2026-11-17, `dev`(fe6159b) 기준으로 `refactor/data-flow`에서 했습니다. `./scripts/run.sh structure-check`가 아래 규칙을 검사하고, `mod-check`가 그 검사를 먼저 돌립니다.

## 목표

화면에 보이는 값이 어디서 왔는지, 누른 것이 어디로 가는지 파일 한두 개만 따라가면 보이게 합니다.

```
화면 (Screen: 상태의 주인)
   │  아래로: 읽는 것은 I…Context, VM에는 Show(...) 같은 입력 메서드
   ▼
부품, ViewModel ── 바인딩 ──▶ View (그리기만)
   ▲                           │
   │  Command                  │  Input (직접 받는 입력)
   └───────────────────────────┘
   │
   ▼  위로 가는 유일한 길: I…Actions
화면이 받아서 상태를 바꾸고, 다시 아래로
```

## 규칙 하나

**폴더는 화면에 보이는 것 하나, 파일 이름의 끝말은 데이터 흐름에서 맡은 역할 하나입니다.**
파일 하나에 타입 하나를 두고, 파일 이름이 곧 타입 이름입니다. 부품의 자식 부품은 그 부품 폴더 아래에 둡니다(`Parts/Dock/Keypad`).

| 끝말 | 하는 일 | 하지 않는 일 |
| --- | --- | --- |
| `Screen` | 화면을 열고 닫음. 부품을 만들어 잇고, 그 화면의 `I…Context`와 `I…Actions`를 구현 | 부품 대신 그 부품의 일을 하기 |
| `ViewModel` | 상태(`State`), 계산, 할 수 있는 일(`Command`). 부모가 알려 주는 입력 메서드 | View, Input, Unity 객체(RectTransform 등)를 알기 |
| `View` | 그리기만. 자기 VM만 읽음 | VM 밖의 상태 읽기 |
| `Panel` | 화면 위 자리. View를 붙이고 위치와 영역을 알려 줌. VM을 만들면 `ViewModel` 속성 하나로만 내놓음 | VM의 메서드를 대신 부르는 멤버 두기 |
| `Input` | 직접 받는 입력(마우스, 드래그, 키, 캐럿, IME)을 VM이나 `I…Actions`로 넘김 | 그리기 |
| `Graphic` | 직접 그리는 메시 | |
| `I…Actions` | 위로 가는 요청 전부를 한 목록에. 그 화면이 구현 | |
| `I…Context` | 부품이 화면에서 읽는 것: 자리와 편집 중인 것 | 무언가를 바꾸는 메서드 |
| (끝말 없음) | 화면의 한 기능(`TextColor`, `ShapeNumbers`), 값(`ValueDrop`), 계산 | |

## 지킬 것

1. **방향은 하나입니다.** 부품은 `I…Context`로 읽고 `I…Actions`로 요청합니다. 상태를 바꾼 부품은 `Render()`를 요청하고, 화면은 편집 중인 것으로부터 모든 부품을 다시 보여 줍니다.
2. **층은 안쪽만 압니다.** Configuration ← Domain ← Application ← Infrastructure·Presentation, Bootstrap은 모두를 압니다(structure-plan §9).
3. **공용 부품(`OverlayEditor/Parts`)은 자기를 쓰는 화면을 모릅니다.** 텍스트·도형·모드 편집기는 서로와 캔버스를 모릅니다. 캔버스는 편집기를 요소 종류(`Kinds`)를 통해서만 엽니다.
4. **같은 사실은 한 곳에 둡니다.** 값 고르기 모드는 `TextValueSource`, 드롭 대상은 드롭 처리(`TextValueDrops`, `ShapeValueDrops`) 한 곳에 있습니다. 칸마다 표시를 칠하는 일은 그 클래스의 한 메서드에서 합니다.
5. **형제 부품의 상태를 쓰지 않습니다.** 알림이 어디에 뜨는지는 편집 화면 틀을 만들 때 정합니다(`noticesInDock`).
6. **이름이 타입을 가리지 않게 합니다.** 폴더(=네임스페이스)는 그 안의 타입이나 자주 쓰는 타입과 이름이 같으면 안 됩니다. 그래서 `Text`·`Shape`가 아니라 `TextEditor`·`ShapeEditor`이고, 편집 화면 틀은 폴더 `EditorFrame`에 타입 `EditorFramePanel`입니다.

## 폴더

```
Presentation/
├─ OverlayEditor/                 게임 화면 위에서 요소를 배치하고 편집
│  ├─ OverlayEditorScreen         배치 편집기 화면: ICanvasContext, ICanvasActions
│  ├─ Canvas/                     배치 화면: Session, Selection, DragInput, MarqueeInput, KeyboardInput…
│  │  ├─ Toolbar/  ElementBar/  AddMenu/  Confirm/
│  ├─ Kinds/                      요소 종류마다 다른 것 (텍스트·도형·모드·키뷰어)
│  ├─ KeyViewers/
│  ├─ TextEditor/                 TextEditorScreen: ITextEditorContext, IEditorActions, IInputDockActions
│  ├─ ShapeEditor/                ShapeEditorScreen
│  ├─ ModEditor/                  ModEditorScreen: IModEditorContext, IModEditorActions
│  └─ Parts/                      편집기들이 같이 쓰는 부품
│     ├─ Dock/ (Keypad/, ValueBrowser/)  EditorFrame/  EditorBar/  Property/  Inspector/
│     └─ Formula/  ColorPicker/  Gradient/  Grow/  ValueColor/  ValueSource/
├─ Settings/
│  ├─ Window/                     SettingsWindowScreen: ISettingsWindowActions, 창 VM과 View
│  └─ Home/  General/  Effects/  PresetEditor/  Tweaks/  EditorTweaks/  Planets/  Judgment/  Chatter/  Profiles/
├─ Overlay/                       게임 화면에 그려지는 오버레이 (매 프레임 그리기라 MVVM 밖)
├─ Screens/  Notices/
└─ Ui/                            공용 컨트롤
```

## 무엇을 바꿨나

| 전 | 후 |
| --- | --- |
| 편집기가 콜백 7개를 독 → 독 Model → 자식 Model로 넘김 | `IInputDockActions` 하나를 편집기가 구현하고, 한 구역에서 받음 |
| 캔버스 툴바·키가 콜백 11개짜리 `LayoutCommands`를 받음 | `ICanvasActions`를 배치 편집기 화면이 구현 |
| 설정 창 VM이 콜백 4개를 받음 | `ISettingsWindowActions`를 설정 창 화면이 구현 |
| 부품이 `IContentEditorHost`로 화면의 모든 것에 닿음 | 읽는 것은 `IEditorContext`, 요청은 `IEditorActions`로 나눔. 부품의 생성자가 무엇을 읽고 무엇을 요청하는지 보여 줌 |
| `ILayoutEditing`, `IModEditorHost`도 읽기와 요청이 섞임 | `ICanvasContext`/`ICanvasActions`, `IModEditorContext`/`IModEditorActions` |
| `OverlayInputDock`, `LayoutToolbar`, `OverlayEditorFrame` 등이 VM의 메서드를 대신 부름 | Panel은 `ViewModel` 하나로만 내놓고, 대신 부르는 멤버를 없앰 |
| `PickFor`가 값 클릭 경로를 delegate로 바꿔치기 | 모드는 `TextValueSource.Armed`, 편집기의 `Pick`이 보고 나눔 |
| 드롭 표시가 `int`(1, 0, -1) | `ValueDrop` (None, Accepts, Refuses) |
| `*Model`인데 실제로는 ViewModel인 타입 33개 | `*ViewModel` |
| `Layout/ContentEditors/Shell`에 63개 파일 | 부품마다 폴더 |
| 파일 하나에 타입 여럿 | 파일 하나에 타입 하나 |

화면과 동작은 그대로입니다. 단계마다 UI 렌더 184장을 dev와 바이트 단위로 비교했습니다.

## ElementUI로 옮긴 것 (2026-11-17)

ElementUI에 SwiftUI에 있던 API 7개를 더하고 Element를 그 위로 옮겼습니다.

| ElementUI API | Element에서 바뀐 곳 |
| --- | --- |
| `Observable` | 입력 중인 수식(`FormulaEditing`)이 바뀔 때 알림. 키패드 키와 독 줄이 스스로 따라가서 `Keypad.Show`와 수식 글자 캐시가 사라짐 |
| `Timeline` | 독 캐럿 깜빡임과 값 칩의 실시간 숫자. 편집기가 매 프레임 넣던 `Blink`와 독의 `Tick` 거르기가 사라짐 |
| 환경 값 (`EnvironmentKey`) | 글자 폭 재기와 움직임 줄이기를 `EditorEnvironment`로 내려보냄. 생성자 두 단계로 넘기던 것이 사라짐 |
| `Keyboard.IsTaken` | "입력칸이 키를 가졌나"를 이것 하나로 물음. 독의 프레임 걸쇠, 팝오버마다의 `IsTyping`, 설정 창의 `IsTyping`이 사라짐 |
| 끌어서 놓기 (`Draggable`, `DropDestination`, `UguiDrop`) | 칩은 `.Draggable`, 받을 곳은 각자 `Over`·`Drop`·`Refused`로 답함. 손으로 쓴 우선순위 순서(`FindDrop`), 칸마다 칠하던 강조, 고스트 그리기가 사라짐 |
| `FixedSize`, `LayoutPriority`, 스크롤 앵커, `RichText`, `OnGeometryChange` | ElementUI에 들어감. Element에서는 아직 쓰지 않음 (아래) |
| `ConfirmationDialog`, 팝오버 붙일 방향 | ElementUI에 들어감. Element에서는 아직 쓰지 않음 (아래) |

## 아직 남은 것

- 편집기의 기능 클래스(`TextColor`, `ShapeNumbers` 같은 것)는 아직 반응형 ViewModel이 아닙니다. 상태를 바꾸면 `Render()`를 요청하고, 다시 그릴 때 자기 위젯에 상태를 밀어 넣습니다. `FormulaEditing`처럼 하나씩 `Observable`로 바꾸면 `Render()` 없이 따라가게 됩니다.
- 독의 수식 줄(`FormulaLineView`)은 아직 손으로 배치합니다. `FixedSize`·`LayoutPriority`·스크롤 앵커·`RichText`로 옮길 수 있지만, 지금 모양과 픽셀까지 맞추는 작업이 따로 필요합니다.
- 레이아웃 편집기의 초기화 확인 창(`ConfirmPanel`)은 그대로 둡니다. ElementUI 대화상자는 붙은 화면만 덮고, 카드 모양이 다르며, Enter가 "확인"입니다(되돌릴 수 있는 초기화라 위험 표시가 없음). 지금 창은 Enter가 취소입니다.
- 캔버스 툴바는 되돌리기 가능 여부를 매 프레임 베껴 옵니다. 기록(`LayoutHistory`)이 Application 층에 있어서 ElementUI의 `Observable`이 될 수 없습니다(층 규칙).
- 손으로 만든 상호작용(캐럿·IME, 그라디언트 핸들 끌기)은 그대로 둡니다.
