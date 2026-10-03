import AppKit
import WebKit

// An isolated, nonpersistent WKWebView fixture. It never starts ImplDmNote's
// input daemon, reads its user store, activates its app, or joins single-instance IPC.
final class Fixture: NSObject, WKScriptMessageHandler {
    let directory: URL
    let frames: [[String: Any]]
    var reports: [[String: Any]] = []
    var index = 0
    var webview: WKWebView!
    var window: NSWindow!

    init(_ directory: URL) throws {
        self.directory = directory
        frames = try JSONSerialization.jsonObject(with: Data(contentsOf: directory.appendingPathComponent("frames.json"))) as! [[String: Any]]
        super.init()
        let controller = WKUserContentController()
        controller.add(self, name: "renderAck")
        let config = String(data: try Data(contentsOf: directory.appendingPathComponent("config.json")), encoding: .utf8)!
        controller.addUserScript(WKUserScript(source: "window.__dmnRenderConfig=\(config);window.__TAURI_INTERNALS__={invoke:async(command,args)=>{if(command!=='dmnote_render_ack')throw new Error(command);window.webkit.messageHandlers.renderAck.postMessage(args);}};", injectionTime: .atDocumentStart, forMainFrameOnly: true))
        let settings = WKWebViewConfiguration()
        settings.userContentController = controller
        settings.websiteDataStore = .nonPersistent()
        webview = WKWebView(frame: NSRect(x: 0, y: 0, width: 320, height: 320), configuration: settings)
        webview.setValue(false, forKey: "drawsBackground")
        window = NSWindow(contentRect: webview.frame, styleMask: .borderless, backing: .buffered, defer: false)
        window.isOpaque = false
        window.backgroundColor = .clear
        window.contentView = webview
        // The native capture must work while hidden; rendering must not steal focus.
        window.orderOut(nil)
        webview.load(URLRequest(url: URL(string: CommandLine.arguments[2])!))
    }

    func fail(_ message: String) {
        fputs("Native render fixture failed: \(message)\n", stderr)
        exit(1)
    }

    func userContentController(_ userContentController: WKUserContentController, didReceive message: WKScriptMessage) {
        guard let ack = message.body as? [String: Any], let result = ack["result"] as? [String: Any] else { fail("invalid ACK"); return }
        if let error = ack["error"] as? String { fail(error); return }
        if result["ready"] as? Bool == true { sendFrame(); return }
        guard result["frameIndex"] as? Int == index else { fail("wrong frame"); return }
        reports.append(result)
        let configuration = WKSnapshotConfiguration()
        configuration.afterScreenUpdates = true
        webview.takeSnapshot(with: configuration) { image, error in
            guard error == nil, let tiff = image?.tiffRepresentation,
                  let bitmap = NSBitmapImageRep(data: tiff), let png = bitmap.representation(using: .png, properties: [:]) else {
                self.fail(String(describing: error)); return
            }
            do { try png.write(to: self.directory.appendingPathComponent("frame-\(self.index).png")) }
            catch { self.fail(String(describing: error)); return }
            self.index += 1
            self.sendFrame()
        }
    }

    func sendFrame() {
        if index == frames.count {
            do { try JSONSerialization.data(withJSONObject: reports, options: [.sortedKeys]).write(to: directory.appendingPathComponent("reports.json")) }
            catch { fail(String(describing: error)) }
            NSApplication.shared.terminate(nil)
            return
        }
        do {
            let frame = String(data: try JSONSerialization.data(withJSONObject: frames[index]), encoding: .utf8)!
            webview.evaluateJavaScript("void window.__dmnRenderFrame(\(frame));") { _, error in if let error { self.fail(String(describing: error)) } }
        } catch { fail(String(describing: error)) }
    }
}

let app = NSApplication.shared
app.setActivationPolicy(.prohibited)
let fixture = try Fixture(URL(fileURLWithPath: CommandLine.arguments[1], isDirectory: true))
DispatchQueue.main.asyncAfter(deadline: .now() + 45) { fixture.fail("timeout") }
app.run()
