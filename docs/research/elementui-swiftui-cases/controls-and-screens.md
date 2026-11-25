# 실제 설정 화면·공통 컨트롤에서 역으로 찾은 SwiftUI 표현

## 읽는 기준

이번 비교는 “SwiftUI에 무슨 API가 있나”부터 시작하지 않는다. **현재 작성된 컴포넌트의 목적을 그대로 두고, SwiftUI였다면 어떤 모양으로 작성했을지** 먼저 그린 뒤 ElementUI에 필요한 표현을 역으로 찾는다.

조사 시점에 대상 소스를 새로 읽었다. tracked 소스 변경은 없었고 조사 문서만 untracked 상태였다. 소스 수정·빌드·게임 실행은 하지 않았다.

아래 Swift 코드는 현대 SwiftUI를 기준으로 한 설계 스케치이며 독립적으로 컴파일한 앱은 아니다. `vm`은 기존 C# 모델의 동작을 Swift의 observable 모델로 옮겼다고 가정한다. 이름 검사·프로필 저장·삭제 재확인·게임 명령은 기존 앱 로직이다. `ElementColorPanel`, `EffectsGrid`, `retainedPages`처럼 앱 전용 내용을 가리키는 이름은 각각 명시한다. 이를 SwiftUI 기본 컴포넌트인 것처럼 세거나, 그 구현 비용까지 사라진다고 보지 않는다.

소스의 행 범위는 찾아갈 위치이며, 예제 사이의 행 수를 기능 등가성이나 절감률로 계산하지 않았다. 실제 화면의 정확한 색·픽셀·키 이벤트 우선순위까지 그대로 이식한 결과물도 아니다.

## 1. SwitchCard — Bool 하나를 바꾸는 카드에 식 트리와 작은 ViewModel이 따라온다

**현재 의도:** 제목·설명·스위치가 있는 카드다. 카드 어디를 눌러도 켜짐을 바꾸고, 비활성 상태에서는 조작과 표현이 함께 바뀐다. 제목이나 설명이 동적일 수도 있다.

근거: [SwitchCards.cs:20](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Declarative/Controls/SwitchCards.cs:20), 같은 파일 35–71행.

현재 코드에서 UI 목적보다 구현 장치가 앞에 드러나는 부분은 다음이다.

```csharp
Expression<Func<bool>> disabled =
  Expression.Lambda<Func<bool>>(Expression.Not(enabled.Body));
Expression<Func<UiColor>> ink = Expression.Lambda<Func<UiColor>>(
  Expression.Condition(enabled.Body,
    Expression.Constant(Colors.Foreground), Expression.Constant(Colors.Muted))
);
return Button(copy.Foreground(ink), FlipOf(on), ButtonStyle.Card).Disabled(disabled);
```

`FlipOf`는 Bool을 반전시키기 위해 별도의 `Flip : ViewModel`을 만들고 Command를 반환한다. string·expression·ItemSlot별 overload도 있다.

**SwiftUI였다면 사용처는 이렇게 작성한다.**

```swift
Toggle(isOn: $vm.enabled) {
    VStack(alignment: .leading, spacing: 4) {
        Text(vm.title).font(.system(size: 17, weight: .semibold))
        Text(vm.detail).font(.caption).foregroundStyle(.secondary)
    }
}
.toggleStyle(CardToggleStyle())
.disabled(!vm.canEdit)
```

`CardToggleStyle`은 SwiftUI 기본 스타일이 아니다. 카드 전체를 누르는 현재 동작을 유지하려면 다음과 같은 공통 스타일을 한 번 작성한다.

```swift
struct CardToggleStyle: ToggleStyle {
    @Environment(\.isEnabled) private var isEnabled

    func makeBody(configuration: Configuration) -> some View {
        Button {
            configuration.isOn.toggle()
        } label: {
            HStack(spacing: 16) {
                configuration.label
                    .frame(maxWidth: .infinity, alignment: .leading)
                Toggle("", isOn: configuration.$isOn)
                    .labelsHidden()
                    .toggleStyle(.switch)
                    .allowsHitTesting(false)
                    .accessibilityHidden(true)
            }
            .frame(minHeight: 48)
            .padding(20)
            .background(.quaternary, in: RoundedRectangle(cornerRadius: 12))
        }
        .buttonStyle(.plain)
        .opacity(isEnabled ? 1 : 0.45)
        .accessibilityValue(configuration.isOn ? "켬" : "끔")
    }
}
```

SwiftUI의 ToggleStyle은 label과 상태 binding을 configuration으로 제공한다. 이 때문에 label을 다시 분석하거나 별도 command 모델을 생성하지 않고 스타일 안에서 동작을 구성할 수 있다. [Apple ToggleStyle](https://developer.apple.com/documentation/swiftui/togglestyle)

**사라지는 것:** expression을 직접 조립하는 코드, `Flip` 클래스, 고정 문자열과 동적 문자열 때문에 갈라지는 사용처 문법, 비활성 상태를 UI 조각마다 전달하는 코드다. 스타일의 20px padding이나 카드 둥글기는 디자인이므로 남는다. 위 예제의 opacity 값도 시각 정책이며 원본과 픽셀 단위로 동일하다는 뜻은 아니다.

**ElementUI에서 필요한 것:** 일반 View/스타일에서 `isEnabled` 읽기, subtree에 적용하는 control style, label을 View로 받는 컨트롤, Bool binding에 연결한 간단한 action, reactive opacity와 hit-testing 제어다. 기존 ControlStyle을 버릴 필요는 없다. 지금도 공통 카드 helper를 더 다듬을 수 있지만, 식 트리·native 상태 bridge까지 화면 측에서 계속 작성하는 것은 프레임워크의 표현 부족이다.

참고로 51–53행의 “Spacer가 양축으로 늘어난다”는 주석은 현재 pin에 대해 오래된 설명이다. 이 카드의 역설계에서 그 과거 문제를 새로운 기능 부족으로 세지 않았다.

## 2. ColorSwatch — popover를 여는 로컬 Bool과 닫힘 처리가 별도 모델·보이지 않는 View가 된다

**현재 의도:** 불투명한 색상 점과 caption을 누르면 색상 편집 패널이 열린다. 편집은 즉시 binding에 반영되고, 바깥 클릭·Escape·닫기·부모 화면 숨김으로 닫힌다. opacity 사용 여부와 기본색 복원도 전달한다.

근거: [ColorSwatch.cs:27](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Declarative/Controls/ColorSwatch.cs:27), [ColorPickerView.cs:24](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Declarative/Controls/ColorPickerView.cs:24), 같은 파일 52–88행.

```csharp
SwatchModel swatch = new();
Binding<bool> open = Bind(() => swatch.Open);
// ... Button(..., () => swatch.Show).Popover(open, ...)

return ZStack(
  Alignment.TopLeading,
  new ColorPickerCardView(model),
  new ClosesWithScreen(() => model.Close.TryExecute(), model.Closed)
);
```

`ClosesWithScreen`은 크기가 0인 NativeView다. MonoBehaviour.OnDisable과 Teardown으로 닫힘·정리를 전달한다. 색상 편집 UI가 아니라 수명주기를 처리하려고 ZStack 자식이 생긴다.

**SwiftUI였다면 이 연결 코드는 다음 모양이다.**

```swift
struct ColorSwatch<Caption: View>: View {
    let title: String
    @Binding var color: Color
    let opaqueColor: Color
    let supportsOpacity: Bool
    let defaultColor: Color?
    @ViewBuilder var caption: Caption
    @State private var isPresented = false

    var body: some View {
        Button { isPresented = true } label: {
            HStack(spacing: 8) {
                Circle().fill(opaqueColor).frame(width: 16, height: 16)
                caption.frame(maxWidth: .infinity)
            }
        }
        .popover(isPresented: $isPresented, arrowEdge: .bottom) {
            ElementColorPanel(
                title: title,
                color: $color,
                supportsOpacity: supportsOpacity,
                defaultColor: defaultColor,
                close: { isPresented = false }
            )
        }
        .onDisappear { isPresented = false }
    }
}
```

`ElementColorPanel`은 현재 `ColorPickerCardView`에 해당하는 **앱의 색상 편집 화면**이다. HEX·그라디언트·기본색 버튼의 구현을 없앴다는 예제가 아니다. `opaqueColor`와 HEX caption도 기존 색상 변환 로직에서 공급한다. 단순한 시스템 색 선택기로 충분한 별도 화면이라면 `ColorPicker(selection:supportsOpacity:)`를 쓸 수 있지만 현재 사용자 정의 패널과 동등하다고 보지 않는다. [Apple ColorPicker](https://developer.apple.com/documentation/swiftui/colorpicker)

**사라지는 것:** 로컬 표시 상태만을 위한 `SwatchModel`/Command, 크기 0의 lifecycle View, native OnDisable 연결이다. 색상 변경에 따른 앱의 추가 상태 변화는 모델의 setter나 명시적 `onChange`에서 처리하며, 무조건 사라지는 작업은 아니다.

**ElementUI에서 필요한 것:** View가 소유하는 가벼운 로컬 State/Action 표현, 일반적인 활성·비활성 lifecycle hook, callback에서 일어난 binding 및 파생 변경을 일관되게 알리는 경로다. Popover와 State 자체는 이미 있다. 새 Popover를 만드는 문제가 아니라 이 기존 기능들을 간결하고 안전하게 연결하는 문제다. SwiftUI의 onDisappear와 Unity의 SetActive(false)를 자동으로 같은 사건으로 취급할 수는 없으므로 ElementUI 쪽에는 숨김·유지·파괴의 의미를 명시해야 한다. [Apple lifecycle modifiers](https://developer.apple.com/documentation/swiftui/view-input-and-events)

## 3. NewProfilePanel — 간단한 이름 입력에서 TMP 설정과 focus 요청 번호를 관리한다

**현재 의도:** 새 프로필 이름을 입력한다. 만들기 버튼은 유효할 때 활성화된다. Enter는 빈 값이어도 제출 검사를 실행해 이유를 보여 준다. 실패하면 입력에 focus를 두고, 성공하면 popover를 닫는다.

근거: [ProfilesView.cs:79](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Settings/Pages/ProfilesView.cs:79), 같은 파일 116–122행, [NewProfileModel.cs:24](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Settings/Pages/NewProfileModel.cs:24), 같은 파일 53–72행.

```csharp
TextField(null, Bind(() => VM.Creating.Name), L("프로필 이름", "Profile name"))
  .Configure<TMP_InputField>(field =>
  {
    field.characterLimit = ElementProfile.MaximumNameLength;
    field.onSubmit.AddListener(_ => VM.Creating.Submit());
  })
  .Configure<TMP_InputField, int>(() => VM.Creating.FocusName, Focus)
  .Frame(maxWidth: float.PositiveInfinity)
```

**SwiftUI였다면 폼의 동작이 입력 옆에 모인다.**

```swift
struct NewProfilePanel: View {
    @Bindable var vm: NewProfileModel
    @FocusState private var nameIsFocused: Bool

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text("처음 설정으로 시작해요.")
                .font(.caption)
                .foregroundStyle(.secondary)
            HStack(spacing: 8) {
                TextField("프로필 이름", text: $vm.name)
                    .focused($nameIsFocused)
                    .onSubmit(submit)
                Button("만들기", action: submit)
                    .buttonStyle(.borderedProminent)
                    .disabled(!vm.canCreate)
            }
            if !vm.problem.isEmpty {
                Text(vm.problem).font(.caption).foregroundStyle(.red)
            }
        }
        .controlSize(.large)
        .padding(8)
        .frame(width: 340, alignment: .leading)
        .onAppear { nameIsFocused = true }
    }

    private func submit() {
        vm.submit()
        nameIsFocused = vm.isOpen
    }
}
```

Enter의 제출 검사는 `.onSubmit`에 남고, 버튼의 실행 가능 조건은 `.disabled`에 남는다. 버튼과 Enter를 무조건 같은 CanExecute gate로 막아 버리는 설계는 원래의 “빈 이름으로 Enter를 누르면 이유를 설명한다”는 동작을 잃는다. focus는 UI 상태를 읽고 쓰는 binding으로 표현한다. [Apple FocusState](https://developer.apple.com/documentation/swiftui/focusstate/)

**사라지는 것:** `TMP_InputField` 타입 참조, `Select()/ActivateInputField()`, focus를 요청하려고 올리는 숫자, 버튼의 기본 높이를 매번 지정하는 코드다. 정확히 100px 너비의 만들기 버튼이 디자인 요구라면 `.frame(width: 100)`은 남겨야 한다. `.controlSize(.large)`가 그 픽셀값을 보장하는 것은 아니다.

**남는 앱 로직:** 이름 중복·허용 문자·저장 실패·실패 메시지 유지 정책이다. SwiftUI에 기본 `.maxLength(...)`가 있다고 가정하지 않았다. 원본의 글자 수 제한까지 엄밀히 유지하려면 앱의 text edit policy 또는 formatter/bridge가 추가로 필요하며, 단순히 `prefix()`로 잘라 IME 조합 중 입력까지 바꾸는 코드를 정답으로 제시하지 않는다.

**ElementUI에서 필요한 것:** submit/cancel/edit-end 이벤트, focus binding, input option, control size, 간단한 Action overload다. validation UI의 VStack/HStack/If 자체는 현재도 가능하지만 일반 입력 동작 때문에 native API까지 알아야 하는 부분은 로컬 helper만으로 해결할 대상이 아니다.

## 4. ProfileCard — 제목 옆의 actions와 이름 변경을 수동 여백·겹침으로 배치한다

**현재 의도:** 카드 클릭으로 프로필을 전환한다. 이름 변경·복제·내보내기·두 번 눌러 삭제하는 버튼이 있고, 이름 변경 중에는 actions를 숨긴다. Enter나 blur로 이름을 저장하고 Escape로 취소한다. 현재 프로필에는 왼쪽 표시선이 있다.

근거: [ProfilesView.cs:126](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Settings/Pages/ProfilesView.cs:126), 같은 파일 193–212행, [ProfileCardModel.cs:42](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Settings/Pages/ProfileCardModel.cs:42).

현재는 제목에 `4 * ActionSize + 8`의 trailing padding을 주고, actions를 ZStack의 우상단에 별도로 올린다. 실제 export/delete 표시 여부와 상관없이 최대 버튼 수만큼 제목 공간을 비운다. 이름 편집 TextField도 큰 카드 Button의 내용 안에 있다.

**SwiftUI로 다시 구성한다면 제목과 actions는 같은 행에서 공간을 나눈다.**

```swift
VStack(alignment: .leading, spacing: 6) {
    HStack(alignment: .top, spacing: 8) {
        if vm.renaming {
            renameField
        } else {
            Text(vm.name)
                .font(.system(size: 20, weight: .semibold))
                .lineLimit(2)
                .frame(maxWidth: .infinity, alignment: .leading)
                .layoutPriority(1)
            HStack(spacing: 2) {
                Button("이름 변경", systemImage: "pencil", action: vm.startRename)
                Button("복제", systemImage: "plus.square.on.square", action: vm.duplicate)
                if vm.filesSupported {
                    Button("내보내기", systemImage: "square.and.arrow.up", action: vm.export)
                }
                if vm.canDelete {
                    Button("삭제", systemImage: "trash", action: vm.delete)
                        .foregroundStyle(vm.deleteArmed ? Color.red : Color.secondary)
                }
            }
            .labelStyle(.iconOnly)
            .buttonStyle(.borderless)
            .controlSize(.small)
        }
    }
    Spacer()
    Text(vm.line).foregroundStyle(vm.lineIsProblem ? Color.red : Color.secondary)
    Text(vm.state).foregroundStyle(vm.stateColor)
}
.font(.caption)
.padding(16)
.frame(maxWidth: .infinity, minHeight: 140, maxHeight: 140, alignment: .leading)
.background(.quaternary, in: RoundedRectangle(cornerRadius: 12))
.overlay(alignment: .leading) {
    Capsule().fill(.tint).frame(width: 3)
        .padding(.vertical, 14)
        .opacity(vm.inUse ? 1 : 0)
        .allowsHitTesting(false)
}
.contentShape(RoundedRectangle(cornerRadius: 12))
.onTapGesture { if !vm.renaming { vm.switchProfile() } }
```

이 배치는 actions와 제목을 실제 형제 관계로 표현한다. 카드의 모든 빈 공간을 클릭할 수 있게 하는 정책과 자식 Button/TextField의 이벤트 우선순위는 이식 시 검증해야 한다. 사용 가능한 폭에서 원래와 다른 줄바꿈이 생길 수 있으므로 기존 디자인을 그대로 보존하는 기계적 번역은 아니다.

이름 변경의 의미도 View에서 드러낼 수 있다.

```swift
@FocusState private var nameIsFocused: Bool
@State private var selection: TextSelection?

private var renameField: some View {
    TextField("프로필 이름", text: $vm.renameText, selection: $selection)
        .focused($nameIsFocused)
        .onAppear {
            nameIsFocused = true
            selection = TextSelection(insertionPoint: vm.renameText.endIndex)
        }
        .onSubmit { finishRename(cancelled: false) }
        .onExitCommand { finishRename(cancelled: true) }
        .onChange(of: nameIsFocused) { _, focused in
            if !focused { finishRename(cancelled: false) }
        }
}

private func finishRename(cancelled: Bool) {
    guard vm.renaming else { return }
    vm.endRename(cancelled: cancelled)
}
```

`onExitCommand`의 Escape는 macOS 기준이다. selection binding은 현재 이름 뒤에 caret을 두려는 의도를 표현한다. 글자 수 제한은 앞의 폼과 마찬가지로 별도 입력 정책이 남는다. [Apple TextSelection](https://developer.apple.com/documentation/swiftui/textselection), [Apple onExitCommand](https://developer.apple.com/documentation/swiftui/view/onexitcommand(perform:))

**사라지는 것:** 최대 action 개수에 대한 수동 padding, 단순 장식선을 위한 Spacer, TMP focus/selection/end-edit 설정, UI가 직접 `wasCanceled`를 읽는 코드다. 삭제 재확인 상태와 문구, 실제 삭제 명령은 그대로 남는다.

**ElementUI에서 필요한 것:** overlay와 shape, opacity, content shape/hit testing, label/iconOnly, control size, layout priority, focus/selection/edit event 계약이다. 제목·actions를 HStack으로 옮기는 일은 현재 ElementUI로도 가능한 로컬 개선이다. 하지만 그 위에서 자연스럽게 필요한 장식·선택·이벤트 표현까지 구성하려면 기본 기능이 더 필요하다.

## 5. PresetEditor — 탭마다 Command를 만들고 여섯 조건을 ZStack에 나열한다

**현재 의도:** 넓으면 이름·저장·삭제가 한 줄, 좁으면 두 줄이다. 탭은 6열 또는 3열×2줄이다. 탭 전환 시 단축키 녹음을 취소한다. 효과와 줌 값은 저장 전까지 draft에 남는다.

근거: [PresetEditorView.cs:27](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Settings/Pages/PresetEditorView.cs:27), 같은 파일 53–109행, [PresetEditorModel.cs:103](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Settings/Pages/PresetEditorModel.cs:103).

```csharp
ShowTab = new Command[TabNames.Length];
for (int i = 0; i < ShowTab.Length; i++)
{
  int tab = i;
  ShowTab[i] = Command(() =>
  {
    Tab = tab;
    Shortcut.Cancel();
  });
}
```

**SwiftUI였다면 선택 상태와 그 변경의 부수 효과를 분리해서 적는다.**

```swift
VStack(alignment: .leading, spacing: 12) {
    ViewThatFits(in: .horizontal) {
        HStack(spacing: 8) { nameField; saveButton; deleteButton }
            .frame(minWidth: 600)
        VStack(spacing: 12) {
            nameField
            HStack { deleteButton; Spacer(); saveButton }
        }
    }
    Text(vm.nameProblem.isEmpty ? "이 프리셋에서 제거할 이펙트를 선택하세요." : vm.nameProblem)
        .font(.caption)
        .foregroundStyle(vm.nameProblem.isEmpty ? Color.secondary : Color.red)
    ViewThatFits(in: .horizontal) {
        tabGrid(columns: 6).frame(minWidth: 600)
        tabGrid(columns: 3)
    }
    switch vm.tab {
    case .screen:
        VStack(spacing: 12) { cameraCard; zoomRow; EffectsGrid(vm.screenEffects) }
    case .shortcut:
        ShortcutEditor(vm.shortcut)
    default:
        EffectsGrid(vm.effects(in: vm.tab))
    }
}
.onChange(of: vm.tab) { vm.shortcut.cancel() }
```

6열/3열 전환까지 유지하려면 탭 strip 자체는 여전히 앱 구성이다. `.pickerStyle(.segmented)` 한 줄로 바꾸면 동일한 두 줄 동작이 되지 않는다.

```swift
private func tabGrid(columns: Int) -> some View {
    LazyVGrid(columns: Array(repeating: GridItem(.flexible(), spacing: 4), count: columns), spacing: 4) {
        ForEach(vm.tabs) { tab in
            Button {
                if vm.tab == tab.id { vm.shortcut.cancel() }
                else { vm.tab = tab.id }
            } label: {
                Text(tab.title)
                    .frame(maxWidth: .infinity, minHeight: 34)
                    .foregroundStyle(vm.tab == tab.id ? Color.white : Color.primary)
                    .background(vm.tab == tab.id ? Color.accentColor : Color.clear, in: Capsule())
            }
            .buttonStyle(.plain)
        }
    }
    .padding(4)
    .background(.quaternary, in: RoundedRectangle(cornerRadius: 22))
}
```

`EffectsGrid`, `ShortcutEditor`, `nameField`, `saveButton`, `deleteButton`은 기존 effect card·단축키 편집·폼 조각에 대응하는 앱 View다. 이름 제한·focus·저장 조건은 앞 사례와 같은 입력/명령 계약을 사용한다. effect domain 모델, draft, 단축키 녹음 자체가 없어진다는 뜻은 아니다. 원본은 같은 탭을 다시 눌러도 녹음을 취소하므로 그 경우는 Button에서 처리하고, 실제 선택 변경은 onChange에서 처리했다.

**사라지는 것:** 탭 수만큼 만드는 Command 배열, View 배열의 수동 채우기, 선택 내용을 표현하기 위한 ZStack+여섯 If다. **남는 것:** 600px 분기, 6/3열 정책, 선택된 탭 색상, draft 데이터다. ViewThatFits와 Grid는 이미 ElementUI에 있으므로 새로 필요한 기능으로 세지 않는다. [Apple ViewThatFits](https://developer.apple.com/documentation/swiftui/viewthatfits), [Apple onChange](https://developer.apple.com/documentation/swiftui/view/onchange(of:initial:_:))

**ElementUI에서 필요한 것:** selection binding 기반 컨테이너, 로컬 action, 공개 OnChange, 구조를 유지하는 reactive branch 구성이다. 현재 Tabs는 외부 selection binding이 없고, 기존 If는 한 번 만든 branch를 유지하는 별도 수명 정책이다. Swift의 switch로 교체할 때도 draft 상태는 모델에 유지하고, 보존해야 하는 scroll/local state를 명시해야 한다.

## 6. SettingsWindow — `compact ? 20 : 32` 여백을 조건부 Spacer 두 개로 만든다

**현재 의도:** 폭이 좁으면 좌우 20, 넓으면 32의 여백이다. header·각 페이지 내용·저장 오류 영역이 이 규칙을 공유한다. 각 페이지는 처음 열 때 만들어지고 보존되며 각자의 스크롤 위치를 가진다.

근거: [SettingsWindowView.cs:20](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Settings/Window/SettingsWindowView.cs:20), 같은 파일 64–83행.

```csharp
private View Sides(View content) =>
  HStack(spacing: 0, VAlignment.Top,
      Gutter(),
      content.Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.TopLeading),
      Gutter())
    .Padding(new Insets(0, 20, 0, 20));

private View Gutter() =>
  If(() => !VM.Compact, () => Spacer().Frame(width: 12, height: 0));
```

**SwiftUI라면 이 목적은 modifier의 값으로 직접 쓴다.**

```swift
private var sideInset: CGFloat { vm.compact ? 20 : 32 }

var body: some View {
    VStack(spacing: 0) {
        HStack(spacing: 12) {
            if vm.canGoBack {
                Button("뒤로", systemImage: "chevron.left", action: vm.back)
            }
            Text(vm.current.title).font(.title).lineLimit(1)
            Spacer()
            Button("닫기", systemImage: "xmark", action: vm.close)
        }
        .labelStyle(.iconOnly)
        .buttonStyle(.plain)
        .frame(height: 48)
        .padding(.horizontal, sideInset)
        .padding(.vertical, 24)

        retainedPages

        if vm.saveFailed {
            HStack(spacing: 16) {
                Text(vm.saveError).font(.caption).foregroundStyle(.red)
                Spacer()
                Button("다시 저장", action: vm.retrySave)
            }
            .padding(.horizontal, sideInset)
            .padding(.top, 12)
            .padding(.bottom, 16)
        }
    }
}

private func page<Content: View>(@ViewBuilder content: () -> Content) -> some View {
    ScrollView {
        content()
            .padding(.horizontal, sideInset)
            .padding(.bottom, 24)
    }
}
```

이 예제의 `retainedPages`는 기존 페이지 보존 정책을 담당하는 **앱 컨테이너 자리**다. SwiftUI의 단순 `if`/`switch`/NavigationStack이 기존 If의 모든 수명·정지 정책과 자동으로 같아진다는 주장은 아니다. 이 사례에서 비교하는 것은 페이지 시스템 전체의 삭제가 아니라 **동적 치수를 표현하기 위해 layout 구조를 늘리는 문제**다. 스크롤바까지 바깥 여백 안으로 넣지 않도록 원본처럼 ScrollView의 내용에 padding을 적용했다.

**사라지는 것:** Sides용 추가 HStack, 두 조건부 Gutter, 0 높이 Spacer, 모든 사용처의 수동 content Frame이다. 화면 폭에 따라 padding 값만 바뀌는데 트리 구조까지 바뀌어야 할 이유가 없어진다.

**ElementUI에서 필요한 것:** reactive Padding/Frame 등의 값 전달, style·label·control size를 하위 View에 적용하는 일관된 환경이다. 정적 `.Padding(vertical:, horizontal:)`는 이미 있지만 현재 Body는 한 번 구성되므로 단순히 `VM.Compact ? 20 : 32`를 값으로 넘기는 로컬 치환으로 동적 동작을 보장할 수 없다. 여기는 작은 편의 메서드 이상의 기능 부족이다.

## 7. InspectorRow — leading·label·value의 슬롯을 View 배열과 CopyTo로 조립한다

**현재 의도:** 모든 inspector 행은 40 높이, 22 너비의 leading 슬롯, 이름, 오른쪽 값 또는 입력 컨트롤을 가진다. slider가 남은 공간을 채우는 변형과 switch 끝 여백을 2 줄이는 변형이 있다.

근거: [OverlayInspectorRow.cs:39](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayInspectorRow.cs:39), 같은 파일 53–84행, 115–121행.

```csharp
View[] parts = new View[trailing.Length + 3];
parts[0] = LeadSlot(lead);
parts[1] = name.Padding(new Insets(0, 8, 0, 0));
parts[2] = control.Padding(new Insets(0, 12, 0, 0));
trailing.CopyTo(parts, 3);
return Lay(parts, false);
```

**SwiftUI라면 슬롯을 View로 직접 받아 조합한다.** 아래는 현재 행의 디자인을 보유하는 앱 View다.

```swift
struct InspectorRow<Leading: View, Content: View>: View {
    let title: String
    var trailingInset: CGFloat = 12
    @ViewBuilder var leading: Leading
    @ViewBuilder var content: Content
    @Environment(\.isEnabled) private var isEnabled

    var body: some View {
        HStack(spacing: 8) {
            leading.frame(width: 22, alignment: .leading)
            LabeledContent {
                content
            } label: {
                Text(title)
            }
        }
        .font(.system(size: 13))
        .lineLimit(1)
        .padding(.leading, 12)
        .padding(.trailing, trailingInset)
        .frame(height: 40)
        .background(.quaternary, in: RoundedRectangle(cornerRadius: 10))
        .opacity(isEnabled ? 1 : 0.45)
    }
}

InspectorRow(title: "불투명도") {
    Image(systemName: "circle.lefthalf.filled")
} content: {
    Slider(value: $vm.opacity, in: 0...100)
    Text(vm.opacity, format: .number.precision(.fractionLength(0)))
        .monospacedDigit()
    Text("%").font(.system(size: 12)).foregroundStyle(.secondary)
}
.disabled(!vm.canEditOpacity)
```

LabeledContent는 label과 값을 의미적으로 결합하고 컨테이너에 맞춰 배치하는 SwiftUI 구성이다. 정확한 slider 폭·label 우선순위·행 간 정렬까지 현재 디자인대로 유지하려면 custom LabeledContentStyle이나 Grid 등 추가 배치가 필요할 수 있다. 슬롯의 의미를 제공한다는 점과 모든 배치 요구를 공짜로 해결한다는 점은 다르다. [Apple LabeledContent](https://developer.apple.com/documentation/swiftui/labeledcontent/)

**사라지는 것:** View 배열 생성·인덱스·CopyTo, Name/Value별 반복 Font/LineLimit, 별도의 EnabledWatch나 disabled 색 전달이 줄어든다. 22·40·12·10 같은 숫자는 앱 디자인이므로 공통 컴포넌트 안에 남는다. 부모 opacity는 모든 자식을 함께 흐리게 하므로 현재의 개별 색 정책과 정확한 픽셀 일치는 별도 스타일 결정이다.

**ElementUI에서 필요한 것:** `Label`/`LabeledContent`에 해당하는 의미 있는 label·value 조합, control에 View label/accessory를 넣는 경로, 일반 View의 환경 읽기, 기본 font/foreground의 일관된 적용, 재사용 modifier/style다. C#은 Swift result builder 문법을 그대로 가질 수 없지만 slot API·builder·작은 조합 helper로 배열 관리까지 사용처에 드러나는 것은 줄일 수 있다. row 디자인이 앱 책임이라는 이유로 이 조합 API의 부족을 무시해서는 안 된다.

## 8. LayoutToolbar — 버튼 이름·아이콘·상태·크기·힌트·단축키가 서로 떨어져 있다

**현재 의도:** 선택 요약 옆에 undo/redo/cancel/save가 있고, 다음 행에는 크기·초기화·snap·삭제·추가·색상·폰트 작업이 있다. 실행할 수 없는 명령은 아이콘도 흐려진다. 단축키는 게임 키보드 처리 계층에 따로 있다.

근거: [LayoutToolbarView.cs:25](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/Canvas/LayoutToolbarView.cs:25), [LayoutParts.cs:34](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/Canvas/LayoutParts.cs:34), 같은 파일 60–96행, [LayoutKeyboard.cs:59](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/Canvas/LayoutKeyboard.cs:59).

```csharp
IconButton(UiIconKind.Undo, "Undo", L("실행 취소", "Undo"), () => VM.Undo, 36)
WideButton(L("저장", "Save"), "Save", () => VM.Save, 104, 36, ButtonStyle.Prominent)
```

helper 내부는 label에 maxWidth Frame을 넣고 버튼을 다시 Frame으로 감싼다. `Ink(command)`는 Command.CanExecute를 읽는 expression tree를 만들어 아이콘 색을 고른다. 같은 명령의 키 입력 연결은 LayoutKeyboard에서 찾는다.

**SwiftUI였다면 명령 선언 옆에 표현과 단축키가 붙는다.**

```swift
HStack(spacing: 8) {
    Text(vm.selectionSummary)
        .lineLimit(1)
        .frame(maxWidth: .infinity, alignment: .leading)

    Button("전체 초기화", action: vm.resetAll)
    Group {
        Button("실행 취소", systemImage: "arrow.uturn.backward", action: vm.undo)
            .disabled(!vm.canUndo)
            .help("실행 취소")
            .keyboardShortcut("z", modifiers: .command)
        Button("다시 실행", systemImage: "arrow.uturn.forward", action: vm.redo)
            .disabled(!vm.canRedo)
            .help("다시 실행")
            .keyboardShortcut("z", modifiers: [.command, .shift])
    }
    .labelStyle(.iconOnly)

    Button("취소", action: vm.cancel)
    Button("저장", action: vm.save)
        .buttonStyle(.borderedProminent)
}
.controlSize(.small)
.buttonStyle(.bordered)
```

snap처럼 켜짐 상태를 가진 작업은 의미상 Toggle로 표현할 수 있다.

```swift
Toggle(isOn: $vm.snapping) {
    Label("자동 맞춤", systemImage: "scope")
}
.toggleStyle(.button)
.labelStyle(.iconOnly)
.help("자동 맞춤")
```

아이콘 이름은 사용 가능한 symbol을 택하는 설계 예시다. 실제 Element 아이콘 자산을 SF Symbols로 교체한다는 뜻은 아니다. `.help`는 시스템 도움말이므로 현재 “아래 status line에 hint 표시”와 완전히 같지 않다. 그 UX를 유지하려면 공통 hover/status 환경 또는 hover callback이 추가로 필요하다.

SwiftUI의 keyboardShortcut은 control의 주 동작과 연결된다. 게임 모드에서는 같은 연결을 제공하되 활성 scope·입력창 focus·현재 플레이 상태를 확인하는 라우팅이 필요하다. Ctrl/Command 매핑, 텍스트 편집 중 undo, 게임 키 전달까지 SwiftUI 호출 한 줄과 동등하게 해결된다고 보지 않는다. [Apple keyboardShortcut](https://developer.apple.com/documentation/swiftui/view/keyboardshortcut(_:modifiers:))

**사라지는 것:** 모든 버튼에 반복하는 크기·아이콘 label 모양·명령 불가 색 변환, 아이콘 버튼을 만들 때 의미를 서로 다른 string 인자로 흩뿌리는 부분이다. 정확한 104×36 버튼이 요구되면 frame은 유지한다. intrinsic control size를 사용하도록 디자인을 바꾸는 선택과 API 부족을 구분해야 한다.

**ElementUI에서 필요한 것:** Label/iconOnly, subtree button style/control size/tint, 비활성 상태의 일관된 스타일, 키보드 shortcut과 command의 선언적 연결, hover·help·status semantics다. 기존 Command와 Hint를 유지하면서 연결할 수 있는 개선이다. 단순히 `WideButton` helper를 하나 더 만드는 것으로 이 모든 관계가 표현되지는 않는다.

## 이 여덟 사례에서 반복해서 필요한 기본 표현

| 사용자가 쓰고 싶은 표현 | 실제 소비 코드에서 대신 하는 일 | 개선 성격 |
| --- | --- | --- |
| `.padding(.horizontal, compact ? 20 : 32)` | HStack + 조건부 Spacer 두 개 | reactive modifier가 필요한 기본 기능 |
| `@State var open`, `Button { open = true }` | 작은 ViewModel + Command + Bind | 기존 state 시스템 위의 사용성·실행 계약 개선 |
| `.focused`, `.onSubmit`, `.onExitCommand` | TMP Configure, 이벤트 listener, 요청 번호 | backend 독립 입력 기능 |
| `.onAppear/.onDisappear/.onChange` | 0 크기 NativeView, OnDisable, Command 배열 | lifecycle·관찰의 공개 API |
| `.opacity`, `.allowsHitTesting`, `.overlay` | 색별 alpha/Hidden/Spacer/ZStack 우회 | 일반 modifier·장식 조합 기능 |
| `Label`, `LabeledContent`, View label/accessory | string label, 수동 HStack, View 배열 | 의미 있는 공통 컨트롤과 슬롯 구성 |
| `.controlSize/.buttonStyle/.labelStyle` | 각 버튼의 Frame·색·아이콘 크기 반복 | 기본 스타일 환경과 control별 적용 계약 |
| `.keyboardShortcut` | 화면 명령과 별도 키 처리 분기 | 명령·focus scope·입력 라우팅의 연결 |
| `switch selection`, binding된 tab | Command 배열 + 여러 If + ZStack | reactive 선택 컨테이너와 명시적 수명 정책 |
| `@Environment(\.isEnabled)` | EnabledWatch와 수동 expression 변환 | 일반 View의 환경값 읽기 |

먼저 바뀌어야 할 것은 메서드 이름의 철자가 아니다. **기본 UI 의도를 값·상태·modifier로 직접 말할 수 있게 만드는 것**이다. 그러면 이미 있는 HStack/VStack/Grid/Popover도 지금보다 훨씬 자연스럽게 조합할 수 있다.
