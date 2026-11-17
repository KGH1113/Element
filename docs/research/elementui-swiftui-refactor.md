# ElementUI·Element 선언형 UI 리팩터링

이 변경은 앞선 SwiftUI 비교 조사에서 실제 화면이 요구했던 공통 기능을 ElementUI로 옮기고, Element 소비부를 그 기능으로 다시 작성한 작업이다. C#의 expression 기반 의존성 추적과 한 번 구성하는 retained view 모델은 유지한다.

## ElementUI에 추가한 표현

| 영역 | 추가·보완한 API와 계약 |
| --- | --- |
| 표현 속성 | 고정·반응형 `Opacity`, `Offset`, `ScaleEffect`, `RotationEffect`, `AllowsHitTesting` |
| 애니메이션 | `Animation(motion, value:)`, `WithAnimation`, `WithoutAnimation`, `Motion` 곡선, `AnimatedFloat`·`AnimatedColor`, 공통 취소 가능한 `Runtime.Delay` |
| 전환 | `Transition.Opacity/Offset/Scale`, 결합 전환, 퇴장 중 입력 차단과 제거 지연, 다시 나타나면 현재 표시값에서 복귀 |
| 도형·장식 | `Rectangle`, `RoundedRectangle`, `Circle`, `Capsule`, `Fill`·`Stroke`, View를 받는 `Background`·`Overlay`, `ClipShape`·`ContentShape` |
| 반응형 레이아웃 | `Padding`, `Frame`, `Font` expression overload; 장식이 본문 크기를 키우지 않는 측정 계약 |
| 입력 | `TextFieldStyle.Plain`, `CharacterLimit`, `InputKind`, `Focused`, `FocusOn`, `OnFocusChanged`, `OnSubmit`, `OnEditEnded`, `OnTextChanged`, 선택·Escape 옵션 |
| 버튼 | Action overload, `ButtonStyling`·`ButtonStyleConfiguration`, `ButtonSurfaceStyle`; 스타일이 모든 interaction 상태의 모양과 색을 소유 |
| 상태·수명 | 모델 없는 `ComponentView`, 컴포넌트 소유 `Computed`, `Binding.Create`·`Map`, `State.ToBinding`, `EnvironmentReader`, `OnChange`, `OnHover`, `OnAppear`·`OnDisappear`·`OnDispose` |
| 이미지·분기 | Texture `Image`의 Fit/Fill/Stretch, 선택값에 따른 보존형 `Switch` |
| 호스트 | `UiHost.FitContent(..., followChanges: true)`, `ContentSizeChanged`, `TreeChanged`·`TreeVersion` |

환경 modifier가 `If`·`ForEach`의 항목들을 한 항목으로 바꾸던 문제와, modifier를 감싼 Spacer·Divider가 stack 축을 잃던 문제도 수정했다. `TextField`·segmented control은 상속된 글꼴·색상·정렬을 따르도록 보완했다.

반응형 `Frame<T>`는 lambda에서 타입을 추론하므로 기존 `.Frame()`, alignment-only, 명시적 `null` 호출과 충돌하지 않는다. 숫자 expression의 float 변환은 expression tree 안에서 수행한다. 입력에서는 모델을 외부에서 변경한 뒤 렌더링 전에 native blur·cancel·submit·change가 도착해도 오래된 문자열이나 숫자가 모델을 다시 덮지 않도록 수정했다. 정상 타이핑·숫자 임시 입력·취소 복원은 별도로 유지한다.

## 실제 화면에서 바뀐 부분

- **검색창:** `ZStack(TextField, 아이콘 행)`과 TMP viewport 여백 조작을 제거했다. 아이콘·plain 입력·지우기 버튼을 `HStack`에 놓고, 글꼴·padding·capsule 배경을 바깥 modifier로 선언한다. 검색, Enter로 첫 결과 선택, Escape로 지우기, 게임 키보드 소유권은 유지한다.
- **HEX·불투명도·배율 입력:** 접두·접미 콘텐츠를 같은 행에 조합한다. 글자 제한·입력 종류·제출·편집 종료·포커스를 공개 modifier로 연결한다.
- **프로필·프리셋:** TMP 이벤트 설정을 입력 modifier로 옮겼다. 프로필 제목과 동작 버튼은 HStack으로 배치하고, 현재 프로필 표시선은 overlay로 표현한다. 프리셋은 보존형 Switch를 사용하고 탭마다 만들던 Command 배열을 제거했다. 선택된 탭을 다시 눌러도 단축키 녹음을 취소한다.
- **설정 창:** 조건부 양쪽 Spacer로 만들던 여백을 반응형 Padding으로 바꿨다. 입장 효과는 Opacity·Offset으로 선언하여 레이아웃 위치를 더했다 빼는 복구 코드를 제거했다. 움직임 줄이기 설정은 화면 모델을 다시 만들지 않고 기존 테마의 정책을 갱신하여 페이지 상태를 보존한다.
- **Inspector·도구 행:** 기본 배경과 내부 hover 도형이 갈라지지 않도록 버튼 스타일이 표면 전체를 소유한다. View 배열 복사와 수동 조립을 줄였다. Open/ToolOpen, Available/Ink, 상태/Fill처럼 중복 저장하던 파생값은 Computed로 옮겼다. Inspector의 Revision 비교·Refit·Sync 반복은 호스트의 invalidation으로 처리한다.
- **스위치·swatch·Grow from:** 도형, overlay, clipping, offset, opacity, 회전으로 기본 시각 요소를 선언한다. 스위치는 thumb 위치만 보간하고 색은 즉시 바꾸는 기존 동작을 유지한다.
- **색상 선택기:** 표시 상태만을 위한 작은 ViewModel과 투명 NativeView 수명 감시를 제거했다. 로컬 State와 수명 modifier로 연결한다.
- **알림·아이콘·색상 범위:** 화면별 Update 보간을 공통 scheduler로 옮겼다. Toast는 선언형 콘텐츠와 취소 가능한 지연을 사용한다. 색상 범위를 드래그할 때에는 현재 표시 범위를 잡고 시작한다.
- **행성 미리보기:** 앱 전용 TextureImage 레이아웃 helper를 없애고 Texture Image의 Fit 모드를 사용한다.

삭제한 앱 helper에는 `SettingsWindowMotion`, `UiToastMotion`, `OverlayValueColorBandMotion`, `EnabledWatch`, `TextureImage`, `LayoutTextCapture`가 있다. 기존 imperative 위젯의 pointer adapter처럼 여전히 필요한 연결부도 자체 Update 대신 공통 애니메이션을 사용한다.

변경을 직접 확인할 대표 진입점:

| 사례 | 구현 |
| --- | --- |
| 검색창의 HStack·plain 입력·Capsule | [OverlayValueBrowserView.cs](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayValueBrowserView.cs) |
| Inspector 행의 기본·hover·pressed 표면 | [OverlayInspectorView.cs](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Layout/ContentEditors/Shell/OverlayInspectorView.cs) |
| 설정 창의 Opacity·Offset·Animation | [SettingsPanel.cs](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Settings/SettingsPanel.cs) |
| 로컬 컴포넌트·파생 상태·도형 조합 | [SwitchIndicator.cs](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Declarative/Controls/SwitchIndicator.cs) |
| 입력 이벤트·접두·접미 콘텐츠 | [ColorPickerLayout.cs](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/ColorPicker/ColorPickerLayout.cs) |
| 알림의 선언형 표현·취소 가능한 지연 | [UiToast.cs](/Users/kgh/dev/src/impl-resourcepack/Element/Presentation/Ui/Widgets/UiToast.cs) |

`Element/Presentation`에 남아 있던 `Configure<TMP_InputField>`, `Configure<TMP_Text>`, `Configure<TextMeshProUGUI>` 호출과 `List<View>`·`CopyTo` 기반 콘텐츠 조립은 제거했다. 스크롤 정책이나 게임의 값 드래그처럼 별도의 앱 책임을 연결하는 native 설정은 유지한다.

## 애니메이션과 상태의 기준

상태는 즉시 최종값으로 바뀌고 렌더링 값만 보간한다. 저장·undo·명령·검색에 중간 보간값을 보내지 않는다. 애니메이션 정책은 deferred effect queue까지 전달하며, 이미 대기 중인 effect가 다시 변경되었을 때 최신 정책을 적용한다. 초기 속성 적용은 즉시 처리하고, 명시적 insertion transition은 등장 자체를 애니메이션할 수 있다.

새 목표는 현재 표시값에서 시작한다. `AnimatedFloat.Set`·`AnimatedColor.Set`에 명시적으로 무애니메이션을 전달하면 같은 목표로 진행 중인 애니메이션도 끝낸다. 이미 같은 값을 가진 State를 `WithAnimation(null)` 안에서 다시 대입하는 것은 상태 변경을 발생시키지 않으므로 별도의 애니메이션 취소 명령이 아니다. 숨김·native host 비활성화·재마운트·파괴는 각각 테스트하며, 최종 파괴 시 scheduler 작업을 정리한다. 힌트·팝오버 기준 좌표는 표현상의 이동·크기·회전을 반영한다.

`Frame`의 무한대·NaN 제약은 유한한 숫자 보간에 넣지 않고 제약을 즉시 전환한다. 효과가 없을 때 scheduler가 잠들고, idle flush의 무할당 성질은 기존 회귀 테스트로 유지한다.

`Transition`은 조건부 branch나 keyed row가 반환하는 View의 바깥쪽에 붙인다. Padding·Frame·Font 같은 투명 wrapper는 제거를 전달하지만, 별도의 native container나 컴포넌트 body 안쪽에 놓인 전환이 바깥 View 전체의 제거를 지연시키지는 않는다. 전체 행을 전환할 때에는 그 경계 바깥에 붙이는 계약을 사용 가이드와 회귀 테스트에 명시했다.

테마의 `IMotionPolicy.ReduceMotion`이 바뀌면 실행 중인 View 애니메이션은 다음 flush에서 끝난다. 앱의 별도 렌더러가 직접 사용하는 `AnimatedFloat`·`AnimatedColor`는 소유자가 정책과 수명을 제공한다.

## 유지한 앱 책임

게임 키보드 캡처, 창·도구의 외부 앵커, 값 드래그와 snapping, gradient·arrow·키 기호의 도메인 그래픽, OS 서비스는 앱 연결부에 남긴다. 기본 시각 속성·입력 이벤트·레이아웃·애니메이션을 표현하기 위해 Unity 객체를 직접 조립하던 부분과 구분한다.

이 작업이 SwiftUI 전체 API와의 동등성을 뜻하지는 않는다. 문서의 조사 사례를 실제 코드에 적용한 구현이며, 일반 Layout 프로토콜·가상화 목록·모든 SwiftUI gesture와 accessibility API까지 복제한 것은 아니다.

## 검증

2026-11-17 기준, 다음 검증을 완료했다.

| 검사 | 결과 |
| --- | --- |
| ElementUI `./scripts/check.sh` | **105 suites 통과**, release·trace 빌드 완료 |
| Element `./scripts/run.sh mod-check` | **47 tests 통과**, Element·Launcher·Updater Release 빌드 경고 0·오류 0 |
| Unity `./scripts/run.sh ui-check` | **6,903 assertions 통과**, 이번 실행에서 **184 PNG** 재생성 |
| `./scripts/run.sh check` | **57개 shell script** 검증 통과 |
| 형식·diff | 변경된 C# 포맷 완료, 두 저장소 `git diff --check` 통과 |
| 설치물 | 설치된 `Element.dll`, `ElementUI.Core.dll`, `ElementUI.UGUI.dll`의 SHA-256이 검증한 빌드·vendor 파일과 일치 |

ElementUI는 로컬 커밋 `6ad41a0ee3c77e969d7800c17c2d7a48c01cfe6c`까지 반영하고, 깨끗한 소스 트리에서 `elementui-sync`로 DLL·analyzer·문서를 동기화했다. Element의 소비 코드·회귀 검사·조사 문서와 동기화된 vendor 파일도 함께 커밋했다. 원격 push는 하지 않았다.

새 검사는 정적·반응형 Frame overload의 기존 호출 호환성, effect transaction 전달, 중단·재시작·reduced motion, 수명·remount·transition, native shape·clipping·texture, 실제 EventSystem 포커스, 중첩 버튼의 hit target, 자동 높이 측정, 검색 초기화 직후 submit/end 순서를 포함한다. idle flush·슬라이더 경로의 할당 회귀 검사와 대규모 무관 effect의 국소 갱신 검사도 통과했다.

Unity 결과는 [checks.txt](/Users/kgh/dev/src/impl-resourcepack/build/ui-preview/checks.txt)와 [editor.log](/Users/kgh/dev/src/impl-resourcepack/build/ui-preview/editor.log)에 있다. 로그에 UI 예외나 렌더 텍스처 해제 경고는 없다. Unity 시작의 licensing token 메시지와 종료의 debugger port 메시지는 남아 있다.

이번 실행의 184 PNG는 모두 빈 이미지 탐지 검사를 통과했다. 검색창·설정 홈·프로필·텍스트 편집기·색상 선택기의 대표 화면은 직접 확인했다. `build/ui-preview`에 남은 전체 212 PNG 중 28개는 과거 파일이므로 이번 실행 결과로 계산하지 않았다. 최신 결과 예시:

- [좁은 화면의 검색창](/Users/kgh/dev/src/impl-resourcepack/build/ui-preview/value-dock-en-540-search.png)
- [텍스트·그라데이션 편집기](/Users/kgh/dev/src/impl-resourcepack/build/ui-preview/layout-ko-gradient-text-editor.png)
- [HEX·불투명도 입력 조합](/Users/kgh/dev/src/impl-resourcepack/build/ui-preview/rgba-color-picker.png)

이 검증은 Unity editor의 native fixture와 합성 입력·렌더링까지다. 실제 ADOFAI를 실행한 플레이 확인, 실제 키보드의 IME 조합 입력, 게임 안의 프레임타임 실측은 수행하지 않았다. 코어의 할당 회귀 통과를 실제 게임 성능 측정으로 해석하지 않는다.

## 설치

`ELEMENT_SKIP_DMNOTE=1 ./scripts/run.sh install`로 **Element 1.4.0 설치를 완료**했다. ElementNote는 기존 설치본을 유지했다.

설치된 런타임: [Runtime/versions/1.4.0](</Users/kgh/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice/Mods/Element/Runtime/versions/1.4.0>).

실행 중인 게임은 재시작해야 새 DLL을 읽는다.

ElementUI의 사용 예제와 세부 계약: [declarative-ui.md](/Users/kgh/dev/src/ElementUI/docs/declarative-ui.md).
