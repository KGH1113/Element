# 데이터 흐름 정리 (MVVM)

2026-11-17, `dev`(fe6159b) 기준. 1단계(입력 독 시범)를 `refactor/data-flow`에서 했습니다.

## 목표

화면에 보이는 값이 어디서 왔는지, 누른 것이 어디로 가는지 파일 한두 개만 따라가면 보이게 합니다.

```
편집기 (상태의 주인)
   │  아래로: Show(...), Blink(...), SetDrop(...)
   ▼
ViewModel ── 바인딩 ──▶ View (그리기만)
   ▲                     │
   │  위로: Command      │  Gesture (직접 받는 입력)
   └─────────────────────┘
   │
   ▼  위로 가는 유일한 길: I…Actions
편집기가 받아서 상태를 바꾸고, 다시 아래로
```

## 규칙 하나

**폴더는 화면에 보이는 것 하나, 파일 이름의 끝말은 데이터 흐름에서 맡은 역할 하나입니다.**
부품의 자식 부품은 그 부품 폴더 아래에 둡니다(`Dock/Keypad`, `Dock/ValueBrowser`).

| 끝말 | 하는 일 | 하지 않는 일 |
| --- | --- | --- |
| `Screen` | 화면을 열고 닫음. VM을 만들고 View를 붙이고, 닫을 때 해제 | 화면 안의 값 계산 (3단계에서 편집기들이 이 이름을 가짐) |
| `ViewModel` | 상태(`State`), 계산, 할 수 있는 일(`Command`). 부모가 알려 주는 입력 메서드(`Show` 등) | View, Gesture, Unity 객체(RectTransform 등)를 모름 |
| `View` | 그리기만. 자기 VM만 읽음 | VM 밖의 상태 읽기, 다른 부품 넘겨받기 |
| `Panel` | 화면 위 자리. View를 편집 화면 틀에 붙이고, 위치와 영역을 알려 줌 | 상태 갖기, VM의 메서드를 대신 전달하기 |
| `Gesture` | 직접 받는 입력(드래그, 캐럿, IME)을 VM에 넘김 | 상태를 직접 바꾸기 |
| `Graphic` | 직접 그리는 메시 | |
| `I…Actions` | 부품이 위로 보내는 일 전부. 부품을 쓰는 화면이 구현 | |
| (끝말 없음) | 상태 없는 값과 계산 (`ValueChipRow`, `ValueDrop`) | |

## 지킬 것

1. **방향은 하나입니다.** View와 Gesture는 VM으로, VM은 `I…Actions`로만 위로 보냅니다. 화면은 VM의 입력 메서드로만 아래로 알려 줍니다.
2. **부품은 자기를 쓰는 화면을 모릅니다.** 입력 독은 텍스트 편집기를 모르고 `IInputDockActions`만 압니다.
3. **같은 사실은 한 곳에 둡니다.** 값 고르기 모드는 `TextValueSource` 하나가 갖습니다. 다른 곳에 사본을 두지 않습니다.
4. **형제 부품의 상태를 쓰지 않습니다.** 알림이 어디에 뜨는지는 편집 화면 틀을 만들 때 정합니다(`noticesInDock`).
5. **같은 것에 문은 하나입니다.** VM을 감싸서 그대로 넘겨주기만 하는 클래스는 만들지 않습니다.

## 1단계: 입력 독 (시범)

```
Layout/ContentEditors/Shell/Dock/
├─ IInputDockActions.cs      독이 위로 보내는 일 전부: 키, AC, 적용, 값 고르기, 값 끌기
├─ InputDockPanel.cs         자리: 틀에 붙이고 Top, KeypadArea, Contains를 알려 줌
├─ InputDockViewModel.cs     상태: 편집기가 Show로 알려 준 것을 줄의 글자로
├─ InputDockView.cs          그리기만
├─ FormulaLineView.cs
├─ Keypad/       KeypadViewModel, KeypadKeyViewModel, KeypadView, KeypadKeyView
└─ ValueBrowser/ ValueBrowserViewModel, ValueBrowserView, ValueFolderViewModel,
                 ValueChipViewModel, ValueChipRow, ValueChipDragGesture
```

| 전 | 후 |
| --- | --- |
| 편집기가 콜백 7개를 `OverlayInputDock` → Model → 자식 Model로 넘김 | 편집기가 `IInputDockActions`를 구현. 독에서 하는 일은 편집기의 한 구역에 모임 |
| `OverlayInputDock`이 Model의 `Owner`, `Refresh`, `Edit`, `PickFor`, `SetDrop`, `Blink`를 넘겨주기만 함 | `InputDockPanel`은 자리만, 상태는 `InputDockViewModel`만 가짐 |
| `Edit(owner)`와 `Refresh(...)`가 나뉘어 렌더 앞뒤에서 호출됨 | `Show(owner, editing, name, result, hint)` 하나 |
| `PickFor(...)`가 값 클릭이 갈 곳을 delegate로 바꿔치기 | `TextValueSource.Armed`가 모드를 갖고, 편집기의 `Pick`이 보고 나눔 |
| 독의 `Dragging`과 드롭 처리의 `Dragging`이 같은 사실을 따로 가짐 | 끌기 시작은 값 목록 VM(`Carrying`), 편집기는 드롭 처리의 것만 봄 |
| 드롭 표시가 `int`(1, 0, -1) | `ValueDrop` (None, Accepts, Refuses) |
| 독 생성자가 `screen.Top.InDock = true`로 옆 부품을 바꿈 | 틀을 만들 때 `noticesInDock`으로 정함 |
| 독 View가 틀의 알림 모델을 따로 넘겨받음 | VM의 `Notices`만 읽음 |
| 이름 끝이 `Model`인데 실제로는 ViewModel | `ViewModel` |

화면과 동작은 그대로입니다.

## 다음

- **2단계 (기계적 이동):**
  - 나머지 `*Model`을 `*ViewModel`로 바꿉니다(Settings 페이지 등).
  - `Layout`을 `OverlayEditor`로, `Shell`을 부품별 폴더(`Parts/…`)로, `Settings/Pages`를 페이지별 폴더로 옮깁니다.
  - 동작은 바뀌지 않습니다.
- **3단계 (화면별로 풀기):**
  - 순서는 Text, Shape, Mod, Canvas, Settings입니다.
  - 편집기마다 `Screen`과 `ViewModel`로 나눕니다.
  - `IContentEditorHost`처럼 모든 것을 건네는 Host 인터페이스를 없애고, 자식에게는 필요한 State와 Command만 줍니다.
  - "어디에 드롭하는 중인가"를 State 하나로 두고, 각 칸이 스스로 표시를 계산합니다.
- **검사:** 끝말과 의존 방향을 `./scripts/run.sh structure-check`로 검사합니다.
