> 이 문서는 v0.4 계열의 통합 조사 기록입니다. 현재 v1.0.0 WebSocket 구현과 검증은 [ADOFAI-IPC v1 경계](adofai-ipc-v1.md)를 참고하세요.

# TUFReplay의 AdofaiIpc 통합 조사와 Element 적용안

## 조사 범위

로컬 `/Users/kgh/dev/src/tuf-replay`의 다음 계층을 확인했다.

- UMM 진입점과 AdofaiIpc dependency shim
- versioned dependency bootstrap
- TUFReplay 자체 runtime bootstrap과 updater 경계
- 모드 core의 namespace 등록과 수명 주기
- 요청 parameter/DTO 규칙
- 빌드, 설치, 패키징, 해시 검증 스크립트
- 실제 설치 결과 디렉터리
- AdofaiIpc 0.3.0의 dependency shim, bootstrap, migration 구현

TUFReplay 저장소에는 코드 수정 금지 지침이 있으므로 해당 저장소는 읽기만 했고 변경하지 않았다.

## 핵심 결론

TUFReplay에는 이름이 비슷하지만 목적이 다른 bootstrap 두 개가 있다.

```text
UMM
  -> AdofaiIpc.DependencyShim.dll
  -> DependencyBootstrap/versions/0.3.0/AdofaiIpc.Bootstrap.dll
  -> TUFReplay.Bootstrap.dll
  -> Runtime/versions/<tuf-version>/TUFReplay.dll
```

1. `AdofaiIpc.DependencyShim`과 `AdofaiIpc.Bootstrap`
   - AdofaiIpc 의존성의 설치, 버전 검사, 활성화 및 dependent mod core 호출을 담당한다.
   - AdofaiIpc를 쓰는 `Element`에도 필요하다.
2. `TUFReplay.Bootstrap`
   - TUFReplay 자체 자동 업데이트와 versioned runtime rollback을 담당한다.
   - 현재 `Element`에는 자체 updater가 없으므로 필요하지 않다.

따라서 현재 `Element`는 공용 dependency bootstrap까지만 도입하고, manifest가 `Element.dll`을 직접 호출하게 만드는 것이 맞다.

## TUFReplay의 실제 dependency 진입 계약

### `Info.json`

TUFReplay의 UMM 진입점은 TUFReplay 코드가 아니다.

```json
{
  "AssemblyName": "AdofaiIpc.DependencyShim.dll",
  "EntryMethod": "AdofaiIpc.DependencyShim.DependencyShim.Load",
  "LoadAfter": ["AdofaiIpc"]
}
```

의미:

- UMM은 고정된 dependency shim만 직접 로드한다.
- shim이 사용할 bootstrap 버전을 선택한다.
- bootstrap이 AdofaiIpc를 준비한 뒤 dependent core를 호출한다.
- `LoadAfter`는 이미 설치된 AdofaiIpc와의 정상적인 로드 순서를 돕는다.
- AdofaiIpc가 없을 때의 설치는 `LoadAfter`가 아니라 bootstrap이 담당한다.

### `AdofaiIpcBootstrap.json`

TUFReplay 설정:

```json
{
  "AssemblyName": "TUFReplay.Bootstrap.dll",
  "EntryMethod": "TUFReplay.Bootstrap.Bootstrap.Load",
  "MinimumAdofaiIpcVersion": "0.3.0"
}
```

공용 bootstrap은 이 manifest를 읽어 어떤 dependent assembly를 호출할지 결정한다. 예전 `DownloadUrl`, `ChecksumUrl` 필드는 읽을 수 있지만 공식 bootstrap이 다운로드 위치를 소유하므로 새 manifest에는 필요 없다.

### dependency bootstrap 상태

```text
DependencyBootstrap/
  state.json
  versions/
    0.3.0/
      AdofaiIpc.Bootstrap.dll
```

`state.json`:

```json
{
  "SchemaVersion": 1,
  "Current": "0.3.0",
  "Previous": null,
  "Trial": null
}
```

shim은 다음 순서로 후보를 실행한다.

1. `Trial`
2. `Current`
3. `Previous`

Trial이 성공하면 Current로 승격한다. 예외가 나거나 `false`를 반환하면 Trial을 지우고 Current/Previous로 fallback한다. Mono가 같은 simple assembly name을 캐시하는 문제를 피하기 위해 선택된 `AdofaiIpc.Bootstrap.dll`은 bytes로 읽어서 로드한다.

## AdofaiIpc bootstrap의 성공 경로

`AdofaiIpc.Bootstrap.Bootstrap.Load`는 UMM load 시점의 `SynchronizationContext`를 잡고 dependency coordinator를 비동기로 시작한 뒤 바로 `true`를 반환한다.

coordinator 흐름:

1. dependent mod의 `AdofaiIpcBootstrap.json` 읽기
2. 이미 설치된 AdofaiIpc가 disabled/outdated인지 검사
3. 설치가 없으면 공식 release에서 프로세스당 한 번 설치 시도
4. 다시 dependency 상태 검사
5. Unity main thread에서 AdofaiIpc 모드를 활성화 및 로드
6. Unity main thread에서 manifest의 dependent assembly/entry method 호출

성공 경로에는 GameObject, coroutine, `Update`, `OnGUI`, scene callback이 생기지 않는다. 게임 성능에 상시 비용이 없다.

오류는 다음처럼 처리한다.

- AdofaiIpc 명시적 비활성화: 사용자의 UMM 설정을 바꾸지 않고 dependent core를 시작하지 않는다.
- 구버전: 필요한 최소 버전과 설치 버전을 보여준다.
- 자동 설치 실패: core를 시작하지 않는다.
- 로드 실패: core를 시작하지 않는다.
- 여러 dependent mod의 문제: AppDomain 공유 registry에 모아 Unity retained-mode uGUI 하나로 표시한다.

## TUFReplay 자체 bootstrap은 왜 별도인가

`TUFReplay.Bootstrap.dll`은 다음 일을 추가로 한다.

- `Runtime/state.json`에서 Current/Previous/Trial runtime 선택
- 게임 시작 시 update engine으로 새 release 검사
- 새 runtime과 함께 제공된 새 AdofaiIpc dependency bootstrap을 Trial로 staging
- 새 TUFReplay runtime 로드 성공 시 둘을 함께 승격
- runtime 초기화 실패 시 새 runtime과 dependency bootstrap trial을 함께 rollback

이 계층은 TUFReplay updater의 원자성을 위한 것이다. `Element`가 수동 설치/단순 패키지만 제공하는 동안에는 복사할 이유가 없다.

나중에 `Element` 자체 updater를 추가할 때는 그 시점에 다음 구조를 도입한다.

```text
Element.Bootstrap.dll
Runtime/state.json
Runtime/versions/<version>/Element.dll
```

지금 미리 넣으면 빌드, 패키징, rollback, 테스트 면적만 커진다.

## Element에 권장하는 최종 진입 구조

### `Info.json`

```json
{
  "Id": "Element",
  "DisplayName": "Element",
  "Author": "impl",
  "Version": "<mod-version>",
  "AssemblyName": "AdofaiIpc.DependencyShim.dll",
  "EntryMethod": "AdofaiIpc.DependencyShim.DependencyShim.Load",
  "LoadAfter": ["AdofaiIpc"]
}
```

### `AdofaiIpcBootstrap.json`

자체 runtime bootstrap을 거치지 않고 core를 직접 호출한다.

```json
{
  "AssemblyName": "Element.dll",
  "EntryMethod": "Element.Main.Load",
  "MinimumAdofaiIpcVersion": "0.3.0"
}
```

### 설치/ZIP 구조

```text
Element/
  Info.json
  AdofaiIpcBootstrap.json
  AdofaiIpc.DependencyShim.dll
  Element.dll
  THIRD_PARTY_NOTICES.md
  Assets/
  DependencyBootstrap/
    state.json
    versions/
      0.3.0/
        AdofaiIpc.Bootstrap.dll
```

`Element.dll`은 manifest가 mod root 기준으로 찾으므로 root에 둔다.

## migration DLL 적용 여부

TUFReplay core의 첫 줄에는 `AdofaiIpcMigrationBridge.PrepareAndNotify`가 있다. 이것은 예전 release가 이미 일반 mod assembly를 entrypoint로 로드한 상태에서 updater가 새 dependency-shim 구조를 설치할 때 사용하는 일회성 전환 장치다.

동작:

- fixed shim과 bootstrap layout을 준비한다.
- `Info.json`을 dependency shim entrypoint로 바꾼다.
- 그 실행에서는 dependent core를 중단한다.
- 사용자에게 AdofaiIpc 재설치/게임 완전 종료/재시작을 안내한다.

현재 `Element`는 자체 in-process updater가 없고 새 버전을 수동으로 완전히 설치할 수 있으므로, 첫 IPC release ZIP이 위 최종 layout을 그대로 제공한다면 migration bridge는 필요하지 않다.

다음 경우에만 `AdofaiIpc.Migration.dll`을 포함한다.

- 이미 실행 중인 구버전 `Element.dll`이 자기 자신을 업데이트한다.
- loaded DLL을 덮어쓰지 않고 다음 실행의 entrypoint를 전환해야 한다.
- 기존 사용자를 자동 업데이트로 final shim layout에 옮겨야 한다.

즉, TUFReplay의 `AdofaiIpc.Migration.dll` 포함은 일반 dependency 사용 규칙이 아니라 해당 프로젝트의 updater 전환 이력이다.

## core 프로젝트 참조

TUFReplay는 core csproj에서 다음처럼 compile-only 참조한다.

```xml
<Reference Include="AdofaiIpc">
  <HintPath>$(AdofaiIpcDll)</HintPath>
  <Private>false</Private>
</Reference>
```

`Private=false`가 중요하다. dependent mod가 AdofaiIpc runtime DLL을 자기 폴더에 복제해서 배포하면 assembly identity와 loader 상태가 갈라질 수 있다. 실제 runtime은 `Mods/AdofaiIpc`가 소유한다.

키 리미터 구현은 SkyHook 타입도 직접 사용하므로 다음 참조도 필요하다.

```xml
<Reference Include="SkyHook.Unity">
  <HintPath>$(AdofaiManaged)/SkyHook.Unity.dll</HintPath>
  <Private>false</Private>
</Reference>
```

## namespace 수명 주기에서 가져올 패턴

TUFReplay의 `TUFReplayIpcFeature`는 다음 원칙을 지킨다.

- feature 자체 `_active` guard로 enable/disable을 idempotent하게 처리
- namespace 이름을 상수로 관리
- display name, mod version, allowed origins를 등록 시 한 번 설정
- 일반 handler와 Unity main-thread handler를 구분
- 모든 handler 등록을 마친 뒤 마지막에 `MarkReady()` 호출
- disable 때 namespace 전체 unregister
- register/unregister 성공을 한 줄로 로그

FeatureRegistry 순서도 중요하다.

```text
Harmony patch
  -> domain features enable
  -> IPC enable
  -> namespace MarkReady
```

종료는 반대 방향이다.

```text
IPC disable/unregister
  -> domain features disable
  -> Harmony unpatch
```

이렇게 해야 ready namespace가 아직 준비되지 않았거나 이미 정리된 서비스를 호출하지 않는다.

`Element` 적용 순서:

1. DM Note limiter state와 label mapper 준비
2. Harmony 키 필터 패치 적용
3. 오버레이 기능은 독립적으로 초기화
4. `element` namespace 등록
5. `key-limiter.sync`, `key-limiter.status`, `health.get` 등록
6. `MarkReady()`

현재 `ModRuntime.Initialize()`는 폰트 bundle 로드 실패 시 전체 runtime 초기화를 중단한다. IPC 키 리미터는 폰트와 무관하므로 다음처럼 분리해야 한다.

```text
essential runtime: key limiter + Harmony + IPC
optional runtime: font/assets + overlay features
```

오버레이 실패 때문에 namespace가 등록되지 않거나 키 리미터가 꺼지면 안 된다.

## handler와 DTO 규칙

TUFReplay의 좋은 패턴:

- `IpcRequest.Params`를 `JObject/JToken`으로 취급한다.
- required/optional parameter 파싱을 `IpcParams`로 모은다.
- 문자열 trim과 빈 문자열 거부를 handler마다 반복하지 않는다.
- 응답은 전용 DTO로 만든다.
- namespace 자체 protocol version을 AdofaiIpc 제품 버전과 분리한다.

TUFReplay health DTO에는 다음 세 버전 개념이 분리되어 있다.

- `ModVersion`: 설치된 모드 release 버전
- `ProtocolVersion`: 해당 namespace API 계약 버전
- AdofaiIpc `/ipc/health`의 `protocolVersion`: gateway protocol 버전

`Element`도 `health.get`에 최소 다음을 반환하는 것이 좋다.

```json
{
  "ok": true,
  "mod": "Element",
  "modVersion": "0.2.0",
  "protocolVersion": 1,
  "keyLimiterSchemaVersion": 1
}
```

DM Note는 gateway 연결 성공만 보고 sync하지 말고 다음을 모두 검사한다.

1. AdofaiIpc health의 gateway protocol 호환성
2. `element` namespace ready
3. `health.get`의 namespace protocol 호환성
4. `key-limiter.sync` payload schema 호환성

TUFReplay는 public field DTO를 사용해 Newtonsoft/Unity 환경에서 단순하게 직렬화한다. 이 프로젝트도 같은 방식을 쓰면 된다.

## AllowedOrigins

TUFReplay는 실제 consumer origin만 whitelist한다.

`Element`의 consumer는 DM Note Tauri WebView이므로 다음을 시작점으로 둔다.

```text
tauri://localhost
http://tauri.localhost
https://tauri.localhost
http://localhost
http://127.0.0.1
```

release macOS 앱에서 실제 `Origin`을 확인한 뒤 불필요한 항목을 줄인다. exact origin과 scheme+host 규칙은 AdofaiIpc 0.3.0 server가 처리한다.

## 빌드 설정에서 가져올 항목

### 환경 변수

TUFReplay의 `scripts/lib/context.sh`는 다음 경로를 한 곳에서 정의한다.

```text
ADOFAI_IPC_DLL
ADOFAI_IPC_BOOTSTRAP_DLL
ADOFAI_IPC_DEPENDENCY_SHIM_DLL
ADOFAI_IPC_INFO_JSON
ADOFAI_IPC_BOOTSTRAP_LOCK
```

`Element`도 같은 이름을 사용하면 로컬 개발 환경과 패키징 절차를 공유하기 쉽다. migration을 쓰지 않는 동안 `ADOFAI_IPC_MIGRATION_DLL`은 생략한다.

### lock file

TUFReplay의 `AdofaiIpcBootstrap.lock`은 버전과 SHA-256을 고정한다.

```text
ADOFAIIPC_VERSION=0.3.0
ADOFAIIPC_BOOTSTRAP_VERSION=0.3.0
ADOFAIIPC_BOOTSTRAP_SHA256=<sha256>
ADOFAIIPC_DEPENDENCY_SHIM_SHA256=<sha256>
```

TUFReplay에 기록된 0.3.0 해시를 동일 binary와 함께 재사용할 수 있지만, 파일을 무작정 복사하기보다 패키지 task에서 현재 설치본을 다시 계산하고 lock과 일치하는지 검증해야 한다.

### 입력 검증

local build 전에:

- AdofaiIpc core DLL 존재
- bootstrap DLL 존재
- dependency shim DLL 존재
- UnityModManager/Harmony/game managed DLL 존재

release package 전에 추가로:

- AdofaiIpc 설치 버전이 lock의 버전과 일치
- bootstrap/shim SHA-256이 lock과 일치
- manifest와 Info.json 존재
- 모든 runtime asset 존재

### artifact staging

launcher payload helper가 담당할 일:

1. `Info.json` 복사
2. `AdofaiIpcBootstrap.json` 복사
3. `AdofaiIpc.DependencyShim.dll` 복사
4. dependency bootstrap version directory 생성
5. `AdofaiIpc.Bootstrap.dll` 복사
6. `DependencyBootstrap/state.json` 생성

core payload helper는 기존처럼 다음을 복사한다.

- `Element.dll`
- `THIRD_PARTY_NOTICES.md`
- `Assets/`

AdofaiIpc core DLL 자체는 복사하지 않는다.

## 설치 스크립트 주의점

TUFReplay install task가 좋은 이유는 새 payload를 덮기 전에 명시적으로 이전 layout을 정리하고, 삭제 대상이 mod install root 안인지 검사하기 때문이다.

`Element`에서 필요한 최소 규칙:

- install path가 root나 비어 있는 경로가 아닌지 검증
- 과거에 로컬 복사된 `AdofaiIpc.dll`이 있다면 제거
- obsolete bootstrap/cache 파일 제거
- 사용자 설정 파일이 생기면 삭제하지 않음
- `DependencyBootstrap/state.json`은 설치한 버전으로 다시 생성
- package는 설치된 mod 폴더를 복사하지 않고 clean build output으로 staging

UMM/Mono가 로드한 DLL은 실행 중 덮어쓰지 않는다. 설치와 업데이트는 게임이 완전히 종료된 상태를 기본 전제로 한다.

## settings 분리에서 가져올 점

TUFReplay는 runtime 기능 설정과 updater 설정을 분리한다.

- 기능 설정: mod root의 `Settings.json`
- updater 채널: `UpdateSettings.json`
- 대용량/영속 데이터: `Data/`

`Element`도 이후 설정이 생기면 관심사별로 분리하는 것이 좋다.

권장:

- `Settings.json`
  - IPC key limiter 허용 여부의 서버 측 kill switch
  - 마지막 정상 snapshot 유지 정책
  - 진단 로그 수준
- DM Note 포크 설정
  - ADOFAI 동기화 on/off
  - 연결 상태는 runtime 상태로만 유지
- limiter snapshot
  - 기본적으로 메모리 상태
  - 게임 재시작 후 DM Note가 재동기화하므로 별도 영속화하지 않음

서버 측 kill switch 기본값은 enabled로 둘 수 있지만, 아직 IPC snapshot이 없으면 항상 fail-open이어야 한다.

## 적용 파일 체크리스트

### 새 파일

- `Element/AdofaiIpcBootstrap.json`
- `Element/AdofaiIpcBootstrap.lock`
- `Element/Shared/Ipc/ElementIpcFeature.cs`
- `Element/Shared/Ipc/HealthIpcHandlers.cs`
- `Element/Shared/Ipc/HealthDtos.cs`
- `Element/Shared/Ipc/IpcParams.cs`
- key limiter sync/status handler 및 DTO
- AdofaiIpc package verification task

### 수정 파일

- `Element/Info.json`
- `Element/Element.csproj`
- `Element/Bootstrap/ModRuntime.cs`
- `scripts/lib/context.sh`
- `scripts/lib/artifacts.sh`
- local/release validation tasks
- install/package staging tasks
- `THIRD_PARTY_NOTICES.md`
- `README.md`

### 하지 않을 것

- `AdofaiIpc.dll`을 Element ZIP에 private runtime dependency로 복사
- namespace handler 등록 전에 `MarkReady()` 호출
- 오버레이 font 로드 성공 여부에 IPC/limiter 초기화를 결합
- TUFReplay 자체 updater bootstrap을 현재 단계에서 복사
- fresh final-layout release에 불필요한 migration bridge 추가
- dependency 성공 경로에 per-frame 검사 추가

## 검증 시나리오

TUFReplay/AdofaiIpc E2E에서 현재 기능에 필요한 사례를 가져온다.

1. AdofaiIpc 설치됨, 정상 버전: dialog 없이 core와 namespace ready
2. AdofaiIpc 없음, 네트워크 가능: 자동 설치 후 같은 실행에서 core 시작
3. AdofaiIpc 없음, 네트워크 차단: 공용 오류 dialog, core 미시작
4. AdofaiIpc UMM 비활성화: 설정을 강제로 켜지 않고 core 미시작
5. AdofaiIpc 구버전: installed/minimum 버전 표시
6. AdofaiIpc runtime 손상: load failure, core 미시작
7. 다른 dependent mod도 실패: dialog 하나에 두 모드 집계
8. 정상 성공 경로 profiler: dependency 관련 GameObject/coroutine/Update 없음
9. mod disable: namespace unregister 후 key limiter/Harmony 정리
10. mod re-enable: handler 중복 없이 namespace ready 복원
11. 오버레이 asset 실패: IPC key limiter는 정상 ready
12. DM Note가 게임보다 먼저/나중에 실행되는 두 순서 모두 sync 성공

## 최종 권장 순서

1. 자체 updater 없이 direct-core dependency layout부터 도입
2. build/package/verification을 TUFReplay 패턴으로 먼저 고정
3. core namespace에 `health.get`만 등록해 bootstrap E2E 검증
4. key limiter service와 Harmony patch 추가
5. `key-limiter.sync/status` 등록
6. DM Note 포크 client 연결
7. 양쪽 재시작/오류/ghost 시나리오 검증
8. 향후 자체 updater가 필요해질 때만 별도 `Element.Bootstrap` 도입
