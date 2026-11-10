# ElementKit 계획

> 상태: P0 완료, P1 중 값 구현(`feat/elementkit`), 오버레이 요소 프로토타입(`feat/elementkit-elements`) · 2026-11-10

다른 모드가 만든 오버레이도 Element 안에서 보이고, Element 방식대로 고칠 수 있게 합니다.
모드는 **값**, **프리셋**, **오버레이 요소**를 넣습니다. 배치와 편집 화면의 틀, 그리고 라벨·버튼·색 선택기 같은
부품은 Element가 줍니다. 요소를 그리고 부품으로 편집 화면을 꾸미는 일은 모드가 자유롭게 합니다. 요소만 넣는 모드는
Element를 필수로 두지 않아도 됩니다([오버레이 요소](#오버레이-요소)).

## 진행 상황

| 항목 | 상태 |
| --- | --- |
| 값 목록 128칸(`OverlayValueMask`), 모드 값 등록 | 완료 |
| 모드가 없을 때 값 보존(텍스트·수식 저장 왕복, 편집기에 `[이름]`, 오버레이엔 표시 안 함) | 완료 |
| ElementKit.dll: `Kit.Attach`, `ValueGroup`, `NumberValue`/`TimeValue`/`TextValue`, 품질 규칙 검사 | 완료 |
| Element 연결: 프레임마다 바뀐 값만 반영(할당 0), 꺼진 모드 숨김, 리로드·새 캔버스 | 완료 |
| 입력 독에 모드별 카테고리 | 완료 |
| ElementKit 이름으로 들어온 요청을 Element 것으로 연결, 다른 복사본 경고 | 완료 |
| 예시 모드 [Sample](../Sample): SampleTag 태그를 옮김 (`./scripts/run.sh sample-install`) | 완료 |
| 오버레이 요소: `[Element]` + `OverlayElement<TSettings>`, Element가 스스로 찾음(호출·필수 모드 없음) | 프로토타입 |
| 편집 화면 부품(`EditorPage`): 라벨·버튼·스위치·슬라이더·숫자·글자·고르기·색 선택기, 자유 배치(`At`) | 프로토타입 |
| 그리기 도우미(`Drawing`): 오버레이 폰트 글자, 둥근 상자, 하이라이트 색 | 프로토타입 |
| 요소 추가 → 기타 → 모드별 목록, 편집 화면(실시간 미리보기·실행 취소), 모드가 없을 때 상자 | 프로토타입 |
| Sample: 예시 막대 요소, Element 없이도 실행(`LoadAfter`, `ElementLink`) | 프로토타입 |
| 색 선택기의 하이라이트 따르기·그라디언트, 종류가 6개를 넘을 때 목록 스크롤 | 남음 |
| Element 없이 Sample을 켠 실제 게임 확인 | 남음 |
| 프리셋, 기타 메뉴, 프리셋 내보내기 | 남음 |
| `Player.Keys`, 묶음 값(`Numbers`), 실행 중 API 수준 확인 | 남음 |
| NuGet 패키지·템플릿, 개발자 모드 | 남음 |
| Element 버전 올리기(다음 버전, 1.5.0 예정)와 Sample의 `Requirements`를 그 버전으로 | 릴리스 때 |

## 왜 만드나

- 지금은 오버레이 모드마다 배치 방식이 따로 있습니다. 플레이어는 모드마다 다른 방법을 배워야 하고, 개발자는 매번 배치 UI를 새로 만듭니다.
- 복잡한 로직이 필요한 오버레이는 지금의 텍스트·도형 꾸미기만으로 만들 수 없습니다. 첫 사례는 **예시 오버레이**입니다. 타일 각도를 읽어 왼손·오른손을 각각 몇 키로 넘길지 보여 줍니다.
- 목표: 개발은 평소 오버레이 모드처럼 하고, 결과가 보이는 곳은 Element로 모읍니다. Element의 사용자 경험(무엇이든 고칠 수 있고, 일곱 살도 쓸 수 있음)은 그대로 지킵니다.

## 원칙

1. **편집 경험은 Element가 가집니다.** 배치, 편집 화면의 틀(어두운 배경, 취소·완료, 입력 독·키패드, 색 선택기, 실행 취소)과 그 안의 부품은 Element 것입니다.
2. **서드파티는 그리고 조합합니다.** 값·프리셋을 넣고, 요소를 넣을 때는 직접 그리고 Element 부품으로 편집 화면을 자유롭게 꾸밉니다.
3. **서드파티 코드는 평소 모드와 같습니다.** 얼불춤(Assembly-CSharp)을 참조하고 Harmony로 패치해도 됩니다.
4. **게임을 느리게 하지 않습니다.** 바뀐 것만 다시 그리고, 서드파티 쪽 문제는 그 모드 안에서 멈춥니다.
5. **모드를 지워도 플레이어가 꾸민 것은 남습니다.** 다시 설치하면 그대로 돌아옵니다.

## 층 구조

```
           플레이어가 만지는 층
┌──────────────────────────────────────────────────────┐
│ 배치      위치 · 앵커 · 커지는 기준 · 정렬 · 여러 개 선택  (Element) │
│ 편집 화면  틀 · 입력 독 · 키패드 · 색 선택기 · 부품     (Element) │
│           부품을 놓을 자리 고르기              ◀── 서드파티 │
│ 그리기    텍스트 · 도형 · 키뷰어                 (Element) │
│           모드 요소                          ◀── 서드파티 │
├──────────────────────────────────────────────────────┤
│ 값        기본 값 28개 + 모드가 추가한 값       ◀── 서드파티 │
│ 프리셋    처음 놓일 요소 묶음                  ◀── 서드파티 │
└──────────────────────────────────────────────────────┘
     게임 데이터: 서드파티가 얼불춤에서 직접 읽음 (Harmony 등)
```

텍스트·도형 조립으로 되는 오버레이는 값과 프리셋으로 만들고, 그걸로 부족한 것만 오버레이 요소로 만듭니다. 여러 모드에 흔히 필요한 것은 **Element 기본 기능을 늘려서** 메웁니다([Element 기본 기능 확장](#element-기본-기능-확장)). 그러면 모든 플레이어가 덕을 봅니다.

## 서드파티가 할 수 있는 것, 하면 안 되는 것

| 할 수 있음 | 하면 안 됨 |
| --- | --- |
| 얼불춤(Assembly-CSharp)·UnityEngine 참조, Harmony 패치 | **Element.dll 내부 참조.** 매 업데이트마다 바뀌고 거의 다 `internal`이며, 라이선스상 다른 모드가 쓸 수 없습니다 |
| 자기 로직으로 값을 계산해 `Set` | **ElementKit.dll을 자기 모드 폴더에 같이 넣기.** 같은 DLL이 두 번 로드되어 타입이 서로 맞지 않습니다([로딩과 버전](#로딩과-버전)) |
| 값과 프리셋 등록 | 자기 배치 UI 만들기. 막지는 않지만 ElementKit의 목적과 맞지 않습니다 |

## 구성

### ElementKit.dll

- **역할:** Element가 서드파티에게 여는 작은 공개 DLL입니다. 실제 동작은 Element.dll이 구현합니다.
- **라이선스:** MIT. Element 루트 라이선스는 다른 모드가 일부라도 쓰는 것을 금지하므로, 참조용 DLL은 따로 풀어야 합니다. 저장소 안 `ElementKit/` 폴더에 자체 `LICENSE`를 둡니다.
- **참조:** UnityEngine만 참조합니다. 공개 API에 `scrFloor`, `HitMargin` 같은 얼불춤 타입을 드러내지 않아서, 얼불춤이 업데이트돼도 ElementKit 계약은 깨지지 않습니다. 서드파티 모드 자신은 얼불춤을 자유롭게 참조합니다.
- **버전:**
  - 1.x 동안 `AssemblyVersion`을 1.0.0.0으로 고정합니다. 실제 버전은 FileVersion과 InformationalVersion에 적습니다.
  - API는 추가만 합니다. PublicApiAnalyzers로 공개 API를 기록해서, 깨는 변경이 들어가면 빌드가 실패하게 합니다.
- **등록 정보 보관:** ElementKit 쪽 static에 둡니다. Element를 UMM에서 껐다 켜도(`ModRuntime` 재생성) 등록이 남아 있습니다.
- **네임스페이스:**
  - `ElementKit`: `Kit.Attach`, `Player`(Element만 아는 정보, 남음)
  - `ElementKit.Values`: 값 등록과 갱신
  - `ElementKit.Presets`: 프리셋 형식(대개 파일로 넣으므로 코드에서 쓸 일은 드묾)

### 설치 위치

```
Mods/Element/
  Info.json
  Runtime/versions/<v>/
    Element.dll         구현 (internal, 업데이트마다 바뀜)
    ElementKit.dll      공개 API (MIT, AssemblyVersion 1.0.0.0 고정)
Mods/Sample/
  Info.json             "Requirements": ["Element-1.3.0"]   ← 빌드가 자동으로 채움
  Sample.dll         ElementKit은 컴파일 때만 참조, 배포하지 않음
  Presets/예시 표시.json
```

### 로딩과 버전

아래는 이 저장소 코드와 UMM(`UserLibs/UnityModManager.dll`) 디컴파일로 확인한 사실입니다.

- **Element 로딩:** 런처가 `Runtime/versions/<v>/Element.dll`을 `Assembly.LoadFrom`으로 불러옵니다. 의존 DLL은 같은 폴더에서 이름으로 찾습니다([`PayloadLoader.cs`](../Element.Launcher/PayloadLoader.cs)). ElementKit.dll도 이 폴더에 두면 Element가 그대로 불러옵니다.
- **최소 버전 표기:** UMM은 `Requirements`의 `Id-1.2.3` 형식을 읽습니다. 필요한 모드를 먼저 로드하고(위상 정렬), 꺼져 있으면 켭니다. 버전이 모자라면 그 모드는 로드하지 않습니다.
- **어셈블리 연결:** UMM의 `AssemblyResolve`는 이미 로드된 어셈블리를 **FullName(버전 포함)** 으로 찾습니다. `AssemblyVersion`을 고정하면 서드파티의 `ElementKit` 참조가 Element가 불러온 것에 그대로 연결됩니다. Element도 이름만으로 찾는 자체 리졸버를 하나 더 둡니다.
- **두 번 로드되는 함정:** UMM은 모드 DLL을 `.cache` 복사본으로 `LoadFrom` 합니다. 모드 폴더에 ElementKit.dll이 있으면 그 사본이 따로 로드되어 타입이 갈라질 수 있습니다. 세 겹으로 막습니다.
  - NuGet 패키지는 컴파일 전용으로 참조합니다.
  - ElementKit.dll이 빌드 결과물에 섞이면 빌드 에러를 냅니다.
  - Element가 실행 중에 ElementKit 어셈블리가 둘 이상인지 확인하고, 어느 모드가 넣었는지 이름을 대서 알려 줍니다.
- **핫 리로드:** `OnUnload`가 있는 모드는 UMM에 Reload 버튼이 생깁니다. `Attach`가 `OnUnload`를 채워 주므로 모든 ElementKit 모드는 기본으로 리로드할 수 있습니다.
- **실행 중 버전 확인:** UMM의 버전 확인은 게임 시작 시점의 `Info.json`을 읽습니다. 자동 업데이트가 같은 실행에서 새 Element를 적용하면 이 확인이 어긋날 수 있습니다. 그래서 `Attach`가 실행 중에 API 수준을 한 번 더 확인합니다.

## 개발자 API

### 시작

```csharp
public static class Main
{
    public static bool Load(UnityModManager.ModEntry mod)
    {
        new Harmony(mod.Info.Id).PatchAll();   // 평소처럼
        return Kit.Attach(mod);                // 이 어셈블리의 값과 Presets 폴더를 등록
    }
}
```

진입점 클래스 이름이 `Kit`인 이유: 네임스페이스와 같은 이름의 클래스(`ElementKit.ElementKit`)는 `using ElementKit;` 뒤에서 이름이 부딪혀 쓸 수 없습니다.

`Attach`가 하는 일:

- 어셈블리에서 값 그룹을 찾아 등록합니다.
- 모드 폴더의 `Presets/*.json`을 등록합니다.
- 모드를 끄거나 리로드·언로드할 때 등록을 정리합니다.
- 실행 중인 Element가 이 모드가 요구하는 API 수준을 갖췄는지 확인합니다.

### 값

```csharp
// static readonly라서 null이 될 일이 없습니다. Attach가 이 필드들을 찾아 등록합니다.
public static class SampleValues
{
    public static readonly ValueGroup Group = new("예시", "Sample");   // 입력 독의 제목

    public static readonly TextValue   Played = Group.Text("sample",  "예시",      "Sample",          sample: "44 44 32 32");
    public static readonly TextValue   Pair   = Group.Text("pair",   "지금 예시", "Pair now",       sample: "44");
    public static readonly NumberValue Keys   = Group.Number("keys", "이번 박 키", "Keys this beat", sample: 4);
}
```

- **종류:** Element 기본 값과 같은 세 가지입니다. 숫자(`Number`), 시간(`Time`, 초 단위, m:ss로 표시), 글자(`Text`, 계산 불가). 글자 값 안의 `<b>` 같은 표시는 읽지 않고 그대로 보여 줍니다.
- **묶음 값(남음):** `Numbers`는 "다음 8타일" 같은 이어진 값입니다. 입력 독에는 한 줄로 보이고 번호를 고릅니다.
- **저장 키:** `<모드Id>/<키>` 형식으로 저장합니다(예: `{Sample/pair}`). 모드끼리 이름이 겹치지 않습니다.
- **`Set` 동작:**
  - 같은 값이면 아무 일도 하지 않습니다. 할당도 없고 다시 그리지도 않습니다.
  - 값이 바뀌면 그 값을 쓰는 텍스트·도형만 다시 그립니다. 지금의 변경 비트 방식을 그대로 씁니다.
  - Element가 꺼져 있거나 `Attach` 전에 불려도 오류 없이 마지막 값만 기억합니다.
  - 메인 스레드가 아닌 곳(입력 스레드 등)에서 불러도 다음 프레임에 반영됩니다.
- **예시 값(`sample`) 필수:** 맵을 켜지 않아도 편집기 미리보기가 보여야 하기 때문입니다.

값은 어디서든 `Set`할 수 있습니다. 자기 Harmony 패치 안에서 불러도 되고, UMM의 `OnUpdate`처럼 매 프레임 불러도 됩니다.

```csharp
mod.OnUpdate = (_, _) =>
{
    int tile = scrController.instance.currentSeqID;   // 레벨을 시작할 때 listFloors로 미리 세어 둠
    SampleValues.Pair.Set(Chart.PairAt(tile));          // 같은 값이면 아무 일도 없음
    SampleValues.Keys.Set(Chart.KeysAt(tile));
};
```

### 프리셋

- **정의:** 프리셋은 처음 놓일 텍스트·도형의 묶음입니다. 서로의 상대 위치, 글자, 색, 크기, 그리고 값 참조를 담습니다.
- **만드는 법:** 손으로 쓰지 않습니다. 게임 안 Element 편집기에서 배치한 뒤 **개발자 모드 → 프리셋으로 내보내기**로 JSON을 만듭니다.
- **등록:** 모드 폴더의 `Presets/*.json`을 자동으로 등록합니다. 코드는 필요 없습니다.
- **형식:** 버전이 붙은 JSON입니다. Element 내부 설정 클래스를 그대로 드러내지 않고 따로 정의한 형식이라, Element 내부가 바뀌어도 프리셋은 그대로 읽힙니다.
- **추가할 때는 복사본:** 플레이어가 추가하면 평범한 텍스트·도형이 됩니다. 나중에 모드가 프리셋을 바꿔도, 이미 놓고 꾸민 요소는 그대로입니다.

### Element만 아는 정보

얼불춤에서 직접 읽을 수 있는 것은 넣지 않고, Element만 가진 것만 엽니다.

- **`Player.Keys`:** ElementNote의 현재 키 목록입니다(몇 키를 쓰는지, 키 이름). Element가 이미 키 리미터로 받고 있습니다([`KeyLimiterContracts.cs`](../Element/Application/KeyLimiter/KeyLimiterContracts.cs)). ElementNote가 연결되지 않았으면 `null`입니다.
- **기본 값 읽기:** 정확도, 진행도처럼 Element가 이미 계산한 값을 읽습니다.

## 개발 흐름

목표는 5분 안에 내 값이 게임 화면에 나오는 것입니다.

```bash
dotnet new install ElementKit.Templates
```

```bash
dotnet new element-mod -n Sample
```

```bash
dotnet build
```

```
1. 로직 작성       평소 모드처럼 (얼불춤 참조, Harmony 패치). UI 코드 없음
2. dotnet build    ADOFAI 경로 자동 탐지 → Mods/Sample에 설치
3. UMM에서 Reload  입력 독에 '예시' 값이 바로 생김
4. 게임 안에서     Element 편집기로 텍스트·도형을 조립 (내 값 + 기본 값)
5. 개발자 모드     선택한 요소 → 프리셋으로 내보내기 → Presets/예시 표시.json
6. 배포            플레이어: 요소 추가 → 기타 → '예시 표시'
```

NuGet 패키지(`ElementKit`)가 해 주는 일:

- **경로:** ADOFAI 설치 경로를 자동으로 찾습니다(macOS·Windows Steam 기본 위치). `ADOFAI_DIR`로 덮어쓸 수 있습니다.
- **참조:** Unity, 얼불춤, UMM, Harmony 참조를 자동으로 붙입니다. `HintPath`를 직접 쓸 필요가 없습니다.
- **Info.json:** 프로젝트 속성으로 `Info.json`을 만듭니다. `Requirements`의 Element 최소 버전은 참조한 ElementKit 버전에서 자동으로 정합니다.
- **설치:** 빌드하면 모드 폴더에 설치합니다. 게임이 켜져 있으면 "UMM에서 Reload를 누르세요"를 출력합니다.
- **배포:** Release 빌드로 배포용 zip을 만듭니다.
- **실수 방지:** ElementKit.dll이 출력에 섞이면 빌드 에러를 냅니다.

**개발자 모드**(Element 설정 → 일반 → 개발자, 기본 꺼짐):

- 등록된 모드, 값, 프리셋 목록
- 값의 현재 값을 실시간으로 보여 줌
- 값을 직접 덮어써서 프리셋 모양 확인(플레이 없이)
- 오류 내역(스택 복사)
- 모드별 `Set` 횟수와 비용(개발자 모드일 때만 측정)
- 선택한 요소를 프리셋으로 내보내기

## 오버레이 요소

평소 모드처럼 개발합니다. 얼불춤을 참조하고 Harmony로 패치하고, 결과를 그리는 클래스 하나에 `[Element]`를 붙이면
Element가 그 요소를 배치하고 편집할 수 있게 합니다. 모드가 Element에서 정보를 가져올 필요는 없습니다.

```csharp
[Serializable]
public sealed class BarsSettings            // JsonUtility로 저장: 숫자, bool, 문자열, Color, 리스트…
{
    public Color Left = new(1f, .62f, .26f);
    public int Groups = 16;
}

[Element("bars", "예시 막대", "Sample bars")]
public sealed class SampleBars : OverlayElement<BarsSettings>
{
    protected override void OnCreate() { /* Root 아래에 UGUI를 만듦. Drawing.Box/Text 도우미 */ }
    protected override void OnSettingsChanged() => Size = new Vector2(480, 96);
    protected override void OnUpdate() { /* 매 프레임 모드 상태를 읽어 갱신 */ }

    protected override void OnEdit(EditorPage page)   // Element 부품으로 편집 화면을 꾸밈
    {
        page.ColorPicker(page.L("왼손 색", "Left hand"), Settings.Left, c => Settings.Left = c);
        page.Slider(page.L("묶음 수", "Groups"), Settings.Groups, 2, 64, v => Settings.Groups = (int)v, step: 1);
        page.Label("…").At(x, y);                    // 아무 데나 놓기도 됨
    }
}
```

**순서:** 놓인 요소마다 인스턴스 하나. `LoadSettings` → `OnCreate` → `OnSettingsChanged` → 보이는 동안 매 프레임 `OnUpdate`.
`IsPreview`는 Element 편집기에서 보일 때 참이라, 데이터가 없으면 예시를 그리면 됩니다.

**편집 화면:** Element가 틀(어두운 배경, 취소·완료, Esc, 실행 취소, 기준점)과 **실시간 미리보기**를 맡습니다.
미리보기는 같은 클래스의 두 번째 인스턴스라, 모드는 따로 만들 것이 없습니다. `OnEdit`는 그 인스턴스에서 불리고,
부품 콜백에서 `Settings`를 바꾸면 Element가 실행 취소를 기록하고 미리보기를 갱신합니다. 완료하면 요소에 저장합니다.

| 부품 | 쓰는 것 |
| --- | --- |
| `Label`, `Button` | 설명 글, 버튼(누른 뒤 부품을 다시 만들어 바뀐 값을 보여 줌) |
| `Switch`, `Slider`, `Number`, `TextField`, `Choice` | 설정 화면의 스위치·슬라이더·입력칸·세그먼트 |
| `ColorPicker` | Element 색 선택기(단색·불투명도) |
| `Gap`, `.At(x, y)`, `.Width(w)`, `Root` | 칸 띄우기, 자유 배치, 직접 만든 UI |

부품은 기본으로 미리보기 아래 세로줄(가득 차면 다음 줄)에 놓이고, 좌표는 편집 화면 단위(약 1120×760, 왼쪽 위 0)입니다.

**그리기 도우미(`Drawing`):** `Text`(오버레이 기본 폰트, 플레이어가 폰트를 바꾸면 따라감), `Box`(크기를 바꾸면 바로 따라가는 둥근 상자),
`Highlight`, `Place`(왼쪽 위 기준 배치). 그 밖은 UGUI를 그대로 씁니다. 그린 것은 오버레이 캔버스 안이라 녹화 모드·렌더를 따릅니다.

**저장:** 설정은 요소마다 `OverlayLayoutSettings.Mod.Data`에 문자열로 저장되어 실행 취소, 취소 버튼, 프로필, 내보내기를 따릅니다.
나중 버전에 필드를 더하면 예전 요소에서는 기본값으로 시작합니다.

**Element 없이 실행:** 요소만 넣는 모드는 Element를 필수로 두지 않습니다.
- ElementKit은 컴파일할 때만 참조(`Private=false`)하고, `Info.json`에 `"LoadAfter": ["Element"]`만 둡니다.
- Element가 켜질 때와 모드가 나중에 로드될 때 UMM 모드 목록에서 ElementKit을 참조한 어셈블리를 찾아 `[Element]` 클래스를 등록합니다. 모드는 아무것도 부르지 않습니다.
- Element가 없으면 아무도 그 클래스를 만들지 않습니다. 모드 본체가 그 클래스를 부르지 않고, 요소가 모드 상태를 읽어 가는 방향이면 됩니다.
- Harmony `PatchAll`은 ElementKit이 없을 때 그 클래스만 건너뛰고 나머지를 패치합니다(이 게임의 0Harmony 2.3.6으로 확인).
- 값(`Kit.Attach`, `Value.Set`)은 ElementKit을 직접 부르므로, Element 없이도 돌려면 Sample의 `ElementLink`처럼 한 클래스에 모아 Element가 있을 때만 부릅니다.

**모드가 없거나 꺼졌을 때, 오류:** 게임에서는 보이지 않고 편집기에서는 마지막 크기의 상자에 "Sample 모드가 필요해요"를 띄웁니다.
이름·위치·설정은 그대로라 다시 설치하면 돌아옵니다. 모드 코드에서 난 오류는 그 요소만 멈추고 모드 로그에 한 번 남깁니다.

**등록 검사:** 키 형식과 중복, 한국어·영어 이름, 인자 없는 생성자. `[Element]`가 빠진 `OverlayElement`도 알려 줍니다.

## 플레이어 경험

- **입력 독:** 기본 카테고리(진행도, 판정, 시간, 속도, 곡, 시스템) 뒤에 모드별 카테고리가 붙습니다. 모드 값은 기본 값과 똑같이 넣고, 끌어 놓고, 계산할 수 있습니다. 마우스를 올리면 어느 모드의 값인지 보입니다.
- **요소 추가 → 기타:** 프리셋이 하나 이상 있을 때만 "기타" 행이 생깁니다. 누르면 도형 종류 메뉴처럼 옆에 목록이 열립니다. 모드가 여러 개면 모드별로 묶고, 많으면 스크롤합니다.

```
┌ 요소 추가 ───────────────────────┐  ┌ 기타 ───────────────────────┐
│ T  텍스트                         │  │ ✋ 예시 표시                  │
│ ⌨  키뷰어                         │  │    왼손·오른손 키 수와 다음 예시 │
│ ◼  도형                           │  │    Sample                 │
│ ⋯  기타                       ›   │  │ …                            │
│    다른 모드가 추가한 프리셋 2개    │  │                              │
└───────────────────────────────────┘  └──────────────────────────────┘
```

- **프리셋 추가:** 화면 가운데에 놓이고, 묶음 전체가 선택된 상태로 추가됩니다(실행 취소 한 번에 되돌림). 그다음은 평범한 텍스트·도형이라 무엇이든 고칠 수 있습니다.
- **모드가 없을 때:**
  - 오버레이에서는 그 값 자리에 아무것도 표시하지 않습니다(지금의 "데이터가 아직 없으면 표시하지 않음" 규칙).
  - 편집기에서는 `[왼손 키]`처럼 이름을 괄호에 넣어 보여 줘서 찾고 지울 수 있습니다. 이번 실행에서 한 번도 등록되지 않은 값은 `[Sample/left-keys]`처럼 키로 보여서 어느 모드가 필요한지 알 수 있습니다.
  - 저장된 내용은 지우지 않아서, 다시 설치하면 돌아옵니다.
- **오류 메시지**([토스 원칙](https://toss.tech/article/21021): 무슨 일이 있었는지와 무엇을 하면 되는지):
  - "Sample 모드에서 오류가 나서 값을 멈췄어요. 모드를 업데이트하거나 만든 분께 알려 주세요."
  - "Sample은 Element 1.4 이상이 필요해요. 게임을 다시 켜면 업데이트가 적용돼요."
  - 개발자 로그: "Sample 폴더에 ElementKit.dll이 있어요. 이 파일을 지우면 정상 동작해요."

## 품질 규칙 (등록할 때 검사)

일곱 살도 쓸 수 있는 경험을 지키려고 SDK가 등록할 때 검사합니다. 어기면 개발자에게 이유와 고칠 방법을 알려 줍니다.

- **값 이름:** 한국어·영어 이름이 둘 다 있어야 하고, 짧고 쉬운 말이어야 합니다("왼손 키"는 되고 "LH_cnt"는 안 됩니다).
- **예시 값:** 반드시 있어야 합니다.
- **단위 금지:** `%`, `키` 같은 단위는 값에 넣지 않습니다. 단위는 플레이어가 글자로 쓰는 것이 Element 규칙입니다.
- **키 형식:** 소문자와 `-`만 씁니다. 한 모드 안에서 겹치면 안 됩니다.
- **프리셋:** 있는 값만 참조해야 하고, 요소는 20개 이하여야 합니다.

## 첫 사례: 예시 오버레이

```
Sample/ (이 저장소 안의 별도 모드, ElementKit 공개 API만 사용)
 ├ Planning/SampleUnits    묶음 단위: 180° × threshold, BPM 1050 이상이면 threshold를 두 배씩
 ├ Planning/SampleChart    순수 C#: 타일(키 수, 다음 타일까지 묶음 수) → 묶음별 키 수
 ├ Planning/SampleText     SampleTag 태그와 같은 글 모양
 ├ Game/LevelSteps        scrLevelMaker.instance.listFloors → 시작 타일 다음부터의 타일
 ├ Game/SampleSession      시작할 때 한 번 세고, 매 프레임 타일 번호만 비교(묶음이 끝날 때만 글을 새로 씀)
 ├ Game/SamplePatches      scnGame.Play · scrPressToStart.ShowText · 나가기
 └ SampleValues            예시 · 앞으로 예시 · 지금 예시 · 다음 예시 · 이번 박 키
```

**규칙**(사용자의 Overlayer 태그 `SampleTag`를 옮김):

1. **묶음:** 반 바퀴(180°)가 한 묶음입니다. 타일 BPM(피치 제외)이 1050 이상이면 1050 아래가 될 때까지 묶음을 두 배씩 늘립니다(2021 BPM이면 360°). 묶음의 실제 길이는 BPM이 달라도 거의 같습니다.
2. **세기:** 플레이를 시작한 타일 다음 타일이 0입니다. 타일마다 다음 타일까지 돈 만큼 쌓고, 각 타격은 그 순간이 들어 있는 묶음의 키로 셉니다. 경계에 놓인 타격은 다음 묶음 몫이고, 쉬는 묶음은 0입니다.
3. **글 모양:** 묶음 두 개씩 `44`처럼 붙여 쓰고, 한쪽이 8을 넘으면 `[10 8]`로 씁니다. 짝마다 띄우고 네 짝마다 줄을 바꿉니다. 지난 묶음은 34개까지 두고, 넘으면 오래된 8개를 지웁니다. 마지막 짝은 그다음 묶음이 끝나야 보입니다.
4. **다시 시작:** 체크포인트로 돌아가면 그 타일부터 다시 셉니다(태그가 재시작 때 초기화하는 것과 같음).

태그와 다른 점:

- 태그는 매 프레임 `CurTile`이 바뀌었는지 보고 그 타일 하나만 더합니다. 빠른 채보에서 한 프레임에 타일이 둘 이상 지나가면 키와 각도를 빠뜨립니다. Sample은 시작할 때 타일 시각으로 전부 세어 두므로 빠뜨리지 않습니다.
- 태그는 타일마다 각도를 정수로 반올림해서 22.5° 같은 각도가 쌓이면 묶음 경계가 밀립니다. Sample은 반올림하지 않습니다.
- 미드스핀 타일은 누르지 않으므로 키로 세지 않습니다. 동시 입력이 필요한 타일(`tapsNeeded`)은 그 수만큼 셉니다.

모든 타일을 보는 조건에서 태그 로직을 C#으로 옮긴 모델과 비교했습니다. 1000 → 2021 BPM 변속이 있는 약 300타일 채보에서, 매 타격 뒤의 글이 한 글자도 다르지 않습니다([SampleTests](../Element.Tests/SampleTests.cs)).

| 값 | 내용 |
| --- | --- |
| `{예시}` | 태그와 같은 글: 지난 묶음들, 네 짝마다 줄바꿈 |
| `{앞으로 예시}` | 다음 타격이 든 짝부터 32묶음, 같은 모양 |
| `{지금 예시}` / `{다음 예시}` | 다음 타격이 든 짝과 그다음 짝, 예: `44`, `[10 8]` |
| `{이번 박 키}` | 다음 타격이 든 묶음의 키 수 |

## Element 쪽 변경

### 값 목록 동적화

- **지금 구조:** `OverlayValueId` enum과 64비트 `ulong` 마스크이고, 28칸을 씁니다([`OverlayValueCatalog.cs`](../Element/Domain/Elements/OverlayValueCatalog.cs)).
- **바꿀 점:**
  - 기본 값은 지금 번호(0~27)를 유지합니다. 모드 값은 등록할 때 28번부터 번호를 받습니다.
  - `OverlayValues` 배열은 필요한 만큼 늘어납니다.
  - `ulong` 마스크를 `ValueMask`(128비트) 구조체로 감쌉니다. 손댈 곳은 10개 파일 정도입니다(`OverlayValues`, `OverlayFormula`, `OverlayTextComposer`, `OverlayShapeResolver`, 텍스트·도형 요소, 편집기).
  - 입력 독 카테고리를 고정 목록에서 동적 목록으로 바꿉니다.
- **성능:** 다시 그리는 판단은 지금처럼 마스크 비교 한 번입니다.

### 없는 값 보존

- **문제:** 지금 수식 파서는 모르는 값 키를 지웁니다([`OverlayFormula.cs` 90행](../Element/Domain/Elements/OverlayFormula.cs#L90)). 모드를 끈 채로 텍스트를 편집하고 저장하면 모드 값이 영구히 사라집니다.
- **고칠 방법:**
  - `모드Id/키` 형식의 모르는 키는 "없는 값" 토큰으로 남겨, 저장할 때 그대로 다시 씁니다.
  - 오버레이에서는 표시하지 않고, 편집기에서는 `[이름]`으로 보입니다.
  - 예전에 없어진 기본 값은 지금처럼 지웁니다.
- **함께 확인할 것:** 설정에 저장된 모르는 요소 종류(`TemplateId`)는 지금도 지우지 않습니다([`OverlayCanvasHost.ConfigureLayouts`](../Element/Presentation/Overlay/Shared/OverlayCanvasHost.cs)).

### 프리셋

- **형식:** 프리셋 JSON 형식을 정의합니다. 기본 배치([`OverlayElementDefaults`](../Element/Domain/Elements/OverlayElementDefaults.cs), 진행도 바 3개 도형 등)도 같은 형식으로 옮겨, Element도 자기 기능을 같은 길로 씁니다.
- **추가 메뉴:** 요소 추가 메뉴에 "기타" 행과 옆 목록을 붙입니다. 도형 종류 메뉴([`OverlayLayoutEditor.ShapeEditor.cs`](../Element/Presentation/Layout/OverlayLayoutEditor.ShapeEditor.cs)의 `ToggleShapeMenu`) 방식을 재사용하고, 메뉴 높이는 3행 또는 4행이 됩니다.
- **추가 동작:** 프리셋을 추가하면 요소들을 화면 가운데에 놓고 모두 선택합니다. 히스토리에는 한 번만 기록해서 실행 취소 한 번에 되돌립니다.
- **내보내기:** 개발자 모드에서 선택한 요소를 상대 위치로 바꿔 JSON으로 씁니다. 파일로 저장하고 경로를 보여 주며, 클립보드에도 복사합니다.

### Element 기본 기능 확장

복잡한 오버레이가 조립만으로 부족해지는 지점은 Element 자체 기능으로 메웁니다.

1. **위치가 값을 따라 움직이기:** 지금 값을 받는 속성은 크기, 둥글기, 테두리 두께, 불투명도, 글자 크기뿐입니다. 판정 바 눈금이나 움직이는 표시를 만들려면 위치도 값을 받아야 합니다.
2. **조건에 따라 색 바꾸기:** 예를 들어 왼손이면 주황, 오른손이면 파랑. 지금은 불투명도로만 흉내 낼 수 있습니다.
3. **묶음(그룹):** 프리셋을 하나처럼 옮기는 기능입니다. 지금은 여러 개 선택으로만 함께 움직입니다.

### 그 밖의 변경

- **ElementKit 구현:** Element.dll에 둡니다. ElementKit의 등록 정보를 읽어 값 목록, 입력 독, 기타 메뉴에 반영합니다.
- **어셈블리:** ElementKit 리졸버와 중복 로드 감지를 추가합니다.
- **패키징:** [`ModLayout`](../Element.Updater/Shared/ModLayout.cs)과 `./scripts/run.sh`에 ElementKit.dll을 추가하고, NuGet 패키지와 템플릿을 만드는 명령을 넣습니다.
- **테스트(`Element.Tests`):**
  - 값 등록·해제·리로드
  - 없는 값의 저장 왕복
  - `ValueMask`로 다시 그리기 판단
  - 프리셋 읽기와 추가(실행 취소 한 번)
  - 예외 격리
  - 기타 행이 프리셋이 없으면 숨는지
  - 지금 `PerformanceTests`에 모드 값 시나리오 추가

## 안정성과 성능

- **격리:** 서드파티 코드가 Element 안에서 실행되는 지점(등록, 프리셋 읽기)은 모두 try/catch로 감쌉니다. 문제가 생기면 그 모드의 값·프리셋만 멈추고, 게임과 다른 요소는 그대로 동작합니다.
  - 서드파티가 자기 Harmony 패치에서 일으킨 오류는 다른 모드와 마찬가지로 그 모드가 맡습니다.
  - UMM 모드는 같은 프로세스에서 돌기 때문에, 이 격리는 보안이 아니라 안정성을 위한 것입니다.
- **할당 없음:** `Set`은 바뀌지 않으면 할당 없이 끝납니다. 바뀌면 변경 비트 하나만 켭니다.
- **다시 그리기:** 그 값을 쓰는 요소만 다시 그립니다. 지금 기본 값과 같은 경로입니다.
- **측정:** 비용은 개발자 모드일 때만 잽니다.

## 단계

| 단계 | 내용 | 예시 모드 |
| --- | --- | --- |
| **P0** Element 내부 준비 | 값 목록 동적화(`ValueMask`), 없는 값 보존, 프리셋 형식(기본 배치도 같은 형식으로), 테스트. 플레이어가 보기엔 변화 없음 | 없음 |
| **P1** ElementKit 1.0 (Element 1.3.0) | ElementKit.dll, `Attach`, 값 등록·`Set`, 입력 독 카테고리, 프리셋 등록과 기타 메뉴, 프리셋 내보내기, `Player.Keys`, 오류 격리·버전 확인·중복 DLL 감지, NuGet·템플릿·핫 리로드, 빠른 시작 문서 | **v1: 텍스트** |
| **P2** | 개발자 모드 전체(값 보기·덮어쓰기, 오류, 비용), 묶음 값(`Numbers`), 값 따라 위치, 조건 색, 묶음 | **v2: 손 타임라인** |
| **P3** | 오버레이 요소 정식화: 색 선택기의 하이라이트·그라디언트, 부품 추가(폰트 목록, 숫자 키패드), NuGet 템플릿 | **예시 막대** 정식 |

## 정할 것

| 항목 | 추천 | 이유 |
| --- | --- | --- |
| ElementKit 라이선스 | MIT | 루트 라이선스로는 다른 모드가 참조할 수 없음 |
| 배포 | NuGet.org | `dotnet add package ElementKit` 한 줄. 릴리스에 NuGet API 키 필요 |
| 위치 | 이 저장소 `ElementKit/` | Element와 함께 테스트하고 버전 맞추기 쉬움 |
| 이름 | `ElementKit` + `Values` / `Presets` | 패키지 하나, `Attach` 한 번. `Element.` 접두어는 Element.dll 네임스페이스와 헷갈림 |
| 프리셋 | 추가할 때 복사본 | 모드 업데이트가 플레이어가 꾸민 것을 덮어쓰지 않음 |
| 값 한도 | 128(기본 + 모드) | 마스크 비교 비용은 거의 같고 당분간 충분 |
| 예시 모드 | 이 저장소의 별도 모드, ElementKit 공개 API만 사용 | API가 정말 충분한지 바로 드러남. Element에 같이 넣을지 따로 배포할지는 나중에 |
| 기본 기능 확장 순서 | 위치 → 조건 색 → 묶음 | 판정 바·타임라인 같은 흔한 오버레이에 먼저 필요 |
| 첫 릴리스 | P0 + P1 = Element 1.3.0 | 예시 v1이 가장 빨리 나옴 |

## 검토했다가 뺀 것

- ~~코드로 그리는 요소 종류~~ → 2026-11-10 다시 넣었습니다. 편집 화면의 틀과 부품은 Element 것이라 모양과 조작은 같고, 무엇을 어디에 둘지는 모드가 정합니다([오버레이 요소](#오버레이-요소)).
- **속성을 선언하면 Element가 편집 화면을 짜 주는 방식**(2026-11-10 첫 프로토타입): 모드의 창의성을 막아서 부품을 주는 방식으로 바꿨습니다.
- **ElementKit이 타일·히트 이벤트 API 제공**(`ElementKit.Game`):
  - 서드파티가 얼불춤을 직접 읽으면 됩니다.
  - ElementKit이 얼불춤 업데이트를 따라가야 하는 부담만 생깁니다.
  - Element만 아는 정보(`Player.Keys`, 기본 값)만 남겼습니다.
- **Element.dll 직접 참조:** 자동 업데이트, `internal` 타입, 라이선스 때문에 안 됩니다.
- **`ValueProvider` 상속:** 값은 손잡이 객체로 충분하고, 어디서든 `Set`할 수 있는 편이 평소 모드 개발과 가깝습니다.
- **이름 `Element.ElementKit` / `Element.ValueKit`:** 패키지 하나에 네임스페이스로 나누는 편이 참조와 버전이 단순합니다. "Element"가 반복되고, Element.dll 네임스페이스와도 겹칩니다.
