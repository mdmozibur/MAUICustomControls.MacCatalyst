// Reads the preview image a drawing file already carries, without loading the drawing:
//   .dwg      AcDb:Preview section (PNG, or a BMP without its file header), all versions R13-2018+
//   .dxf      THUMBNAILIMAGE section (hex BMP), written by AutoCAD when thumbnails are on
//   .sc       <preview> base64 PNG, the first child of the <smartcad> root
//   .scx      the same document, AES-encrypted UTF-32 (decrypted only up to </preview>)
// Shared by the Quick Look thumbnail and preview extensions (and the test tool in Tests/).

import CommonCrypto
import CoreGraphics
import Foundation
import ImageIO
import UniformTypeIdentifiers

struct CadPreview {
    /// The decoded preview image.
    let image: CGImage
    /// PNG bytes of the image (the stored bytes when the file already holds a PNG).
    let pngData: Data
    /// Short file format label for the thumbnail badge ("DWG", "DXF", "SC").
    let badge: String
    /// One line for the Space bar preview, e.g. "AutoCAD 2018 drawing (DWG)".
    let summary: String
    /// True when the image has transparency (it is drawn on white then).
    let hasAlpha: Bool
}

enum CadPreviewError: Error {
    case noPreview
    case unreadable
}

enum CadPreviewReader {
    static func read(url: URL) throws -> CadPreview {
        switch url.pathExtension.lowercased() {
        case "dwg": return try readDwg(url)
        case "dxf": return try readDxf(url)
        case "sc": return try readSc(url)
        case "scx": return try readScx(url)
        default: throw CadPreviewError.unreadable
        }
    }

    // MARK: DWG

    private static let dwgPreviewSentinel: [UInt8] = [
        0x1F, 0x25, 0x6D, 0x07, 0xD4, 0x36, 0x28, 0x28, 0x9D, 0x57, 0xCA, 0x3F, 0x9D, 0x44, 0x10, 0x2B,
    ]

    private static func readDwg(_ url: URL) throws -> CadPreview {
        let data = try Data(contentsOf: url, options: .alwaysMapped)
        guard data.count > 0x20 else { throw CadPreviewError.unreadable }
        let version = String(decoding: data.prefix(6), as: UTF8.self)

        // Every version from R13 (AC1012) on keeps the absolute address of the preview section
        // (its start sentinel) at 0x0D, outside the encrypted/compressed parts of the file. R12 and
        // older have no such section.
        let address = Int(data.uint32(at: 0x0D))
        guard address > 0x0D, address + 16 + 5 <= data.count,
              data.bytes(at: address, count: 16) == dwgPreviewSentinel
        else { throw CadPreviewError.noPreview }

        // overall size (RL), image count (RC), then per image: code (RC), start (RL), size (RL).
        // Code 1 is the header block; 2 = BMP (DIB without file header), 3 = WMF, 6 = PNG.
        var position = address + 16 + 4
        let count = Int(data[position])
        position += 1
        var png: (Int, Int)?
        var bmp: (Int, Int)?
        for _ in 0..<count {
            guard position + 9 <= data.count else { break }
            let code = data[position]
            let start = Int(data.uint32(at: position + 1))
            let size = Int(data.uint32(at: position + 5))
            position += 9
            guard size > 0, start > 0, start + size <= data.count else { continue }
            if code == 6 { png = (start, size) } else if code == 2 { bmp = (start, size) }
        }

        let label = dwgVersionName(version)
        let summary = label.map { "AutoCAD \($0) drawing (DWG)" } ?? "AutoCAD drawing (DWG)"
        if let (start, size) = png {
            return try makePreview(png: data.subdata(in: start..<start + size), badge: "DWG", summary: summary)
        }
        if let (start, size) = bmp {
            return try makePreview(dib: data.subdata(in: start..<start + size), badge: "DWG", summary: summary)
        }
        throw CadPreviewError.noPreview
    }

    private static func dwgVersionName(_ version: String) -> String? {
        switch version {
        case "AC1009": return "R11/R12"
        case "AC1012": return "R13"
        case "AC1014": return "R14"
        case "AC1015": return "2000"
        case "AC1018": return "2004"
        case "AC1021": return "2007"
        case "AC1024": return "2010"
        case "AC1027": return "2013"
        case "AC1032": return "2018"
        default: return nil
        }
    }

    // MARK: DXF

    private static func readDxf(_ url: URL) throws -> CadPreview {
        let data = try Data(contentsOf: url, options: .alwaysMapped)
        // The section sits near the end (after OBJECTS), so search backwards.
        guard let token = data.range(of: Data("THUMBNAILIMAGE".utf8), options: .backwards)
        else { throw CadPreviewError.noPreview }

        // Group code / value line pairs: 90 = byte count, 310 = hex chunks, 0 ENDSEC ends it.
        var hex = [UInt8]()
        var lines = LineReader(data: data, start: token.upperBound)
        _ = lines.next() // rest of the THUMBNAILIMAGE line
        while let codeLine = lines.next(), let value = lines.next() {
            guard let code = Int(codeLine.trimmingCharacters(in: .whitespaces)) else { break }
            if code == 0 { break }
            if code == 310 { hex.append(contentsOf: value.trimmingCharacters(in: .whitespaces).utf8) }
        }
        guard let dib = Data(hexBytes: hex), !dib.isEmpty else { throw CadPreviewError.noPreview }
        return try makePreview(dib: dib, badge: "DXF", summary: "AutoCAD drawing (DXF)")
    }

    // MARK: SmartCAD .sc / .scx

    private static func readSc(_ url: URL) throws -> CadPreview {
        let handle = try FileHandle(forReadingFrom: url)
        defer { try? handle.close() }
        // The preview is the first child of the root, so it ends within the first few hundred KB.
        var buffer = Data()
        while buffer.count < 16 << 20, let chunk = try handle.read(upToCount: 256 << 10), !chunk.isEmpty {
            buffer.append(chunk)
            if let preview = try scPreview(in: buffer, encoding: .utf8) { return preview }
            if buffer.count >= 64 << 10, buffer.range(of: Data("<preview>".utf8)) == nil { break }
        }
        throw CadPreviewError.noPreview
    }

    private static func readScx(_ url: URL) throws -> CadPreview {
        let handle = try FileHandle(forReadingFrom: url)
        defer { try? handle.close() }
        // AES-128-CBC with the app's fixed key and IV (Extension/ShapelistViewmodelExtension.cs),
        // PKCS7 padding, UTF-32LE text. CBC decrypts a prefix fine, so stop at </preview>.
        let key = Array("mongla_||_chalna".utf8)
        let iv = Array("ramnabad_channel".utf8)
        var cryptor: CCCryptorRef?
        guard CCCryptorCreate(CCOperation(kCCDecrypt), CCAlgorithm(kCCAlgorithmAES), CCOptions(kCCOptionPKCS7Padding),
                              key, key.count, iv, &cryptor) == kCCSuccess, let cryptor
        else { throw CadPreviewError.unreadable }
        defer { CCCryptorRelease(cryptor) }

        let previewTag = Data("<preview>".utf32LittleEndian)
        var plain = Data()
        while plain.count < 64 << 20, let chunk = try handle.read(upToCount: 256 << 10), !chunk.isEmpty {
            var out = [UInt8](repeating: 0, count: chunk.count + kCCBlockSizeAES128)
            var moved = 0
            let status = chunk.withUnsafeBytes { input in
                CCCryptorUpdate(cryptor, input.baseAddress, chunk.count, &out, out.count, &moved)
            }
            guard status == kCCSuccess else { throw CadPreviewError.unreadable }
            plain.append(contentsOf: out.prefix(moved))
            if let preview = try scPreview(in: plain, encoding: .utf32LittleEndian) { return preview }
            if plain.count >= 256 << 10, plain.range(of: previewTag) == nil { break }
        }
        throw CadPreviewError.noPreview
    }

    private static func scPreview(in data: Data, encoding: String.Encoding) throws -> CadPreview? {
        let open = Data(encoding == .utf8 ? Array("<preview>".utf8) : Array("<preview>".utf32LittleEndian))
        let close = Data(encoding == .utf8 ? Array("</preview>".utf8) : Array("</preview>".utf32LittleEndian))
        guard let start = data.range(of: open),
              let end = data.range(of: close, in: start.upperBound..<data.endIndex)
        else { return nil }

        guard let base64 = String(data: data.subdata(in: start.upperBound..<end.lowerBound), encoding: encoding),
              let png = Data(base64Encoded: base64, options: .ignoreUnknownCharacters)
        else { throw CadPreviewError.unreadable }

        // VersionString="0.9.10" on the root element.
        var summary = "SmartCAD 2D drawing"
        if let head = String(data: data.subdata(in: data.startIndex..<start.lowerBound), encoding: encoding),
           let attribute = head.range(of: "VersionString=\""),
           let quote = head[attribute.upperBound...].firstIndex(of: "\"") {
            summary += " (version \(head[attribute.upperBound..<quote]))"
        }
        return try makePreview(png: png, badge: "SC", summary: summary)
    }

    // MARK: Images

    private static func makePreview(png: Data, badge: String, summary: String) throws -> CadPreview {
        guard let source = CGImageSourceCreateWithData(png as CFData, nil),
              let image = CGImageSourceCreateImageAtIndex(source, 0, nil)
        else { throw CadPreviewError.unreadable }
        return CadPreview(image: image, pngData: png, badge: badge, summary: summary, hasAlpha: image.hasAlpha)
    }

    /// A DIB (BITMAPINFOHEADER + palette + pixels) as stored in DWG/DXF: put a BITMAPFILEHEADER in
    /// front so ImageIO can read it, then hand ImageIO's PNG encoding to the preview.
    private static func makePreview(dib: Data, badge: String, summary: String) throws -> CadPreview {
        guard dib.count >= 40 else { throw CadPreviewError.unreadable }
        let headerSize = Int(dib.uint32(at: 0))
        let bitCount = Int(dib.uint16(at: 14))
        let compression = dib.uint32(at: 16)
        let colorsUsed = Int(dib.uint32(at: 32))
        var paletteBytes = (colorsUsed != 0 ? colorsUsed : (bitCount <= 8 ? 1 << bitCount : 0)) * 4
        if headerSize == 40 && compression == 3 { paletteBytes += 12 } // BI_BITFIELDS masks
        let pixelOffset = 14 + headerSize + paletteBytes
        guard headerSize >= 12, pixelOffset - 14 <= dib.count else { throw CadPreviewError.unreadable }

        var bmp = Data([0x42, 0x4D]) // "BM"
        bmp.appendUInt32(UInt32(14 + dib.count))
        bmp.appendUInt32(0)
        bmp.appendUInt32(UInt32(pixelOffset))
        bmp.append(dib)

        guard let source = CGImageSourceCreateWithData(bmp as CFData, nil),
              let image = CGImageSourceCreateImageAtIndex(source, 0, nil)
        else { throw CadPreviewError.unreadable }

        let png = NSMutableData()
        guard let destination = CGImageDestinationCreateWithData(png, UTType.png.identifier as CFString, 1, nil)
        else { throw CadPreviewError.unreadable }
        CGImageDestinationAddImage(destination, image, nil)
        guard CGImageDestinationFinalize(destination) else { throw CadPreviewError.unreadable }
        return CadPreview(image: image, pngData: png as Data, badge: badge, summary: summary, hasAlpha: false)
    }
}

// MARK: Helpers

private extension CGImage {
    var hasAlpha: Bool {
        switch alphaInfo {
        case .none, .noneSkipFirst, .noneSkipLast: return false
        default: return true
        }
    }
}

private extension String {
    var utf32LittleEndian: [UInt8] {
        unicodeScalars.flatMap { scalar -> [UInt8] in
            let value = scalar.value
            return [UInt8(value & 0xFF), UInt8((value >> 8) & 0xFF), UInt8((value >> 16) & 0xFF), UInt8(value >> 24)]
        }
    }
}

private extension Data {
    func uint32(at offset: Int) -> UInt32 {
        let i = startIndex + offset
        return UInt32(self[i]) | UInt32(self[i + 1]) << 8 | UInt32(self[i + 2]) << 16 | UInt32(self[i + 3]) << 24
    }

    func uint16(at offset: Int) -> UInt16 {
        let i = startIndex + offset
        return UInt16(self[i]) | UInt16(self[i + 1]) << 8
    }

    func bytes(at offset: Int, count: Int) -> [UInt8] {
        let i = startIndex + offset
        return [UInt8](self[i..<i + count])
    }

    mutating func appendUInt32(_ value: UInt32) {
        append(contentsOf: [UInt8(value & 0xFF), UInt8((value >> 8) & 0xFF), UInt8((value >> 16) & 0xFF), UInt8(value >> 24)])
    }

    init?(hexBytes: [UInt8]) {
        guard hexBytes.count % 2 == 0 else { return nil }
        func nibble(_ c: UInt8) -> UInt8? {
            switch c {
            case 0x30...0x39: return c - 0x30
            case 0x41...0x46: return c - 0x41 + 10
            case 0x61...0x66: return c - 0x61 + 10
            default: return nil
            }
        }
        var bytes = [UInt8]()
        bytes.reserveCapacity(hexBytes.count / 2)
        var i = 0
        while i < hexBytes.count {
            guard let high = nibble(hexBytes[i]), let low = nibble(hexBytes[i + 1]) else { return nil }
            bytes.append(high << 4 | low)
            i += 2
        }
        self.init(bytes)
    }
}

/// Lines of an ASCII DXF (CRLF or LF), read lazily from a mapped file.
private struct LineReader {
    let data: Data
    var position: Int

    init(data: Data, start: Int) {
        self.data = data
        self.position = start
    }

    mutating func next() -> String? {
        guard position < data.endIndex else { return nil }
        let end = data[position...].firstIndex(of: 0x0A) ?? data.endIndex
        var line = data[position..<end]
        if line.last == 0x0D { line = line.dropLast() }
        position = end < data.endIndex ? end + 1 : end
        return String(decoding: line, as: UTF8.self)
    }
}
