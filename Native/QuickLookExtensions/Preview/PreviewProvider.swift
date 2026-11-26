// Quick Look preview extension (data based): the Space bar preview and Finder's preview pane.
// Shows the preview image the file already carries with a line about the file. Files without a
// preview image throw, and Quick Look falls back to its generic preview (icon, name, size).

import Foundation
import QuickLook
import UniformTypeIdentifiers

@objc(PreviewProvider)
final class PreviewProvider: QLPreviewProvider, QLPreviewingController {
    func providePreview(for request: QLFilePreviewRequest) async throws -> QLPreviewReply {
        let url = request.fileURL
        let preview = try CadPreviewReader.read(url: url)

        var details = preview.summary
        if let bytes = try? url.resourceValues(forKeys: [.fileSizeKey]).fileSize {
            details += " · " + ByteCountFormatter.string(fromByteCount: Int64(bytes), countStyle: .file)
        }
        let html = Self.page(details: details, width: preview.image.width, height: preview.image.height,
                             paper: preview.hasAlpha)

        let reply = QLPreviewReply(dataOfContentType: .html, contentSize: CGSize(width: 800, height: 600)) { reply in
            reply.attachments = ["preview.png": QLPreviewReplyAttachment(data: preview.pngData, contentType: .png)]
            return Data(html.utf8)
        }
        reply.stringEncoding = .utf8
        return reply
    }

    private static func page(details: String, width: Int, height: Int, paper: Bool) -> String {
        let escaped = details
            .replacingOccurrences(of: "&", with: "&amp;")
            .replacingOccurrences(of: "<", with: "&lt;")
            .replacingOccurrences(of: ">", with: "&gt;")
        // Transparent SmartCAD previews (dark lines) sit on white paper; AutoCAD's own previews
        // bring their background with them.
        let imageBackground = paper ? "background: #fff;" : ""
        return """
        <!DOCTYPE html>
        <html><head><meta charset="utf-8">
        <style>
          :root { color-scheme: light dark; }
          html, body { margin: 0; height: 100%; }
          body { display: flex; flex-direction: column; align-items: center; justify-content: center;
                 gap: 14px; padding: 24px; box-sizing: border-box;
                 font: 13px -apple-system, system-ui, sans-serif; color: CanvasText; background: Canvas; }
          img { flex: 1 1 auto; min-height: 0; max-width: 100%; object-fit: contain; \(imageBackground)
                aspect-ratio: \(width) / \(height); border-radius: 4px;
                box-shadow: 0 1px 3px rgba(0, 0, 0, 0.25); }
          p { margin: 0; flex: none; opacity: 0.7; }
        </style></head>
        <body><img src="cid:preview.png" alt=""><p>\(escaped)</p></body></html>
        """
    }
}
