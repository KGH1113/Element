use std::future::Future;

use tauri::{AppHandle, Emitter, State};
use uuid::Uuid;

use crate::{
    commands::run_mutation,
    errors::CmdResult,
    models::{obs::ObsStatus, SettingsPatchInput},
    state::AppState,
};

async fn run_obs_lifecycle<T, Operation, OperationFuture>(
    state: &AppState,
    operation: Operation,
) -> T
where
    Operation: FnOnce() -> OperationFuture,
    OperationFuture: Future<Output = T>,
{
    let _lifecycle_guard = state.obs_lifecycle_lock.lock().await;
    operation().await
}

fn persist_and_apply_obs_token(state: &AppState, token: String) -> CmdResult<ObsStatus> {
    // 디스크 저장 성공 뒤 메모리 토큰 교체, 실패 시 기존 인증 상태 유지
    let stored_token = token.clone();
    state.store.update(|s| {
        s.obs_token = Some(stored_token.clone());
    })?;
    if state.obs_bridge.is_running() {
        state.obs_bridge.set_token(token);
    }
    Ok(state.obs_bridge.status())
}

/// OBS 스위치를 저장하고 다른 설정처럼 알린다.
/// 잠금 순서 고정: OBS lifecycle mutex -> run_mutation mutation ticket
async fn persist_obs_offered(app: &AppHandle, offered: bool) -> CmdResult<()> {
    run_mutation(app.clone(), move |app, state| {
        let diff = state.settings.apply_patch(SettingsPatchInput {
            obs_mode_enabled: Some(offered),
            ..Default::default()
        })?;
        Ok(state.emit_settings_changed(&diff, app)?)
    })
    .await
}

/// OBS 스위치: 브라우저 소스를 내줄지만 정한다. 서버를 실제로 켜고 끄는 일은
/// reconcile이 맡는다 - 게임 화면 키뷰어가 켜져 있으면 OBS를 꺼도 서버는 남는다
async fn set_obs_offered(app: &AppHandle, state: &AppState, offered: bool) -> CmdResult<ObsStatus> {
    run_obs_lifecycle(state, || async {
        persist_obs_offered(app, offered).await?;
        match state.reconcile_viewer_server_locked(app).await {
            Ok(status) => Ok(status),
            Err(error) => {
                // 켜지 못했으면 스위치를 되돌리고, 게임 화면용 서버라도 다시 띄운다
                if offered {
                    if let Err(revert_error) = persist_obs_offered(app, false).await {
                        log::error!("[ObsBridge] OBS 스위치 복구 실패: {revert_error:#}");
                    }
                    if let Err(retry_error) = state.reconcile_viewer_server_locked(app).await {
                        log::error!("[ObsBridge] 서버 복구 실패: {retry_error:#}");
                    }
                }
                Err(error.into())
            }
        }
    })
    .await
}

#[tauri::command]
pub async fn obs_start(app: AppHandle, state: State<'_, AppState>) -> CmdResult<ObsStatus> {
    set_obs_offered(&app, &state, true).await
}

#[tauri::command]
pub async fn obs_stop(app: AppHandle, state: State<'_, AppState>) -> CmdResult<ObsStatus> {
    set_obs_offered(&app, &state, false).await
}

#[tauri::command]
pub fn obs_status(state: State<'_, AppState>) -> CmdResult<ObsStatus> {
    Ok(state.obs_bridge.status())
}

#[tauri::command]
pub async fn obs_regenerate_token(
    app: AppHandle,
    state: State<'_, AppState>,
) -> CmdResult<ObsStatus> {
    run_obs_lifecycle(&state, || async move {
        // 잠금 순서 고정: OBS lifecycle mutex -> run_mutation mutation ticket
        // mutation ticket은 run_mutation의 blocking 작업 안에서만 보유
        let token = Uuid::new_v4().simple().to_string();
        run_mutation(app, move |app, state| {
            let status = persist_and_apply_obs_token(state, token)?;
            let _ = app.emit("obs:status", &status);
            // 게임 화면 키뷰어도 같은 토큰으로 접속한다 - 새 주소를 바로 받아 가게 한다
            crate::services::native_viewers::notify_changed(app);
            Ok(status)
        })
        .await
    })
    .await
}

#[cfg(test)]
mod tests {
    use std::{sync::Arc, time::Duration};

    use futures_util::{SinkExt, StreamExt};
    use tokio::sync::oneshot;
    use tokio_tungstenite::{connect_async, tungstenite::Message};

    use super::*;
    use crate::{
        errors::CommandError,
        models::obs::{make_envelope, ObsEnvelope, OBS_PROTOCOL_VERSION},
        services::obs_bridge::ServerReach,
        state::AppStore,
    };

    async fn start_bridge_with_token(state: &AppState, port: u16, token: String) -> CmdResult<u16> {
        state
            .obs_bridge
            .start(port, token, ServerReach::Loopback)
            .await
            .map_err(CommandError::msg)
    }

    fn test_directory(label: &str) -> std::path::PathBuf {
        std::env::temp_dir().join(format!("dmnote-{label}-{}", Uuid::new_v4()))
    }

    async fn authenticate(port: u16, token: &str) -> ObsEnvelope {
        let (mut websocket, _) = connect_async(format!("ws://127.0.0.1:{port}"))
            .await
            .expect("OBS WebSocket 연결 실패");
        let hello = make_envelope(
            "hello",
            0,
            serde_json::json!({ "token": token, "protocol": OBS_PROTOCOL_VERSION }),
        );
        websocket
            .send(Message::text(hello.to_string()))
            .await
            .expect("OBS hello 전송 실패");

        tokio::time::timeout(Duration::from_secs(2), async {
            loop {
                match websocket.next().await {
                    Some(Ok(Message::Text(text))) => {
                        return serde_json::from_str(&text).expect("OBS 응답 파싱 실패");
                    }
                    Some(Ok(_)) => {}
                    Some(Err(error)) => panic!("OBS 응답 수신 실패: {error}"),
                    None => panic!("OBS 응답 전 연결 종료"),
                }
            }
        })
        .await
        .expect("OBS 인증 응답 타임아웃")
    }

    #[tokio::test]
    async fn lifecycle_lock_serializes_start_token_capture_and_regeneration() {
        let directory = test_directory("obs-token-lifecycle-race");
        let store = AppStore::initialize_for_test(&directory).unwrap();
        store
            .update(|data| {
                data.obs_token = Some("old-token".to_string());
            })
            .unwrap();
        let state = Arc::new(AppState::initialize(store).unwrap());

        let (captured_tx, captured_rx) = oneshot::channel();
        let (release_start_tx, release_start_rx) = oneshot::channel();
        let start_state = Arc::clone(&state);
        let start_task = tokio::spawn(async move {
            run_obs_lifecycle(&start_state, || async {
                let captured_token = start_state.resolve_and_save_obs_token().unwrap();
                captured_tx.send(captured_token.clone()).unwrap();
                release_start_rx.await.unwrap();
                let port = start_bridge_with_token(&start_state, 0, captured_token.clone())
                    .await
                    .unwrap();
                (port, captured_token)
            })
            .await
        });

        assert_eq!(captured_rx.await.unwrap(), "old-token");

        let (regenerate_attempted_tx, regenerate_attempted_rx) = oneshot::channel();
        let (regenerated_tx, mut regenerated_rx) = oneshot::channel();
        let regenerate_state = Arc::clone(&state);
        let regenerate_task = tokio::spawn(async move {
            regenerate_attempted_tx.send(()).unwrap();
            let status = run_obs_lifecycle(&regenerate_state, || async {
                persist_and_apply_obs_token(&regenerate_state, "new-token".to_string()).unwrap()
            })
            .await;
            regenerated_tx.send(status).unwrap();
        });

        regenerate_attempted_rx.await.unwrap();
        assert_eq!(
            state.store.with_state(|data| data.obs_token.clone()),
            Some("old-token".to_string())
        );
        assert!(matches!(
            regenerated_rx.try_recv(),
            Err(oneshot::error::TryRecvError::Empty)
        ));

        release_start_tx.send(()).unwrap();
        let (port, captured_token) = start_task.await.unwrap();
        let regenerated_status = regenerated_rx.await.unwrap();
        regenerate_task.await.unwrap();

        let stored_token = state.store.with_state(|data| data.obs_token.clone());
        let final_status = state.obs_bridge.status();
        assert_eq!(captured_token, "old-token");
        assert_eq!(stored_token.as_deref(), Some("new-token"));
        assert_eq!(regenerated_status.token.as_deref(), Some("new-token"));
        assert_eq!(final_status.token, stored_token);

        let rejected = authenticate(port, "old-token").await;
        assert_eq!(rejected.msg_type, "error");
        assert_eq!(rejected.payload["code"], "AUTH_FAILED");
        let accepted = authenticate(port, "new-token").await;
        assert_eq!(accepted.msg_type, "hello_ack");

        state.obs_bridge.stop();
        state.store.flush_and_shutdown().unwrap();
        drop(state);
        std::fs::remove_dir_all(directory).unwrap();
    }
}
