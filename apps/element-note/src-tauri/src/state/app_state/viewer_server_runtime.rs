use super::*;
use crate::{models::obs::ObsStatus, services::obs_bridge::ServerReach};

// OBS 스위치로 서버를 다시 띄울 때, 연결돼 있던 페이지가 소켓을 놓기를 기다리는 한도
const VIEWER_SERVER_RESTART_GRACE: Duration = Duration::from_millis(500);

/// 로컬 키뷰어 서버를 켤지, 켠다면 어디까지 열지 정하는 단 하나의 판정.
/// 게임 화면 속 키뷰어(같은 PC)와 OBS 브라우저 소스(다른 PC일 수 있음)가 이 서버를 함께 쓴다.
/// 바탕화면 오버레이는 이 서버와 무관하다
pub(super) fn viewer_server_plan(
    in_game_viewers_enabled: bool,
    obs_mode_enabled: bool,
) -> Option<ServerReach> {
    if obs_mode_enabled {
        Some(ServerReach::Lan)
    } else if in_game_viewers_enabled {
        Some(ServerReach::Loopback)
    } else {
        None
    }
}

impl AppState {
    /// 게임 화면·OBS 페이지를 내주는 서버가 떠 있는지
    pub fn is_viewer_server_running(&self) -> bool {
        self.obs_bridge.is_running()
    }

    /// 서버를 저장된 두 스위치(얼불춤 안·OBS)에 맞춘다.
    /// 부팅 때와 둘 중 하나가 바뀔 때마다 부른다 - 서버를 켜고 끄는 길은 여기뿐이다
    pub fn reconcile_viewer_server(&self, app: &AppHandle) {
        let app = app.clone();
        tauri::async_runtime::spawn(async move {
            let state = app.state::<AppState>();
            let _lifecycle_guard = state.obs_lifecycle_lock.lock().await;
            if let Err(error) = state.reconcile_viewer_server_locked(&app).await {
                log::error!("[ViewerServer] 서버를 설정에 맞추지 못함: {error:#}");
            }
        });
    }

    /// obs_lifecycle_lock을 쥔 채로 부른다. 서버 상태가 달라졌으면 설정 화면(obs:status)과
    /// 게임(native-viewers:changed)에 알린다
    pub(crate) async fn reconcile_viewer_server_locked(
        &self,
        app: &AppHandle,
    ) -> Result<ObsStatus> {
        self.prepare_viewer_server(app);
        let outcome = self.apply_viewer_server_plan().await;
        let status = self.obs_bridge.status();
        // 그대로면 조용히 둔다. 시작 실패도 "꺼짐"으로 바뀐 것이라 알린다
        if !matches!(outcome, Ok(false)) {
            let _ = app.emit("obs:status", &status);
            crate::services::native_viewers::notify_changed(app);
        }
        outcome.map(|_| status)
    }

    /// 저장된 두 스위치대로 서버를 켜고 끈다. 달라진 것이 있으면 true.
    /// 창과 이벤트를 건드리지 않는다 - 오버레이 창은 이 서버와 따로 논다
    pub(crate) async fn apply_viewer_server_plan(&self) -> Result<bool> {
        let (in_game_viewers_enabled, obs_mode_enabled, port) = self.store.with_state(|state| {
            (
                state.in_game_viewers_enabled,
                state.obs_mode_enabled,
                state.obs_port,
            )
        });
        let wanted = viewer_server_plan(in_game_viewers_enabled, obs_mode_enabled);
        if wanted == self.obs_bridge.reach() {
            return Ok(false);
        }
        // 범위가 바뀌면 다른 주소에 다시 바인딩해야 한다 - 같은 포트와 토큰으로 다시 띄운다
        let restarting = self.obs_bridge.is_running();
        self.obs_bridge.stop();
        let Some(reach) = wanted else {
            return Ok(true);
        };
        if restarting {
            self.obs_bridge
                .wait_for_sessions_to_close(VIEWER_SERVER_RESTART_GRACE)
                .await;
        }
        // 저장 불가 시 시작 중단, 서버 토큰의 디스크 존재 보장
        let token = self.resolve_and_save_obs_token()?;
        let actual_port = self
            .obs_bridge
            .start(port, token, reach)
            .await
            .map_err(|error| anyhow!(error))?;
        // 성공한 포트를 store에 저장 (fallback 시 다음 시작에 재사용)
        // 저장 실패 시 실행 서버 유지, 다음 시작에서 fallback 재시도
        if actual_port != port {
            if let Err(error) = self.store.update(|state| {
                state.obs_port = actual_port;
            }) {
                log::warn!("[ViewerServer] fallback 포트 저장 실패: {error}");
            }
        }
        // 초기 스냅샷 캐싱 (신규 클라이언트에 전송됨)
        self.refresh_obs_snapshot();
        Ok(true)
    }

    /// 서버가 페이지 파일과 RPC를 내줄 수 있게 앱 핸들을 건넨다
    fn prepare_viewer_server(&self, app: &AppHandle) {
        if cfg!(debug_assertions) {
            // dev 모드: Vite dev server로 리다이렉트
            self.obs_bridge
                .set_dev_url("http://localhost:3400".to_string());
        } else {
            // 프로덕션: Tauri 임베딩 에셋으로 서빙 (포터블 단일 exe 지원)
            let handle = app.clone();
            let fetcher = std::sync::Arc::new(move |path: &str| {
                let resolver = handle.asset_resolver();
                resolver.get(path.into()).map(|asset| {
                    let mime = asset.mime_type.clone();
                    (asset.bytes.to_vec(), mime)
                })
            });
            self.obs_bridge.set_asset_fetcher(fetcher);
        }
        // AppHandle 전달 (invoke_request 디스패치용)
        self.obs_bridge.set_app_handle(app.clone());
    }
}
