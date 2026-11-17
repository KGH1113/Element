# ElementUI 사례 조사: 오버레이 편집기와 입력

2026-11-17 기준 실제 사용 코드를 SwiftUI의 자연스러운 표현과 비교한 조사다. 구현 변경 제안서이며, 이번 작업에서는 이 문서만 추가했다.

비교의 목적은 C# 문법을 Swift 문법과 똑같이 만드는 것이 아니다. 입력·장식·상태·수명·움직임을 선언하면 framework가 반복 작업을 맡을 수 있는지 확인한다. 아래 SwiftUI 코드는 **컴파일하지 않은 부분 스케치**다. `model`, `ValueGroupRow`, `ArrowShape`, `BandContent`, `InspectorRow`, `SwitchIndicator` 등은 앱에서 정의한다고 가정한 타입이며 SwiftUI 표준 API가 아니다. 생략된 앱 동작과 현재 구현과의 차이는 사례마다 적었다.

소스 근거는 현재 Element 저장소와 인접한 ElementUI 저장소를 직접 읽었다. ElementUI의 vendored pin `258e5d7898a25f4e5c846e9e0a13ef560fc7fd61`과 조사한 HEAD `618e5c5e49a22ffae80da36a3ee19efdc789a9e2` 사이의 source tree diff는 없었다. 실행 중인 게임의 시각·입력 동작을 재현하거나 새 성능 수치를 측정하지는 않았다.

## 1. 검색창: chrome와 입력을 분리하면 ZStack의 필요가 줄어든다

실제 [SearchBox](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayValueBrowserView.cs:134)는 TextField 위에 아이콘·clear 버튼을 얹는다. [Dress](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayValueBrowserView.cs:163)는 background의 색·radius·stroke, text viewport 좌우 inset, alignment를 바꾼 뒤 40자 제한·검색 갱신·Enter 첫 결과 선택·Escape 검색 지우기를 연결한다.

현재 작업을 요약하면 다음과 같다.

```csharp
ZStack(
    TextField(null, Bind(() => VM.Query.Value), placeholder)
        .Configure<TMP_InputField>(Dress),
    HStack(searchIcon, Spacer(), clearButton)
)
```

동일한 대표 동작을 유지한 SwiftUI 구성은 다음에 가깝다.

```swift
HStack(spacing: 8) {
    Image(systemName: "magnifyingglass")
        .foregroundStyle(.secondary)

    TextField("값 찾기", text: $query)
        .textFieldStyle(.plain)
        .focused($searchFocused)
        .onSubmit { model.pickFirst() }
        .onExitCommand { model.clearSearch() }

    if !query.isEmpty {
        Button { model.clearSearch() } label: {
            Image(systemName: "xmark")
                .frame(width: 24, height: 24)
        }
        .buttonStyle(.plain)
    }
}
.font(.system(size: 13))
.padding(.leading, 10)
.padding(.trailing, 4)
.frame(width: listWidth, height: fieldHeight)
.background(editorControl, in: Capsule())
.onChange(of: query) { _, value in model.search(value) }
```

이 스케치의 query binding에는 40자 제한과 모델 쓰기 정책이 들어가야 한다. SwiftUI 표준 `.characterLimit(40)`가 있는 것은 아니다. `onExitCommand`는 desktop Escape 처리 예시이며 게임의 키 라우팅과 동일한 플랫폼 동작을 보장한다는 뜻도 아니다.

필요한 공통 기능은 **plain TextField, field chrome 분리, focus·submit·cancel·change 이벤트, shape background**다. 아이콘·입력·clear를 실제 형제로 배치하면 viewport offset을 수동으로 맞추지 않아도 된다. ZStack 자체의 문제가 아니라, chrome가 고정된 입력을 꾸미기 위해 overlay가 필요한 구조가 문제다.

처음 제시된 짧은 SwiftUI 검색창에는 clear·Enter·Escape 등의 현재 기능이 없었다. 따라서 줄 수 차이를 모두 framework 개선 효과로 계산할 수 없다.

## 2. HEX·퍼센트 입력: 장식과 편집 정책을 조합한다

현재 [HEX field](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/ColorPicker/ColorPickerLayout.cs:80)는 swatch를 ZStack에 얹고 `textViewport` 왼쪽에 38을 예약한다. [퍼센트 field](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/ColorPicker/ColorPickerLayout.cs:110)는 `%`를 얹고 오른쪽 23을 예약하며 TMP alignment·caret margin을 설정한다. 두 field 모두 native end-edit listener와 [focus 요청 helper](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/ColorPicker/ColorPickerLayout.cs:168)가 필요하다.

```swift
HStack(spacing: 10) {
    Circle()
        .fill(model.opaqueSwatch)
        .frame(width: 16, height: 16)
        .allowsHitTesting(false)

    TextField("HEX", text: $model.hexDraft)
        .textFieldStyle(.plain)
        .focused($focusedField, equals: .hex)
        .onSubmit { model.commit() }
}
.padding(.horizontal, 12)
.frame(height: 40)
.background(fieldSurface, in: RoundedRectangle(cornerRadius: 8))

HStack(spacing: 2) {
    TextField("", text: $model.opacityDraft)
        .textFieldStyle(.plain)
        .multilineTextAlignment(.trailing)
        .focused($focusedField, equals: .opacity)
        .onSubmit { model.commit() }
    Text("%").foregroundStyle(.secondary)
}
.padding(.horizontal, 10)
.frame(width: 72, height: 40)
.background(fieldSurface, in: RoundedRectangle(cornerRadius: 8))
```

`focusedField`는 앱의 FocusState이며, 이전 field가 focus를 잃을 때도 commit하도록 별도 `onChange`가 필요하다. 잘못된 값을 입력한 뒤 어느 field로 focus를 돌릴지는 앱의 validation 판단이다. 문자 수·허용 문자 규칙도 이 스케치에서는 앱 binding/validation에 남아 있다.

필요한 공통 기능은 **plain control, 입력 alignment, focus binding, submit와 blur/cancel의 구분, 장식의 hit testing 제어**다. 숫자 `value:format:` binding도 유용하지만 이 화면은 잘못된 임시 문자열을 보존하고 오류를 보여준다. 무조건 숫자 binding으로 바꾸면 편집 동작이 달라진다. `String draft → parse → commit/error` 정책을 지원해야 한다.

### 오류 줄과 화면 수명

[고정 높이 error line](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/ColorPicker/ColorPickerLayout.cs:71)은 현재도 짧다. 애니메이션을 원할 때 다음과 같은 기능이 추가로 필요할 뿐, 기존 코드가 길어진 직접 원인이라고 분류할 수는 없다.

```swift
Text(model.error)
    .font(.caption2)
    .foregroundStyle(.red)
    .frame(maxWidth: .infinity, minHeight: 14, alignment: .leading)
    .opacity(model.error.isEmpty ? 0 : 1)
    .animation(reduceMotion ? nil : .easeOut(duration: 0.12),
               value: model.error.isEmpty)
```

현재 의도는 오류가 없어도 자리를 유지하는 것이다. opacity는 그 의미를 유지하지만, conditional view removal과 transition은 공간까지 바꿀 수 있다. 오류의 접근성 알림은 시각적 opacity와 별도의 계약이다.

반면 수명에서는 실제 workaround가 있다. [ColorPickerView](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Declarative/Controls/ColorPickerView.cs:52)는 화면이 숨겨지거나 사라지는 것을 알기 위해 ZStack에 보이지 않는 `ClosesWithScreen`을 넣는다. [그 구현](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Declarative/Controls/ColorPickerView.cs:63)은 NativeView에 DeactivationSignal을 붙이고 Teardown에서 정리한다. 자연스러운 목표는 표시 뷰에 `.onDisappear { model.close() }`를 붙이는 것이다. **일시적 disappearance와 영구 disposal은 다르므로**, model 정리는 owner 수명과 별도로 정의해야 한다.

## 3. 에디터 전체 fade: Opacity·Animation·WithAnimation의 실제 사용처

현재 [OverlayEditorFrame](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayEditorFrame.cs:38)은 CanvasGroup과 시작 시간을 저장한다. [초기 alpha](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayEditorFrame.cs:60)를 설정하고 [Tick](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayEditorFrame.cs:137)에서 unscaled time으로 alpha를 올린다.

```swift
EditorContent(model: model)
    .opacity(appeared ? 1 : 0)
    .animation(reduceMotion ? nil : .linear(duration: 0.16),
               value: appeared)
    .onAppear { appeared = true }
```

동일한 상태 변경을 transaction으로 표현할 수도 있다.

```swift
.onAppear {
    withAnimation(reduceMotion ? nil : .linear(duration: 0.16)) {
        appeared = true
    }
}
```

필요한 공통 기능은 **subtree opacity, 값에 연결한 animation, WithAnimation transaction, appearance callback, reduced-motion environment**다. 개별 Text/Image 색의 alpha만 바꾸는 기능으로는 subtree fade를 편하게 표현하기 어렵다.

opacity와 input 차단은 별개의 의미다. opacity 0이 자동으로 hit testing을 끈다고 가정하면 안 된다. 또한 현재는 진입 fade만 존재한다. 퇴장 transition을 넣는다면 기능 변경이며, animation 완료까지 실제 객체 제거를 지연하는 수명 계약이 필요하다. Canvas root·게임 input interception·editor 좌표 scaling·drag ghost host는 이 modifier 몇 개로 없어지지 않는다.

## 4. Grow 9점 선택기: shape·stroke·rotation·hit target

[현재 Box](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayGrowPointsView.cs:27)는 사각형의 네 변을 VStack/HStack/Spacer로 만든다. [개별 점](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayGrowPointsView.cs:59)은 작은 점·큰 점·화살표를 모두 만들고 Hidden으로 전환한다. [화살표 bridge](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayGrowArrowView.cs:18)는 native graphic과 direction Watch를 연결한다.

```swift
Grid(horizontalSpacing: 2, verticalSpacing: 2) {
    ForEach(0..<3) { row in
        GridRow {
            ForEach(0..<3) { column in
                GrowPoint(point: model.points[row * 3 + column])
            }
        }
    }
}
.background {
    Rectangle()
        .stroke(.white.opacity(0.16), lineWidth: 1)
        .frame(width: 50, height: 50)
        .allowsHitTesting(false)
}
.frame(width: 78, height: 78)
.background(barColor, in: Circle())
```

개별 point는 다음처럼 표현할 수 있다.

```swift
Button { point.pick() } label: {
    Group {
        if point.look == .arrow {
            ArrowShape()
                .stroke(.primary, lineWidth: 1.8)
                .rotationEffect(point.angle)
        } else {
            Circle()
                .fill(point.dotColor)
                .frame(width: point.isChosen ? 12 : 5,
                       height: point.isChosen ? 12 : 5)
        }
    }
    .frame(width: 24, height: 24)
    .contentShape(Rectangle())
}
.buttonStyle(.plain)
```

필요한 공통 기능은 **shape primitives, stroke, rotation, contentShape**다. `ArrowShape`는 앱에서 정의하는 오른쪽 방향의 화살표 path다. [현재 화살표의 선·날개 기하](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayGrowArrow.cs:23)를 유지하려면 이 작은 도형 정의는 여전히 필요하다. SF Symbol로 바꾸는 것은 외형 변경이다.

현재 상태 전환은 즉시 변경이다. `.animation(..., value: point.look)`이나 `.transition(.opacity)`는 가능한 추가 표현이어야 하지만 현재 동작 재현에 필수는 아니다. Hidden으로 모든 표현을 유지하는 현재 수명과 conditional insertion/removal도 구분해야 한다. 점이 5px이어도 버튼의 24×24 hit target을 유지하는 것이 중요하다.

## 5. 값 색상 band: 공통 animation과 도메인 편집 알고리즘의 경계

[현재 band](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayValueColorBandView.cs:57)는 표시 중인 range와 target range를 따로 보관한다. [Redraw/Glide](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayValueColorBandView.cs:121)가 exponential interpolation과 도착 판정을 수행하며, 별도의 [MonoBehaviour adapter](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayValueColorBandMotion.cs:10)가 Update를 전달한다.

표현 계층의 목표를 요약하면 다음과 같다.

```swift
BandContent(
    range: model.displayRange,
    stops: model.stops,
    selectedID: model.selectedID
)
.animation(reduceMotion || model.isDragging ? nil : .smooth,
           value: model.displayRange)
.gesture(stopDrag)
```

앱의 변경 시점은 다음처럼 transaction을 명시할 수 있다.

```swift
func finishDrag() {
    model.endDrag()
    withAnimation(reduceMotion ? nil : .smooth) {
        model.fitRangeToStops()
    }
}
```

이 스케치만으로 현행 동작이 완전히 재현되는 것은 아니다.

- `.smooth`는 현재 `1 - exp(-dt * 14)`와 다른 motion curve다.
- `BandContent`의 range가 animatable data여야 band·knob·label·marker가 함께 보간된다.
- [drag 시작](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayValueColorBandView.cs:280)은 target이 아닌 **현재 표시 중인 range**에서 animation을 동결한다. presentation 값을 읽고 drag로 넘기는 계약이 필요하다. `.animation(value:)` 한 줄로 이를 해결했다고 할 수 없다.
- [label 충돌 회피](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayValueColorBandView.cs:208), [range 좌표 변환](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayValueColorBandView.cs:68), [snap·Alt 우회·범위 확장](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayValueColorBandView.cs:290), color ramp graphics는 앱 도메인 로직이다.

공통으로 줄일 수 있는 것은 **animation scheduling, Update adapter, 종료·취소·retargeting, reduced motion, 수명 관리**다. WithAnimation을 도입할 때는 여러 State 변경이 같은 transaction을 공유해야 한다. 각 setter가 따로 animation을 시작하면 하나의 조작에서 시각적 상태가 어긋날 수 있다.

## 6. 검색 결과와 chip: scroll intent·overlay·identity·drag

[Results](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayValueBrowserView.cs:247)는 결과 row를 만들고 query가 바뀌면 Configure로 ScrollRect를 맨 위로 돌린다. [Scroller](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayValueBrowserView.cs:347)도 sensitivity와 inertia를 native component에서 설정한다.

```swift
ScrollViewReader { proxy in
    ScrollView {
        VStack(alignment: .leading, spacing: groupGap) {
            Color.clear.frame(height: 0).id("results-top")
            ForEach(model.visibleFolders) { folder in
                ValueGroupRow(folder: folder)
            }
            if model.nothingFound {
                EmptyResults(query: model.query)
            }
        }
    }
    .onChange(of: model.query) { _, _ in
        proxy.scrollTo("results-top", anchor: .top)
    }
}
```

이는 **scroll position/scroll-to와 change callback**의 수요다. 실제 엔진에서는 결과가 layout된 뒤 목표로 이동하는 순서를 보장해야 한다.

[chip](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayValueBrowserView.cs:318)은 지금까지 가장 넓었던 수치의 폭을 유지하기 위해 숨긴 sizing text와 live text를 겹친다. 그 의도는 SwiftUI에서도 남는다.

```swift
Button { chip.pick() } label: {
    HStack(spacing: 7) {
        Text(chip.label)
        if chip.hasLiveValue {
            Text(chip.room)
                .hidden()
                .overlay(alignment: .leading) { Text(chip.live) }
                .frame(maxWidth: liveWidth, alignment: .leading)
        }
    }
    .padding(.horizontal, 12)
    .frame(height: chipHeight)
    .background(chipFill, in: Capsule())
    .contentShape(Capsule())
}
.buttonStyle(.plain)
```

필요한 공통 기능은 **overlay content, contentShape, gesture composition**이다. `monospacedDigit()`만으로 widest-so-far 폭 보존을 대체할 수는 없다. wrapping은 SwiftUI HStack도 자동으로 처리하지 않는다. `ValueGroupRow`에는 custom Flow/Layout이 필요하며, ElementUI는 Flow를 이미 제공한다. heading 폭 통일·최대 폭 clamp도 레이아웃 규칙이다.

현재 [drag handler](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayValueChipHandle.cs:12)는 threshold, begin/move/end, 숨겨질 때 cancel을 처리한다. SwiftUI `.draggable`은 OS drag/drop payload 방식이므로 이 게임 내부 dock drag와 동일하지 않다. 일반 Gesture API로 옮기더라도 tap/drag 경쟁, 좌표계, 취소 정책은 유지해야 한다.

ForEach의 keyed identity는 ElementUI에도 있다. Lazy collection은 필요하면 별도로 선택할 기능이며, 이 코드만 보고 렉이나 성능 향상 폭을 단정할 수 없다.

## 7. 기준 값 popover와 tooltip: 존재하는 API와 host 통합 요구를 분리한다

[OverlayValueSourceMenu](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayValueSourceMenu.cs:22)는 panel을 만든 뒤 [Show](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayValueSourceMenu.cs:42)에서 model·view mount, 크기 측정, 화면 clamp, 좌표·z-order를 처리한다.

```swift
Button { sourceMenuOpen = true } label: {
    SourceRow(name: model.sourceName)
}
.popover(isPresented: $sourceMenuOpen,
         attachmentAnchor: .rect(.bounds),
         arrowEdge: .top) {
    VStack(spacing: 2) {
        if model.sources.isEmpty {
            Text("이 글에는 아직 값이 없어요")
                .foregroundStyle(.secondary)
        } else {
            ForEach(model.sources) { source in
                SourceChoice(source: source) {
                    model.follow(source)
                    sourceMenuOpen = false
                }
            }
        }
        Divider()
        Button("다른 값 고르기") { model.pickAnotherValue() }
    }
    .padding(6)
    .frame(width: 232)
}
```

ElementUI `.Popover`는 **이미 존재**한다. 이 사례를 “popover 기능 없음”으로 분류하면 안 된다. 추가 수요는 **native anchor, 게임 에디터 overlay host, frame-coordinate 배치, editor Escape/밖 클릭 라우팅과의 통합**이다. SwiftUI 기본 popover placement와 현행 top-right 좌표·toolbar 아래 clamp는 완전히 같지 않다.

[ShowTip](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayEditorFrame.cs:189)은 tooltip graphic·text를 만들고 측정·clamp·z-order·active toggle을 직접 처리한다. 단순 플랫폼 tooltip은 SwiftUI `.help(text)`로 표현할 수 있지만, 현재 요구는 임의의 frame anchor 위에 표시하는 것이다. 이 경우 root overlay와 anchor 기반 custom layout이 필요하다. `.help()` 한 줄로 현재 시각적 요구를 모두 해결했다고 할 수 없다.

opacity·transition·allowsHitTesting(false)는 이러한 overlay에서 appearance와 input을 분리하는 공통 기능이다. 앱 고유 overlay host까지 없어지는 것은 아니다.

## 8. Inspector property row: content slot으로 조합 의도를 드러낸다

[Inspector Content](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayInspectorView.cs:56)는 leading view를 고르고 trailing List에 조건별로 Add한 뒤 ToArray한다. [Of/Filled](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayInspectorRow.cs:39)는 배열 인덱스·CopyTo로 앞부분을 붙인다.

```swift
InspectorRow {
    switch model.leading {
    case .swatch(let color): Swatch(color: color)
    case .icon(let name): Image(systemName: name)
    case .glyph(let text): Text(text)
    case .none: EmptyView()
    }
} title: {
    Text(model.label).foregroundStyle(model.ink)
} trailing: {
    if model.showsValue {
        Text(model.valueText).foregroundStyle(model.valueInk)
    }
    if let unit = model.unit {
        Text(unit).foregroundStyle(.secondary)
    }
    if model.hasSwitch {
        SwitchIndicator(value: model.switchState)
    }
    if model.opensTool {
        Image(systemName: "chevron.right")
    }
}
```

`InspectorRow`는 앱의 `@ViewBuilder` slot을 가진 component라고 가정한다. 목표는 caller가 임시 배열을 관리하지 않고 **leading·title·trailing content만 선언하게 하는 것**이다.

이 사례의 모든 보일러플레이트가 framework 결핍은 아니다. ElementUI [MountChild](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Views/View.cs:120)는 null child를 건너뛴다. readonly flag에 따른 최초 구성은 `flag ? view : null`로 이미 줄일 수 있다. [프로젝트 LangVersion](/Users/kgh/dev/src/impl-resourcepack/Element/Element.csproj:6)도 latest이므로 collection expression/spread로 CopyTo를 간소화할 여지가 있다. 실제 변경 시 build 검증은 필요하다.

Swift result-builder 문법을 그대로 복제하기보다 **명시적인 component slot, nullable child 관례, collection 구성, Group/builder ergonomics**를 목표로 삼는 편이 적절하다. `() => VM.Click` 같은 Command selector를 직접 `VM.Click` 값으로 바꿀 때는 ItemSlot의 same-key model 교체를 따라가는 의미를 잃지 않아야 한다.

## Binding 범위 정정

현재 Bind는 field-only가 아니다. [UI.Bind의 설명](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/UI.cs:224)은 writable field/property 또는 State.Value를 명시한다. [MemberPath](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Reactive/MemberPath.cs:137)는 property의 public setter를 확인하고, [Setter](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Reactive/MemberPath.cs:144)는 해당 path에 쓴다.

다만 임의 custom property가 모두 추적 가능한 것은 아니다. [Lowering.Watchable](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Reactive/Lowering.cs:209)은 auto-property 또는 `[Pure]` property/getter를 허용하고, 다른 getter는 거부한다. 정확한 표현은 **추적 가능한 field/property path와 State.Value binding은 지원되며, 자유로운 getter/setter delegate binding과 projection API는 별도 부족 항목**이다.

## 공통 기능을 추가할 때 필요한 동작 계약

이 사례들이 요구하는 공통 계층은 subtree opacity, transforms, shape·stroke·overlay, value-linked animation, WithAnimation transaction, focus/edit events, hit testing, gesture, lifecycle, scroll intent다. API 이름만 추가해서는 선언형 사용성이 완성되지 않는다.

- `Hidden`: 자리와 객체를 유지하며 숨기는 의미.
- `Opacity`: 투명도이며 input 차단 여부는 별도로 지정.
- conditional view: 현재 ElementUI If는 branch를 보존하고 inactive effect를 멈춘다. SwiftUI conditional insertion/removal과 동일한 수명으로 간주할 수 없다.
- transition: 퇴장 중 표시 객체와 state를 언제 제거할지 정의.
- animation: interruption 시 target이 아닌 현재 presentation에서 이어갈 수 있어야 함.
- WithAnimation: 같은 변경에 포함된 여러 property와 view에 transaction이 전파되어야 함.
- focus/editing: 외부 binding update, draft text, caret·selection, IME, submit·blur·Escape의 순서를 정의.
- lifecycle: appearance, visibility, active branch, permanent disposal을 구분.

이 계약이 갖춰져야 코드를 짧게 쓰는 것과 동작을 예측하기 쉬운 것이 함께 성립한다. Custom graphics, 편집 도메인의 계산, native game host는 필요한 경계로 유지한다.
