//! Native WebSocket byte transport. The TypeScript SDK owns the wire protocol and reconnect policy.
use crate::errors::{CmdResult, CommandError};
use futures_util::{SinkExt, StreamExt};
use serde::Serialize;
use std::{collections::HashMap, sync::Mutex, time::Duration};
use tauri::{ipc::Channel, Manager, State, WebviewWindow};
use tokio::sync::{mpsc, oneshot};
use tokio_tungstenite::tungstenite::{
    client::IntoClientRequest, http::HeaderValue, protocol::WebSocketConfig, Message,
};

const MAX_MESSAGE_BYTES: usize = 1024 * 1024;
#[derive(Default)]
pub struct NativeIpcSockets(Mutex<HashMap<String, SocketHandle>>);
impl NativeIpcSockets {
    pub fn close_all(&self) {
        if let Ok(mut sockets) = self.0.lock() {
            for (_, socket) in sockets.drain() {
                let _ = socket.close.send(());
            }
        }
    }
}
struct SocketHandle {
    sender: mpsc::Sender<String>,
    close: oneshot::Sender<()>,
}
#[derive(Clone, Serialize)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum NativeSocketEvent {
    Message { text: String },
    Closed { reason: String },
}
fn validate_url(value: &str) -> CmdResult<()> {
    let url =
        reqwest::Url::parse(value).map_err(|_| CommandError::msg("Invalid local IPC address"))?;
    if url.scheme() != "ws"
        || !matches!(url.host_str(), Some("127.0.0.1") | Some("localhost"))
        || !url
            .port()
            .is_some_and(|port| (32145..=32155).contains(&port))
        || url.path() != "/ipc/ws"
        || url.query().is_some()
        || url.fragment().is_some()
        || !url.username().is_empty()
        || url.password().is_some()
    {
        return Err(CommandError::msg(
            "Only the local ADOFAI WebSocket is allowed",
        ));
    }
    Ok(())
}
fn require_main(window: &WebviewWindow) -> CmdResult<()> {
    if window.label() != "main" {
        return Err(CommandError::msg(
            "IPC connections belong to the main window",
        ));
    }
    Ok(())
}
#[tauri::command]
pub async fn adofai_ipc_ws_connect(
    window: WebviewWindow,
    state: State<'_, NativeIpcSockets>,
    socket_id: String,
    url: String,
    protocol: String,
    events: Channel<NativeSocketEvent>,
) -> CmdResult<()> {
    require_main(&window)?;
    validate_url(&url)?;
    if uuid::Uuid::parse_str(&socket_id).is_err() || protocol != "adofai-ipc.v3" {
        return Err(CommandError::msg("Invalid IPC socket or protocol"));
    }
    let mut request = url
        .into_client_request()
        .map_err(|_| CommandError::msg("Could not prepare IPC connection"))?;
    request.headers_mut().insert(
        "Sec-WebSocket-Protocol",
        HeaderValue::from_static("adofai-ipc.v3"),
    );
    let config = WebSocketConfig {
        max_message_size: Some(MAX_MESSAGE_BYTES),
        max_frame_size: Some(MAX_MESSAGE_BYTES),
        ..Default::default()
    };
    let (socket, response) = tokio::time::timeout(
        Duration::from_millis(1500),
        tokio_tungstenite::connect_async_with_config(request, Some(config), false),
    )
    .await
    .map_err(|_| CommandError::msg("Local IPC connection timed out"))?
    .map_err(|error| CommandError::msg(format!("Could not connect to local IPC: {error}")))?;
    if response
        .headers()
        .get("Sec-WebSocket-Protocol")
        .and_then(|value| value.to_str().ok())
        != Some("adofai-ipc.v3")
    {
        return Err(CommandError::msg(
            "The game does not support ADOFAI-IPC 1.0.0",
        ));
    }
    let (sender, mut receiver) = mpsc::channel::<String>(32);
    let (close, mut closing) = oneshot::channel();
    {
        let mut sockets = state
            .0
            .lock()
            .map_err(|_| CommandError::msg("IPC transport is unavailable"))?;
        if sockets.contains_key(&socket_id) {
            return Err(CommandError::msg("IPC socket already exists"));
        }
        sockets.insert(socket_id.clone(), SocketHandle { sender, close });
    }
    let app = window.app_handle().clone();
    tauri::async_runtime::spawn(async move {
        use tauri::Manager;
        let (mut write, mut read) = socket.split();
        let reason = loop {
            tokio::select! {
                _ = &mut closing => { let _ = write.send(Message::Close(None)).await; break "Connection closed".to_string(); }
                text = receiver.recv() => match text {
                    Some(text) => if let Err(error) = write.send(Message::Text(text)).await { break error.to_string(); },
                    None => break "Connection closed".to_string(),
                },
                incoming = read.next() => match incoming {
                    Some(Ok(Message::Text(text))) => if events.send(NativeSocketEvent::Message { text }).is_err() { break "Main window closed".to_string(); },
                    Some(Ok(Message::Ping(bytes))) => if let Err(error) = write.send(Message::Pong(bytes)).await { break error.to_string(); },
                    Some(Ok(Message::Close(_))) | None => break "Game IPC disconnected".to_string(),
                    Some(Err(error)) => break error.to_string(),
                    _ => {},
                }
            }
        };
        if let Ok(mut sockets) = app.state::<NativeIpcSockets>().0.lock() {
            sockets.remove(&socket_id);
            // A late close from an old probe must not end a newer connection's render session.
            // Keep registration blocked until input restoration completes.
            if sockets.is_empty() {
                app.state::<crate::services::render::RenderService>()
                    .end(&app, None);
            }
        }
        let _ = events.send(NativeSocketEvent::Closed { reason });
    });
    Ok(())
}
#[tauri::command]
pub async fn adofai_ipc_ws_send(
    window: WebviewWindow,
    state: State<'_, NativeIpcSockets>,
    socket_id: String,
    text: String,
) -> CmdResult<()> {
    require_main(&window)?;
    if text.len() > MAX_MESSAGE_BYTES {
        return Err(CommandError::msg("IPC message is too large"));
    }
    let sender = state
        .0
        .lock()
        .map_err(|_| CommandError::msg("IPC transport is unavailable"))?
        .get(&socket_id)
        .map(|socket| socket.sender.clone())
        .ok_or_else(|| CommandError::msg("IPC socket is disconnected"))?;
    sender
        .send(text)
        .await
        .map_err(|_| CommandError::msg("IPC socket is disconnected"))
}
#[tauri::command]
pub fn adofai_ipc_ws_close(
    window: WebviewWindow,
    state: State<'_, NativeIpcSockets>,
    socket_id: String,
) -> CmdResult<()> {
    require_main(&window)?;
    if let Some(socket) = state
        .0
        .lock()
        .map_err(|_| CommandError::msg("IPC transport is unavailable"))?
        .remove(&socket_id)
    {
        let _ = socket.close.send(());
    }
    Ok(())
}
#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn restricts_transport_to_exact_local_websocket_surface() {
        assert!(validate_url("ws://127.0.0.1:32145/ipc/ws").is_ok());
        assert!(validate_url("ws://localhost:32155/ipc/ws").is_ok());
        for url in [
            "http://127.0.0.1:32145/ipc/ws",
            "ws://example.com:32145/ipc/ws",
            "ws://127.0.0.1:3000/ipc/ws",
            "ws://127.0.0.1:32145/ipc/ws/other",
            "ws://user@127.0.0.1:32145/ipc/ws",
            "ws://127.0.0.1:32145/ipc/ws?proxy=true",
        ] {
            assert!(validate_url(url).is_err(), "accepted {url}");
        }
    }
}
