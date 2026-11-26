// Test tool for CadPreviewReader: prints what it finds in each file and writes the preview PNGs.
// Built and run by run_tests.sh: CadPreviewTest <output dir> <files...>

import Foundation

let arguments = CommandLine.arguments
guard arguments.count >= 3 else {
    print("usage: CadPreviewTest <output dir> <files...>")
    exit(2)
}

let output = URL(fileURLWithPath: arguments[1], isDirectory: true)
try? FileManager.default.createDirectory(at: output, withIntermediateDirectories: true)
var failures = 0
for path in arguments.dropFirst(2) {
    let url = URL(fileURLWithPath: path)
    let start = Date()
    do {
        let preview = try CadPreviewReader.read(url: url)
        let ms = Int(Date().timeIntervalSince(start) * 1000)
        let name = url.deletingPathExtension().lastPathComponent + "." + url.pathExtension.lowercased() + ".png"
        try preview.pngData.write(to: output.appendingPathComponent(name))
        print("OK    \(preview.image.width)x\(preview.image.height) alpha=\(preview.hasAlpha) \(ms)ms [\(preview.badge)] \(preview.summary) — \(url.lastPathComponent)")
    } catch {
        let ms = Int(Date().timeIntervalSince(start) * 1000)
        print("NONE  \(error) \(ms)ms — \(url.lastPathComponent)")
        if case CadPreviewError.unreadable = error { failures += 1 }
    }
}
exit(failures == 0 ? 0 : 1)
