# ADOFAI-IPC 1.0.0

ImplResourcePack과 ImplDmNote는 wire protocol 3, `/ipc/ws`, `adofai-ipc.v3`를 사용합니다. 구 HTTP API와 npm SDK 의존성은 제거했습니다. 앱의 canonical SDK 원본과 라이선스를 `apps/impl-dm-note/vendor/adofai-ipc`에 고정합니다.

## 경계

- 모드 Application의 `IKeyLimiterProfileApplier`와 `IKeyLimiterKeyResolver`는 transport DTO, HTTP, WebSocket에 의존하지 않습니다. `ModRuntime`이 game mapper와 IPC adapter를 조합합니다.
- `KeyLimiterSessionController`는 연결 peer의 프로필 소유권, schema 2, session/revision 검증과 연결 종료 후 제한 해제를 담당합니다.
- 앱 application coordinator는 `MessagePort`, `SchedulerPort`, `RenderSurfacePort`만 사용합니다. `bootstrap/ipcRuntime.ts`가 SDK, native byte transport, Tauri render surface, wall clock과 React 상태 UI를 연결합니다.
- Rust transport는 loopback 주소 검증과 bounded 송수신만 담당합니다. 프로토콜, namespace lifecycle과 reconnect는 TypeScript SDK가 처리합니다. 초기 게임 탐색 정책은 application의 `ConnectionSupervisor`에 주입합니다.

## 키 리미터

앱은 `impl-resourcepack`에 `key-limiter.profile.set` 명령을 보냅니다. 모드는 correlated `key-limiter.profile.applied` 결과와 `key-limiter.state.changed` 상태를 전송합니다. 동일 session/revision의 중복은 허용하고 stale revision과 다른 앱의 takeover는 거부합니다. 앱은 한 프로필의 결과를 기다리는 동안 최신 변경 하나만 보관하며 reconnect 때 현재 프로필을 다시 전송합니다.

## Renderer와 ImplDmNote

`tuf-replay-renderer` namespace의 앱 프로토콜은 2입니다. 앱의 `dmnote.attach` 명령에 Renderer가 `dmnote.attached`로 응답합니다. Renderer는 특정 peer에게 `dmnote.begin/reset/frame/end`를 전달하고, 앱은 같은 command id에 `dmnote.begun/reset/frame.ready/ended` 또는 `dmnote.failed`를 응답합니다. `dmnote.pulse`는 실제 시계로 1초마다 보냅니다.

캡처 surface는 별도 virtual clock을 사용하며 physical keyboard/mouse/HID 입력과 실제 앱 저장소를 변경하지 않습니다. 최고 한 프레임만 처리하며 이전 프레임을 읽은 뒤 다음 프레임을 지시합니다. frameIndex, output/replay time, 손/발 surface와 세션 디렉터리를 유지합니다. 대용량 RGBA/PNG를 JSON으로 인코딩하지 않습니다.

`end`는 pending frame을 기다리지 않고 native surface를 종료합니다. 연결 종료와 main window 종료 시 native input gate를 해제하며, 기존 실제 시계의 15초 watchdog도 유지합니다. namespace unload, reconnect와 늦은 frame result는 이전 세션을 복구하지 않습니다.

## 검증

게임이 아직 실행되지 않았어도 SDK 연결과 구독을 유지합니다. 초기 검색이 끝났다는 사실과 namespace가 준비됐다는 사실을 구분하며, 앱이 다시 보이거나 포커스를 얻으면 보존된 연결을 확인합니다. SDK heartbeat가 응답 없는 소켓을 교체하고, 게임 재시작 후 각 namespace의 준비 이벤트로 키 프로필과 Renderer 연결을 복원합니다. 초기 준비 실패를 버전 불일치로 표시하지 않습니다.

`./scripts/run.sh desktop-frontend-check`는 SDK 원본 검증, 타입·lint·format 검사, 전체 프론트엔드 테스트와 빌드를 수행합니다.

저장소 루트의 `./scripts/run.sh mod-check`, `desktop-check`, `desktop-message-test`, `desktop-render-test`는 게임 설치 없이 수행합니다. `install`은 검증한 모드 산출물을 설치합니다. `build`는 기존대로 빌드·테스트·설치를 수행합니다. `package`와 `desktop-bundle`은 배포 산출물을 만들며 release 업로드 또는 npm publish를 수행하지 않습니다.

실제 게임 검증에서는 키 프로필 변경, 앱/게임/IPC 재시작, 두 앱 소유권, 손·발 동시 렌더, 캡처 대기 중 취소, 연결 종료와 real-input 복구를 확인합니다. Windows native WebView2 캡처는 Windows 환경에서 별도로 검증합니다.
