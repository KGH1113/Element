//! Capture the whole native webview, including DOM text, CSS and WebGL, with alpha.
use tauri::WebviewWindow;

pub fn supported() -> bool {
    cfg!(any(target_os = "macos", target_os = "windows"))
}

pub async fn frame(window: &WebviewWindow, width: u32, height: u32, rgba: bool) -> Result<Vec<u8>, String> {
    let snapshot_width = width as f64 / window.scale_factor().map_err(|e| e.to_string())?;
    let (sender, receiver) = tokio::sync::oneshot::channel();
    window
        .with_webview(move |webview| capture(webview, sender, width, height, rgba, snapshot_width))
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
    width: u32,
    height: u32,
    rgba: bool,
    snapshot_width: f64,
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
        if rgba && error.is_null() && !image.is_null() {
            // Retain only the immutable CGImage, then copy pixels off the UI thread.
            // This avoids NSImage -> TIFF -> PNG -> FFmpeg PNG decoding per frame.
            unsafe {
                let image: *mut Object = msg_send![image.cast::<Object>(), CGImageForProposedRect:std::ptr::null_mut::<c_void>() context:std::ptr::null_mut::<Object>() hints:std::ptr::null_mut::<Object>()];
                if !image.is_null() {
                    let retained = raw::retain(image.cast()) as usize;
                    let sender = sender.lock().unwrap().take();
                    tauri::async_runtime::spawn_blocking(move || {
                        let result = raw::pixels(retained, width, height);
                        raw::release(retained);
                        if let Some(sender) = sender { let _ = sender.send(result); }
                    });
                    return;
                }
                if let Some(sender) = sender.lock().unwrap().take() {
                    let _ = sender.send(Err("render-image-conversion-failed".into()));
                }
                return;
            }
        }
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
        // Do not capture at Retina resolution and downsample it again in FFmpeg.
        let point_width: *mut Object = msg_send![class!(NSNumber), numberWithDouble:snapshot_width];
        let _: () = msg_send![config, setSnapshotWidth:point_width];
        let _: () = msg_send![view, takeSnapshotWithConfiguration:config completionHandler:RcBlock::as_ptr(&block)];
        let _: () = msg_send![config, release];
    }
}

#[cfg(target_os = "windows")]
fn capture(
    webview: tauri::webview::PlatformWebview,
    sender: tokio::sync::oneshot::Sender<Result<Vec<u8>, String>>,
    _width: u32,
    _height: u32,
    _rgba: bool,
    _snapshot_width: f64,
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
    _width: u32,
    _height: u32,
    _rgba: bool,
    _snapshot_width: f64,
) {
    let _ = sender.send(Err("render-platform-unsupported".into()));
}

#[cfg(target_os = "macos")]
mod raw {
    use std::ffi::c_void;
    #[repr(C)] struct Point { x: f64, y: f64 }
    #[repr(C)] struct Size { width: f64, height: f64 }
    #[repr(C)] struct Rect { origin: Point, size: Size }
    #[link(name = "CoreGraphics", kind = "framework")]
    extern "C" {
        fn CGImageRetain(image: *const c_void) -> *const c_void;
        fn CGImageRelease(image: *const c_void);
        fn CGColorSpaceCreateDeviceRGB() -> *const c_void;
        fn CGColorSpaceRelease(space: *const c_void);
        fn CGBitmapContextCreate(data: *mut c_void, width: usize, height: usize, bits: usize, stride: usize, space: *const c_void, info: u32) -> *mut c_void;
        fn CGContextDrawImage(context: *mut c_void, rect: Rect, image: *const c_void);
        fn CGContextRelease(context: *mut c_void);
    }
    pub unsafe fn retain(image: *const c_void) -> *const c_void { CGImageRetain(image) }
    pub unsafe fn release(image: usize) { CGImageRelease(image as *const c_void); }
    pub fn pixels(image: usize, width: u32, height: u32) -> Result<Vec<u8>, String> {
        let length = width as usize * height as usize * 4;
        if width == 0 || height == 0 || length > 128 * 1024 * 1024 { return Err("render-frame-too-large".into()); }
        let mut bytes = vec![0; length];
        unsafe {
            let space = CGColorSpaceCreateDeviceRGB();
            // 32-bit big endian + premultiplied-last is byte ordered RGBA.
            let context = CGBitmapContextCreate(bytes.as_mut_ptr().cast(), width as usize, height as usize,
                8, width as usize * 4, space, (4 << 12) | 1);
            CGColorSpaceRelease(space);
            if context.is_null() { return Err("render-bitmap-allocation-failed".into()); }
            CGContextDrawImage(context, Rect { origin: Point { x: 0.0, y: 0.0 }, size: Size { width: width as f64, height: height as f64 } }, image as *const c_void);
            CGContextRelease(context);
        }
        // FFmpeg overlay expects straight alpha, unlike the native bitmap.
        for pixel in bytes.chunks_exact_mut(4) {
            let alpha = pixel[3] as u32;
            if alpha > 0 && alpha < 255 {
                for channel in &mut pixel[..3] { *channel = ((*channel as u32 * 255 + alpha / 2) / alpha).min(255) as u8; }
            }
        }
        Ok(bytes)
    }
}
