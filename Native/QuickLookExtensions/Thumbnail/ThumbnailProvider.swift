// Quick Look thumbnail extension: Finder icons, gallery/column view, Open and Save panels, Spotlight.
// Draws the preview image the file already carries (see CadPreviewReader); files without one keep
// the plain document icon.

import CoreGraphics
import QuickLookThumbnailing

@objc(ThumbnailProvider)
final class ThumbnailProvider: QLThumbnailProvider {
    override func provideThumbnail(for request: QLFileThumbnailRequest,
                                   _ handler: @escaping (QLThumbnailReply?, Error?) -> Void) {
        let preview: CadPreview
        do {
            preview = try CadPreviewReader.read(url: request.fileURL)
        } catch {
            handler(nil, error)
            return
        }

        // Fit the image into the requested size, keeping its aspect ratio.
        let image = preview.image
        let width = CGFloat(image.width), height = CGFloat(image.height)
        let scale = min(request.maximumSize.width / width, request.maximumSize.height / height)
        let size = CGSize(width: max(1, (width * scale).rounded()), height: max(1, (height * scale).rounded()))

        let reply = QLThumbnailReply(contextSize: size) { context -> Bool in
            let bounds = CGRect(origin: .zero, size: size)
            // SmartCAD's previews are dark lines on a transparent background: give them paper.
            if preview.hasAlpha {
                context.setFillColor(CGColor(gray: 1, alpha: 1))
                context.fill(bounds)
            }
            context.interpolationQuality = .high
            context.draw(image, in: bounds)
            return true
        }
        reply.extensionBadge = preview.badge
        handler(reply, nil)
    }
}
