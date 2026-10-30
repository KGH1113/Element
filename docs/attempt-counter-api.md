# 시도 횟수 API

다른 모드가 특정 플레이를 시도 횟수에서 빼는 방법입니다. 예를 들어 리플레이를
재생하는 동안에는 시도 횟수가 늘지 않게 할 수 있습니다.

## 동작 방식

모드는 규칙 하나를 한 번 등록합니다. 시도 횟수 카운터는 플레이가 시작될 때마다
등록된 규칙을 모두 물어보고, 하나라도 `true`를 돌려주면 그 플레이를 세지
않습니다. 오버레이에는 지금까지의 횟수가 그대로 보입니다.

"끄기 → 작업 → 다시 켜기"가 없으므로, 다시 켜는 호출을 빠뜨리거나 중간에 예외가
나도 카운터가 꺼진 채로 남지 않습니다. 규칙은 모드 자신의 상태(리플레이 재생 중인지
등)를 읽기만 하면 됩니다.

- 규칙은 플레이가 시작되는 순간(`scnGame.Play`, 연습 모드·공식 레벨은
  `scrPressToStart.ShowText`) 메인 스레드에서 한 번 호출됩니다. 그 전에 상태를
  정해 두세요.
- 같은 `owner`로 다시 등록하면 이전 규칙을 바꿉니다. 여러 번 등록해도 괜찮습니다.
- 규칙이 예외를 던지면 그 규칙은 지워지고, 그 플레이는 셉니다. 로그에 모드 이름이
  남습니다.
- 오토플레이·노페일 플레이는 원래 세지 않으므로 규칙을 묻지 않습니다.
- UMM에서 이 모드를 껐다 켜도 규칙은 유지됩니다.

## API

어셈블리 `ImplResourcePack`의 `ImplResourcePack.AttemptCounter`:

| 멤버 | 설명 |
| --- | --- |
| `const int ApiVersion = 1` | 호환되지 않게 바뀔 때만 올라갑니다. |
| `void SetSkipRule(string owner, Func<bool> skip)` | `owner`의 규칙을 등록하거나 바꿉니다. |
| `void RemoveSkipRule(string owner)` | `owner`의 규칙을 지웁니다. 없으면 아무 일도 없습니다. |

## 예시

ImplResourcePack DLL을 참조하지 않고 리플렉션으로 연결합니다. ImplResourcePack이
없거나 버전이 다르면 아무 일도 하지 않습니다.

```csharp
static void SkipAttemptsWhileReplaying()
{
  foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
  {
    if (assembly.GetName().Name != "ImplResourcePack")
      continue;
    Type api = assembly.GetType("ImplResourcePack.AttemptCounter", throwOnError: false);
    if (api?.GetField("ApiVersion")?.GetValue(null) is not 1)
      return;
    api.GetMethod("SetSkipRule", new[] { typeof(string), typeof(Func<bool>) })
      ?.Invoke(null, new object[] { "TUFReplay", (Func<bool>)(() => ReplaySessionService.HasActiveContext) });
    return;
  }
}
```

ImplResourcePack보다 먼저 로드될 수 있으면, 게임 시작 뒤(예: 첫 씬 로드) 한 번 더
호출하세요. 다시 등록해도 규칙은 하나만 남습니다.
