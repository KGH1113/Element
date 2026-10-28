import AppKit
import Darwin

private enum ArgKey: String {
    case mainPid = "--main-pid"
    case mainBundleId = "--main-bundle-id"
    case mainBundlePath = "--main-bundle-path"
}

private func argValue(_ key: ArgKey) -> String? {
    let args = CommandLine.arguments
    guard let index = args.firstIndex(of: key.rawValue), index + 1 < args.count else {
        return nil
    }
    return args[index + 1]
}

final class DockHelperAppDelegate: NSObject, NSApplicationDelegate {
    private let mainPid: pid_t?
    private let mainBundleId: String
    private let mainBundlePath: String?
    private var monitorTimer: Timer?
    private lazy var dockMenu: NSMenu = {
        let menu = NSMenu()
        let openItem = NSMenuItem(
            title: "Open ImplDmNote",
            action: #selector(openMainFromMenu),
            keyEquivalent: ""
        )
        openItem.target = self
        menu.addItem(openItem)

        menu.addItem(NSMenuItem.separator())

        let quitItem = NSMenuItem(
            title: "Quit ImplDmNote",
            action: #selector(quitMainAndHelper),
            keyEquivalent: "q"
        )
        quitItem.target = self
        menu.addItem(quitItem)
        return menu
    }()

    init(mainPid: pid_t?, mainBundleId: String, mainBundlePath: String?) {
        self.mainPid = mainPid
        self.mainBundleId = mainBundleId
        self.mainBundlePath = mainBundlePath
    }

    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.regular)
        startMainProcessMonitor()
    }

    func applicationDockMenu(_ sender: NSApplication) -> NSMenu? {
        dockMenu
    }

    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        activateOrLaunchMain()
        return false
    }

    @objc private func openMainFromMenu() {
        activateOrLaunchMain()
    }

    @objc private func quitMainAndHelper() {
        terminateMainApplications()
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.8) { [mainBundleId] in
            let running = NSRunningApplication.runningApplications(withBundleIdentifier: mainBundleId)
            for app in running where !app.isTerminated {
                app.forceTerminate()
            }
            NSApp.terminate(nil)
        }
    }

    private func startMainProcessMonitor() {
        monitorTimer?.invalidate()
        monitorTimer = Timer.scheduledTimer(withTimeInterval: 1.0, repeats: true) { [weak self] _ in
            guard let self else { return }
            if self.mainIsAlive() {
                return
            }
            NSApp.terminate(nil)
        }
    }

    private func mainIsAlive() -> Bool {
        if let pid = mainPid {
            return kill(pid, 0) == 0
        }

        let apps = NSRunningApplication.runningApplications(withBundleIdentifier: mainBundleId)
        return apps.contains { !$0.isTerminated }
    }

    private func terminateMainApplications() {
        if let pid = mainPid, let app = NSRunningApplication(processIdentifier: pid), !app.isTerminated {
            _ = app.terminate()
        }

        let running = NSRunningApplication.runningApplications(withBundleIdentifier: mainBundleId)
        for app in running where !app.isTerminated {
            _ = app.terminate()
        }
    }

    private func activateOrLaunchMain() {
        let workspace = NSWorkspace.shared
        let running = mainPid
            .flatMap { NSRunningApplication(processIdentifier: $0) }
            .flatMap { $0.isTerminated ? nil : $0 }
            ?? NSRunningApplication
            .runningApplications(withBundleIdentifier: mainBundleId)
            .first(where: { !$0.isTerminated })
        // Opening the bundle, even while it runs, makes LaunchServices send it
        // a reopen event, which shows a window hidden in the background (tray
        // mode or a game launch). Activation alone would leave it hidden.
        guard let bundleURL = running?.bundleURL
            ?? mainBundlePath.map({ URL(fileURLWithPath: $0) })
            ?? workspace.urlForApplication(withBundleIdentifier: mainBundleId)
        else {
            running?.activate(options: [.activateIgnoringOtherApps])
            return
        }
        let configuration = NSWorkspace.OpenConfiguration()
        configuration.activates = true
        workspace.openApplication(at: bundleURL, configuration: configuration) { _, _ in }
    }
}

let mainPid = argValue(.mainPid).flatMap { pid_t($0) }
let mainBundleId = argValue(.mainBundleId) ?? "io.github.kgh1113.impldmnote"
let mainBundlePath = argValue(.mainBundlePath)

let app = NSApplication.shared
ProcessInfo.processInfo.processName = "ImplDmNote"
let delegate = DockHelperAppDelegate(
    mainPid: mainPid,
    mainBundleId: mainBundleId,
    mainBundlePath: mainBundlePath
)
app.delegate = delegate
app.setActivationPolicy(.regular)
app.run()
