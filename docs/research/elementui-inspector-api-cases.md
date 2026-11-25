# Text 속성 패널에서 확인한 ElementUI API 사례

2026-11-17. 다른 조사 스레드에서 전달받은 Text 속성 패널 사례를 현재 소스로 재검토했다. 코드 변경과 게임 내 hover 캡처는 수행하지 않았다. 이 문서는 [전체 SwiftUI API 조사](elementui-swiftui-api-audit.md)의 보충 자료다.

대상 화면의 조립 진입점은 [OverlayTextEditor.BuildInspector](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Text/OverlayTextEditor.cs:195)다. Font, Size, Text color, Animate, Grow from 행의 외형과 구성, 화면 크기 갱신, 상태 모델을 살폈다.

## 버튼의 기본 배경과 hover 도형은 서로 다른 주체가 만든다

**P1: 상태별 외형을 한곳에서 제어할 수 있는 button style 계약이 부족하다.**

현재 행은 다음과 같이 구성한다.

```csharp
Button(Content(), () => VM.Click, ButtonStyle.Plain)
    .Background(() => VM.Fill.Value, OverlayInspectorRow.Radius)
    .Named(VM.Name);
```

근거: [OverlayInspectorView.cs:52](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayInspectorView.cs:52). `OverlayInspectorRow`의 Height는 40, Radius는 10이다. [OverlayInspectorRow.cs:24](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayInspectorRow.cs:24)

| 계층 | 외형을 결정하는 곳 | 이 행의 결과 |
| --- | --- | --- |
| 바깥 Background | 화면의 `VM.Fill`과 `Radius=10` | 반경 10인 둥근 사각형 |
| 내부 ButtonPeer.Fill | `Theme.ButtonRadius(Plain, height)` | 높이 40에서 반경 20인 캡슐 |
| 내부 fill의 평상시 색 | `ElementTheme.ButtonFill` | Plain은 투명 |
| 내부 fill의 hover 색 | 같은 theme의 Hovered 분기 | 흰색, alpha .08 |
| 내부 fill의 pressed/selected/disabled | theme의 다른 상태 분기 | 외부 Background와 별개로 계산 |

내부 radius는 [ButtonPeer.OnResized](/Users/kgh/dev/src/ElementUI/src/ElementUI.UGUI/Peers/ControlPeers.cs:113)가 설정한다. [UguiTheme.ButtonRadius](/Users/kgh/dev/src/ElementUI/src/ElementUI.UGUI/UguiTheme.cs:73)의 Plain은 기본 분기 `height / 2`로 들어간다. [ElementTheme.cs:65](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Declarative/ElementTheme.cs:65)의 compact override는 Standard/Prominent만 처리하므로 이 Plain 행에는 영향을 주지 않는다. hover 색은 [ElementTheme.cs:74](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Declarative/ElementTheme.cs:74)에서 결정한다.

따라서 평상시에는 투명해서 보이지 않던 두 번째 도형이 hover 때 나타난다. **반경 10과 20의 불일치는 소스로 확인했다. 실제 화면에서 얼마나 눈에 띄는지, clipping이나 주변 그래픽이 어떤 영향을 주는지는 아직 캡처로 검증하지 않았다.** 전달받은 조사에서는 vendored DLL도 같은 경로임을 확인했으며, 이번 후속 검토에서는 현재 소스를 독립적으로 확인했다.

이것을 “Background가 내부 버튼까지 자동으로 clip하지 않는 버그”라고 부르면 정확하지 않다. background, control style, clipping, hit shape는 서로 다른 개념이다. SwiftUI도 사용자 정의 외형은 [ButtonStyle](https://developer.apple.com/documentation/swiftui/buttonstyle)의 body에서 구성할 수 있도록 별도 경로를 제공한다. 비교해야 할 부분은 background가 자동으로 전파되는지가 아니라, **원하는 상태별 외형을 선언적으로 일관되게 소유할 수 있는지**다.

권장 방향은 다음과 같다.

- button style이 normal/hovered/pressed/selected/disabled 상태의 fill·shape·stroke·transition을 함께 결정한다.
- 기본 배경과 hover highlight가 같은 shape를 쓰도록 명시할 수 있게 한다.
- 필요하면 서로 다른 shape도 의도적으로 선택할 수 있게 한다.
- keyboard focus 상태와 focus 표시도 같은 계약에서 고려한다. 현재 `ButtonState`에는 focus가 없다.
- Background modifier가 내부 ButtonPeer의 radius를 몰래 바꾸도록 만들지 않는다.

예를 들어 `.ButtonStyle(InspectorRowStyle(...))`처럼 상태 외형의 소유권을 명시하는 API가 후보가 될 수 있다. 이것은 아직 존재하지 않는 제안이다. 기존 ButtonStyle enum과 host theme는 기본 제공 스타일로 유지할 수 있다.

## OnHover는 공개 API가 없지만 기반 기능은 이미 있다

**P1: 일반 View의 hover 상태를 사용자 State로 연결하는 modifier가 없다.** 이는 전체 조사 20번의 구체적인 사례다. 공개 `ViewModifiers`와 실제 vendored Core DLL의 API 목록에는 `OnHover`가 없다.

반면 [IContainerPeer.Hovered](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Backend/Peers.cs:96)는 `Action<bool>` setter를 이미 제공한다. 설명상 해당 container와 그 자식 영역으로 들어오면 true, 나가면 false를 전달한다. [HintView](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Views/Modifiers.cs:237)는 별도 `Hint area` container를 만들고 `_area.Hovered = OnHover`로 연결한다.

즉 pointer enter/exit 감지부터 새로 만들 필요가 없다. 일반 View에서 이 기능을 소비할 수 있는 선언형 표면이 빠져 있다. 다음은 **현재 없는 제안 API**다.

```csharp
private readonly State<bool> _hovered = new(false);

row.OnHover(inside => _hovered.Value = inside);
```

다른 후보는 hover 상태에 대한 binding 또는 style configuration의 `IsHovered`다. 일반적인 포인터 관찰에는 OnHover가 유용하고, 버튼의 normal/hover/pressed 색상을 맞추는 데는 상태를 제공하는 button style이 더 직접적이다. 버튼마다 hover State를 수동으로 반복해야 하는 설계를 최종 목표로 삼을 필요는 없다.

구현 시에는 Hint와 OnHover를 함께 붙여도 이벤트를 덮어쓰지 않는지, 자식 사이를 이동할 때 불필요하게 enter/exit가 반복되는지, hidden/unmount/disabled 시 상태가 어떻게 끝나는지 검증해야 한다. padding·background·content shape 중 어느 영역을 관찰하는지도 명시해야 한다. 이 modifier 역시 앞선 ForEach/If/Spacer의 구조 보존 문제를 재현해서는 안 된다. 포인터 위치를 매 프레임 polling하는 방식으로 바꿀 이유도 없다.

## MotionSeconds가 모든 버튼 상태 전환에 적용되는 것은 아니다

`Pressable.StateChanged`는 Repaint를 호출하고, Repaint는 `Fill.color`를 즉시 대입한다. ButtonPeer 경로에 hover 색상 보간이 없으며 Selectable의 transition도 None이다. [ControlPeers.cs:83](/Users/kgh/dev/src/ElementUI/src/ElementUI.UGUI/Peers/ControlPeers.cs:83), [ControlPeers.cs:115](/Users/kgh/dev/src/ElementUI/src/ElementUI.UGUI/Peers/ControlPeers.cs:115)

`MotionSeconds=.16`이 있어도 이 버튼 경로에는 적용되지 않는다. 다만 [UguiTheme.cs:36](/Users/kgh/dev/src/ElementUI/src/ElementUI.UGUI/UguiTheme.cs:36)의 설명은 switch와 selected segment의 이동 시간이다. **전역 애니메이션 설정을 버튼이 어겼다고 볼 근거는 없다.** 현재 부족한 것은 버튼 hover/press 전환 정책을 선언하는 공통 API다.

hover가 반드시 애니메이션이어야 한다고 결론내리지 않는다. 애니메이션을 선택한다면 현재 표시 색에서 재시작하고, 포인터가 빠르게 드나들어도 반응을 지연시키지 않으며, reduce motion과 disabled 전환을 처리해야 한다. idle 프레임에서 이 기능 때문에 driver가 계속 돌면 안 된다.

## Show의 수동 처리는 세 가지 책임으로 나뉜다

대상은 [OverlayEditorInspector.Show](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayEditorInspector.cs:76)다.

| 처리 | 실제 책임 | 판정 |
| --- | --- | --- |
| `_panel.Refit(Width)` | 내용 높이에 맞춰 바깥 Unity host rect 크기 재결정 | P1/P2: opt-in 자동 fitting 또는 intrinsic size 알림의 부족 |
| `_panel.Sync()` | 새 native control에 Element의 커서·키보드 정책 적용 | 정책은 앱 책임. 생성 시 정책 전달/알림 API가 있으면 반복 코드를 줄일 수 있음 |
| `Root.anchoredPosition` | 열린 tool host와 inspector host의 왼쪽 위치 맞추기 | 현재 앱의 정당한 panel 배치 정책. 이것만으로 framework 결함은 아님 |

### Refit은 내부 layout invalidation의 실패가 아니다

`ShowOpen`의State가 바뀌면 If branch와 measure invalidation은 이미 자동으로 갱신된다. 하지만 `UiRoot.UpdateLayout`은 현재 root 크기 안에서 내용을 배치할 뿐, 바깥 Unity RectTransform의 높이를 내용 높이로 다시 결정하지 않는다. [UiRoot.cs:87](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Layout/UiRoot.cs:87)

`UiHost.FitContent`는 flush → ContentSize 측정 → `_rect.sizeDelta` 설정 → root resize → flush를 수행하는 일회성 메서드다. [UiHost.cs:66](/Users/kgh/dev/src/ElementUI/src/ElementUI.UGUI/UiHost.cs:66). [ScreenPanel.Refit](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Declarative/ScreenPanel.cs:82)이 이것을 호출한다.

개선 후보는 width-constrained auto-fit 모드 또는 content-size invalidation 알림이다. ScreenPanel이 이를 받아 처리하면 각 inspector가 `_fitted`, Revision, `_toolOpen`으로 갱신 시점을 따로 추적할 필요가 줄어든다. 모든 root를 자동 크기로 바꾸면 고정 크기 게임 패널을 깨뜨리므로 반드시 선택 가능한 정책이어야 한다. 같은 프레임의 여러 변경을 한 번으로 합치고 size-change/layout의 순환 호출을 막아야 한다.

### Sync는 새 control에 정책을 붙인다

[ScreenPanel.Sync](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Declarative/ScreenPanel.cs:95)는 MountedCount가 바뀌었을 때만 Selectable을 수집한다. keyboard를 받지 않는 패널에서는 navigation을 끄고, 별도 커서가 없는 control에 텍스트/포인터 커서를 붙인다. 첫 tool open 때 새로 생성되는 back row에도 적용하려고 Show에서 호출한다.

따라서 “매 프레임 전체 hierarchy를 스캔한다”는 설명은 틀리다. count guard가 있다. 다만 mount notification, backend 생성 시 host policy 주입, cursor/focus/navigation semantics가 있으면 이 사후 스캔은 줄일 수 있다. 앱의 커서 정책 자체를 framework의 고정 정책으로 옮길 필요는 없다.

### anchoredPosition은 별도 host 사이의 관계다

열린 tool이 inspector보다 왼쪽까지 넓어질 경우 inspector를 같은 왼쪽에 맞추는 정책이다. 이는 `Mathf.Min(Left, toolRoot.anchoredPosition.x)`로 표현되어 있다. [OverlayEditorInspector.cs:80](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayEditorInspector.cs:80)

두 host를 하나의 선언형 layout/presentation tree에 넣거나 anchor/geometry 기능을 제공하면 줄일 여지는 있다. 그러나 현재 경계에서 이 계산은 overlay editor의 책임이며, 모든 RectTransform 접근을 제거하는 것이 목표는 아니다.

## 플래그 조합과 파생 상태는 앱 설계도 함께 정리할 수 있다

추가로 전달된 모델·ViewBuilder 사례는 전부 framework 기능 부족으로 분류하면 부정확하다. **기존 API로 줄일 수 있는 앱 코드**와 **추가 API가 필요한 부분**을 나눴다.

| 사례 | 확인한 구현 | 판단 |
| --- | --- | --- |
| value/swatch/icon/glyph/toggle/opens 플래그 | 생성자가 `swatch > icon > glyph`, `toggle > opens`로 일부 입력을 무시 | 허용되지 않는 조합을 만들 수 있는 앱 모델 문제 |
| `_value`와 `ValueText` | 같은 표시 문자열을 두 곳에 저장하고 Set에서 동기화 | 하나의 source로 줄일 후보 |
| Active·Drop과 Fill | setter마다 Paint를 호출해 색을 동기화 | observable source와 기존 Computed로 파생 가능 |
| IsComputed와 ValueInk | 수식 여부에 따라 표시 색을 저장 | 파생 상태로 표현 가능 |
| Available과 Ink | 사용 가능 여부가 표시 색을 결정 | 파생 상태로 표현 가능. Available과 SwitchOn은 다른 의미 |
| Open과 ToolOpen | ShowOpen에서 문자열과 빈 문자열 여부를 함께 저장 | Open에서 ToolOpen을 계산할 수 있음 |

근거: [OverlayBarButtonModel.cs:41](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayBarButtonModel.cs:41), 같은 파일 [150행](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayBarButtonModel.cs:150)·[186행](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayBarButtonModel.cs:186), [OverlayInspectorModel.cs:30](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayInspectorModel.cs:30).

leading 장식을 None/Swatch/Icon/Glyph 중 하나로 모델링하고, trailing value·unit·toggle·disclosure의 허용 관계를 별도로 정하면 더 명확해진다. ShowsValue와 Toggle은 동시에 허용되는 조합이므로 모든 플래그를 한 enum으로 합치면 현재 표현력을 잃는다.

ElementUI에는 이미 [Computed<T>](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Model/ViewModel.cs:45)가 있다. 다만 현재 `_active`, `_drop` 같은 일반 필드를 그대로 두고 Computed만 추가하면 외부 Set/SetDrop 호출을 자동으로 관찰하지 못할 수 있다. 원본을 State로 만들거나 명시적인 갱신 경계를 함께 마련해야 한다. 또한 Revision은 외부 host fitting을 위한 신호이므로 1차 상태 중복을 줄였다는 이유만으로 삭제할 수 없다.

`SwatchGradient`의 custom comparer도 단순 중복 상태와 다르다. 같은 gradient 객체를 내부 변경한 뒤 다시 표시하려는 요구가 있으므로 함께 제거하면 안 된다. [OverlayBarButtonModel.cs:117](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayBarButtonModel.cs:117), 같은 파일 [201행](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayBarButtonModel.cs:201).

## List와 배열 복사는 현재 C# API에서도 줄일 수 있다

[OverlayInspectorView.Content](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayInspectorView.cs:56)는 List<View>에 조건별로 자식을 추가한 뒤 ToArray한다. 이 조건들은 생성 시 정해지는 readonly 구성값이다.

ElementUI의 stack은 이미 null child를 건너뛴다. [Stacks.cs:34](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Views/Stacks.cs:34), [View.cs:120](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Views/View.cs:120). 따라서 이 사례는 현재의 params API로도 다음 형태가 가능하다.

```csharp
HStack(
    leading,
    showsValue ? valueView : null,
    showsToggle ? toggleView : null,
    opens ? disclosureView : null);
```

이는 새 API 제안이 아니라 기존 null-child 계약을 이용한 구성 형태다. 실제 helper에 적용한 코드는 작성하거나 빌드하지 않았다. 동적 값이 바뀌는 조건은 여전히 If가 필요하지만, 여기의 정적 플래그까지 모두 reactive If로 바꿀 이유는 없다.

[OverlayInspectorRow.Of/Filled](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayInspectorRow.cs:39)의 배열 인덱스·CopyTo도 C# collection expression/spread로 간소화할 후보가 된다. 프로젝트는 [LangVersion=latest](/Users/kgh/dev/src/impl-resourcepack/Element/Element.csproj:6)를 사용한다. 이 문서에서는 실제 compiler로 변경안을 검증하지 않았다.

Group, collection overload, 작은 builder helper, nullable-child 문서화는 추가적인 편의 개선 후보지만, 이 코드를 간단히 쓰려면 Swift result builder를 새로 만들어야 한다는 결론은 아니다. Body는 mount 시 한 번 구성되므로, List/ToArray가 보인다는 이유만으로 매 프레임 GC나 게임 지연을 주장할 수도 없다.

## 같은 람다 형태가 서로 다른 의미를 갖는다

| 표현 | 의미 |
| --- | --- |
| `() => VM.ValueText.Value` | 값을 다시 읽고 의존성을 추적하는 expression |
| `Expression<Func<UiColor>>` | 같은 추적 expression을 helper의 인자로 전달하는 타입 |
| `() => VM.Click` | 클릭할 때 실행할 동작 자체가 아니라, 실행할 Command를 고르는 expression |

근거: [OverlayBarButtonView.cs:23](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayBarButtonView.cs:23), [OverlayInspectorView.cs:52](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayInspectorView.cs:52), [OverlayInspectorRow.cs:64](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayInspectorRow.cs:64).

특히 마지막 표현은 SwiftUI의 action closure를 기대하는 작성자에게 혼동을 줄 수 있다. named argument `command:`, 문서, IDE 설명에서 literal·tracked value·binding·command selector의 의미를 구분하는 것이 먼저다.

하지만 람다를 무조건 직접 값 인자로 바꾸는 것도 올바르지 않다. 이 row view들은 ItemSlot을 받는다. 같은 key의 model이 교체되면 expression은 현재 VM의 State와 Command를 다시 따라갈 수 있다. `Text(VM.ValueText)`나 `Button(..., VM.Click)` 형태로 mount 당시 source를 캡처하면 이 교체 의미를 잃을 수 있다.

직접 State/Computed를 받는 overload나 공통 source abstraction을 추가한다면, 고정 source를 붙잡는 경우와 ItemSlot의 현재 source를 따라가는 경우를 구별해야 한다. 문법이 짧아져도 의존성 추적과 model 교체가 유지되어야 한다.

## 후속 검증 기준

- normal/hovered/pressed/selected/disabled 상태에서 배경과 highlight의 shape가 의도대로 같은지 Unity 캡처로 확인한다.
- 높이 40뿐 아니라 compact size, scale 변화, label wrap에서도 radius와 clipping을 확인한다.
- 상태별 shape와 hit area를 별개로 검증한다. 시각적으로 둥글어진 것만으로 hit shape가 바뀌었다고 가정하지 않는다.
- hover/press 전환은 선택한 motion 정책대로 동작하며, 빠른 입력이나 disabled 전환에 끊김이 없어야 한다.
- auto-fit은 내용 변화가 있을 때만 계산하고, 고정 host는 기존 크기를 유지해야 한다.
- 새 branch의 control이 생성될 때 cursor/navigation 정책이 적용되며, 전체 트리 스캔이 필요하다면 실제 빈도와 비용을 측정한다.

이 사례의 핵심은 **상태별 버튼 외형의 소유권**, 그리고 **내부 layout·외부 host 크기·앱 전용 panel 위치를 분리하는 것**이다. 각각을 같은 “SwiftUI처럼 간단히”라는 목표 아래 다루되, 서로 다른 책임을 한 API에 섞지 않아야 한다.
