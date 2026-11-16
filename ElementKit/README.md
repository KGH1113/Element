# ElementKit

다른 얼불춤 모드가 Element에 **값**과 **오버레이 요소**를 더하는 라이브러리입니다. 플레이어는 모드가 더한 값을 Element의 텍스트에 넣고, 모드가 그리는 요소를 Element의 요소처럼 배치하고 꾸밉니다.

ElementKit.dll은 Element가 가지고 있습니다(`Mods/Element/Runtime/versions/<버전>/`). 모드는 컴파일할 때만 참조하고 함께 배포하지 않습니다. 라이선스는 [MIT](LICENSE)입니다.

## 준비

```xml
<!-- MyMod.csproj: 컴파일 때만 참조합니다. 복사본이 모드 폴더에 들어가면 타입이 둘로 갈라집니다. -->
<Reference Include="ElementKit">
  <HintPath>(얼불춤 폴더)/Mods/Element/Runtime/versions/(Element 버전)/ElementKit.dll</HintPath>
  <Private>false</Private>
</Reference>
```

```json
// Info.json: Element가 있으면 그다음에 불러옵니다. Element가 없어도 모드는 켜집니다.
"LoadAfter": ["Element"]
```

편집 화면을 [ElementUI](#elementui로-편집-화면-쓰기)로 쓰면 같은 폴더의 `ElementUI.Core.dll`도 같은 방법(`Private=false`)으로 참조합니다.

## 값 더하기

값은 `static readonly` 필드에 두고, `Load`에서 `Kit.Attach`를 한 번 부릅니다(Element 없이도 켜지게 하려면 [아래](#element-없이도-켜지게)처럼). 값이 바뀔 때마다 `Set`합니다. 같은 값이면 아무 일도 하지 않으므로 매 프레임 불러도 됩니다.

```csharp
using ElementKit;
using ElementKit.Values;

public static class MyValues
{
    public static readonly ValueGroup Group = new("내 모드", "My mod");   // 값들의 제목
    public static readonly NumberValue Streak = Group.Number("streak", "연속", "Streak", sample: 12);
    public static readonly TextValue Hand = Group.Text("hand", "지금 손", "Hand", sample: "왼손");
}

public static bool Load(UnityModManager.ModEntry mod)
{
    new Harmony(mod.Info.Id).PatchAll();
    return Kit.Attach(mod);
}

// 게임을 읽는 곳에서
MyValues.Streak.Set(streak);
```

키(`"streak"`)는 텍스트에 저장되므로 공개한 뒤에는 바꾸지 않습니다.

편집기의 값 목록에는 모드가 UMM에 보이는 이름으로 한 줄 들어가고, `ValueGroup`은 그 안에서 값들의 제목이 됩니다. 그룹을 여러 개 만들어도 목록의 줄은 하나입니다.

## 오버레이 요소와 편집 화면 더하기

`OverlayElement<TSettings>`를 상속하고 `[Element]`를 붙이면 Element가 찾아서 **Add Element → 기타**에 넣습니다. 부를 것은 없습니다. 설정 클래스는 Element가 레이아웃·프로필과 함께 저장하고, 편집 화면은 Element의 부품(`EditorPage`)으로 만듭니다.

```csharp
[Serializable]
public sealed class StreakSettings
{
    public Color Fill = new(1f, .62f, .26f);
    public float Width = 320;
}

[Element("streak-bar", "연속 막대", "Streak bar")]
public sealed class StreakBar : OverlayElement<StreakSettings>
{
    private RoundedBox _bar;

    protected override void OnCreate() => _bar = Drawing.Box(Root);

    protected override void OnSettingsChanged()
    {
        Size = new Vector2(Settings.Width, 24);
        _bar.Color = Settings.Fill;
        Drawing.Place(_bar.Rect, 0, 0, Settings.Width, 24);
    }

    protected override void OnEdit(EditorPage page)
    {
        page.ColorPicker(page.L("색", "Color"), Settings.Fill, color => Settings.Fill = color);
        page.Slider(page.L("너비", "Width"), Settings.Width, 80, 960, value => Settings.Width = value, step: 1);
    }
}
```

`IsPreview`가 참이면 편집기 안이므로, 보여 줄 것이 없을 때 예시를 그립니다. 편집 화면의 취소·완료, 실행 취소, 미리보기는 Element가 맡습니다.

### ElementUI로 편집 화면 쓰기

`OnEdit` 대신 `Editor`를 override하면 편집 화면을 ElementUI 뷰로 씁니다. 컨트롤을 `vm.Settings`의 필드에 `Bind`로 잇고, Element의 색·폰트·기준점·단축키 선택기는 `EditorParts`에서 가져옵니다. 화면은 Element 텍스트 편집기처럼 미리보기 오른쪽 속성 열에 놓이고, 컨트롤은 그 열의 행으로 그려집니다(`Card`는 제목 아래 행 묶음, 슬라이더는 짧은 막대가 있는 행, 옆에 다 들어가지 않는 `Picker`와 `MultiPicker`는 눌러서 목록을 여는 행). 길면 스크롤됩니다. 모드 코드는 그대로 두면 됩니다. 실행 취소, 미리보기, 저장은 `OnEdit`과 같습니다. 둘 중 하나만 override합니다.

```csharp
using static ElementUI.UI;
using static ElementKit.EditorParts;

protected override View Editor(EditorViewModel<StreakSettings> vm) =>
    VStack(spacing: 12,
        ColorPicker(vm.L("색", "Color"), Bind(() => vm.Settings.Fill)),
        Slider(vm.L("너비", "Width"), Bind(() => vm.Settings.Width), 80, 960, step: 1));
```

실행 취소는 설정을 새 객체로 되돌리므로, 설정은 늘 `vm.Settings`로 읽습니다.

## Element 없이도 켜지게

- 요소 클래스는 Element만 만듭니다. 모드의 나머지 코드가 요소 클래스를 건드리지 않으면 Element가 없어도 그대로 켜집니다.
- 값(`Kit.Attach`, `Set`)은 ElementKit을 직접 부릅니다. ElementKit 타입을 쓰는 메서드는 Element가 없으면 실행되기 전에 실패하므로, 이런 호출은 따로 메서드에 두고 Element가 있을 때만 부릅니다.

```csharp
private static bool _element;

public static bool Load(UnityModManager.ModEntry mod)
{
    try
    {
        AttachToElement(mod);
        _element = true;
    }
    catch (Exception e) when (e is FileNotFoundException or FileLoadException or TypeLoadException)
    {
        mod.Logger.Log("Element가 없어서 값을 보여 줄 곳이 없습니다.");
    }
    return true;
}

[MethodImpl(MethodImplOptions.NoInlining)]
private static void AttachToElement(UnityModManager.ModEntry mod) => Kit.Attach(mod);

// Set도 같은 방법으로: if (_element) SetStreak(streak);
[MethodImpl(MethodImplOptions.NoInlining)]
private static void SetStreak(int streak) => MyValues.Streak.Set(streak);
```

## 폴더

폴더는 파일 정리용이고 네임스페이스는 늘지 않습니다. 모든 공개 타입은 `ElementKit`, 값은 `ElementKit.Values`에 있습니다.

| 폴더 | 들어 있는 것 |
| --- | --- |
| `Kit.cs` | `Kit.Attach`, `Kit.ApiLevel` |
| `Values/` | `ValueGroup`, `NumberValue`, `TimeValue`, `TextValue` |
| `Elements/` | `OverlayElement`, `ElementAttribute`, `Anchor` |
| `Editing/` | `EditorPage`, `EditorTabs`, `EditorControl`, `EditorIcon`; ElementUI로 쓸 때 `EditorViewModel`, `EditorParts` |
| `Drawing/` | `Drawing`, `RoundedBox`, `Paint`과 그라디언트 |
| `Input/` | `Shortcut` |
| `Hosting/` | Element만 쓰는 내부 코드 |

`EditorPage`, `EditorTabs`, `EditorControl`, `EditorViewModel`, `EditorParts`, `Drawing`, `RoundedBox`는 Element가 구현해서 건네줍니다. 모드는 쓰기만 하고 상속할 수 없습니다.
