import Foundation
import StoreKit

// A C API over StoreKit 2, which is Swift-only and therefore not reachable from .NET bindings.
// Every call completes asynchronously through `SKBCompletion(context, status, utf8Text)`; `context`
// is the caller's opaque handle and is passed back untouched.
public typealias SKBCompletion = @convention(c) (UnsafeMutableRawPointer?, Int32, UnsafePointer<CChar>?) -> Void

// Status codes shared with StoreKitBridge.cs.
private enum SKBStatus: Int32 {
    case success = 0
    case userCancelled = 1
    case pending = 2
    case productNotFound = 3
    case unverified = 4
    case failed = 5
}

private func complete(_ completion: SKBCompletion, _ context: UnsafeMutableRawPointer?, _ status: SKBStatus, _ text: String?) {
    if let text {
        text.withCString { completion(context, status.rawValue, $0) }
    } else {
        completion(context, status.rawValue, nil)
    }
}

private func isoString(_ date: Date) -> String {
    ISO8601DateFormatter().string(from: date)
}

private func describe(_ transaction: Transaction) -> [String: Any] {
    var entry: [String: Any] = [
        "productId": transaction.productID,
        "productType": transaction.productType.rawValue,
        "purchaseDate": isoString(transaction.purchaseDate),
        "originalTransactionId": String(transaction.originalID),
    ]
    if let expiration = transaction.expirationDate {
        entry["expirationDate"] = isoString(expiration)
    }
    if let revocation = transaction.revocationDate {
        entry["revocationDate"] = isoString(revocation)
    }
    if transaction.isUpgraded {
        entry["isUpgraded"] = true
    }
    return entry
}

private func json(_ value: Any) -> String {
    guard let data = try? JSONSerialization.data(withJSONObject: value, options: []),
          let text = String(data: data, encoding: .utf8) else {
        return "null"
    }
    return text
}

/// Buys one product. Success returns the transaction as JSON; failures return a message.
@_cdecl("skb_purchase")
public func skb_purchase(_ productId: UnsafePointer<CChar>, _ context: UnsafeMutableRawPointer?, _ completion: SKBCompletion) {
    let identifier = String(cString: productId)
    Task {
        do {
            guard let product = try await Product.products(for: [identifier]).first else {
                complete(completion, context, .productNotFound, "The App Store has no product '\(identifier)'.")
                return
            }

            switch try await product.purchase() {
            case .success(.verified(let transaction)):
                await transaction.finish()
                complete(completion, context, .success, json(describe(transaction)))
            case .success(.unverified(_, let error)):
                complete(completion, context, .unverified, error.localizedDescription)
            case .userCancelled:
                complete(completion, context, .userCancelled, nil)
            case .pending:
                // Ask to Buy or a payment that needs approval; it arrives later through Transaction.updates.
                complete(completion, context, .pending, nil)
            @unknown default:
                complete(completion, context, .failed, "Unknown purchase result.")
            }
        } catch {
            complete(completion, context, .failed, error.localizedDescription)
        }
    }
}

/// Lists the verified, unrevoked transactions that currently entitle the user (active
/// subscriptions and owned non-consumables) as a JSON array.
@_cdecl("skb_current_entitlements")
public func skb_current_entitlements(_ context: UnsafeMutableRawPointer?, _ completion: SKBCompletion) {
    Task {
        var entitlements: [[String: Any]] = []
        for await result in Transaction.currentEntitlements {
            if case .verified(let transaction) = result, transaction.revocationDate == nil {
                entitlements.append(describe(transaction))
            }
        }
        complete(completion, context, .success, json(entitlements))
    }
}

/// Re-syncs purchases with the App Store ("Restore Purchases"). Prompts for sign-in when needed.
@_cdecl("skb_sync")
public func skb_sync(_ context: UnsafeMutableRawPointer?, _ completion: SKBCompletion) {
    Task {
        do {
            try await AppStore.sync()
            complete(completion, context, .success, nil)
        } catch {
            complete(completion, context, .failed, error.localizedDescription)
        }
    }
}

private final class TransactionObserver: @unchecked Sendable {
    static let shared = TransactionObserver()
    private let lock = NSLock()
    private var task: Task<Void, Never>?

    func start() {
        lock.lock()
        defer { lock.unlock() }
        guard task == nil else { return }

        // Purchases completed outside a purchase() call (Ask to Buy approvals, renewals, purchases on
        // another device, promoted in-app purchases) must be finished, or StoreKit redelivers them.
        task = Task.detached(priority: .background) {
            for await result in Transaction.updates {
                if case .verified(let transaction) = result {
                    await transaction.finish()
                }
            }
        }
    }
}

/// Starts finishing transactions delivered outside purchase(). Call once, as early as possible.
@_cdecl("skb_start_transaction_observer")
public func skb_start_transaction_observer() {
    TransactionObserver.shared.start()
}
