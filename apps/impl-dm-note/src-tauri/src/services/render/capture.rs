//! Capture the whole native webview, including DOM text, CSS and WebGL, with alpha.
use tauri::WebviewWindow;

pub fn supported() -> bool {
    cfg!(any(target_os = "macos", target_os = "windows"))
}

pub async fn png(window: &WebviewWindow) -> Result<Vec<u8>, String> {
    let (sender, receiver) = tokio::sync::oneshot::channel();
    window
        .with_webview(move |webview| capture(webview, sender))
        .map_err(|e| e.to_string())?;
    tokio::time::timeout(std::time::Duration::from_secs(15), receiver)
        .await
        .map_err(|_| "render-native-capture-timeout".to_string())?
        .map_err(|_| "render-native-capture-closed".to_string())?
}

#[cfg(target_os = "macos")]
fn capture(
    webview: tauri::webview::PlatformWebview,
    sender: tokio::sync::oneshot::Sender<Result<Vec<u8>, String>>,
) {
    use block2::RcBlock;
    use objc::runtime::Object;
    use objc::{class, msg_send, sel, sel_impl};
    use std::{
        ffi::c_void,
        sync::{Arc, Mutex},
    };
    // WKWebView retains its completion block until the compositor snapshot completes.
    let sender = Arc::new(Mutex::new(Some(sender)));
    let block = RcBlock::new(move |image: *mut c_void, error: *mut c_void| {
        let result = if !error.is_null() || image.is_null() {
            Err("render-native-capture-failed".into())
        } else {
            unsafe {
                let image = image.cast::<Object>();
                let tiff: *mut Object = msg_send![image, TIFFRepresentation];
                let bitmap: *mut Object =
                    msg_send![class!(NSBitmapImageRep), imageRepWithData:tiff];
                let properties: *mut Object = msg_send![class!(NSDictionary), dictionary];
                let data: *mut Object =
                    msg_send![bitmap, representationUsingType:4usize properties:properties];
                if data.is_null() {
                    Err("render-png-encoding-failed".into())
                } else {
                    let bytes: *const u8 = msg_send![data, bytes];
                    let length: usize = msg_send![data, length];
                    Ok(std::slice::from_raw_parts(bytes, length).to_vec())
                }
            }
        };
        if let Some(sender) = sender.lock().unwrap().take() {
            let _ = sender.send(result);
        }
    });
    unsafe {
        let view = webview.inner().cast::<Object>();
        let config: *mut Object = msg_send![class!(WKSnapshotConfiguration), new];
        let _: () = msg_send![config, setAfterScreenUpdates:true];
        let _: () = msg_send![view, takeSnapshotWithConfiguration:config completionHandler:RcBlock::as_ptr(&block)];
        let _: () = msg_send![config, release];
    }
}

#[cfg(target_os = "windows")]
fn capture(
    webview: tauri::webview::PlatformWebview,
    sender: tokio::sync::oneshot::Sender<Result<Vec<u8>, String>>,
) {
    use webview2_com::{
        callback::CapturePreviewCompletedHandler,
        Microsoft::Web::WebView2::Win32::COREWEBVIEW2_CAPTURE_PREVIEW_IMAGE_FORMAT_PNG,
    };
    use windows::Win32::{
        Foundation::HGLOBAL,
        System::Com::{StructuredStorage::CreateStreamOnHGlobal, STATFLAG_NONAME, STREAM_SEEK_SET},
    };
    let result = unsafe {
        (|| -> windows::core::Result<()> {
            let stream = CreateStreamOnHGlobal(HGLOBAL::default(), true)?;
            let captured = stream.clone();
            let callback = CapturePreviewCompletedHandler::create(Box::new(move |status| {
                let result = (|| -> Result<Vec<u8>, String> {
                    status.ok().map_err(|e| e.to_string())?;
                    let mut stat = Default::default();
                    captured
                        .Stat(&mut stat, STATFLAG_NONAME)
                        .map_err(|e| e.to_string())?;
                    if stat.cbSize > 128 * 1024 * 1024 {
                        return Err("render-frame-too-large".into());
                    }
                    captured
                        .Seek(0, STREAM_SEEK_SET, None)
                        .map_err(|e| e.to_string())?;
                    let mut bytes = vec![0; stat.cbSize as usize];
                    let mut read = 0;
                    captured
                        .Read(
                            bytes.as_mut_ptr().cast(),
                            bytes.len() as u32,
                            Some(&mut read),
                        )
                        .ok()
                        .map_err(|e| e.to_string())?;
                    bytes.truncate(read as usize);
                    Ok(bytes)
                })();
                let _ = sender.send(result);
                Ok(())
            }));
            let core = webview.controller().CoreWebView2()?;
            core.CapturePreview(
                COREWEBVIEW2_CAPTURE_PREVIEW_IMAGE_FORMAT_PNG,
                &stream,
                &callback,
            )?;
            Ok(())
        })()
    };
    if let Err(error) = result {
        log::error!("[render] native capture dispatch failed: {error}");
    }
}

#[cfg(not(any(target_os = "macos", target_os = "windows")))]
fn capture(
    _: tauri::webview::PlatformWebview,
    sender: tokio::sync::oneshot::Sender<Result<Vec<u8>, String>>,
) {
    let _ = sender.send(Err("render-platform-unsupported".into()));
}
