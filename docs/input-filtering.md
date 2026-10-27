# 입력 필터링: 키 리미터, 채터 블로커, 고스트 키

2026-10-27 기준 구현 메모. 게임 버전 r150에서 확인한 API를 기준으로 한다.

## 키 리미터: 얼불춤 기본 키 리미터 사용

ImplDmNote가 보낸 키 프로필(`key-limiter.profile.set`)은 `KeyLimiterService`가 스냅샷으로 만들고, `GameKeyLimiter`가 매 프레임(UMM `OnUpdate`) 스냅샷이 바뀌었을 때만 게임에 반영한다.

- 게임은 `Persistence.keyLimiterKeys`(`KeysSetting`)의 `_unityKeysCache`(KeyCode)와 `_asyncKeysCache`(SkyHook 네이티브 코드)로 `RDInputType_Keyboard.MainIgnoreActive`와 `RDInputType_AsyncKeyboard.Main`의 메인 입력을 거른다. 둘 다 메인 스레드에서 읽는다.
- 모드는 두 캐시 필드만 새 `HashSet`으로 교체한다. setter·`Add`·`Clear`를 부르지 않으므로 플레이어가 저장한 키(`generalPrefs`)는 바뀌지 않는다.
- 해제(앱 연결 끊김, 토글 끔, 모드 비활성화) 시 캐시를 `null`로 돌려 게임이 저장된 키를 다시 읽게 한다. 강제 종료돼도 디스크에는 아무것도 남지 않는다.
- 빈 집합은 게임에서 "제한 없음"이다. 한쪽만 매핑되면 `KeyCode.None` / `ushort.MaxValue` 센티널을 넣어 그쪽도 제한한다.
- 일시정지 메뉴의 키 편집기는 setter로 캐시를 덮어쓴다. ImplDmNote 키가 적용 중일 때는 `SettingsMenu.StartEditingKeys`를 막고 `PauseSettingButton.GetDescriptionText`에 안내 문구를 붙인다. 다른 경로로 캐시가 바뀌면 다음 프레임에 다시 설치한다.
- 동작 차이: 게임 기본 리미터는 `gameworld`이고 일시정지 메뉴가 닫혀 있으면 항상 적용된다(카운트다운, 실패 화면 포함). 마우스·게임패드는 제한하지 않는다. 키뷰어에는 막힌 키도 표시된다.
- 이전 방식(`CountValidKeysPressed` 후처리 + SkyHook `HookCallback` 접두 패치)은 삭제했다. 입력마다 실행되던 패치와 SkyHook 스레드 작업이 사라졌다.

ImplDmNote의 **Enable Key Limiter** 토글을 끄면 `enabled: false` 프로필을 보내 해제한다.

## 채터 블로커 (Tweaks > Chatter blocker)

KeyboardChatterBlocker에는 라이선스가 없어 코드를 가져오지 않고 새로 구현했다.

- 알고리즘: 같은 키를 **뗀 시각부터** 다음 누름까지의 간격이 창(기본 20ms, 5~150ms)보다 짧으면 그 누름과 짝이 되는 뗌을 함께 버린다. 원본은 누름-누름 간격만 보므로 길게 누른 키를 뗄 때 생기는 채터를 놓치고, 같은 간격이면 빠른 연타를 더 많이 막는다.
- 막힌 뗌 직후의 튐도 잡도록, 버린 뗌도 마지막 뗌 시각으로 기록한다.
- 비동기 입력: `SkyHookManager.HookCallback` 접두 패치. SkyHook 이벤트 시각(`GetTimeInTicks`)을 쓰고, 키별 상태는 고정 배열에 둔다. 할당·잠금·로그·Unity 호출이 없고 예외는 네이티브로 나가지 않는다. 원본의 문제(훅 스레드 로그 → UMM 로그 리스트 경쟁, 스레드 간 `List` 공유, 벽시계 사용, Windows 전용 Escape 판정)는 모두 없다.
- Unity 입력: `RDInputType_Keyboard.MainIgnoreActive(WentDown)` 후처리로 게임의 프레임별 누름 캐시에서 채터를 뺀다. 뗌은 눌린 키만 `Input.GetKey`로 확인하며 프레임 단위 정밀도다.
- 한계: 홀드 도중 튐이 생기면 홀드가 일찍 끝난다(원본과 동일). KeyboardChatterBlocker가 함께 로드되면 설정 화면에 경고를 띄운다.

## 고스트 키 (ImplDmNote)

키마다 `ghost: { enabled, key, settings }`를 저장한다. `settings`는 키 스타일 전체이며 위치·크기는 항상 원래 키를 따른다.

- 렌더링: 오버레이·OBS·게임 내 키뷰어·리플레이 렌더러는 `expandGhostKeys`로 고스트를 원래 키 위의 가상 키로 덧붙인다. 같은 z-index에 DOM 뒤쪽에 있어 원래 키 바로 위에 그려진다. 기본값은 대기 시 렌더링하지 않음(`idleTransparent`), 눌렀을 때 원래 키의 눌림 색, 원래 노트 색의 테두리만 있는 노트, 카운터 꺼짐이다.
- 입력 라우팅: 백엔드(`match_key_for_mode`)는 원래 키를 먼저, 그다음 고스트 키를 찾는다. 카운터와 키음도 고스트를 인식한다.
- 키 리미터: 고스트 키는 허용 키 목록에 들어가지 않고 `excludedGhostKeys`로만 보고된다.
- 마이그레이션: 이전에 다른 키와 같은 자리에 겹쳐 둔 `keyLimiterExcluded` 키는 한 번만 그 키의 고스트로 옮긴다(`ghostKeysFolded`, 백업 `store.pre-ghost-keys.json`). 프리셋을 불러올 때도 같은 규칙을 적용한다. 겹치지 않는 제외 키는 그대로 동작한다.

## 얼불춤 안에 표시 (ImplDmNote)

ImplDmNote의 **Show inside ADOFAI**를 끄면 `mirror.available`에 `inGame: false`를 보내고 `mirror.native.describe`는 `in-game-disabled`로 거절한다. 모드는 Add Element 갤러리의 키뷰어 카드, 배치된 키뷰어 페이지와 편집기 프레임을 숨긴다. 배치는 저장된 채로 남아 다시 켜면 돌아온다.
