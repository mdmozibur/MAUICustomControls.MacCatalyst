using System.Runtime.InteropServices;
using System.Text.Json;

namespace MAUICustomControls.MacCatalyst.Platforms.MacCatalyst;

public enum StoreKitPurchaseStatus
{
    Success = 0,
    UserCancelled = 1,
    Pending = 2,
    ProductNotFound = 3,
    Unverified = 4,
    Failed = 5,
}

public sealed record StoreKitPurchaseResult(StoreKitPurchaseStatus Status, string? Message, StoreKitEntitlement? Transaction);

public sealed record StoreKitEntitlement(
    string ProductId,
    string ProductType,
    DateTimeOffset PurchaseDate,
    DateTimeOffset? ExpirationDate,
    string OriginalTransactionId);

// StoreKit 2 from .NET. StoreKit 2 is Swift-only, so the app links Native/StoreKitBridge (a small
// Swift framework exposing a C API; build it with Native/StoreKitBridge/build_xcframework.sh) as a
// NativeReference, and these calls reach it through its exported C symbols.
public static class StoreKitBridge
{
    private const string Library = "__Internal";

    [DllImport(Library)]
    private static extern unsafe void skb_purchase(byte* productId, IntPtr context, delegate* unmanaged<IntPtr, int, byte*, void> completion);

    [DllImport(Library)]
    private static extern unsafe void skb_current_entitlements(IntPtr context, delegate* unmanaged<IntPtr, int, byte*, void> completion);

    [DllImport(Library)]
    private static extern unsafe void skb_sync(IntPtr context, delegate* unmanaged<IntPtr, int, byte*, void> completion);

    [DllImport(Library)]
    private static extern void skb_start_transaction_observer();

    public static void StartTransactionObserver() => skb_start_transaction_observer();

    public static async Task<StoreKitPurchaseResult> PurchaseAsync(string productId)
    {
        var (status, text) = await InvokeAsync(context => StartPurchase(productId, context));

        var purchaseStatus = (StoreKitPurchaseStatus)status;
        if (purchaseStatus != StoreKitPurchaseStatus.Success || text is null)
        {
            return new StoreKitPurchaseResult(purchaseStatus, text, null);
        }

        using var document = JsonDocument.Parse(text);
        return new StoreKitPurchaseResult(purchaseStatus, null, ParseEntitlement(document.RootElement));
    }

    public static async Task<IReadOnlyList<StoreKitEntitlement>> GetCurrentEntitlementsAsync()
    {
        var (_, text) = await InvokeAsync(StartEntitlements);
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        using var document = JsonDocument.Parse(text);
        return document.RootElement.EnumerateArray().Select(ParseEntitlement).ToList();
    }

    public static async Task SyncAsync()
    {
        var (status, text) = await InvokeAsync(StartSync);
        if (status != (int)StoreKitPurchaseStatus.Success)
        {
            throw new InvalidOperationException(text ?? "App Store sync failed.");
        }
    }

    private static StoreKitEntitlement ParseEntitlement(JsonElement entry)
    {
        static DateTimeOffset? ReadDate(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && DateTimeOffset.TryParse(value.GetString(), out var date) ? date : null;

        return new StoreKitEntitlement(
            entry.GetProperty("productId").GetString() ?? string.Empty,
            entry.TryGetProperty("productType", out var type) ? type.GetString() ?? string.Empty : string.Empty,
            ReadDate(entry, "purchaseDate") ?? DateTimeOffset.MinValue,
            ReadDate(entry, "expirationDate"),
            entry.TryGetProperty("originalTransactionId", out var id) ? id.GetString() ?? string.Empty : string.Empty);
    }

    private static unsafe void StartPurchase(string productId, IntPtr context)
    {
        var utf8 = System.Text.Encoding.UTF8.GetBytes(productId + "\0");
        fixed (byte* pointer = utf8)
        {
            skb_purchase(pointer, context, &OnCompleted);
        }
    }

    private static unsafe void StartEntitlements(IntPtr context) => skb_current_entitlements(context, &OnCompleted);

    private static unsafe void StartSync(IntPtr context) => skb_sync(context, &OnCompleted);

    // The context is a GCHandle to the awaiting TaskCompletionSource; OnCompleted frees it.
    private static Task<(int Status, string? Text)> InvokeAsync(Action<IntPtr> call)
    {
        var completion = new TaskCompletionSource<(int, string?)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handle = GCHandle.Alloc(completion);
        try
        {
            call(GCHandle.ToIntPtr(handle));
        }
        catch
        {
            handle.Free();
            throw;
        }

        return completion.Task;
    }

    [UnmanagedCallersOnly]
    private static unsafe void OnCompleted(IntPtr context, int status, byte* text)
    {
        var handle = GCHandle.FromIntPtr(context);
        var completion = (TaskCompletionSource<(int, string?)>)handle.Target!;
        handle.Free();
        completion.TrySetResult((status, text == null ? null : Marshal.PtrToStringUTF8((IntPtr)text)));
    }
}
