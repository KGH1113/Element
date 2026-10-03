use std::{collections::HashMap, time::Duration};

use serde::{Deserialize, Serialize};

use crate::errors::{CmdResult, CommandError};

const FIRST_IPC_PORT: u16 = 32_145;
const LAST_IPC_PORT: u16 = 32_155;

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AdofaiIpcHttpRequest {
    pub url: String,
    pub method: String,
    #[serde(default)]
    pub headers: HashMap<String, String>,
    pub body: Option<String>,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct AdofaiIpcHttpResponse {
    pub status: u16,
    pub headers: HashMap<String, String>,
    pub body: String,
}

fn validate_url(url: &reqwest::Url) -> CmdResult<()> {
    let port = url
        .port()
        .ok_or_else(|| CommandError::msg("ADOFAI-IPC URL must include a port"))?;
    let is_loopback = matches!(url.host_str(), Some("127.0.0.1") | Some("localhost"));
    let is_ipc_port = (FIRST_IPC_PORT..=LAST_IPC_PORT).contains(&port);

    if url.scheme() != "http" || !is_loopback || !is_ipc_port || !url.path().starts_with("/ipc") {
        return Err(CommandError::msg("Rejected non-local ADOFAI-IPC URL"));
    }

    Ok(())
}

#[tauri::command]
pub async fn adofai_ipc_http_request(
    request: AdofaiIpcHttpRequest,
) -> CmdResult<AdofaiIpcHttpResponse> {
    let url = reqwest::Url::parse(&request.url)
        .map_err(|error| CommandError::msg(format!("Invalid ADOFAI-IPC URL: {error}")))?;
    validate_url(&url)?;

    let method = reqwest::Method::from_bytes(request.method.as_bytes())
        .map_err(|error| CommandError::msg(format!("Invalid HTTP method: {error}")))?;
    if method != reqwest::Method::GET && method != reqwest::Method::POST {
        return Err(CommandError::msg(
            "Only GET and POST are allowed for ADOFAI-IPC",
        ));
    }

    // Render frames reuse the existing loopback connection rather than creating
    // and tearing down a TCP client for every pull and acknowledgement.
    static CLIENT: once_cell::sync::Lazy<reqwest::Client> = once_cell::sync::Lazy::new(|| {
        reqwest::Client::builder().connect_timeout(Duration::from_millis(250))
            .timeout(Duration::from_secs(2)).no_proxy().build().expect("IPC HTTP client")
    });
    let client = &*CLIENT;

    let mut builder = client.request(method, url);
    for (name, value) in request.headers {
        builder = builder.header(name, value);
    }
    if let Some(body) = request.body {
        builder = builder.body(body);
    }

    let response = builder
        .send()
        .await
        .map_err(|error| CommandError::msg(format!("ADOFAI-IPC request failed: {error}")))?;
    let status = response.status().as_u16();
    let headers = response
        .headers()
        .iter()
        .filter_map(|(name, value)| {
            value
                .to_str()
                .ok()
                .map(|value| (name.as_str().to_owned(), value.to_owned()))
        })
        .collect();
    let body = response
        .text()
        .await
        .map_err(|error| CommandError::msg(format!("Could not read IPC response: {error}")))?;

    Ok(AdofaiIpcHttpResponse {
        status,
        headers,
        body,
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn accepts_only_the_local_ipc_surface() {
        assert!(
            validate_url(&reqwest::Url::parse("http://127.0.0.1:32145/ipc/health").unwrap())
                .is_ok()
        );
        assert!(validate_url(&reqwest::Url::parse("http://localhost:32155/ipc").unwrap()).is_ok());
        assert!(
            validate_url(&reqwest::Url::parse("http://example.com:32145/ipc").unwrap()).is_err()
        );
        assert!(validate_url(&reqwest::Url::parse("http://127.0.0.1:3000/ipc").unwrap()).is_err());
        assert!(
            validate_url(&reqwest::Url::parse("http://127.0.0.1:32145/other").unwrap()).is_err()
        );
    }
}
