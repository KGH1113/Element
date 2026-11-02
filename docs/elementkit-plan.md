# ElementKit 계획

> 상태: P0 완료, P1 중 값 부분 구현(`feat/elementkit`) · 2026-11-02

다른 모드가 만든 오버레이도 Element 안에서 보이고, Element 방식대로 고칠 수 있게 합니다.
모드는 **값**과 **프리셋**만 넣고, 화면·배치·편집은 전부 Element가 맡습니다.

## 진행 상황

| 항목 | 상태 |
| --- | --- |
| 값 목록 128칸(`OverlayValueMask`), 모드 값 등록 | 완료 |
| 모드가 없을 때 값 보존(텍스트·수식 저장 왕복, 편집기에 `[이름]`, 오버레이엔 표시 안 함) | 완료 |
| ElementKit.dll: `Kit.Attach`, `ValueGroup`, `NumberValue`/`TimeValue`/`TextValue`, 품질 규칙 검사 | 완료 |
| Element 연결: 프레임마다 바뀐 값만 반영(할당 0), 꺼진 모드 숨김, 리로드·새 캔버스 | 완료 |
| 입력 독에 모드별 카테고리 | 완료 |
| ElementKit 이름으로 들어온 요청을 Element 것으로 연결, 다른 복사본 경고 | 완료 |
| 예시 모드 [Tabub](../Tabub) (`./scripts/run.sh tabub-install`) | 완료 |
| 프리셋, 기타 메뉴, 프리셋 내보내기 | 남음 |
| `Player.Keys`, 묶음 값(`Numbers`), 실행 중 API 수준 확인 | 남음 |
| NuGet 패키지·템플릿, 개발자 모드 | 남음 |
| Element 버전 올리기(1.3.0)와 Tabub의 `Requirements`를 `Element-1.3.0`으로 | 릴리스 때 |

## 왜 만드나

- 지금은 오버레이 모드마다 배치 방식이 따로 있습니다. 플레이어는 모드마다 다른 방법을 배워야 하고, 개발자는 매번 배치 UI를 새로 만듭니다.
- 복잡한 로직이 필요한 오버레이는 지금의 텍스트·도형 꾸미기만으로 만들 수 없습니다. 첫 사례는 **타법 오버레이**입니다. 타일 각도를 읽어 왼손·오른손을 각각 몇 키로 넘길지 보여 줍니다.
- 목표: 개발은 평소 오버레이 모드처럼 하고, 결과가 보이는 곳은 Element로 모읍니다. Element의 사용자 경험(무엇이든 고칠 수 있고, 일곱 살도 쓸 수 있음)은 그대로 지킵니다.

## 원칙

1. **플레이어가 만지는 층은 Element만 가집니다.** 배치, 모양, 그리기는 서드파티가 건드리지 않습니다.
2. **서드파티는 아래층에 넣습니다.** 값(데이터)과 프리셋(처음 놓일 배치)만 넣습니다.
3. **서드파티 코드는 평소 모드와 같습니다.** 얼불춤(Assembly-CSharp)을 참조하고 Harmony로 패치해도 됩니다.
4. **게임을 느리게 하지 않습니다.** 바뀐 것만 다시 그리고, 서드파티 쪽 문제는 그 모드 안에서 멈춥니다.
5. **모드를 지워도 플레이어가 꾸민 것은 남습니다.** 다시 설치하면 그대로 돌아옵니다.

## 층 구조

```
           플레이어가 만지는 층  (Element만 가짐)
┌───────────────────────────────────────────────────┐
│ 배치    위치 · 앵커 · 커지는 기준 · 정렬 · 여러 개 선택     │
│ 모양    글자 크기·색, 도형 크기·채우기·테두리·둥글기        │
│ 그리기  텍스트 · 도형 · 키뷰어                          │
├───────────────────────────────────────────────────┤
│ 값      기본 값 28개 + 모드가 추가한 값     ◀── 서드파티  │
│ 프리셋  처음 놓일 텍스트·도형 묶음          ◀── 서드파티  │
└───────────────────────────────────────────────────┘
     게임 데이터: 서드파티가 얼불춤에서 직접 읽음 (Harmony 등)
```

복잡한 오버레이가 텍스트·도형 조립만으로 부족하면, 서드파티 코드에 그리기를 맡기지 않고 **Element 기본 기능을 늘려서** 메웁니다([Element 기본 기능 확장](#element-기본-기능-확장)). 그러면 모든 플레이어가 덕을 봅니다.

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
Mods/Tabub/
  Info.json             "Requirements": ["Element-1.3.0"]   ← 빌드가 자동으로 채움
  Tabub.dll         ElementKit은 컴파일 때만 참조, 배포하지 않음
  Presets/타법 표시.json
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
public static class TabubValues
{
    public static readonly ValueGroup Group = new("타법", "Tabub");   // 입력 독의 제목

    public static readonly TextValue   Hand     = Group.Text("hand",        "지금 손",   "Hand",       sample: "왼손");
    public static readonly TextValue   Pattern  = Group.Text("pattern",     "지금 타법", "Pattern",    sample: "2 + 2 + 1");
    public static readonly NumberValue LeftKeys = Group.Number("left-keys", "왼손 키",   "Left keys",  sample: 5);
}
```

- **종류:** Element 기본 값과 같은 세 가지입니다. 숫자(`Number`), 시간(`Time`, 초 단위, m:ss로 표시), 글자(`Text`, 계산 불가). 글자 값 안의 `<b>` 같은 표시는 읽지 않고 그대로 보여 줍니다.
- **묶음 값(남음):** `Numbers`는 "다음 8타일" 같은 이어진 값입니다. 입력 독에는 한 줄로 보이고 번호를 고릅니다.
- **저장 키:** `<모드Id>/<키>` 형식으로 저장합니다(예: `{Tabub/left-keys}`). 모드끼리 이름이 겹치지 않습니다.
- **`Set` 동작:**
  - 같은 값이면 아무 일도 하지 않습니다. 할당도 없고 다시 그리지도 않습니다.
  - 값이 바뀌면 그 값을 쓰는 텍스트·도형만 다시 그립니다. 지금의 변경 비트 방식을 그대로 씁니다.
  - Element가 꺼져 있거나 `Attach` 전에 불려도 오류 없이 마지막 값만 기억합니다.
  - 메인 스레드가 아닌 곳(입력 스레드 등)에서 불러도 다음 프레임에 반영됩니다.
- **예시 값(`sample`) 필수:** 맵을 켜지 않아도 편집기 미리보기가 보여야 하기 때문입니다.

값은 어디서든 `Set`할 수 있습니다. 보통은 자기 Harmony 패치 안에서 부릅니다.

```csharp
[HarmonyPatch(typeof(scrMarginTracker), nameof(scrMarginTracker.AddHit))]
static class AfterHit
{
    static void Postfix()
    {
        var stretch = Plan.At(scrController.instance.currentSeqID);   // 레벨을 시작할 때 listFloors로 미리 분석
        TabubValues.Hand.Set(stretch.HandName);
        TabubValues.Pattern.Set(stretch.Pattern);                      // 같은 값이면 아무 일도 없음
    }
}
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
dotnet new element-mod -n Tabub
```

```bash
dotnet build
```

```
1. 로직 작성       평소 모드처럼 (얼불춤 참조, Harmony 패치). UI 코드 없음
2. dotnet build    ADOFAI 경로 자동 탐지 → Mods/Tabub에 설치
3. UMM에서 Reload  입력 독에 '타법' 값이 바로 생김
4. 게임 안에서     Element 편집기로 텍스트·도형을 조립 (내 값 + 기본 값)
5. 개발자 모드     선택한 요소 → 프리셋으로 내보내기 → Presets/타법 표시.json
6. 배포            플레이어: 요소 추가 → 기타 → '타법 표시'
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

## 플레이어 경험

- **입력 독:** 기본 카테고리(진행도, 판정, 시간, 속도, 곡, 시스템) 뒤에 모드별 카테고리가 붙습니다. 모드 값은 기본 값과 똑같이 넣고, 끌어 놓고, 계산할 수 있습니다. 마우스를 올리면 어느 모드의 값인지 보입니다.
- **요소 추가 → 기타:** 프리셋이 하나 이상 있을 때만 "기타" 행이 생깁니다. 누르면 도형 종류 메뉴처럼 옆에 목록이 열립니다. 모드가 여러 개면 모드별로 묶고, 많으면 스크롤합니다.

```
┌ 요소 추가 ───────────────────────┐  ┌ 기타 ───────────────────────┐
│ T  텍스트                         │  │ ✋ 타법 표시                  │
│ ⌨  키뷰어                         │  │    왼손·오른손 키 수와 다음 타법 │
│ ◼  도형                           │  │    Tabub                 │
│ ⋯  기타                       ›   │  │ …                            │
│    다른 모드가 추가한 프리셋 2개    │  │                              │
└───────────────────────────────────┘  └──────────────────────────────┘
```

- **프리셋 추가:** 화면 가운데에 놓이고, 묶음 전체가 선택된 상태로 추가됩니다(실행 취소 한 번에 되돌림). 그다음은 평범한 텍스트·도형이라 무엇이든 고칠 수 있습니다.
- **모드가 없을 때:**
  - 오버레이에서는 그 값 자리에 아무것도 표시하지 않습니다(지금의 "데이터가 아직 없으면 표시하지 않음" 규칙).
  - 편집기에서는 `[왼손 키]`처럼 이름을 괄호에 넣어 보여 줘서 찾고 지울 수 있습니다. 이번 실행에서 한 번도 등록되지 않은 값은 `[Tabub/left-keys]`처럼 키로 보여서 어느 모드가 필요한지 알 수 있습니다.
  - 저장된 내용은 지우지 않아서, 다시 설치하면 돌아옵니다.
- **오류 메시지**([토스 원칙](https://toss.tech/article/21021): 무슨 일이 있었는지와 무엇을 하면 되는지):
  - "Tabub 모드에서 오류가 나서 값을 멈췄어요. 모드를 업데이트하거나 만든 분께 알려 주세요."
  - "Tabub은 Element 1.4 이상이 필요해요. 게임을 다시 켜면 업데이트가 적용돼요."
  - 개발자 로그: "Tabub 폴더에 ElementKit.dll이 있어요. 이 파일을 지우면 정상 동작해요."

## 품질 규칙 (등록할 때 검사)

일곱 살도 쓸 수 있는 경험을 지키려고 SDK가 등록할 때 검사합니다. 어기면 개발자에게 이유와 고칠 방법을 알려 줍니다.

- **값 이름:** 한국어·영어 이름이 둘 다 있어야 하고, 짧고 쉬운 말이어야 합니다("왼손 키"는 되고 "LH_cnt"는 안 됩니다).
- **예시 값:** 반드시 있어야 합니다.
- **단위 금지:** `%`, `키` 같은 단위는 값에 넣지 않습니다. 단위는 플레이어가 글자로 쓰는 것이 Element 규칙입니다.
- **키 형식:** 소문자와 `-`만 씁니다. 한 모드 안에서 겹치면 안 됩니다.
- **프리셋:** 있는 값만 참조해야 하고, 요소는 20개 이하여야 합니다.

## 첫 사례: 타법 오버레이

```
Tabub/ (이 저장소 안의 별도 모드, ElementKit 공개 API만 사용)
 ├ Planning/TabubPlanner  순수 C#: 비트 목록 → 손 구간과 누르기 묶음. Unity 없이 단위 테스트
 ├ Planning/TabubBeats    기준 BPM 2021로 비트 환산
 ├ Game/LevelBeats        scrLevelMaker.instance.listFloors → 타격별 비트
 ├ Game/TabubSession      레벨 시작에 한 번 계획, 타격마다 값만 갱신(할당 없음)
 ├ Game/TabubPatches      scnGame.Play · scrPressToStart.ShowText · AddHit · 체크포인트 · 나가기
 └ TabubValues            지금 손 · 지금 타법 · 지금 손 키 · 왼손 키 · 오른손 키 · 다음 손 · 다음 타법
```

**규칙**(구현됨):

1. **비트:** 기준 BPM을 2021로 고정합니다. `다음 타격까지의 비트 = (회전각 / 180°) × (2021 / 현재 BPM)`. 게임이 계산해 둔 타일 시각(`entryTime`)으로 `시간 × 2021 / 60`을 쓰면 같은 값이고, 소용돌이·변속·일시정지도 자동으로 반영됩니다. 첫 타격이 0비트입니다.
2. **손 구간:** `[0, 4)`, `[4, 8)`, …처럼 4비트씩 나눠 짝수 구간은 왼손, 홀수 구간은 오른손이 맡습니다. 경계에 놓인 타격은 다음 손 몫입니다. 빈 구간도 순서를 차지합니다.
3. **동타 묶기:** 구간 안에서 다음 타격이 `허용 오차 0.5비트 × 2` 안에 있으면 두 타격을 그 사이 시점에 2키로 함께 누릅니다. 아니면 1키입니다.
4. **너무 바쁜 구간:** 한 손 최대 입력 8을 넘는 구간이 있으면 손 교대 간격을 반으로 줄여(4 → 2 → 1 → 0.5비트) 다시 나눕니다.
5. **동시 입력 타일**(`tapsNeeded`)은 같은 비트의 타격 여러 개로 들어가 2키로 묶이고, 미드스핀 타일은 타격이 아니라서 뺍니다.
6. **실제 키 배정**(어떤 손가락·키로 굴릴지)은 아직 하지 않습니다. 손 배치 설정이 필요해서 다음 단계로 남깁니다.

예: 직각 5타 `0, 0.5, 1, 2, 3비트` → `2 + 2 + 1`(0.25, 1.5, 3비트). 빠른 8타 → `2 + 2 + 2 + 2`(0.25, 1.25, 2.25, 3.25비트). 둘 다 [TabubTests](../Element.Tests/TabubTests.cs)로 확인합니다.

| 버전 | 화면 | 만드는 법 |
| --- | --- | --- |
| v1 | "왼손 · 2 + 2 + 1", "다음 오른손 · 2 + 2 + 2 + 2" | 텍스트에 `{지금 손}`, `{지금 타법}`, `{다음 손}`, `{다음 타법}` |
| v2 | 다음 8타일 손 순서 | 원 8개 × 2줄. 위 줄 채우기 불투명도 = `{다음 왼손 n} × 100`, 아래 줄은 오른손. 배경은 사각형 |

v2까지 지금 Element의 "모든 숫자는 값을 받는다" 규칙만으로 만들 수 있습니다. 색을 손에 따라 바꾸려면 [조건 색](#element-기본-기능-확장)이 필요합니다.

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

| 단계 | 내용 | 타법 모드 |
| --- | --- | --- |
| **P0** Element 내부 준비 | 값 목록 동적화(`ValueMask`), 없는 값 보존, 프리셋 형식(기본 배치도 같은 형식으로), 테스트. 플레이어가 보기엔 변화 없음 | 없음 |
| **P1** ElementKit 1.0 (Element 1.3.0) | ElementKit.dll, `Attach`, 값 등록·`Set`, 입력 독 카테고리, 프리셋 등록과 기타 메뉴, 프리셋 내보내기, `Player.Keys`, 오류 격리·버전 확인·중복 DLL 감지, NuGet·템플릿·핫 리로드, 빠른 시작 문서 | **v1: 텍스트** |
| **P2** | 개발자 모드 전체(값 보기·덮어쓰기, 오류, 비용), 묶음 값(`Numbers`), 값 따라 위치, 조건 색, 묶음 | **v2: 손 타임라인** |
| **P3** 필요할 때만 | "그리기 방식 추가": 상자·크기·색·불투명도 같은 표준 속성만 갖고, 편집은 Element 편집기가 맡는 조건. 이를 위해 내부 요소 종류 레지스트리 도입 | 없음 |

## 정할 것

| 항목 | 추천 | 이유 |
| --- | --- | --- |
| ElementKit 라이선스 | MIT | 루트 라이선스로는 다른 모드가 참조할 수 없음 |
| 배포 | NuGet.org | `dotnet add package ElementKit` 한 줄. 릴리스에 NuGet API 키 필요 |
| 위치 | 이 저장소 `ElementKit/` | Element와 함께 테스트하고 버전 맞추기 쉬움 |
| 이름 | `ElementKit` + `Values` / `Presets` | 패키지 하나, `Attach` 한 번. `Element.` 접두어는 Element.dll 네임스페이스와 헷갈림 |
| 프리셋 | 추가할 때 복사본 | 모드 업데이트가 플레이어가 꾸민 것을 덮어쓰지 않음 |
| 값 한도 | 128(기본 + 모드) | 마스크 비교 비용은 거의 같고 당분간 충분 |
| 타법 모드 | 이 저장소의 별도 모드, ElementKit 공개 API만 사용 | API가 정말 충분한지 바로 드러남. Element에 같이 넣을지 따로 배포할지는 나중에 |
| 기본 기능 확장 순서 | 위치 → 조건 색 → 묶음 | 판정 바·타임라인 같은 흔한 오버레이에 먼저 필요 |
| 첫 릴리스 | P0 + P1 = Element 1.3.0 | 타법 v1이 가장 빨리 나옴 |

## 검토했다가 뺀 것

- **코드로 그리는 요소 종류**(`OverlayElement<T>` + 자동 생성 편집 화면):
  - 모드마다 그리는 방식이 달라지면 Element의 일관된 편집 경험이 깨집니다.
  - 필요해지면 P3의 "그리기 방식 추가"로, 표준 속성 계약 안에서만 엽니다.
- **ElementKit이 타일·히트 이벤트 API 제공**(`ElementKit.Game`):
  - 서드파티가 얼불춤을 직접 읽으면 됩니다.
  - ElementKit이 얼불춤 업데이트를 따라가야 하는 부담만 생깁니다.
  - Element만 아는 정보(`Player.Keys`, 기본 값)만 남겼습니다.
- **Element.dll 직접 참조:** 자동 업데이트, `internal` 타입, 라이선스 때문에 안 됩니다.
- **`ValueProvider` 상속:** 값은 손잡이 객체로 충분하고, 어디서든 `Set`할 수 있는 편이 평소 모드 개발과 가깝습니다.
- **이름 `Element.ElementKit` / `Element.ValueKit`:** 패키지 하나에 네임스페이스로 나누는 편이 참조와 버전이 단순합니다. "Element"가 반복되고, Element.dll 네임스페이스와도 겹칩니다.
