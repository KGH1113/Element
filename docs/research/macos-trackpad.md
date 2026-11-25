# ADOFAI 레벨 에디터 macOS 트랙패드 조사

조사일: 2026-09-09. 범위: 설치된 게임의 `ilspycmd` 정적 분석, 다른 앱의 공식 조작 문서 및 공개 소스 분석. 게임과 비교 앱에서 실제 트랙패드를 움직여 본 결과는 아니다. 기능 코드는 수정하지 않았다.

**결론: 구현 가능성이 높다. 두 손가락 패닝과 핀치 줌은 Krita·Affinity 같은 2D 캔버스 방식으로 구성하고, 입력 계층은 Blender처럼 macOS의 스크롤·확대 제스처를 구분하는 설계가 적합하다.** ADOFAI에는 포인터 기준 즉시 줌과 카메라 이동 함수가 이미 있다. 추가 작업의 중심은 카메라 수학보다 네이티브 입력 수집, UI와의 입력 배분, 기존 휠 처리와의 중복 방지다. 실제 Unity 플레이어 안에서 AppKit 이벤트가 어떻게 전달되는지는 프로토타입으로 검증해야 한다.

**1. 실제 게임 코드에서 확인한 내용**

분석 대상은 로컬 Steam 설치의 `ADanceOfFireAndIce.app/Contents/Resources/Data/Managed/Assembly-CSharp.dll`이다. SHA-256은 `d0cab90275486f57b2bf6349ef672c7fd56ad631fb74b0fa4129af093995ce54`. 이 결과를 다른 게임 버전에 그대로 적용할 수 있다고 보장하지 않는다.

`DOTNET_ROOT="$HOME/.dotnet"` 환경으로 `ilspycmd -t scnEditor`, `-t scrCamera`, `-t RDInput`, `-t CustomStandaloneInputModule`을 실행했다. 아래 줄 번호는 이번 `scnEditor` 디컴파일 출력 기준이며 원본 소스 줄 번호는 아니다.

| 위치 | 확인한 동작 | 트랙패드 구현에 미치는 영향 |
|---|---|---|
| `RDInput.mouseScrollDelta` | 창 밖이면 0, 그 외에는 `UnityEngine.Input.mouseScrollDelta` 반환 | 이 경로에는 핀치 배율이나 제스처 시작·종료·관성 정보가 없다. 네이티브 입력 경로를 별도로 마련해야 한다. |
| `scnEditor.Update`, 2171–2218 | 포인터 아래 UI의 부모들을 탐색해 `ScrollRect`가 있으면 카메라 휠 처리를 생략한다. 설정·파티클 편집 패널이 활성화된 경우도 생략한다. | 이미 존재하는 UI 우선 처리 규칙을 보존해야 한다. 단순히 모든 스크롤을 카메라로 보내면 안 된다. |
| 같은 메서드, 2199–2215 | 세로 이동량의 절댓값이 `0.05`보다 클 때만 처리한다. Ctrl을 누르면 재생 속도를 변경하고, 그 외에는 `ZoomCamera`를 호출한다. | 현재 카메라 휠 경로는 수평 패닝을 처리하지 않는다. 작은 움직임은 임계값에서 잘리며, 핀치를 Ctrl+휠로 합성하면 재생 속도와 충돌한다. |
| `ZoomCamera`, 2273–2315 | `delta`, `anchorAtPointer`, `instant` 인자를 받는다. `camUserSizeMultiplier - delta × scrollSpeed`를 `0.5–15`로 제한한다. | 연속 배율 입력을 게임의 카메라 크기 값으로 변환해 재사용할 수 있다. 이 범위는 UI의 확대율 퍼센트가 아니다. |
| 같은 메서드 | 기본 경로는 0.1초 `Ease.OutQuad` Tween. 포인터 고정 경로는 기존 `_anchorZoomTween`을 종료하며, `instant=true`이면 바로 적용한다. | 핀치마다 0.1초 애니메이션을 재시작할 필요가 없다. 직접 조작에는 즉시 적용 경로가 유리하다. |
| 같은 메서드 | 확대 전후 `ScreenToWorldPoint` 차이로 카메라 위치를 보정한다. `mousePosition0`와 `cameraPositionAtDragStart`도 갱신한다. | 기존의 포인터 고정 및 드래그 기준점 보정 로직을 활용할 수 있다. 다만 기준 포인터는 네이티브 이벤트 좌표가 아니라 `Input.mousePosition`이다. |
| `HandleMouseActions`, 2739–2744 | 화면 이동량을 화면 높이와 `orthographicSize × 2`를 이용해 월드 이동량으로 변환하고 `DragCamera` 호출 | 줌 상태에 맞는 패닝 변환의 근거가 이미 있다. |
| `DragCamera`, 7530–7534 | 카메라 transform Tween을 종료하고 x/y 위치를 대입한다. z는 -10으로 설정한다. | 이름의 `delta` 인자는 실제로 목표 위치다. 누적 이동량을 그대로 전달하면 안 된다. private 메서드이므로 Harmony 접근 등을 검토해야 한다. |
| `scrCamera.UpdateSize` | 카메라의 기본 크기·`userSizeMultiplier`·`zoomSize`를 곱해 `orthographicSize` 결정 | `orthographicSize`만 임의로 바꾸면 이후 게임 업데이트에 덮일 수 있다. 에디터의 크기 상태와 함께 갱신해야 한다. |

재생 중 일시정지 상태에도 별도 마우스 카메라 드래그 경로가 있다. 첫 구현은 편집 모드에 한정하고, 일시정지 미리보기 지원은 별도 검증하는 편이 명확하다. 게임 실행 파일은 `file` 검사 결과 arm64와 x86_64를 포함한 universal binary다. 네이티브 라이브러리 역시 실행 아키텍처와 맞아야 한다.

**2. 다른 앱의 조작 방식**

앱마다 기본값과 설정이 다르다. 특히 3D 뷰는 회전이 주요 기능이므로 두 손가락 이동의 의미가 2D 캔버스와 다를 수 있다.

| 앱 | 확인한 트랙패드 동작 | ADOFAI에 참고할 점 |
|---|---|---|
| Autodesk Fusion | 공식 학습 자료에서 두 손가락 이동은 패닝, 핀치는 줌, Shift+두 손가락 이동은 궤도 회전으로 안내 | 패닝·핀치의 기본 조합은 그대로 참고할 수 있다. [공식 자료, 2쪽](https://files.upskill-dev.autodesk.com/public/introduction-to-cad-learn-fusion-360-in-90-minutes/211004_SBS_M4-01_Navigation-and-display-settings.pdf) |
| Blender | 5.0 공식 문서의 기본 3D 조작은 두 손가락 이동으로 궤도 회전, Shift로 패닝, Ctrl 또는 OSKey로 줌 | 3D 회전 우선 매핑을 ADOFAI의 기본값으로 복제할 이유는 적다. 입력 처리 구현은 유용하다. [공식 문서](https://docs.blender.org/manual/en/5.0/getting_started/configuration/hardware.html) |
| Affinity | 공식 공통 제스처 문서는 스크롤로 문서 패닝, 핀치로 줌, 두 번 탭으로 현재 배율과 100% 전환, 회전 제스처로 커서 기준 캔버스 회전을 안내 | ADOFAI의 평면 편집 화면과 가까운 모델이다. 이 근거는 Publisher 경로에 있는 Affinity 공통 문서이며 Designer 2 버전별 실기 검증은 아니다. [공식 문서](https://affinity.help/publisher/en-US.lproj/pages/GetStarted/gestures.html) |
| Krita | 공개 코드에서 트랙패드 패닝에 픽셀 이동량, 네이티브 핀치에 연속 배율과 고정 지점을 사용 | 가장 직접적인 2D 구현 참고 사례다. 아래 소스 분석 참고. |
| Shapr3D | 공식 문서는 Default·Classic 및 다른 CAD의 내비게이션 프리셋을 제공하며 트랙패드 매핑도 프리셋에 따라 달라짐을 설명 | 필요하면 후속으로 조작 프리셋을 제공하되, 첫 버전의 기본 조작을 명확하게 유지한다. 문서만으로 모든 프리셋의 정확한 핀치 매핑을 단정하지 않는다. [공식 문서](https://support.shapr3d.com/hc/en-us/articles/7873881091356-Navigation-Presets) |

**3. 공개 구현에서 확인한 핵심**

Blender의 `GHOST_SystemCocoa.mm`은 `NSEventTypeScrollWheel`과 `NSEventTypeMagnify`를 별개로 받는다. 일반 휠과 멀티터치 스크롤을 구분하고, 제스처에는 `scrollingDeltaX/Y`, `phase`, `momentumPhase`를 활용한다. 화면 배율에 맞게 backing 좌표로 변환하고 자연스러운 스크롤 방향 정보도 전달한다. 관성이 이어지는 도중 키를 눌렀을 때 패닝이 갑자기 줌으로 바뀌지 않도록 관성 입력을 무시하는 방어 코드도 있다. 핀치·스마트 줌·회전은 각각 별도 내부 이벤트로 전달한다. [Blender 원본 소스](https://github.com/blender/blender/blob/main/intern/ghost/intern/GHOST_SystemCocoa.mm)

Krita는 Qt가 제공하는 네이티브 제스처를 사용한다. `kis_input_manager.cpp`에서 시작·종료를 처리하고, `kis_pan_action.cpp`의 트랙패드 패닝은 `QWheelEvent.pixelDelta()`를 사용한다. `kis_zoom_action.cpp`는 네이티브 확대 이벤트의 값에 1을 더한 배율을 현재 줌에 곱하고 고정 지점을 유지한다. 휠 한 칸처럼 확대 단계를 오가는 처리와 네이티브 핀치 처리를 분리한 구조다. [입력 관리자](https://github.com/KDE/krita/blob/master/libs/ui/input/kis_input_manager.cpp), [패닝](https://github.com/KDE/krita/blob/master/libs/ui/input/kis_pan_action.cpp), [줌](https://github.com/KDE/krita/blob/master/libs/ui/input/kis_zoom_action.cpp)

분석은 공개 개발 브랜치를 읽은 것이므로 배포 버전과 차이가 있을 수 있다. 조사 직후 조회한 브랜치 HEAD는 Blender `6294beebb75fb6a0303571dc47be51611dae2a59`, Krita `1a894fcc82c6a0becda46353ed9126ffd5c2d2ed`였다. 코드를 가져온 시점과 HEAD 조회 시점은 별도 요청이다. 비공개인 Fusion·Affinity·Shapr3D의 내부 구현은 확인하지 않았다.

**4. ADOFAI에 권장하는 설계 — 아래는 조사 결과에 따른 제안**

입력 경로는 `AppKit 이벤트 → 네이티브 버퍼 → Unity 메인 스레드 → 에디터 카메라`로 구성한다. Objective-C++ 라이브러리에서 앱 내부 이벤트 모니터 등의 수신 방식을 검증하고 C ABI로 데이터를 넘긴다. OS 콜백에서 바로 Unity 객체를 조작하지 않는다. AppKit은 정밀 스크롤, 확대량, 제스처 단계와 관성 단계를 제공한다. [Apple NSEvent](https://developer.apple.com/documentation/AppKit/NSEvent?language=objc), [트랙패드 이벤트 처리 지침](https://developer.apple.com/library/archive/documentation/Cocoa/Conceptual/EventOverview/HandlingTouchEvents/HandlingTouchEvents.html)

- **두 손가락 패닝:** x/y 이동량을 손실 없이 누적해 다음 게임 프레임에 적용한다. 프레임마다 첫 이벤트만 처리하거나 작은 이동을 임계값으로 버리지 않는다. OS가 제공한 관성을 그대로 소비하고 별도 관성 애니메이션을 겹치지 않는다.
- **핀치 줌:** 확대 이벤트를 직접 처리한다. 원하는 시각적 확대 비율이 `f`라면 카메라 크기 배수는 대체로 `현재 배수 / f`가 된다. 게임의 선형 delta 인자로 역변환한 후 `ZoomCamera(..., anchorAtPointer: true, instant: true)`를 재사용하는 방식을 우선 검토한다. 양수 배율·유한값·줌 제한을 검사하고 기본 크기와 게임 상태에 따른 실제 결과를 검증한다.
- **좌표:** AppKit의 point와 Unity 렌더링 픽셀을 구분한다. Retina 2배 값을 고정하지 말고 창·뷰·렌더 타깃의 실제 비율과 좌표 원점을 반영한다. 현재 줌 함수가 사용하는 Unity 포인터와 네이티브 포인터가 일치하는지 먼저 측정한다.
- **UI 배분:** 제스처 시작 지점에서 캔버스와 패널 중 처리 대상을 정하고 관성 종료까지 유지한다. 팝업·포커스 상실·씬 변경에는 취소한다. UI에서 시작한 스크롤이 나중에 캔버스를 움직이지 않게 한다.
- **중복 방지:** `scnEditor.Update` 안의 카메라용 휠 읽기 지점에만 제한적인 패치를 적용하는 방식을 검토한다. 네이티브 캔버스 제스처를 처리한 경우에만 기존 동작을 억제한다. `RDInput.mouseScrollDelta`를 전역으로 0으로 만드는 패치는 UI와 다른 기능에도 영향을 줄 수 있다.
- **마우스 병용:** 일반 마우스 휠은 기존 줌을 유지하고 네이티브 제스처만 패닝으로 구분한다. `hasPreciseScrollingDeltas` 하나만으로 장치를 확정하지 않는다. Magic Mouse도 정밀 스크롤을 제공할 수 있어 phase·momentum·설정 등을 함께 검토해야 한다.
- **생명주기:** 모드 비활성화 시 이벤트 모니터와 버퍼를 정리한다. 플랫폼 또는 네이티브 로드 실패 시 게임의 기존 입력으로 복귀하도록 한다. arm64와 x86_64 실행을 각각 검증한다.

패치 방식은 아직 확정할 수 없다. 네이티브 모니터가 이벤트를 소비하는 시점과 Unity UI의 처리 시점이 다를 수 있고, C#에서 현재 UI 대상을 판단할 때는 이미 이벤트가 Unity로 전달되었을 수 있다. 첫 프로토타입은 수신 이벤트를 기록하면서 Unity 입력과 프레임별로 비교해야 한다. 그 결과에 따라 네이티브 단계에서 소비할지, 게임의 카메라 입력 분기만 억제할지 결정한다.

기본 제스처는 두 손가락 패닝·핀치 줌으로 충분하다. 스마트 줌은 전체 레벨 또는 선택 타일 맞춤 기능과 결합할 후보이며, 회전은 현재 요청 범위 밖의 선택 기능으로 남기는 편이 좋다.

**5. 구현 전후 확인할 항목**

1. 실제 게임 프로세스에서 scroll·magnify·phase·momentum·좌표를 수신할 수 있는지, 동일 입력이 Unity 휠로도 들어오는지 기록한다.
2. 느리고 작은 패닝, 대각선 이동, 빠른 플릭과 중단 시 이동 손실·튐이 없는지 확인한다.
3. 핀치 중 포인터 아래 타일이 유지되는지, 확대 한계에 닿았다가 반대로 움직이면 즉시 반응하는지 확인한다.
4. 타일 또는 장식 드래그와 제스처가 겹칠 때 기준점이 튀지 않는지 확인한다.
5. 속성 ScrollRect, 설정창, 파티클 편집기, 팝업, 창 밖, 포커스 전환에서 카메라 오동작이 없는지 확인한다.
6. 관성 도중 Ctrl/Shift 입력이나 패널 진입으로 재생 속도·줌·스크롤 대상이 갑자기 바뀌지 않는지 확인한다.
7. Retina·외부 모니터·창 크기 변경·전체 화면, 자연스러운 스크롤 켜짐/꺼짐, 마우스 병용을 확인한다.
8. 편집 모드와 재생 모드 전환, 모드 비활성화 및 재활성화, 네이티브 라이브러리 로드 실패 시 기존 입력 복귀를 확인한다.

이번 조사로 확인한 것은 정적 연결 지점과 타 앱의 문서·구현 패턴이다. 네이티브 입력 수신, 카메라 패치 및 실제 트랙패드 감도 검증은 아직 수행하지 않았다.
