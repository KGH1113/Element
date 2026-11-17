# ElementUI의 SwiftUI 표현력·조합성 조사

조사일: 2026-11-17. 대상은 Element의 게임 내 UI와 이를 구현하는 ElementUI다. ElementNote React UI는 대상에서 제외했다. 구현 소스는 변경하지 않았다.

이 문서는 API 중심의 1차 조사다. 이후 실제 화면을 SwiftUI로 역설계한 [2차 조사](elementui-swiftui-counterfactual-review.md)에서 Opacity·presentation transform·범용 animation을 우선 기반 기능으로 재분류했다. 현재 개선 순서와 코드 비교는 2차 조사 및 연결된 세 사례집을 우선 참고한다.

추가로 전달된 Text 속성 패널의 hover 도형, OnHover 부재, host 크기 갱신, 플래그·파생 상태 사례는 [Text 속성 패널 추가 조사](elementui-inspector-api-cases.md)에 근거와 함께 정리했으며, 아래에도 결론을 반영했다.

**결론: 사용자가 지적한 검색창은 ElementUI의 기능 경계가 부족해서 복잡해진 사례가 맞다. 가장 직접적인 원인은 장식을 제거한 입력 primitive가 없다는 것이다. 더 넓게 보면 입력 동작의 선언형 API 부족, modifier를 붙였을 때 구조가 달라지는 문제, 컨트롤마다 다른 환경값 적용 규칙이 함께 발견됐다.**

다만 `ZStack`이 있다는 사실 자체가 문제는 아니다. ElementUI에는 이미 `HStack`, 순서에 의미가 있는 `Padding`·`Frame`·`Background`, 폰트 상속, `State`, `ControlStyle`, 키 기반 `ForEach`가 있다. 이 기반을 유지하면서 빠진 계약을 보완하는 편이 맞다.

## 조사 범위와 증거의 수준

| 대상 | 확인 내용 |
| --- | --- |
| Element | 커밋 `d2b53f1726ff088601e381a569acb97c74d53c29`, 조사 시작 시 working tree clean |
| 배포되는 ElementUI | `vendor/elementui/README.md`의 pin `258e5d7898a25f4e5c846e9e0a13ef560fc7fd61` |
| ElementUI 소스 | `/Users/kgh/dev/src/ElementUI`, HEAD `618e5c5e49a22ffae80da36a3ee19efdc789a9e2` |
| 버전 일치 확인 | pin과 HEAD의 tracked tree diff가 비어 있음. 이 문서의 현 소스 줄 번호를 pin에도 적용할 수 있음 |
| 바이너리 확인 | 실제 vendored `ElementUI.Core.dll`의 `ViewModifiers`를 `ilspycmd`로 확인. 소스와 같은 공개 modifier 목록 |
| 소비 코드 조사 | `Element/Presentation` 아래 C# 파일 327개에서 패턴을 검색하고 관련 구현을 추적 |
| 기본 검증 | ElementUI의 기존 `./scripts/check.sh` 실행: 56개 suite 통과, 종료 코드 0. 출력에 warning 2개, error 0개. 경고 원인은 이번 조사 범위에서 분석하지 않음 |
| 실행 검증의 한계 | 새 회귀 테스트는 작성하지 않았고 Unity/ADOFAI에서 신규 시나리오를 실행하지 않았음. 아래 동작 결함 후보는 소스 경로로 도출한 재현 조건이며 게임에서 관찰한 결과와 구분함 |

ADOFAI 게임 어셈블리 분석은 필요하지 않았다. 검색창의 제약이 ElementUI Core와 UGUI peer, 소비 화면 코드에서 확인됐기 때문이다. 게임 입력과 IME의 실제 이벤트 순서는 후속 Unity 검증 대상이다. `apple-design` 스킬과 프로젝트가 지정한 [Toss 오류 메시지 원칙](https://toss.tech/article/21021)을 참고했고, SwiftUI 비교는 Apple 공식 문서를 기준으로 했다.

우선순위는 보안 심각도가 아니라 작업 순서다. **P0**는 기본 조합을 막거나 의미를 바꾸는 문제, **P1**은 현재 화면에서 반복되는 우회 코드, **P2**는 확장성·표현 편의 또는 실제 수요를 확인한 뒤 진행할 영역이다.

## 검색창은 정확히 무엇 때문에 길어졌나

현재 [SearchBox](../../Element/Presentation/Layout/ContentEditors/Shell/OverlayValueBrowserView.cs)는 137행에서 `ZStack`으로 입력창과 아이콘·clear 버튼을 겹친다. 이어지는 `Dress()` 163행은 다음을 직접 처리한다.

| 현재 코드의 책임 | 위치 | 있어야 할 표현 수준 |
| --- | --- | --- |
| 배경색·반지름·테두리 제거 | `Dress`, 166–170행 | 입력 style 또는 바깥 컨테이너의 background |
| 아이콘과 clear 버튼을 위한 입력 영역 확보 | 173–175행, `SearchStart`·`SearchEnd` 상수 | HStack이 자식의 공간을 배분 |
| 본문·placeholder의 수직 정렬 | 177–179행 | 입력 컨트롤의 text alignment/metrics 계약 |
| 40자 제한 | 180행 | TextField 옵션 |
| Escape 시 원래 문자열 복원 방지 | 181행 | cancel/editing 정책 |
| 변경·Enter·Escape 동작 | 182–189행 | change/submit/cancel 이벤트 |
| native 입력창 저장 | 165행과 `SearchField` | focus 상태·focus 요청 API |

SwiftUI의 plain text field는 장식 없는 style이고, `.font`는 하위 텍스트의 기본 환경값으로 흐른다. 이 둘이 있으므로 아이콘과 입력 부분을 `HStack`의 형제로 놓고 바깥에 패딩과 배경을 붙일 수 있다. [PlainTextFieldStyle](https://developer.apple.com/documentation/swiftui/plaintextfieldstyle), [font(_:)](https://developer.apple.com/documentation/swiftui/view/font(_:))

현재 ElementUI의 `ControlStyle.TextField`에도 `Field()`가 있다. 하지만 이것은 장식 없는 편집 코어가 아니다. `TextFieldView` → `Backend.CreateTextField` → `TextFieldPeer`를 거치며 peer가 배경, 테두리, 내부 여백을 무조건 만든다. 스타일에서 `Field()`를 HStack 안으로 옮겨도 그 외형은 따라온다.

근거: [ControlStyle.cs:251](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/ControlStyle.cs:251), [Controls.cs:334](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Views/Controls.cs:334), [ControlPeers.cs:485](/Users/kgh/dev/src/ElementUI/src/ElementUI.UGUI/Peers/ControlPeers.cs:485). 배경은 `SurfaceRaised`, 테두리는 `Line` 1px, viewport inset은 좌우 10·상하 4이며 preferred height도 테마 control height다.

사용자가 제시한 SwiftUI 코드는 시각적 뼈대를 잘 보여 준다. 다만 현재 Element 검색창에는 조건부 clear 버튼, 검색 실행, 첫 결과 선택, Escape 처리, focus와 게임 입력 간 조정도 있다. 따라서 최종 코드의 길이를 그 예제와 일대일로 비교하면 안 된다. **목표는 동일한 행동을 유지하면서 화면 코드가 `TMP_InputField`, `RoundedRect`, `textViewport`, `UnityEvent`를 몰라도 되게 하는 것이다.**

아래는 **제안 API를 포함한 목표 예시이며 현재 컴파일되는 코드가 아니다.** `TextField` 간편 overload, `TextFieldStyle`, `Shapes.Capsule`는 새 API 제안이다. 기존의 `Bind`, `HStack`, `Font`, horizontal padding은 유지할 수 있다.

```csharp
private readonly State<string> _searchText = new("");

private View SearchBar() =>
    HStack(spacing: 8,
        Image(ElementIcons.Of(UiIconKind.Search), 15)
            .Foreground(Colors.Muted),
        TextField(Bind(() => _searchText.Value), placeholder: "검색")
            .TextFieldStyle(TextFieldStyle.Plain))
    .Font(Font.Body.Sized(14))
    .Padding(vertical: 0, horizontal: 14)
    .Frame(width: 200, height: 36)
    .Background(Colors.SurfaceRaised, shape: Shapes.Capsule);
```

현재 UI의 행동까지 포함한 버전에서는 plain field에 `Focused`, `OnSubmit`, `OnCancel`, 변경 이벤트를 선언하고, clear 버튼을 같은 HStack의 마지막 자식으로 추가하면 된다. 검색 전용 거대 `SearchField` 하나를 추가하는 것보다 이 일반 조합 능력을 먼저 만드는 편이 HEX·퍼센트 입력에도 효과가 있다.

## 실제 우회 코드의 분포

검색 결과는 결함 개수가 아니다. 주석·합당한 native 연동도 포함하는 정적 패턴 집계다.

| 패턴 | 출현 수 | 파일 수 | 해석 |
| --- | ---: | ---: | --- |
| `.Configure<T>` | 21 | 10 | 그중 14곳이 `TMP_InputField`. 입력 컨트롤에 우회가 집중됨 |
| `NativeView` 참조 | 17 | 17 | 색상 평면·그라디언트·게임 전용 표시 등 정당한 확장도 포함 |
| `ZStack(` | 16 | 12 | 겹침 자체는 정상적인 UI 구성. 입력창 장식 때문에 강제되는 겹침을 구분해야 함 |
| `.Font(` | 114 | 33 | 폰트 상속 부재의 증거가 아님. 실제 leaf별 환경 소비를 따로 확인 |
| `.Background(` | 50 | 24 | 기본 background 기능은 이미 널리 쓰임 |

검색창과 같은 문제가 [ColorPickerLayout.cs:80](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/ColorPicker/ColorPickerLayout.cs:80)의 HEX 입력과 110행의 `%` 입력, [LayoutToolbarView.cs:130](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/Canvas/LayoutToolbarView.cs:130)의 scale 입력에서도 반복된다. 모두 장식을 겹치고 TMP의 viewport 또는 margin을 직접 맞춘다.

## 확인된 문제와 개선 범위

### 1. P0 — 입력 primitive와 외형을 분리할 수 없다

앞에서 설명한 검색창의 직접 원인이다. `ControlStyle`은 바깥 조합을 바꿀 수 있지만 입력 코어의 chrome까지 제어하지 못한다. plain/default 스타일, content inset, control height, focus ring을 분리해야 한다. 스타일이 편집 상태나 binding을 다시 구현해서는 안 된다.

plain 구현에서 배경 Graphic을 무조건 없애는 것도 충분하지 않다. 투명한 입력 영역의 hit testing, caret, selection, placeholder, IME clipping, keyboard focus가 계속 동작해야 한다. 바깥 capsule의 빈 여백을 눌렀을 때 focus할지는 명시적인 상호작용 정책으로 결정해야 한다. 현재 검색창 전체 hit area를 의도치 않게 줄이지 않는 검증이 필요하다.

### 2. P0 — submit·cancel·editing end와 기본 입력 옵션이 공개 API에 없다

공개 `TextField`는 label·binding·placeholder만 받는다. `ITextFieldPeer`에는 Changed·Ended가 있지만 submit/cancel 이유가 없다. 소비 화면은 `onSubmit`, `onEndEdit`, `wasCanceled`, `characterLimit`, `restoreOriginalTextOnEscape`, 입력 타입, select-all을 직접 다룬다.

근거: [UI.cs:195](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/UI.cs:195), [Peers.cs:179](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Backend/Peers.cs:179), [ProfilesView.cs:200](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Settings/Pages/ProfilesView.cs:200), [EditorTweaksView.cs:94](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Settings/Pages/EditorTweaksView.cs:94).

후보는 `OnSubmit`, `OnEditingEnded(reason)`, `OnCancel`, 글자 수 제한, input kind, selection policy다. 이름보다 Enter·blur·Escape·hide·dispose를 구분하는 계약이 중요하다. 기존 `Command`/`Edited`/probe와 통합해 이벤트만 공개하고 갱신 알림은 빠지는 일이 없도록 해야 한다. SwiftUI에도 전용 [onSubmit](https://developer.apple.com/documentation/swiftui/view/onsubmit(of:_:))이 있다.

### 3. P0 — focus를 상태로 표현하고 관찰할 수 없다

`FocusName++`, `State<int> FocusHex` 같은 요청 번호를 감시하고 `ActivateInputField()`와 `Select()`를 호출하는 구현이 여러 곳에 있다. 이는 도메인 값이 아니라 선언형 API의 부족을 메우기 위한 이벤트 신호다.

근거: [ProfilesView.cs:116](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Settings/Pages/ProfilesView.cs:116), [PresetEditorView.cs:55](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Settings/Pages/PresetEditorView.cs:55), [ColorPickerLayout.cs:169](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/ColorPicker/ColorPickerLayout.cs:169).

필요한 것은 focus 대상 ID의 binding, 실제 focus 상태, 같은 필드로의 재요청, scope별 복원, 비표시 시 해제다. SwiftUI의 [FocusState](https://developer.apple.com/documentation/swiftui/focusstate/)는 읽기와 쓰기 양방향으로 focus를 연결하는 비교 대상이다. 다만 같은 프레임에 키 입력을 게임으로 돌려줘도 되는지 판단하는 책임은 여전히 Element의 통합 계층에도 있다.

### 4. P0 — 편집 중 외부 binding 갱신을 어떻게 처리할지 거의 정의되어 있지 않다

**소스로부터 도출한 재현 조건이다. 현재 clear 버튼에서 반드시 발생한다고 확인한 것은 아니다.**

`TextFieldView`는 binding 변경을 받아도 `_peer.IsEditing`이 true면 native text 갱신을 건너뛴다. 반면 Ended에서는 native 문자열을 무조건 binding에 쓴다.

1. field에 focus가 있고 화면에 표시된 값은 `old`다.
2. 외부에서 State/binding을 `new`로 바꾸고 focus가 있는 동안 flush한다.
3. native text로의 반영을 건너뛴다.
4. native 쪽이 `old`인 채로 편집을 끝내면 Ended가 model을 `old`로 되돌린다.

근거: [Controls.cs:343](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Views/Controls.cs:343), [ControlPeers.cs:519](/Users/kgh/dev/src/ElementUI/src/ElementUI.UGUI/Peers/ControlPeers.cs:519), 같은 파일 534행. FakeTextField에는 Editing과 End가 있어 게임 없이도 조건을 고정한 회귀 테스트를 만들 수 있다.

live binding과 draft/commit 모드, 외부 초기화의 우선순위, 편집 중 갱신의 지연 적용을 명문화해야 한다. 매번 `SetText`를 호출하는 단순한 수정은 caret이나 일본어·한국어 IME를 망가뜨릴 수 있다. ×를 클릭하면 먼저 blur가 발생할 수도 있으므로 클릭 순서 문제와 이 일반적인 동기화 문제는 따로 검증해야 한다.

### 5. P0 — Font 등의 modifier가 If/ForEach의 레이아웃 구조를 바꾼다

**구현 경로에서 확인한 동작 불일치다. 새로운 실행 테스트는 하지 않았다.**

stack은 `CollectItems()`로 If/ForEach를 펼친다. 그러나 `EnvironmentView`의 기반인 `WrapperView`는 이를 전달하지 않고 자신 하나를 item으로 반환한다. 따라서 다음 두 코드는 폰트 적용 범위뿐 아니라 배치도 다르다.

```csharp
HStack(ForEach(items, key, row).Font(font))
HStack(ForEach(items, key, row)).Font(font)
```

첫 번째 코드에서는 ForEach가 stack 밖에서 사용하는 세로 배치로 동작한다. 마찬가지로 직접 배치한 `If(false)`는 item을 반환하지 않지만, `.Font(...)`로 감싼 `If(false)`는 폭이 0인 wrapper 하나를 반환해 불필요한 spacing을 남긴다.

근거: [Stacks.cs:42](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Views/Stacks.cs:42), [View.cs:355](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Views/View.cs:355), [Modifiers.cs:8](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Views/Modifiers.cs:8), [Structure.cs:151](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Views/Structure.cs:151), 같은 파일 265–292행.

이는 메서드 이름을 추가하기 전에 고칠 가치가 있다. `Font`·`Foreground`·`ControlStyle`처럼 구조를 바꾸지 않는 modifier와 `Frame`·`Padding`처럼 영역을 만드는 modifier의 계약을 구분해야 한다. 모든 wrapper를 일률적으로 펼치면 background나 padding의 의미가 깨지므로 그것도 해결책이 될 수 없다.

### 6. P0 — Spacer·Divider는 modifier를 끼우면 stack의 축을 잃는다

**구현 경로에서 확인한 동작 불일치다. 새로운 실행 테스트는 하지 않았다.**

`Spacer`는 직접적인 `Parent is StackView`일 때만 한 방향으로 늘어난다. `Spacer().Padding(0)`의 Parent는 wrapper이므로 다시 양방향으로 늘어나는 것으로 취급된다. `Divider`도 직접적인 부모가 HStack일 때만 세로선이 되며, `Divider().Foreground(...)`에서는 그 판정이 사라진다.

근거: [Primitives.cs:202](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Views/Primitives.cs:202), 같은 파일 223행.

HStack 바로 안에 있는 Spacer의 과거 문제는 이미 수정되었고 [LayoutTests.cs:160](/Users/kgh/dev/src/ElementUI/tests/ElementUI.Tests/LayoutTests.cs:160)에도 테스트가 있다. 남은 문제는 wrapper·구조 view와의 조합이다. 단순히 부모의 타입을 검사하는 대신 배치를 담당하는 container의 축을 전달하는 계약이 필요하다.

### 7. P0/P1 — 같은 modifier도 control마다 다르게 적용된다

| 대상 | 현재 구현 | 부족한 계약 |
| --- | --- | --- |
| Text | Font, TextAlign, LineLimit, reactive Foreground를 읽음 | 기준이 되는 구현 |
| TextField | Font와 정적 Foreground만 사용. TextAlign.Leading 고정 | alignment, reactive Foreground, 입력 고유의 text metrics |
| TextField peer | MidlineLeft와 NoWrap을 다시 설정 | 선언된 값을 backend까지 전달하는 책임 |
| Segmented Picker/Tabs | options와 selection 중심. peer가 Callout 폰트·선택 색상을 고정 | label의 font/style 계약 |
| Image(Icon) | Foreground는 읽지만 크기는 constructor의 고정값 | symbol의 font 연동, size 지정의 우선순위 |

근거: [Primitives.cs:43](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Views/Primitives.cs:43), [Controls.cs:337](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Views/Controls.cs:337), 같은 파일 443행, [ControlPeers.cs:553](/Users/kgh/dev/src/ElementUI/src/ElementUI.UGUI/Peers/ControlPeers.cs:553), 같은 파일 629·686행.

실제 영향을 보여 주는 소비 코드가 [ModColumnStyle.cs:279](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Mods/ModColumnStyle.cs:279)에 있다. `Segments()` 아래의 TMP를 모두 찾아 fontSize=13으로 설정한다.

“Font 상속을 구현한다”는 목표는 너무 넓다. 각 control이 어떤 환경값을 따르고 선택 상태의 대비 등을 어디에서 덮어쓸지 먼저 표로 정의해야 한다. 효과가 없는 modifier를 아무 알림 없이 받아들이는 경우도 줄여야 한다.

### 8. P1 — style 확장은 있지만 control 사이에서 일관되지 않다

`ControlStyle`로 Toggle·Slider·TextField·Picker·Card·Section을 실제로 재구성할 수 있다. 유용한 기존 기능이다. 반면 Button은 `StyledControl`을 거치지 않고 `ButtonStyle` enum을 backend로 전달하는 별도 경로를 사용한다. subtree 단위의 button style이나 pressed/hover/disabled 상태로 구성하는 custom style의 표현은 일관되지 않다.

또한 Button 이외의 많은 control은 label이 string으로 고정되어 있다. 아이콘·subtitle·동적 label·보충 설명을 하나로 구성하려면 바깥에서 행을 다시 조합해야 한다.

근거: [ControlStyle.cs:27](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/ControlStyle.cs:27), [Controls.cs:38](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Views/Controls.cs:38), [UI.cs:156](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/UI.cs:156).

기존 host 전체의 ControlStyle을 유지하면서 control별 override, 상태 configuration, View label overload, label 숨김 지정을 추가하는 방향을 생각할 수 있다. 하나의 거대한 style class에 모든 설정을 계속 추가하는 방식은 피해야 한다.

### 9. P1 — background는 있지만 장식 view로 조합할 수 없다

`.Background`는 색상·cornerRadius·고정적인 stroke에 한정된다. 임의의 View를 background/overlay로 넣는 API, 주 콘텐츠 크기에 맞춰지는 장식, clip/mask, 독립적인 border, shape fill/stroke는 없다.

근거: [ViewModifiers.cs:98](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/ViewModifiers.cs:98), [Modifiers.cs:275](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Views/Modifiers.cs:275). SwiftUI의 overlay는 주 view가 크기를 결정하는 구성이므로 자식을 동등하게 취급하는 ZStack과 구분된다. [Apple overlay](https://developer.apple.com/documentation/swiftui/view/overlay(alignment:content:))

다만 capsule 모양 자체는 이미 만들 수 있다. `Background(color, 999)`는 [RoundedRect.cs:45](/Users/kgh/dev/src/ElementUI/src/ElementUI.UGUI/Graphics/RoundedRect.cs:45)에서 반지름을 짧은 변의 절반으로 제한한다. `Capsule()`은 우선 의도를 명확히 표현하는 용어상의 개선이며, “둥근 검색창을 그릴 수 없다”는 기능 부족은 아니다.

SwiftUI의 `.quaternary`도 blur material 자체가 아니라 계층적인 shape style이다. 이 예제를 위해 부하가 큰 blur를 구현할 필요는 없다. [Apple quaternary](https://developer.apple.com/documentation/swiftui/shapestyle/quaternary-swift.type.property)

### 10. P1 — reactive modifier의 지원 범위가 고르지 않다

Hidden·Disabled·Hint·Foreground·Background의 색상에는 expression 버전이 있다. 반면 Frame·Padding·Font·LineLimit·radius·alignment·image size는 고정값 중심이다. Body를 한 번만 구성하는 방식이므로 일반 값을 전달한 뒤 state가 바뀌어도 다시 평가하지 않는다.

근거: [ViewModifiers.cs:16](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/ViewModifiers.cs:16), [View.cs:414](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Views/View.cs:414).

그 결과 폭·높이·글자 크기를 상태에 따라 바꾸는 일반적인 UI에도 여러 branch, Configure, custom View 등이 필요하다. 실제 수요가 있는 속성부터 typed reactive overload를 추가하고, 값이 같으면 처리하지 않으며 치수 변화는 measure, 색상 변화는 paint로 나누는 무효화 구분을 유지하는 것이 적절하다.

### 11. P1/P2 — 공간을 양보하는 순서·baseline·공유 열을 지정할 수 없다

stack은 기본적으로 fixed/flexible 구분과 폭 배분으로 동작한다. `layoutPriority`, `fixedSize`, ideal size, weighted flexibility에 해당하는 공개 지정 방법이 없다. alignment도 leading/center/trailing과 top/center/bottom으로 한정되며 first/last baseline이나 custom alignment guide는 없다.

근거: [Stacks.cs:73](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Views/Stacks.cs:73), [Geometry.cs:85](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Layout/Geometry.cs:85), [ViewModifiers.cs:28](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/ViewModifiers.cs:28).

현재 Grid는 동일한 폭의 열에 적합하지만 label 열의 intrinsic 폭을 공유하는 form/table에는 적합하지 않다. 검색 브라우저는 group heading과 sidebar의 폭을 직접 계산하고, ModColumnStyle도 label 폭을 바탕으로 track이나 picker의 구성을 고른다. 이 계산이 전부 불필요하다는 뜻은 아니지만 공통 layout 표현을 추가하면 줄일 수 있는 부분이다.

근거: [OverlayValueBrowserView.cs:249](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayValueBrowserView.cs:249), 같은 파일 355행, [ModColumnStyle.cs:287](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Mods/ModColumnStyle.cs:287).

### 12. P2 — adaptive/custom layout의 확장에는 제약이 있다

`ViewThatFits`는 이미 있지만 후보의 폭을 기준으로 선택하며 높이나 axis 지정을 다루지 않는다. `View`의 Measure/Place는 override할 수 있지만 자식을 정식 dirty/cache 경로로 배치하는 `PlaceAt`은 internal이므로 다른 assembly에서 custom layout을 완성하기 어렵다.

근거: [Stacks.cs:442](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Views/Stacks.cs:442), [View.cs:221](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Views/View.cs:221).

SwiftUI Layout의 모든 기능을 서둘러 재현할 필요는 없다. 우선 axis를 지정하는 ViewThatFits와, 필요하다면 measure/place만 공개하는 제한적인 LayoutSubview 계약이 후보가 된다.

### 13. P1/P2 — Binding을 변환·투영하는 공개 경로가 없다

`State<T>`와 `Bind(() => ...Value)`는 이미 있다. 문제는 단순 state의 부재가 아니라 자유로운 getter/setter delegate로 변환하는 binding, 다른 타입으로의 projection, validation을 포함한 adapter를 간결하게 표현하기 어렵다는 점이다. 공개 Bind는 field뿐 아니라 public setter가 있는 auto-property, 허용된 `[Pure]` property/getter 경로, State.Value도 지원한다. 2차 조사에서 [MemberPath.cs:137](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Reactive/MemberPath.cs:137)과 [Lowering.cs:209](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Reactive/Lowering.cs:209)을 확인해 기존 field-only 설명을 정정했다.

근거: [UI.cs:228](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/UI.cs:228), [Sources.cs:8](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Model/Sources.cs:8). SwiftUI에는 [Binding(get:set:)](https://developer.apple.com/documentation/swiftui/binding/init(get:set:)-7ufcp)이 있다.

이를 추가할 때 setter closure를 호출하는 것만으로는 부족하다. 의존하는 source, 쓰기 알림, Edited, undo 경계, 늦게 도착한 결과의 세대 확인까지 지켜야 한다. typed projection을 우선하면 이 라이브러리의 차분 갱신 방식을 유지하기 쉽다.

### 14. P1 — 일반 View에서 환경값을 읽는 수단이 부족하다

환경값을 전달하는 기능 자체는 있다. 그러나 custom View가 isEnabled, theme, reduce motion 등을 사용할 수 있는 일반적인 읽기 API는 없다. 소비 측의 `EnabledWatch`는 크기가 0인 NativeView에서 EnabledChanged를 State로 옮긴다.

근거: [EnabledWatch.cs:7](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Declarative/Controls/EnabledWatch.cs:7), [SwitchCards.cs:40](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Declarative/Controls/SwitchCards.cs:40).

ControlStyle의 configuration에는 IsEnabled가 있으므로 “환경값이 전혀 없다”는 뜻은 아니다. 일반 View에도 필요한 읽기 경로를 제공하고 변경을 어느 단위로 추적할지 정하는 것이 개선점이다.

### 15. P1 — lifecycle을 표현하려고 보이지 않는 native view가 필요해진다

ColorPicker는 ZStack에 `ClosesWithScreen`이라는 크기 0의 NativeView를 넣고 native component의 OnDisable과 Teardown에서 닫기 처리를 호출한다. lifecycle만을 위한 레이아웃 요소가 섞여 있다.

근거: [ColorPickerView.cs:52](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Declarative/Controls/ColorPickerView.cs:52), 같은 파일 63–88행.

공개 mount/unmount만으로는 부족하다. 창의 SetActive(false), If의 비활성화, 표시·숨김, 파괴의 차이를 정의한 lifecycle hook이 필요하다. `Configure`의 IDisposable cleanup overload는 이미 있으므로 이를 무시하고 모두 새로 만들 필요도 없다.

### 16. P1/P2 — If의 수명은 SwiftUI와 비슷한 구문에서 예상하는 것과 다르다

ElementUI의 If는 처음 연 branch를 유지하고 닫아도 destroy하지 않으며, 효과와 입력을 멈춘 뒤 재사용한다. 스크롤 위치와 입력을 보존하기에 편리하고 실제 값 브라우저도 이 동작을 활용한다.

근거: [Structure.cs:94](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Views/Structure.cs:94), [OverlayValueBrowserView.cs:225](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayValueBrowserView.cs:225).

따라서 이를 곧바로 버그라고 볼 수는 없다. 다만 조건이 해제되면 local state나 resource를 파괴하려는 UI에도 같은 동작이 강제된다. 유지하는 조건과 다시 생성하는 조건을 선택하는 방법, identity reset, lifecycle 설명이 필요하다. 많은 branch를 여는 화면에서는 유지되는 메모리도 측정해야 한다.

### 17. P1 — 스크롤 위치·동작을 선언할 수 없다

ScrollView는 axis와 content를 받지만 offset binding, scrollTo, anchor, behavior 등은 DSL에 없다. 값 브라우저는 검색어가 바뀌면 ScrollRect.verticalNormalizedPosition을 1로 되돌리고 scrollSensitivity와 inertia도 직접 설정한다.

근거: [UI.cs:110](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/UI.cs:110), [OverlayValueBrowserView.cs:261](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayValueBrowserView.cs:261), 같은 파일 347행.

위치 controller나 binding, reset 요청, reduce motion에 맞는 behavior를 공개하면 이 Configure를 줄일 수 있다. 스크롤할 때마다 view 전체를 다시 구성하는 구현은 피해야 한다.

### 18. P1/P2 — keyed ForEach는 있지만 표시 범위만 만드는 list는 아니다

ForEach는 key별로 row를 유지하고 새 object를 ItemSlot에 교체한다. 이는 이미 잘 구현된 부분이다. 다만 모든 row를 생성·유지·측정하므로 virtualization은 아니다. ScrollView에서 clip해도 row 수는 줄지 않는다.

근거: [Structure.cs:206](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Views/Structure.cs:206), 같은 파일 265행. SwiftUI의 [LazyVStack](https://developer.apple.com/documentation/swiftui/lazyvstack)은 필요할 때 item을 만드는 별도 기능이다.

검색 결과나 폰트 목록이 많을 때는 구문을 줄이는 것보다 중요할 수 있다. 먼저 실제 데이터 규모에서 mount 시간, GameObject 수, 스크롤 시 layout 비용을 측정하고 lazy/virtualized container를 독립적인 기능으로 설계해야 한다. keyed identity를 유지하면서 focus·편집 중인 row도 보존해야 한다.

### 19. P2 — Tabs의 selection을 외부 상태와 연결할 수 없다

Tabs는 내부 State<int>와 tab key로 만든 memory key에 selection을 보관한다. public API는 `Tabs(params TabPage[])`이며 선택 binding은 없다.

근거: [UI.cs:139](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/UI.cs:139), [Structure.cs:298](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Views/Structure.cs:298).

다른 버튼에서 특정 탭을 열거나 URL·navigation state와 동기화하거나 현재 탭을 다른 view에서 사용하려면 선택 ID의 binding이 필요하다. 현재의 내부 기억 기능은 편리한 default로 유지할 수 있다.

### 20. P1/P2 — gesture·keyboard·hit testing을 조합하는 일반 API가 없다

tap/hover/drag/key, hit-testing 활성·비활성, content shape 등을 일반 view에 붙이는 공개 modifier가 없다. Hint 내부에는 hover 처리가 있지만 사용자의 선언에 hover state를 전달하는 일반 기능은 아니다.

근거: [ViewModifiers.cs:13](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/ViewModifiers.cs:13), [OverlayValueBrowserView.cs:344](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayValueBrowserView.cs:344), [OverlayValueChipHandle.cs:12](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayValueChipHandle.cs:12).

drag API를 추가한다면 시작·변화·종료·취소, 스크롤과의 우선권, capture, hidden 시 취소까지 하나의 계약으로 정의해야 한다. 값을 formula에 놓았을 때의 의미는 앱에 남겨야 한다. 게임 고유의 캔버스 조작을 모두 DSL로 옮길 필요는 없다.

### 21. P0/P1 — animation은 내부에 있지만 view 상태에 연결하는 공개 계약은 없다

UGUI 쪽에는 switch나 segmented selection 등의 내부 animation이 있다. 따라서 “animation 엔진이 없다”는 뜻은 아니다. 반면 일반 view에 state와 연결된 transition이나 animation, reduce-motion 대응을 선언하는 API는 없다.

근거: [ControlPeers.cs:577](/Users/kgh/dev/src/ElementUI/src/ElementUI.UGUI/Peers/ControlPeers.cs:577), 공개 [ViewModifiers.cs](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/ViewModifiers.cs).

2차 조사에서 우선순위를 상향했다. 설정창·toast·표시용 switch·disabled 버튼이 Opacity·Offset·ScaleEffect와 animation을 각각 native 코드로 구현하고 있으므로 plain field와 함께 먼저 갖춰야 할 기반이다. 추가할 때는 현재 표시값에서 새 목표로 전환하기, transaction의 deferred Flush 전달, 중단 가능성, 숨긴 branch의 정지, reduce motion, idle 시 driver를 돌리지 않기를 합격 조건으로 삼아야 한다. 매 프레임 모든 요소를 다시 layout하는 방식은 피해야 한다.

### 22. P1/P2 — image와 typography의 일반적인 표현 범위가 좁다

`Image`는 `Icon` 전용이며 texture/sprite, intrinsic 크기, aspect fit/fill 같은 일반적인 image 계약이 없다. 소비 측의 `TextureImage`가 RawImage 생성과 aspect-fit 계산을 구현하고 있다. symbol 크기도 Font 환경값으로 정해지지 않는다.

근거: [UI.cs:39](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/UI.cs:39), [Primitives.cs:249](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Views/Primitives.cs:249), [TextureImage.cs:16](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Declarative/Controls/TextureImage.cs:16).

typography는 role·size·bold 중심이며 weight 단계, tracking, line spacing, font family, truncation 선택, minimum scale 등이 부족하다. 다만 LineLimit과 ellipsis는 존재한다.

근거: [Theme.cs:86](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Backend/Theme.cs:86), [RectPeer.cs:186](/Users/kgh/dev/src/ElementUI/src/ElementUI.UGUI/Peers/RectPeer.cs:186).

SF Symbols 에셋을 Unity로 그대로 가져오는 것과는 별개다. 우선 현재 icon system과 Texture를 유지하면서 size·font·color의 우선 규칙과 일반 image 표시를 정리하는 것이 현실적이다.

### 23. P1/P2 — 입력 validation과 formatted value의 표현이 고정적이다

NumberField는 float binding, min/max, InvariantCulture, 일정한 format으로 동작한다. 범위 안이면 입력 중에 적용하고 편집 종료 시 clamp하거나 원래 값으로 되돌린다. 중간 문자열, 파싱 실패, 사용자에게 제시할 수정 방법을 view에서 구성하는 계약은 없다.

근거: [Controls.cs:379](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Views/Controls.cs:379), 같은 파일 399행.

간단한 슬라이더 보조 입력에는 현재 default가 적합하다. HEX, 정수 전용, 단위가 붙는 입력, 현지화된 숫자, draft/commit에는 parse/format과 validation 결과를 분리하는 것이 유용하다. 오류는 상황·이유·다음 행동을 설명하고 입력 근처에 적절히 표시할 수 있어야 한다. [Toss의 원칙](https://toss.tech/article/21021)

### 24. P2 — semantic metadata와 접근성의 공개 모델이 없다

label/value/hint/role, view/test ID, focus order를 backend에 의존하지 않고 붙이는 공개 계약이 없다. 소비 측 Named helper는 native GameObject.name을 바꾸는 방식이라 그리기 object를 가지지 않는 stack 등에 똑같이 사용할 수 없다.

근거: [ViewModifiers.cs](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/ViewModifiers.cs), [NamedViews.cs:9](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Declarative/Controls/NamedViews.cs:9). 현재 `.Named`는 접근성 지원을 의미하지 않는다.

먼저 debug/test identity와 control semantics를 분리해 정의하는 것이 좋다. OS screen reader 지원까지 약속하려면 Unity host 쪽 bridge가 필요하며, modifier만 추가하고 지원이 끝났다고 해서는 안 된다. Hint도 화면의 hover hint와 accessibility hint는 별도 계약이다.

### 25. P2 — 작은 동작에도 Command 준비 코드가 길어지기 쉽다

Button은 Command를 고르는 expression을 받는 설계이며 raw Action overload가 없다. ColorSwatch의 popover를 열거나 switch card를 반전시키는 데도 작은 ViewModel/Command가 필요하다.

근거: [UI.cs:156](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/UI.cs:156), [ColorSwatch.cs:35](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Declarative/Controls/ColorSwatch.cs:35), [SwitchCards.cs:60](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Declarative/Controls/SwitchCards.cs:60).

Command는 갱신 감지·CanExecute·편집 경계에서 역할을 하므로 없애야 할 장치는 아니다. 간단한 local action을 같은 실행 계약으로 처리하는 편의 API가 있으면 SwiftUI처럼 가볍게 표현할 수 있다. ViewModel이 있다는 사실 자체를 결함으로 세어서는 안 된다.

### 26. P2 — Padding 등의 표현을 줄일 수 있지만 주요 원인은 아니다

`.Padding(vertical: 0, horizontal: 14)`는 이미 있다. 사용자 예제의 horizontal padding을 위해 새 기능을 만들 필요는 없다. 다만 한쪽 방향의 padding을 위해 매번 `new Insets(top, leading, bottom, trailing)`을 쓰는 것은 읽기 어려우므로 edge 지정이나 named parameter 편의 API는 유용하다.

근거: [ViewModifiers.cs:16](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/ViewModifiers.cs:16).

마찬가지로 `TextField(null, binding, placeholder)`에서 null label을 생략하는 overload, system font size의 짧은 표현 등도 개선할 수 있다. 다만 C#의 params, lambda, `Bind(() => ...)`를 완전히 Swift 구문과 맞추는 것과 필요한 Unity 설정을 없애는 것은 별개다. 후자를 먼저 진행해야 한다.

### 27. P1 — ViewModel 없는 재사용 컴포넌트의 기본 형태가 없다

사용자 예제처럼 SearchBar가 local State와 Body만 소유하는 컴포넌트를 작성하려면 현재 API에서는 불필요한 모델 또는 저수준 View 구현이 필요하다. 비제네릭 `View`에는 합성용 Body가 없고, Body를 제공하는 `View<TViewModel>`은 null이 아닌 모델이나 ItemSlot을 생성자에 요구한다.

근거: [View.cs:394](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Views/View.cs:394), 같은 파일 401·410행.

메서드가 View를 반환하는 조합 자체는 이미 가능하다. 부족한 것은 local State와 lifecycle을 소유하는 작은 재사용 컴포넌트를 만들기 위한 가벼운 기본형이다. 예를 들어 비제네릭 합성용 ComponentView를 제공하고 기존 `View<TViewModel>`을 유지하는 방향을 검토할 수 있다. 이름은 제안이며, 인스턴스 수명과 State의 소유권을 명확히 하는 것이 핵심이다.

### 28. P1 — binding write의 부수 변경과 외부 push 경계를 작성자가 관리해야 한다

binding은 자신이 쓴 경로와 관련 source를 probe한다. 그 쓰기에서 다른 일반 필드까지 변경되는 경우 모두 자동 추적되는 것은 아니다. ColorPickerView는 color binding을 쓴 직후 `Runtime.Probe()`를 추가하며, 그 쓰기로 map color가 custom color로 바뀌는 등의 부수 변경을 반영하기 위한 것이라고 설명한다.

근거: [UI.cs:257](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/UI.cs:257), [ColorPickerView.cs:46](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Declarative/Controls/ColorPickerView.cs:46).

State·Computed·Command의 추적이 없다는 뜻이 아니다. 일반 필드를 외부에서 바꾸는 경우와 binding write의 부수 변경에 대해, transaction·targeted invalidation·command 경계를 사용자가 분명히 지정하도록 돕는 API와 진단이 필요하다는 뜻이다. 모든 binding마다 전체 Probe를 추가하면 현재의 좁은 범위 갱신 장점을 잃는다.

### 29. P2/P3 — 비동기 작업·화면 presentation의 고급 API도 범위가 제한적이다

공개 API 전체를 살폈을 때 AsyncCommand/Task의 취소·수명 관리, Navigation/Sheet/Dialog와 같은 일반 presentation primitive는 없다. Popover는 이미 지원하므로 presentation 전체가 없다는 주장은 틀리다. Command가 async callback을 거부하는 UI005 진단도 있으며, 이는 async void를 막는 유효한 제약이다.

근거: [UI.cs](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/UI.cs), [ViewModifiers.cs](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/ViewModifiers.cs), [AnalyzerTests.cs:79](/Users/kgh/dev/src/ElementUI/tests/ElementUI.Tests/AnalyzerTests.cs:79).

이 항목들은 검색창 개선의 전제 조건이 아니다. 실제 화면 요구가 생길 때 loading·중복 실행 방지·취소·dispose 후 갱신·main-thread dispatch, modal focus·dismiss·presentation 수명을 갖춘 별도 계약으로 설계할 영역이다. SwiftUI의 모든 API를 목록대로 복제하는 로드맵으로 사용해서는 안 된다.

## 추가 Text 속성 패널 사례의 결론

| 사례 | 확인 결과 | 분류 |
| --- | --- | --- |
| 기본 배경과 hover 모양 | 외부 Background radius 10, 내부 Plain ButtonPeer hover radius 20 | 상태별 외형을 한곳에서 소유하기 어려운 P1 style API 문제 |
| OnHover | 공개 modifier는 없음. IContainerPeer.Hovered와 Hint 내부 구현은 있음 | 기존 기반 기능을 일반 View에 연결하는 P1 API 공백 |
| MotionSeconds | 버튼 hover 색은 즉시 바뀜. 해당 시간값의 계약은 switch/segment 이동 | 전역 설정 위반이 아니라 버튼 transition 정책의 부재 |
| Refit | 내부 layout은 자동 갱신되지만 바깥 host를 내용 높이에 맞추는 동작은 일회성 | opt-in auto-fit 또는 content-size 알림의 P1/P2 부족 |
| Sync | MountedCount가 바뀔 때 새 control에 cursor/navigation 정책 적용 | 앱 정책은 정당함. mount/host policy API로 간소화할 여지 |
| anchoredPosition | 별도 tool host와 inspector host의 위치 관계 계산 | 현재 앱 통합 계층의 책임 |
| 플래그·파생 State | 일부 조합을 묵시적으로 무시하고 표시 값을 중복 저장 | 기존 Computed·State를 활용할 수 있는 앱 모델 개선 |
| List/Add/ToArray·CopyTo | readonly 구성값을 mount 시 조합 | 기존 null child·C# collection 표현으로 줄일 여지. 성능 결함으로 미확정 |
| 반복 람다 | tracked value와 Command selector가 비슷하게 보임 | 의미 구분의 개선 대상. ItemSlot 교체 추적을 잃지 않도록 주의 |

근거와 코드 경로, SwiftUI 비교, 검증 조건은 [추가 조사 문서](elementui-inspector-api-cases.md)에 있다. 버튼의 바깥 Background가 내부 hover shape를 자동으로 변경해야 한다는 주장은 하지 않는다. 문제는 두 외형을 의도대로 통제하는 선언형 경로가 부족하다는 것이다.

## 현재의 부족으로 세지 않은 항목

| 항목 | 판단 |
| --- | --- |
| HStack / VStack / ZStack | 이미 있다. ZStack은 overlay·크기 확보·겹쳐 그리기에 정당한 용도가 있다 |
| modifier 순서 | Padding/Frame/Background는 실제로 안쪽을 감싼다. 순서 전체가 잘못된 것은 아니다 |
| Font / Foreground 환경 | 전달 구조는 있다. 문제는 개별 leaf의 사용 방식과 wrapper의 구조 계약이다 |
| State / Bind | 이미 있다. `@State`에 해당하는 기능을 처음부터 만들 필요는 없다 |
| ForEach identity | domain key와 ItemSlot을 통한 유지가 구현되어 있다 |
| Hidden | 크기를 유지하며 그리기를 숨긴다. If로 item 자체가 없어지는 것과 구분된다 |
| Disabled | 조상의 disabled가 우선하는 전달 방식이 구현되어 있다 |
| ControlStyle | 현재 ModColumnStyle에서 충분히 쓰인다. 폐기보다 빈틈을 보완해야 한다 |
| Computed | 이미 있다. Text 속성 패널의 수동 Set/Paint를 모두 framework 기능 부재로 볼 수 없다 |
| Adaptive Grid / ViewThatFits / Flow | 이미 있다. 고급 layout 기능의 부족과 구분해야 한다 |
| Popover | 선언형 API가 이미 있다. 다만 일반 overlay와는 다르다 |
| direct-child Spacer | 과거 양축 stretch는 수정되었다. SwitchCards의 설명은 현재 pin보다 오래된 내용이다. wrapper 문제는 별개다 |
| NativeView | 색상 평면, gradient, 게임 formula, native host 등에 적합한 확장 수단이다. 사용 개수 0을 목표로 하지 않는다 |
| 게임 입력 제어 | focus의 범용 부분은 framework로 옮길 수 있다. ADOFAI에 키를 돌려주는 시점 등은 Element의 통합 책임도 남는다 |

또한 [HairLine.cs:10](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Declarative/Controls/HairLine.cs:10)은 축소 시 Divider의 표시를 자체 native 렌더링으로 보완한다. 이는 SwiftUI와 비슷한 메서드 이름을 추가해 해결할 문제가 아니라 scale이나 pixel alignment를 포함한 backend 렌더링 품질의 검증 항목이다. 이번 조사에서는 Unity에서 시각적으로 재현하지 않았다.

## 개선을 진행할 순서

| 단계 | 구현 단위 | 완료 기준 |
| --- | --- | --- |
| 1 | modifier의 구조 보존, Opacity·Offset·ScaleEffect, hit-testing 분리 | 스타일을 붙여도 배치 의미가 유지되고 기본 alpha·좌표 조작을 제거 가능 |
| 2 | Animation(value)·WithAnimation·transaction·Transition·reduce motion | 설정창·toast·editor가 각자의 clock/Update를 소유하지 않음 |
| 3 | shape·background/overlay·image, plain TextField·focus·입력 이벤트 | swatch와 검색·HEX·%를 일반 View 조합으로 표현 |
| 4 | control별 환경·style·동적 layout·lifecycle·OnHover·host fitting | native 접근 없이 상태별 외형과 일반 화면 동작을 선언 |
| 5 | 실측에 근거한 lazy list, layout 확장, Binding projection | 많은 데이터와 복잡한 form에서 성능·코드량 개선을 보여 줌 |
| 6 | typography·고급 gesture·metadata·추가 presentation | 현재 앱에서 반복되는 요구부터 순서대로 확장 |

1단계와 2단계는 병행해서 설계할 수 있다. 입력 API만 먼저 늘려도 자연스러운 modifier 조합에서 배치가 바뀌는 문제가 남아 있으면 사용자는 다른 우회 방법을 찾아야 한다.

책임을 다음과 같이 나누면 유지보수하기 쉽다.

- **Core의 입력 모델**: binding, 편집 state, validation, focus·submit·cancel의 의미.
- **style**: border, fill, insets, label, accessory, focus 표시. 입력 이벤트를 다시 구현하지 않는다.
- **UGUI peer**: TMP·Selectable·IMGUI/Unity 이벤트 변환, caret·IME·hit area 유지.
- **layout**: logical child, modifier의 투명성, axis, measure/place/invalidation.
- **Element 화면**: 검색·결과 선택·HEX 해석 등의 목적과 게임으로의 입력 전달.

기존 peer interface나 ControlStyle을 확장할 때는 ElementKit 등 다른 mod의 구현·ABI도 확인해야 한다. 새 API는 우선 ElementUI 본체에 구현하며 `vendor/elementui`의 DLL을 직접 편집하는 방식은 아니다. 구현을 허용받은 단계에서 ElementUI를 검증하고 `./scripts/run.sh elementui-sync ...`로 pin을 갱신한 뒤 Element 쪽 관련 검사와 install을 진행한다.

## 필요한 회귀 검증

| 검증 | 조건과 기대 결과 |
| --- | --- |
| 검색창 조합 | icon/plain field/조건부 clear로 구성한 HStack. 200×36·좁은 폭·폰트 변경에서도 겹치지 않음 |
| field 기본 동작 | 빈 문자열·긴 문자열·selection·caret·placeholder·paste·일본어/한국어 IME |
| 입력 이벤트 | Enter, blur, Escape, hide, dispose의 이유를 구분. command나 Edited가 두 번 실행되지 않음 |
| 외부 갱신 | focus 중 clear/undo/model 교체. 어떤 값이 우선하는지 명세대로 유지 |
| focus | 프로그램 요청, 같은 field로 재요청, clear 후 유지, scope 복원, 비표시 시 해제 |
| 투명 modifier | ForEach/If에 Font·Foreground·ControlStyle을 붙였는지와 무관하게 child frames와 spacing이 같음 |
| axis | Spacer/Divider를 wrapper로 한 겹 또는 여러 겹 감싸도 부모 stack의 배치 방향을 유지 |
| 환경 | Text/TextField/Segmented/Icon의 대응표대로 font·foreground·alignment가 적용됨 |
| 버튼 상태·hover | normal/hover/pressed/selected/disabled의 shape와 색상이 하나의 선언을 따름. Hint와 OnHover가 공존하고 hidden 시 상태가 정리됨 |
| 장식 | Background/Overlay가 main content의 intrinsic size를 바꾸지 않고 pointer를 불필요하게 막지 않음 |
| lifetime | retained If와 재생성 모드의 state·subscription·resource 해제가 명확함 |
| host fitting | 내용 변화만으로 opt-in host 크기가 갱신되며 고정 크기 host는 영향을 받지 않음 |
| scroll/list | 검색 초기화, 위치 유지, 행 추가·삭제·동일 key 교체, focus가 있는 row, 긴 목록 |
| 성능 | 기존 idle/slider allocation 테스트 유지. mount 시간, 레이아웃 횟수, GC, GameObject 수도 실제 게임에서 측정 |

기존 56개 suite 통과는 기반이 동작한다는 확인이지만 여기에 제시한 미추가 조합까지 보장하지는 않는다. 특히 native 입력·폰트·IME·raycast와 렌더링 품질은 fake backend로 판단할 수 없다.

이번 조사에서 최우선으로 판단한 것은 **조합해도 modifier의 의미가 유지되는 것, 입력 기능과 장식을 분리할 수 있는 것, 같은 환경값을 control 사이에서 일관되게 다루는 것**의 세 가지다. 이를 갖추면 검색창뿐 아니라 실제 HEX·%·프로필 입력의 우회 코드도 함께 줄일 수 있다.
