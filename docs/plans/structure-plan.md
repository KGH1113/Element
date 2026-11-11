# 구조 진단과 리팩터링 계획

2026-11-11, `dev`(be03a1d) 기준. 코드는 아직 바꾸지 않았습니다.
블록 편집기 브랜치는 폐기하기로 해서 계획에서 뺐습니다(2026-11-11). ElementKit은 브랜치에서 함께 진단했고, 그 뒤 dev에 합쳤습니다.

## 한눈에

Element는 263개 파일, 약 45,000줄입니다. 층(Domain, Application, Configuration, Infrastructure, Presentation, Bootstrap)으로 나눈 틀은 있습니다.
하지만 다섯 가지 문제가 되풀이됩니다.

| 문제 | 대표 사례 | 원칙 |
| --- | --- | --- |
| 한 클래스가 너무 많은 일을 함 | `SettingsPanel` 9조각 3,803줄, `OverlayLayoutEditor` 9조각 2,910줄 | 단일 책임 |
| 새 종류를 하나 더하려면 여러 곳을 고쳐야 함 | 오버레이 요소 종류 하나에 6개 파일 약 14곳, 값 하나에 4개 층 약 8곳 | 개방-폐쇄 |
| 의존 방향이 거꾸로 감 | Infrastructure → Presentation 9개 파일, Domain ↔ Configuration 순환 | 의존 역전 |
| 같은 일을 하는 코드가 여러 벌 있음 | 실행 취소 3벌, 팝업 위치 계산 4벌, 한/영 문구 함수 5벌, 오류 색 7벌 | 한 일은 한 곳에서 |
| 폴더마다 나누는 기준이 다름 | ElementNote 코드가 4개 폴더, TUFReplay 코드가 5개 폴더에 흩어져 있음 | 폴더 하나에 규칙 하나 |

**결론:**
- 규칙부터 정합니다(아래 "목표 구조").
- dev에서 영역별로 나눠 고칩니다(아래 "8. 진행").
- 마지막에 폴더와 네임스페이스를 목표 구조로 옮깁니다.

## 1. 층 사이 의존 방향

아래 표는 층마다 다른 층을 참조하는 파일 수입니다. 행이 참조하는 쪽, 열이 참조받는 쪽입니다.

| 참조하는 층 ＼ 참조받는 층 | Domain | Application | Configuration | Infrastructure | Presentation | Bootstrap |
| --- | --- | --- | --- | --- | --- | --- |
| Domain | – | 0 | **14** | 0 | 0 | 0 |
| Application | 14 | – | 4 | 0 | 0 | 0 |
| Configuration | **2** | 0 | – | 0 | 0 | 0 |
| Infrastructure | 25 | 19 | 12 | – | **9** | **3** |
| Presentation | 49 | 11 | 42 | 0 | – | 0 |
| Bootstrap | 2 | 6 | 2 | 6 | 5 | – |

**잘된 점:** Presentation과 Application은 Infrastructure를 전혀 참조하지 않습니다.

**거꾸로 된 곳:**

- **Infrastructure가 Presentation을 참조합니다(9개 파일).**
  - 게임 입력 패치 3곳이 설정 화면의 정적 상태 `UIInteractionState`를 직접 읽습니다(`SettingsInputPatches`, `EditorTrackpadController`, `EditorTweaksController`).
  - `KeyLimiterMenuRow`와 `NativeViewerLayoutTarget`이 설정 화면의 문구 `SettingsLocale`를 씁니다.
  - UI 오브젝트인 `NativeViewerMock`이 Infrastructure에 있습니다.
  - 인터페이스 `IKeyViewerElements`와 `IOverlayFontSource`는 Presentation에 선언되어 있고, Infrastructure가 그것을 구현합니다.
- **Infrastructure가 Bootstrap을 참조합니다(3개 파일).** `PatchGroupAttribute` 하나를 쓰려고 참조합니다.
- **Domain과 Configuration이 서로 참조합니다(순환).**
  - Domain이 저장 설정 클래스를 작업 모델로 쓰는 것은 괜찮습니다.
  - 문제는 반대 방향입니다. `ElementSettings.Migrate`가 Domain의 `OverlayElementMigration`을 부르고, `GameplayTweaksSettings`가 Domain의 `ChatterFilter`를 씁니다.
- **Domain과 Application에 Unity·게임 객체가 들어 있습니다.**
  - `KeyLimiterSnapshot`과 `StatusSnapshot`이 `UnityEngine`을 씁니다(`KeyCode`, `SystemLanguage`).
  - Application 포트의 메서드 시그니처에 게임 객체 `scrMarginTracker`가 들어 있습니다(`OverlaySources`, `OverlayCoordinator`, `JudgementFeature`).
- **Configuration이 동작까지 맡습니다.**
  - 파일 입출력: `ProfileFileCodec`, `ElementSettings.Load`.
  - 프리셋 추가·이름 바꾸기·지우기와 영어로만 된 오류 문구: `EffectSettings`.
  - 같은 일을 Profiles는 Application에서 하므로, 두 기능의 방식이 다릅니다.

## 2. 한 클래스가 너무 많은 일을 함

partial 파일로 나눠도 클래스는 하나입니다. 필드를 모든 조각이 함께 쓰기 때문입니다.

| 클래스 | 크기 | 하는 일 |
| --- | --- | --- |
| `SettingsPanel` | 9조각 3,803줄, 필드 약 146개 | 설정 창 틀, 모든 탭, 팝업 3개, Esc 처리, 단축키 규칙 |
| `OverlayLayoutEditor` | 9조각 2,910줄, 필드 약 83개 | 배치, 선택, 도구 막대, 요소 막대, 추가 메뉴, 팝업, 상태 줄, 편집기 열기 |
| `ModRuntime` | 661줄 | 객체 조립, 게임 이벤트 14개 받기, 매 프레임 루프, 플레이 표시 규칙, 게임 직접 읽기 |
| `OverlayModEditor` | 702줄, 필드 약 40개 | 편집 화면 틀, 미리보기, 실행 취소, 팝업 4종, 키·마우스, 모드 오류 격리 |
| `DmNoteNativeIpcFeature` | 478줄 | IPC 세션, 네이티브 창 수명, 배치 편집기 대상, 렌더 위치, 포커스, 폰트 |
| `OverlayFormula` | 520줄 | 토큰 모델, 해석, 키패드 편집, 계산, 표시 글자 |

**설정 창 페이지가 번호로만 구분됩니다.**
- 페이지를 담는 배열은 `new GameObject[10]`인데, 이름은 11개입니다. 그래서 "Profile editor" 페이지는 만들어지지 않습니다(`SettingsPanel.Shell.cs:113`).
- 숫자로 페이지를 가리키는 곳(`_pages[7]`, `NavigateTo(4)` 등)이 34곳입니다.
- 제목과 뒤로 가기는 번호를 비교하는 조건문으로 정합니다.
- 그래서 탭 하나를 더하려면 6곳 이상을 고쳐야 합니다.

**`OverlayLayoutEditor` 조각 파일 이름이 내용과 맞지 않습니다.**
- `.ShapeEditor.cs`에는 편집기 연결과 도형 하위 메뉴가 있습니다.
- `.Settings.cs`에는 키뷰어·정렬 요소 막대가 있습니다.
- 공통 필드 22개는 `.Toolbar.cs`에 선언되어 있습니다.

**파일 하나에 종류가 여러 개 들어 있습니다.**

| 파일 | 타입 수 |
| --- | --- |
| `SettingsInputPatches.cs` | 12 |
| `RecordingUiPatches.cs` | 10 |
| `OverlayElementSettings.cs` | 10 |
| `ModEditorControl.cs` | 10 |
| `RenderContracts.cs` | 8 |
| `GameplayTweaksSettings.cs` | 7 |
| `SettingsGradientGraphics.cs` | 6 |

## 3. 새 종류를 더할 때 여러 곳을 고쳐야 함

| 더하는 것 | 지금 고치는 곳 | 바꿀 방향 |
| --- | --- | --- |
| 오버레이 요소 종류 | 6개 파일 약 14곳. `TemplateId` 문자열 분기가 22곳 | 종류마다 설명서 하나(`IElementKind`: 이름, 아이콘, 만들기, 편집기 열기, 닫을 때 규칙). 전체 화면 편집기는 `IContentEditor` 한 자리 |
| 오버레이 값 | 4개 층 약 8곳 | 값 정의 하나에 읽기·쓰기·기본 색까지 담아, 값을 더할 때 정의만 늘리기 |
| 설정 탭 | 6곳 이상 | `ISettingsPage` 한 클래스 = 탭 하나. 패널은 페이지 사이를 오가기만 함 |
| 게임 패치 | 연결 방식 3가지가 섞여 있음: `Main.Instance?.Runtime?.X()` 18곳, 생성자에서 넣는 정적 필드, 손으로 만든 Harmony 인스턴스 2개(실패 목록과 되돌리기에 빠짐) | 모든 패치를 `Game/Patches`의 특성 방식으로. 패치는 Bootstrap이 한 번 넣어 준 대상에 넘기기만 함 |
| 같은 게임 메서드 패치 | `scnEditor.Update` 3곳, 그 밖에 6개 메서드가 2곳씩. 순서는 우선순위 숫자에 달림 | 게임 메서드 하나에 패치 하나, 그 안에서 처리할 목록을 순서대로 부름 |

**`OverlayCanvasHost`가 충돌 지점입니다.** 요소 종류마다 분기와 목록이 늘어나서, 종류를 더할 때마다 같은 메서드를 고칩니다(ElementKit 요소를 더할 때도 그랬습니다).
요소들이 같은 프레임 메서드를 공유하지 않는 것이 원인입니다. Text는 `Refresh(changes, settle)`, Shape는 `Refresh(changes)`를 씁니다.

## 4. 같은 일을 하는 코드가 여러 벌

| 일 | 벌 수 | 위치 |
| --- | --- | --- |
| 실행 취소 기록 | 4 | `LayoutHistory`, 글자 편집기, 도형 편집기, 모드 편집기(1초 안 묶음) |
| 팝업 위치 계산 | 4 | `OverlayEditorUi.PlaceUnder`, `OverlayGrowPopover.Show`, `PopoverAboveToolbar`, `PlaceShapeMenu` |
| 목록 메뉴 | 4 | 추가 메뉴, 도형 메뉴, `BlockMenu`, `ModListPopover` |
| 팝업 열고 닫기 목록 | 편집기마다 | `VisiblePanels`, `CloseTopPopover`, `RaisePopovers`… / `AnyControlOpen`, `LookPopupOpen`… |
| 한/영 문구 함수 | 5 | `SettingsPanel`, `SettingsShortcutField`, `ShortcutKeys`, `SettingsUiRuntime`, `RenderNotices`. 공용 `SettingsLocale`은 `SettingsPalette.cs` 안에 숨어 있음 |
| 아이콘 체계 | 4 | `SettingsNavigationIcon`(숫자), `LayoutAction`, `SettingsGradientIconKind`, 그림마다 클래스 하나 |
| 오류 색 `Color(1,.66f,.53f)` | 7 | 직접 쓴 곳 7곳, 이름 붙인 사본 2개 |
| 색 팔레트 | 4 | `SettingsUiFactory`, `SettingsPalette`, `OverlayTheme`, `OverlayEditorUi` |
| 헥스 색 읽기 | 3 | `OverlayTheme`, `SettingsColorPicker`, `OverlayTextComposer`. Domain에는 이미 `OverlayRgba.Parse`가 있음 |
| 판정 색 | 3 | `OverlayTextEditor`, `OverlayElementDefaults`, `OverlayValues` |
| 단축키 겹침 규칙 | 2 | `AssignSettingsShortcut`, `AssignPresetShortcut` |
| 키 입력 받기 | 2 | `SettingsShortcutField.Tick`, `SettingsPanel.Chatter`(둘 다 KeyCode 전체를 훑음) |
| 설정 정리(Normalize) | 5 | `ElementSettings.Migrate`가 이미 하는데, `Main`, `SettingsUiRuntime`, `GameplayTweaksController`(2곳)에서 다시 함 |
| "지금 플레이 중인가" | 2 | `ModRuntime`, `GameplayState`. 레벨 선택 화면과 CLS에서 결과가 다름 |
| 행성 색·불투명도 규칙 | 2 | `PlanetPreviewRenderer`(불투명도 제한함), `PlanetAppearanceController`(안 함) |
| 버전 쓰기 | 2 | `apps/element-note/scripts/sync-version.cjs`, `scripts/tasks/version/version.mjs` |
| 테스트 도우미 `Check` | 22 | 테스트 파일마다 복사됨. `Near`는 6벌이고 허용 오차가 서로 다름 |

## 5. 폴더 규칙

**지금 규칙이 섞인 곳:**

- **Infrastructure**
  - Attempts가 `Infrastructure/Attempts`와 `Infrastructure/Game/Attempts`에 나뉘어 있습니다.
  - ElementNote 코드가 4곳에 있습니다: `DmNoteApp/`, `Game/DmNote/`(실제로는 게임이 아니라 네이티브 웹뷰), `Ipc/`, `Game/Input/DmNoteKey*`.
  - TUFReplay 코드가 5곳에 있습니다: `Replays/`, `Rendering/`, `Game/Input/`, `Bootstrap/ReplayRuntime`, `Bootstrap/RenderRuntime`.
  - `Game/Input`은 잡동사니입니다: 설정 창 입력 막기, 채터 차단, 키 제한, TUFReplay 일시정지가 함께 있습니다.
  - "Tweaks"는 설정 탭 이름일 뿐 코드 경계가 아닙니다.
- **Presentation**
  - `Settings/`에 설정 창과 공용 UI 부품(팩토리, 색 선택기, 세그먼트 등)이 함께 있습니다. 그래서 Layout 28개 파일, Infrastructure 7개 파일, 오버레이 런타임 5개 파일이 "설정 화면"을 참조합니다.
  - 반대로 Settings는 Layout의 `LayoutActionIcon`과 `OverlayFontPopover`를 씁니다. 두 폴더가 서로 참조하는 셈입니다.
  - `Layout/`과 `Layout/Editing/`을 나누는 기준이 없습니다. 색 팝업은 `Layout/`에, 폰트·기준점 팝업은 `Editing/`에 있습니다.
  - `Overlay/Shared`는 잡동사니입니다.
- **Domain**
  - `Combo`(11줄), `TimingScale`(13줄)처럼 파일 하나짜리 폴더가 있습니다.
  - `Input`, `Editor`, `DmNote`는 내용과 이름이 맞지 않습니다.
- **Application**
  - `Features/`는 기능별이 아니라 "기능 클래스 모음"입니다.
  - `IOverlayFeature`는 인터페이스로 쓰이는 곳이 한 군데도 없습니다.
  - Attempts와 Judgement가 각각 두 폴더에 나뉘어 있습니다.
- **저장소 전체**
  - `docs/` 20개 파일에 기능 명세, 프로토콜 참고, 날짜 붙은 조사, 상태 보고, 계획이 섞여 있습니다.
  - `scripts/`는 일부 명령만 `workflows/`를 거칩니다.
  - ElementNote 이름이 `dmnote`, `desktop`, `element-note` 세 가지로 쓰입니다.
- **테스트**
  - `Element.Tests/`는 파일 27개가 한 폴더에 있습니다.
  - 새 묶음을 더할 때마다 `Program.cs`에 직접 등록해야 합니다.
  - `OverlayElementTests.cs`는 913줄입니다.

## 목표 구조

**규칙 세 가지:**
1. 먼저 층으로 나눕니다.
2. 층 안에서는 기능 이름으로 나눕니다. 같은 기능은 모든 층에서 같은 이름을 씁니다(Judgement, Attempts, KeyLimiter, Overlay…).
3. 폴더마다 무엇이 들어가는지 한 줄 규칙을 둡니다.

```
Element/
  Domain/            순수 규칙. Unity 객체·파일·화면 글자 없음 (게임 enum은 허용)
    Overlay/           값, 수식(해석·계산·키패드 따로), 글자, 도형, 그라디언트, 폰트
    Layout/            배치, 맞춤, 정렬, 실행 취소 기록 UndoStack<T>
    KeyViewer/  Render/  Judgement/(콤보·타이밍 배율 포함)  Bpm/  Status/  Attempts/
    KeyLimiter/  Tweaks/  Blocks/
  Application/       기능별 사용 사례 + 그 기능이 필요한 포트(인터페이스)
    Overlay/  Judgement/  Attempts/  KeyLimiter/  Profiles/  Effects/  Fonts/
    Input/             InputGate (지금의 UIInteractionState)
    Settings/          SettingsMigrator (모든 버전 단계를 한 곳에서)
    Rendering/  Mods/  Updates/  Files/
  Configuration/     저장되는 필드와 Clone/SameAs/Normalize만. Element의 다른 층을 참조하지 않음
    Overlay/  Gameplay/  Effects/  Profiles/        (파일 하나에 타입 하나)
  Infrastructure/    바깥 상대별. 그 상대와 주고받는 코드만 두고, Application 포트를 구현
    Game/              게임 안
      Patches/           모든 Harmony 패치. 게임 클래스 하나에 파일 하나, 넘겨주기만 함
      Play/  Input/  Effects/  Planets/  Editor/  Recording/
    ElementNote/       App/  Ipc/  Viewers/  Keys/
    TufReplay/         API, 기록 읽기, 렌더, 입력 일시정지
    Mods/              UMM에서 ElementKit 모드 찾기
    Platform/          OS: 파일·폰트 선택 창, 트랙패드, 바로가기
    Storage/           모드 폴더의 파일: 시도 기록, 설정 백업, 폰트 묶음
    Updates/
  Presentation/
    Ui/                두 화면 이상이 쓰는 UI 부품. 기능도 ElementSettings도 모름
      Theme/             팔레트, 강조색, L(ko,en) 하나, 오류 색 하나
      Widgets/           팩토리, 버튼, 스위치, 슬라이더, 세그먼트, 값 줄, 단축키 칸, 토스트
      Popovers/          IPopover, PopoverStack, PopoverPlacement, ListMenu, 색·폰트·기준점 팝업
      Graphics/          벡터 그래픽 기반 + 아이콘 enum 하나
      ColorPicker/
    Settings/          설정 창만. 패널(틀·이동·Esc) + Pages/(탭 하나에 클래스 하나)
    Layout/            배치 편집기
      Canvas/            편집 세션: 선택, 끌기, 도구 막대, 요소 막대, 추가 메뉴, 상태 줄 (조각이 아니라 클래스로)
      Kinds/             요소 종류마다 설명서 하나. 종류를 아는 유일한 곳
      ContentEditors/    전체 화면 편집기 (IContentEditor)
        Shell/             공용 틀: OverlayEditorFrame, 편집기 키 처리
        Blocks/            작업대, 팔레트, 모양(TextLook, ShapeLook 따로), 미리보기
        Mods/              모드 요소 편집 화면, 부품 하나에 파일 하나
      Graphics/          테두리, 간격 같은 그리기 전용
    Overlay/           게임 화면에 그리기. Layout과 Settings를 참조하지 않음
      Canvas/            캔버스, 종류별 생성 목록
      Elements/  Text/  Shape/  Mods/  Blocks/
      Values/            값 쓰기 (기능마다 하나)
      Fonts/  Migration/
    Notices/           플레이어에게 보이는 결과 문구 (지금의 Rendering/)
  Bootstrap/         조립만: 만들고, 잇고, 정리. 게임 읽기·화면·규칙 없음
    Main.cs            UMM 입구 (공개 계약이라 이름과 네임스페이스 유지)
    Api/               다른 모드가 리플렉션으로 붙는 API (AttemptCounter)
    Composition/       영역마다 조립기 하나: 모드, 오버레이, 설정, 리플레이
    Hosting/           PatchGroups, FrameLoop (매 프레임 목록 하나), 실패 제한
```

**ElementKit**(아직 공개 전이라 지금 바꾸기 좋음):

```
ElementKit/          네임스페이스는 ElementKit 하나 (폴더는 파일 정리용)
  Kit.cs
  Values/            값 종류, ValueGroup
  Elements/          ElementAttribute, OverlayElement(<T>), Anchor
  Editing/           EditorPage, EditorTabs, EditorControl, EditorIcon
  Drawing/           Drawing, RoundedBox, Paint
  Input/             Shortcut
  Hosting/           내부 전용: Kit.Attach가 쓰는 상태만 (KitRegistry, ValueScanner)
```

- `Hosting/ElementClasses`는 Element만 쓰므로 `Element/Infrastructure/Mods`로 옮깁니다.
- `EditorPage`, `EditorControl`, `Drawing` 등은 다른 모드가 상속하지 못하게 생성자를 `internal`로 막습니다(Element는 InternalsVisibleTo로 접근). 상속을 허용하면 나중에 추상 멤버를 더할 때마다 그 모드들이 깨집니다.
- 다른 모드가 쓰는 공개 API가 `Element.AttemptCounter`와 ElementKit 두 가지입니다. 하나로 모을지 정해야 합니다.

**테스트:**

```
Element.Tests/
  Program.cs         실행만. 테스트 묶음은 리플렉션으로 찾음
  Support/           Assert(종류별 허용 오차 하나), TempFolder, Fakes/
  Domain/  Application/  Configuration/  Infrastructure/  Presentation/   Element와 같은 모양, 파일당 약 400줄 이하
```

**저장소:**
- `docs/`를 `features/`, `integrations/`, `research/`, `plans/`, `process/`, `releases/`, `media/`로 나눕니다.
- `scripts/`는 모든 명령이 `run.sh` → `workflows/` → `tasks/`를 거치게 하고, 제품 이름은 `element-note` 하나만 씁니다.

## 6. 옮기기 전에 먼저 막을 함정

- **프로필 전환이 소리 없이 깨질 수 있습니다.** `SettingsCopy.IsSettingsObject`는 네임스페이스가 `Element.Configuration`으로 시작하는 클래스만 "제자리에 복사"합니다. 설정 클래스를 다른 네임스페이스로 옮기면 객체가 통째로 바뀌어, 다른 곳에서 들고 있던 참조가 끊깁니다. 옮기기 전에 표식 특성으로 바꿉니다.
- **UI 검사가 깨집니다.** `scripts/ui-preview/prepare.mjs`가 소스 경로 약 25개를 직접 적어 두었습니다. 폴더를 옮길 때 함께 고쳐야 합니다.
- **다른 모드와의 연결이 끊길 수 있습니다.** `Element.Main`(런처가 리플렉션으로 부름)과 `Element.AttemptCounter`(다른 모드가 리플렉션으로 붙음)는 이름과 네임스페이스를 바꾸면 안 됩니다.
- **메인 체크아웃의 `Element.sln`은 커밋하지 않습니다.** 워크트리 경로가 들어간 변경이 커밋되지 않은 채 있어서, 커밋하면 솔루션이 깨집니다.

## 7. 구조를 보다가 찾은 성능 문제

| 문제 | 언제 | 확인 |
| --- | --- | --- |
| `DmNoteNativeIpcFeature.Tick`이 매 프레임 클로저와 델리게이트를 새로 만듦(`:391-394`) | 항상 | 확인함 |
| `NativeExclusions`가 매 프레임 `yield` 반복자를 만듦 | 배치 편집기가 열려 있을 때 | 확인함 |
| `UpdateToolbar`가 매 프레임 아이콘 약 12개마다 `GetComponentInParent`를 부름 | 배치 편집기가 열려 있을 때 | 보고만 받음 |
| `GameplayTweaksController`가 설정을 적용할 때마다 다시 정리함(색을 끄는 동안 매 샘플) | 설정 변경 중 | 보고만 받음 |
| 갤러리를 새로 그릴 때마다 `SettingsUiFactory`를 새로 만들어 둥근 모서리 텍스처를 다시 만듦 | 설정 화면 | 보고만 받음 |
| `OverlayFormula.TryEvaluate`가 계산할 때마다 숫자 문자열을 다시 해석함 | 수식이 있을 때 | 보고만 받음 |

## 8. 진행

`refactor/structure` 브랜치에서 영역마다 작업 폴더를 따로 두고 나눠 고친 뒤 합쳤습니다. 합칠 때마다 `mod-check`와 `ui-check`를 다시 돌렸고, 화면은 작업 전 스크린샷과 픽셀까지 같습니다.

| 단계 | 영역 | 한 일 | 상태 |
| --- | --- | --- | --- |
| 1 | core | `SettingsMigrator` 하나, `[CopiedInPlace]` 표식, 설정·프로필 파일 입출력을 Infrastructure로, `OverlayFormula`·`OverlayElementDefaults` 나누기, 판정 색 한 곳 | 완료 |
| 1 | runtime | 패치는 `Game/Patches`에서 `GameHooks`로, `ModRuntime`은 조립만(`FrameLoop`, `GameplaySession`), `RenderSession`, `InputGate`, 포트를 Application으로, IPC 채널 하나, Infrastructure를 상대별 폴더로 | 완료 |
| 1 | settings | `SettingsPanel`은 창 틀만, 페이지마다 클래스(`Pages/`), 설정 창 안 중복 정리, 프리셋 편집을 `Application/Effects`로 | 완료 |
| 1 | layout | 요소 종류(`Layout/Kinds`), 편집기 자리(`IContentEditor`), 배치 편집기를 부품 클래스로(`Layout/Canvas`), 팝업 하나(`Ui/Popovers`), `UndoStack<T>`, 모드 편집기 나누기 | 완료 |
| 1 | tooling | `scripts/` 규칙 하나, `docs/` 폴더, ElementKit 폴더와 `internal` 생성자 | 완료 |
| 2 | runtime2 | ElementNote 뷰어 코드를 일별로, 큰 Infrastructure 클래스 나누기, 내부 이름 ElementNote로 | 완료 |
| 2 | core2 | Domain·Application·Configuration을 기능 폴더로, 저장 형식 왕복 검사, 테스트를 층별 폴더·`[Suite]`·`Support/`로 | 완료 |
| 2 | editors | 글자·도형 편집기를 일별 클래스로(공통 부분은 `ContentEditors/Shell`) | 완료 |
| 2 | ui | 공용 UI 부품을 `Presentation/Ui`로, 아이콘 하나로, 색 선택기 나누기, `Layout/Editing` 정리 | 멈춤 |

남은 것:
- 위 표의 멈춤 항목
- 배치 편집기가 열려 있는 동안 `SetEditorExclusions`·`GameWebView.ExcludeEditor`가 매 프레임 메모리를 잡음
- `SettingsUiRuntime`에 남은 기능 코드(프리셋 단축키, 행성 미리보기 만들기)를 Presentation으로

## 9. 작업 규칙

- **동작은 그대로 둡니다.** 화면, 문구, 저장 파일 형식, 설정 XML이 바뀌면 안 됩니다. 고치기로 한 버그만 예외이고, 커밋 메시지에 따로 적습니다.
- **공개 이름은 바꾸지 않습니다.** `Element.Main`, `Element.AttemptCounter`, ElementKit의 공개 API, `Element.Configuration` 네임스페이스가 그 대상입니다(`SettingsCopy` 표식을 바꾸기 전까지).
- **의존 방향을 지킵니다.**
  - Configuration(저장되는 설정 클래스)은 Element의 다른 층을 모릅니다. 가장 안쪽입니다.
  - Domain은 Configuration만 압니다. 저장 설정 클래스를 그대로 작업 모델로 씁니다. Unity·게임의 enum은 쓸 수 있지만, 객체는 쓰지 않습니다.
  - Application은 Domain과 Configuration만 압니다.
  - Infrastructure와 Presentation은 Application·Domain·Configuration만 알고, 서로는 모릅니다.
  - Bootstrap은 모두를 압니다.
- **파일 하나에 타입 하나를 둡니다.** 서로 붙어 다니는 작은 요청·응답 타입만 한 파일에 모으고, 파일 이름을 그 묶음 이름으로 짓습니다. MonoBehaviour는 항상 따로 둡니다.
- **partial 조각을 새로 만들지 않습니다.** 일이 다르면 클래스를 나눕니다.
- **포트는 그 포트가 필요한 기능 폴더(Application/<기능>)에 둡니다.**
- **네임스페이스는 폴더 경로와 같게 합니다.**
- **매 프레임 도는 코드에서는 메모리를 새로 잡지 않습니다.** LINQ, 클로저, `yield`, 박싱을 쓰지 않고, 델리게이트는 한 번 만들어 둡니다.
- **UI 검사(`scripts/ui-preview`)는 비공개 필드와 메서드를 이름으로 찾습니다.** 이름을 바꾸거나 옮기면 검사도 함께 고치고 `ui-check`로 확인합니다.
- **검사와 커밋:** 바꾼 C# 파일은 csharpier로 정리합니다. `./scripts/run.sh mod-check`를 통과해야 하고, 화면을 건드렸으면 `./scripts/run.sh ui-check`도 통과해야 합니다. 커밋은 작게 나눠 `refactor(<영역>): …` 형식으로 남깁니다.
