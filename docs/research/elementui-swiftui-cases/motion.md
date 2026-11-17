# 실제 화면에서 본 ElementUI의 불투명도·변형·애니메이션 부족

조사일: 2026-11-17. 코드 변경 없이 현재 소비자 코드와 ElementUI 구현을 읽었다. 이 문서는 새 구현 제안이며, 아래 SwiftUI 코드는 컴파일하거나 앱에서 실행한 코드가 아니다. 각 화면의 의도를 비교하기 위한 부분 예시로, Unity의 호스팅·입력·리소스 관리까지 SwiftUI 코드로 옮겼다는 뜻은 아니다.

**일반적인 fade, offset, 버튼 눌림 크기 변화까지 화면마다 시간·현재값·목표값·Update·종료 처리를 작성하고 있다.** `.Opacity`, presentation transform, 상태 기반 animation, transaction, transition은 특수 효과보다 기본 UI 합성을 위해 필요한 기능이다.

## 조사 기준과 예시를 읽는 방법

- Element 소비자 코드와 `/Users/kgh/dev/src/ElementUI`의 소스를 확인했다. 앞선 조사에서 Element의 vendored pin `258e5d7898a25f4e5c846e9e0a13ef560fc7fd61`과 ElementUI HEAD의 tracked source tree가 같음을 확인했다.
- 이번 추가 조사에서는 게임 화면의 animation을 촬영하거나 프레임 시간을 측정하지 않았다. 경로·상태·수명에 관한 결론은 소스 분석 결과다.
- SwiftUI 예시의 `SettingsContent`, `NoticeCard`, `EditorContent`, `SaveLabel`은 각각 현재 화면의 내용을 담는 **앱에서 작성할 View**다. SwiftUI 내장 API가 아니다.
- `appeared`, `notice`, `isOn`, `isAvailable`, `canSave`, `dragging`, `range`는 해당 화면이 소유하는 상태를 줄여 쓴 것이다. `reduceMotion`은 SwiftUI의 `accessibilityReduceMotion` 환경값에 대응한다. 색상 이름은 앱 테마 색상을 가리키는 지역 상수다.
- 실제 원본의 감쇠 곡선과 아래 예시의 `easeOut`은 동일하지 않다. 예시는 “어떤 속성이 어떤 상태 때문에 움직이는가”를 비교한다. 정확한 곡선 보존은 animation specification 단계에서 별도로 검증해야 한다.
- Unity `anchoredPosition`의 y는 위쪽이 양수이고 SwiftUI `offset(y:)`는 아래쪽이 양수다. 위치 예시는 이 차이를 반영했다.

## 1. 설정창 등장: layout 위치와 표시 위치를 수동으로 분리한다

현재 [SettingsWindowMotion.cs](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Settings/SettingsWindowMotion.cs:26)는 CanvasGroup alpha, elapsed time, 감쇠 함수, y offset, Update 활성화·종료, OnDisable 복구를 관리한다. [SettingsPanel.cs](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Settings/SettingsPanel.cs:89)에서 CanvasGroup과 전용 MonoBehaviour를 붙인다.

구체적으로 다음 동작이 있다.

- `Show()`는 motion reduction이 꺼져 있으면 alpha를 0으로 만들고 Unity y offset -8에서 시작한다. 즉 최종 위치보다 8 아래에서 시작한다.
- [Update](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Settings/SettingsWindowMotion.cs:39)는 offset에 `(1 + phase) * exp(-phase)`, opacity에 별도 지수식을 사용하고 .24초에 완료한다.
- [RemoveOffset](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Settings/SettingsWindowMotion.cs:62)은 FitWindow가 위치를 덮어썼는지 검사한 뒤 자신이 더했던 offset만 뺀다.

SwiftUI에서는 다음과 같은 의도를 선언할 수 있다.

```swift
SettingsContent()
  .opacity(appeared ? 1 : 0)
  .offset(y: appeared || reduceMotion ? 0 : 8)
  .animation(
    reduceMotion ? nil : .easeOut(duration: 0.24),
    value: appeared
  )
  .onAppear { appeared = true }
```

`appeared`는 처음 false인 상태다. 현재 앱처럼 같은 화면 인스턴스를 다시 보여 주는 경우에는 표시 주기에 맞춰 이 상태를 초기화하는 정책도 필요하다. 예시의 한 곡선은 원본의 두 감쇠식을 그대로 재현하지 않는다.

**필요한 ElementUI 기능:** `.Opacity`, `.Offset`, `.Animation(value:)`, 표시 lifecycle, reduced-motion 환경. 특히 layout이 정한 위치와 animation이 보여 주는 위치를 나누는 presentation transform이 필요하다. `_lastOffset`, `_appliedPosition`, `RemoveOffset()`는 이 경계가 소비자 코드에 노출된 증거다.

**제거할 수 있는 책임:** 매 프레임 alpha/offset 계산, layout 위치에 delta를 더했다 빼는 보정, animation의 활성화·종료 관리.

**남아야 하는 책임:** 게임 위에 띄울 canvas와 window의 크기·위치 정책, 재표시 시점, 원본의 시각적 의도. 텍스트를 흐리게 하지 않기 위해 scale을 사용하지 않은 [기존 설명](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Settings/SettingsWindowMotion.cs:6)도 보존해야 한다.

## 2. Toast: 표시 상태·전환·수명·입력 차단을 함께 수동 관리한다

[UiToastMotion.cs](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Widgets/UiToastMotion.cs:9)는 enter .18초, hold 1.8초, exit .22초를 관리한다. [Apply](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Widgets/UiToastMotion.cs:48)는 opacity와 위치를 계산한다. 처음에는 최종 위치보다 8 위에 있고 아래로 내려온다. 끝나면 callback으로 GameObject를 숨긴다.

[UiToast.Show](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Widgets/UiToast.cs:37)는 내용을 바꾸고 animation을 처음부터 다시 시작한다. [Build](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Widgets/UiToast.cs:87)는 CanvasGroup의 raycast와 interactable을 별도로 끈다.

SwiftUI에서는 상태·transition·취소 가능한 timeout을 나눠 표현할 수 있다.

```swift
if let notice {
  NoticeCard(notice)
    .transition(reduceMotion
      ? .identity
      : .asymmetric(
          insertion: .offset(y: -8).combined(with: .opacity),
          removal: .opacity
        ))
    .allowsHitTesting(false)
    .task(id: notice.id) {
      do {
        try await Task.sleep(for: .seconds(1.98))
      } catch {
        return
      }

      withAnimation(reduceMotion ? nil : .easeOut(duration: 0.22)) {
        self.notice = nil
      }
    }
}

// 새 알림을 표시하는 action 내부:
withAnimation(reduceMotion ? nil : .easeOut(duration: 0.18)) {
  notice = newNotice
}
```

`notice`는 표시할 내용과 식별자 `id`를 가진 optional 값이다. 이 코드는 기본 표시·제거 관계를 설명한다. 현재 `Play()`처럼 연속 알림마다 입장 animation을 다시 시작할지, 현재 표시 위치에서 내용만 바꾸고 timeout을 연장할지, replacement에도 transition을 줄지는 별도 identity 정책이다. 원본의 exit는 선형이고 예시는 ease-out인 점도 다르다. Reduced-motion일 때 원본은 전체 시간 동안 alpha 1을 유지하다 즉시 숨긴다. 위 예시는 timeout 길이까지 동일하게 재현하지 않는다.

**필요한 ElementUI 기능:** `.Transition`, `WithAnimation`, `.AllowsHitTesting`, 취소 가능한 task/effect, exit가 끝날 때까지 제거될 view를 유지하는 수명 관리.

**제거할 수 있는 책임:** 화면별 enter/hold/exit 계산기, callback 기반 animation 종료 후 숨기기, 새 알림과 이전 timeout의 수동 충돌 처리.

**남아야 하는 책임:** 알림 내용·유지 시간·교체 정책, 게임과 설정창 사이의 canvas 순서, 창 크기 제한.

`Opacity(0)`, 입력 차단, Hidden, 조건부 제거는 같은 동작이 아니다. 이들을 하나의 API에 섞으면 보이지 않는 view가 입력을 받거나 exit animation 전에 제거되는 문제가 생긴다.

## 3. 아이콘 버튼: 일반적인 hover·press feedback을 native 컴포넌트로 작성한다

[UiIconInteraction.cs](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Widgets/UiIconInteraction.cs:18)는 pointer enter/exit/down/up, hovered/pressed, interactable, target color/scale, reduced motion, 보간, 종료 오차, enable/disable 복구를 관리한다. 눌렀을 때 glyph가 .92배가 되고 색이 어두워지며, 떼면 복원되는 일반적인 상호작용이다.

- [Refresh](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Widgets/UiIconInteraction.cs:88): 상태에서 색과 scale 목표를 계산한다. pointer down은 즉시 적용한다.
- [Update](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Widgets/UiIconInteraction.cs:107): 현재 color/scale에서 지수 보간한다.
- [UiFactory.IconButton](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Widgets/UiFactory.cs:145), [GlyphButton](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Widgets/UiFactory.cs:180): 비슷한 glyph와 feedback 컴포넌트를 조립한다.

SwiftUI에서는 style과 상태를 소유하는 작은 View로 분리할 수 있다. 다음 예시는 hover 색과 press scale만 보여 준다. 원본의 눌림 색 darkening과 focus ring은 생략했다.

```swift
struct GlyphButtonStyle: ButtonStyle {
  func makeBody(configuration: Configuration) -> some View {
    GlyphButtonBody(
      label: configuration.label,
      isPressed: configuration.isPressed
    )
  }
}

private struct GlyphButtonBody<Label: View>: View {
  let label: Label
  let isPressed: Bool

  @Environment(\.isEnabled) private var isEnabled
  @Environment(\.accessibilityReduceMotion) private var reduceMotion
  @State private var hovered = false

  var body: some View {
    label
      .foregroundStyle(
        !isEnabled ? Color.secondary
          : hovered ? Color.accentColor : Color.primary
      )
      .scaleEffect(
        isPressed && isEnabled && !reduceMotion ? 0.92 : 1
      )
      .animation(
        reduceMotion || isPressed ? nil : .easeOut(duration: 0.12),
        value: isPressed
      )
      .animation(
        reduceMotion ? nil : .easeOut(duration: 0.12),
        value: hovered
      )
      .frame(width: 40, height: 40)
      .contentShape(Rectangle())
      .onHover { hovered = $0 }
  }
}
```

`GlyphButtonBody`는 예시에서 직접 정의한 View다. hover 상태를 ButtonStyle 자체의 저장 위치에 기대지 않고 해당 View에 둔다. Glyph에만 scale을 적용한 뒤 hit target 크기를 정하는 순서는 의도적이다. 정확한 hover·press 동시 변경 정책과 원본의 지수 곡선은 구현 검증 대상이다.

**필요한 ElementUI 기능:** `.ScaleEffect`, style configuration의 pressed/enabled/hovered/focused 상태, `.OnHover`, interaction shape, scoped animation, 특정 변경의 즉시 적용.

**제거할 수 있는 책임:** 사용자 화면의 pointer handler 네 개, 매 프레임 보간, target/current 복구, 동일한 feedback 부착 코드.

**남아야 하는 책임:** glyph·palette·hit target 크기·keyboard focus 표현의 디자인. Unity 입력을 framework backend로 연결하는 코드는 필요하지만, 매 앱 화면이 이를 작성할 이유는 없다.

## 4. SwitchIndicator: subtree opacity와 thumb offset을 직접 펼쳐 쓴다

[SwitchIndicator](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Declarative/Controls/SwitchIndicator.cs:43)는 클릭을 받지 않고 상태만 보여 주는 작은 switch다. [Paint](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Declarative/Controls/SwitchIndicator.cs:71)는 available=false일 때 track과 thumb 색의 alpha를 각각 .4배 하고, thumb의 x를 계산하여 `UiSwitchMotion`에 전달한다.

[UiSwitchMotion](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Widgets/UiSwitchMotion.cs:9)은 목표값·첫 표시를 관리하고 [Update](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Widgets/UiSwitchMotion.cs:21)에서 현재 위치를 보간하다 오차가 .05 미만이면 멈춘다.

```swift
ZStack(alignment: .leading) {
  Capsule()
    .fill(isOn == false ? offColor : accentSurface)

  Circle()
    .fill(isOn == false ? foreground : softAccent)
    .frame(width: 10, height: 10)
    .offset(x: 4 + thumbTravel)
}
.frame(width: 30, height: 18)
.opacity(isAvailable ? 1 : 0.4)
.allowsHitTesting(false)
.animation(
  reduceMotion ? nil : .easeOut(duration: 0.16),
  value: isOn
)
```

`isOn`은 optional Bool이고 `thumbTravel`은 false=0, true=12, nil=6으로 계산한 값이다. 현재 코드에서는 track 색은 즉시 바뀌고 thumb만 움직인다. 위 예시처럼 넓은 범위에 animation을 적용하면 색도 함께 보간될 수 있다. 원본과 같게 하려면 animation 범위를 thumb의 offset으로 제한해야 한다.

**필요한 ElementUI 기능:** Shape, `.Opacity`, `.Offset`, `.Animation(value:)`, `.AllowsHitTesting`.

**제거할 수 있는 책임:** 각 graphic에 반복하는 alpha 정책, 별도 thumb MonoBehaviour, 현재값·목표값·정지 임계값 관리.

**남아야 하는 책임:** 삼상 상태의 의미, 크기와 palette, 어느 속성만 움직일지에 관한 디자인.

이 UI는 특수 custom canvas가 필요해서 native인 사례가 아니다. 기본 도형과 modifier로 조합할 수 있어야 한다. 다만 subtree 전체에 일관된 opacity 정책을 제공하는 것과, 겹치는 자식들을 먼저 하나의 이미지로 합성한 뒤 opacity를 적용하는 것은 별개다. SwiftUI에서도 `opacity`와 `compositingGroup`은 별도 API다. ElementUI의 opacity 계약과 필요시의 group-compositing 계약을 따로 정의해야 하며, 자식별 alpha 곱셈이 항상 정확히 같은 합성 결과라고 가정하면 안 된다.

## 5. Disabled button: 입력 상태와 흐리게 보이는 정도를 별도로 선언할 수 없다

[UiButton.Awake](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Widgets/UiButton.cs:16)는 CanvasGroup을 만들고, [DoStateTransition](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Widgets/UiButton.cs:62)은 disabled이면 alpha .5, 아니면 1을 적용한다.

```swift
Button(action: save) {
  SaveLabel()
}
.disabled(!canSave)
.opacity(canSave ? 1 : 0.5)
```

`save`는 action이고 `canSave`는 저장 가능 상태다. 기본 ButtonStyle이 이미 disabled opacity를 적용하는 환경에서는 중복으로 흐려지지 않도록 style 정책을 정해야 한다. 이 예시는 입력 금지와 시각적 표현을 독립적으로 선언할 수 있다는 점을 보여 준다.

**필요한 ElementUI 기능:** `.Opacity`와 enabled 환경을 소비하는 style. `.Disabled`는 이미 존재한다.

**제거할 수 있는 책임:** 단지 자식 전체를 흐리게 하려고 CanvasGroup을 생성하고 native transition override에서 alpha를 적용하는 부분.

**남아야 하는 책임:** `UiButton`의 pointer selection 출처와 focus 복구 정책. opacity를 추가하면 이 클래스 전체가 불필요해진다는 결론은 아니다. Disabled가 항상 .5 alpha여야 한다는 뜻도 아니다.

## 6. 편집기 전체 fade-in: 같은 기반 기능을 또 다른 host에서 재구현한다

[OverlayEditorFrame](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayEditorFrame.cs:38)은 `_openedAt`, `_fade`, `FadeSeconds`를 보관한다. [생성자](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayEditorFrame.cs:60)에서 CanvasGroup과 초기 alpha를 만들고, [Tick](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayEditorFrame.cs:137)마다 .16초 선형 fade를 계산한다.

```swift
EditorContent()
  .opacity(appeared ? 1 : 0)
  .animation(
    reduceMotion ? nil : .linear(duration: 0.16),
    value: appeared
  )
  .onAppear { appeared = true }
```

또는 조건부 editor에 `.transition(.opacity)`를 붙이고 여는 action을 `withAnimation`으로 감쌀 수 있다. 현재 코드는 enter만 fade하고 close는 별도 제거 경로이므로, transition을 도입할 때 자동으로 exit까지 바꾸는 것은 별도 UX 변경이다.

**필요한 ElementUI 기능:** `.Opacity`, 상태 기반 animation, lifecycle 또는 conditional transition.

**제거할 수 있는 책임:** fade를 위한 clock과 매 Tick alpha 쓰기.

**남아야 하는 책임:** 게임 화면의 dim layer, canvas 순서, 화면 픽셀을 편집기 좌표로 바꾸는 scale 정책, 편집 종료. Host가 native인 것과 fade를 앱이 직접 계산해야 하는 것은 다른 문제다.

## 7. 값 색상 band: 전문 drawing과 일반 animation clock을 분리할 수 없다

[OverlayValueColorBandView](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayValueColorBandView.cs:55)는 현재 `_low/_high`와 목표 `_targetLow/_targetHigh`를 나눈다. [Build](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayValueColorBandView.cs:103)에서 별도 [OverlayValueColorBandMotion](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayValueColorBandMotion.cs:10)을 붙인다.

- [Redraw](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayValueColorBandView.cs:120): 새 범위를 구하고 첫 draw/motion reduction/목표 도달 여부에 따라 animation을 시작한다.
- [Glide](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayValueColorBandView.cs:141): 현재 범위를 지수 보간하고 오차 기준으로 정지한다.
- [Begin](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayValueColorBandView.cs:280): drag를 시작하면 진행 중인 glide를 멈추고 **현재 화면에 보이는 범위**를 고정한다.

SwiftUI에서는 custom drawing View가 보간 가능한 값을 노출하는 방식으로 나타낼 수 있다.

```swift
struct ValueBand: View, Animatable {
  var lower: Double
  var upper: Double

  var animatableData: AnimatablePair<Double, Double> {
    get { AnimatablePair(lower, upper) }
    set {
      lower = newValue.first
      upper = newValue.second
    }
  }

  var body: some View {
    Canvas { context, size in
      // 앱의 gradient/stop drawing을 이 범위로 계산한다.
      // 실제 draw 코드는 기존 도메인 로직에 해당하므로 여기서는 생략한다.
    }
  }
}

ValueBand(lower: range.lower, upper: range.upper)
  .animation(
    dragging || reduceMotion ? nil : .easeOut(duration: 0.2),
    value: range
  )
```

`ValueBand`는 이 예시에서 정의하는 custom View이며 `range`는 `lower/upper`를 가진 Equatable 값이다. Canvas의 draw 내용, overlay label, gesture는 생략했다. 원본의 지수 보간과 .2초 ease-out은 다르다.

**필요한 ElementUI 기능:** custom animatable value, 공통 scheduler 참여, 즉시/animated transaction, 현재 presentation value에서 중단·재지정하는 계약.

**제거할 수 있는 책임:** 별도 Update MonoBehaviour, clock 전달, 보간·정지·motion reduction의 반복 구현.

**남아야 하는 책임:** gradient mesh, 값→픽셀 변환, label collision, snapping, stop hit testing. 이들은 편집기 도메인 로직이다. 단순 `.ScaleEffect`로 바꾸면 글자와 handle까지 확대되어 원래 동작과 다르다.

위 SwiftUI 예시도 drag 시작 시 보간 중인 실제 표시 범위를 읽고 고정하는 정책까지 자동으로 해결하지 않는다. “model은 목표를 즉시 갖고 presentation은 그쪽으로 이동한다”는 분리와, gesture가 그 presentation을 안전하게 이어받는 API가 필요하다.

## 공통 원인: scheduler는 있는데 공개 animation 합성 계약이 없다

ElementUI에 animation engine이 전혀 없는 것은 아니다.

- [Runtime.StartAnimation](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Reactive/Runtime.cs:84)과 [IAnimation](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Reactive/Runtime.cs:338)은 internal이다.
- [StepAnimations](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/Reactive/Runtime.cs:185)가 공통 animation 목록을 진행한다.
- [Driver.LateUpdate](/Users/kgh/dev/src/ElementUI/src/ElementUI.UGUI/UiHost.cs:111)는 작업이 있는 동안 flush하고, 일이 없어지면 driver를 비활성화한다.
- [SwitchPeer](/Users/kgh/dev/src/ElementUI/src/ElementUI.UGUI/Peers/ControlPeers.cs:170)와 [SegmentedPeer](/Users/kgh/dev/src/ElementUI/src/ElementUI.UGUI/Peers/ControlPeers.cs:644)는 이 scheduler를 사용한다.
- 반면 [공개 ViewModifiers](/Users/kgh/dev/src/ElementUI/src/ElementUI.Core/ViewModifiers.cs:13)에는 Opacity·Offset·ScaleEffect·RotationEffect·Animation·Transition이 없다.

그 결과 앱이 `SettingsWindowMotion`, `UiToastMotion`, `UiIconInteraction`, `UiSwitchMotion`, `OverlayValueColorBandMotion`에서 clock·현재값·목표값·settling·수명 처리 일부를 반복한다. 이 구조적 중복이 근거이며, 측정하지 않은 성능 향상률을 의미하지는 않는다.

기존 scheduler를 활용하는 공개 animatable-property/transaction API가 필요하다. 화면마다 새 timer를 만드는 방향은 적절하지 않다. Animation이 없을 때 새 작업을 만들지 않는 기존 원칙을 유지해야 한다.

## 필요한 API 계층과 우선순위

| 우선순위 | 계층 | 실제로 줄어드는 소비자 책임 |
| --- | --- | --- |
| P0 | `Opacity`, `Offset`, `ScaleEffect`, `RotationEffect`, anchor | alpha·위치·scale을 native graphic마다 쓰고 복구하는 코드 |
| P0 | `Animation(animation, value)`와 animatable property | target/current, clock, 보간, settle 판정을 화면마다 구현하는 코드 |
| P0/P1 | `WithAnimation`, transaction override | 같은 action의 여러 속성을 동기화하고 drag·첫 mount·특정 자식만 즉시 적용하는 코드 |
| P1 | Transition과 removal 수명 | fade가 끝나기 전에 view가 사라지지 않게 callback으로 수명을 묶는 코드 |
| P1 | interaction style의 pressed/hovered/focused/enabled | 일반 버튼 feedback을 pointer handler와 MonoBehaviour로 작성하는 코드 |
| P1 | `AllowsHitTesting`, clipping, content shape | opacity·disabled·visibility·input 영역을 혼동해서 보정하는 코드 |
| P1/P2 | custom animatable value와 presentation interruption | 값 색상 band 같은 전문 drawing이 별도 animation driver까지 구현하는 코드 |

`RotationEffect`는 공개 transform family의 일관성을 위해 함께 설계할 항목이다. 이번 실제 사용처에서 직접 확인한 회전은 [gradient handle 방향 계산](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayGradientHandles.cs:259)이므로, 이를 평범한 버튼 회전 animation의 반복 증거로 과장하지 않는다. 직접적인 다수 사례는 opacity·offset·scale이다.

## Transaction을 붙일 때 놓치면 안 되는 계약

1. **Model과 presentation:** binding/model 값은 action의 목표값으로 즉시 갱신하고 화면에 보이는 값만 보간한다. 매 animation frame마다 model을 다시 써서 저장·도메인 계산·command가 반복 실행되게 만들면 안 된다.
2. **Deferred flush:** 현재 `BeginBatch/EndBatch`는 notification batching이다. `WithAnimation` block이 끝난 뒤 effect가 실행될 수 있으므로 전역 `CurrentAnimation`을 block 동안만 설정하는 것으로 충분하지 않다. 변경 기록/dirty effect에 animation transaction이 전달되어야 한다.
3. **Scope와 override:** nested animation, 같은 프레임 여러 변경, animation 제외 자식, immediate drag, 첫 mount, reduced motion의 우선순위를 정의해야 한다. `.Animation(value:)`가 무관한 모든 상태 변경을 자동으로 움직이는 API가 되어서는 안 된다.
4. **중단과 재지정:** 새 목표가 오면 현재 presentation에서 이어가야 한다. 필요하면 velocity를 보존하고, gesture는 현재 표시값을 기준으로 시작할 수 있어야 한다.
5. **Layout과 transform:** Offset/Scale/Rotation은 layout 결과를 덮어쓰기보다 presentation layer에서 합성해야 한다. 화면 resize와 animation이 동시에 발생해도 SettingsWindowMotion처럼 이전 delta를 추측해서 빼지 않아야 한다.
6. **수명과 input:** hide/unmount/dispose 취소, exit 완료 후 제거, 재등장 중 reverse, 새 toast의 이전 timeout 취소를 정의한다. opacity와 input 차단은 분리한다.
7. **정책과 합성:** reduced motion은 duration=0 하나로 끝나지 않을 수 있다. 이동을 fade로 대체하거나 즉시 변경하는 정책을 style/transaction에서 선택할 수 있어야 한다. Opacity와 offscreen group composition 역시 독립 계약이다.

## 검증 범위

구현 단계에서는 코드가 짧아지는지와 함께 다음을 확인해야 한다.

- fade 도중 화면 크기를 바꿔도 layout 위치와 presentation offset이 충돌하지 않는가.
- hover/press를 빠르게 번갈아도 현재 표시값에서 재지정되고, disabled/unmount 후 animation이 남지 않는가.
- first mount는 원하지 않는 animation을 만들지 않고, exit transition 동안 필요한 view만 유지하는가.
- opacity 0인 view의 input 정책이 명시한 대로 작동하는가.
- toast를 연속 표시하면 이전 timeout이 새 알림을 지우지 않는가.
- drag는 즉시 반응하고, 시작할 때 보간 중인 band의 표시 범위를 정확히 이어받는가.
- model 값이 animation frame마다 다시 쓰이지 않고, idle 시 기존처럼 불필요한 frame 작업이 없는가.

SwiftUI 비교의 개념적 근거는 Apple의 [Animations](https://developer.apple.com/documentation/SwiftUI/Animations)와 [Animatable](https://developer.apple.com/documentation/SwiftUI/Animatable) 문서다. 이 문서들은 `withAnimation`/`animation(_:value:)`에 따른 animatable 값의 보간과 custom view의 animatable data를 설명한다. ElementUI가 SwiftUI의 rendering engine 전체를 복제해야 한다는 뜻은 아니다. 현재 retained view·reactive graph·sleeping driver 위에서 같은 수준의 **선언 가능한 의도**를 제공하는 것이 목표다.
