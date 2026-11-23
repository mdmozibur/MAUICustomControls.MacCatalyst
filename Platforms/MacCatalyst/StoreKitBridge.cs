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
    /// <summary>The App Store could not be reached.</summary>
    NetworkError = 6,
    /// <summary>This Mac or Apple Account may not make purchases (Screen Time restrictions).</summary>
    NotAllowed = 7,
    /// <summary>The product is not sold in the user's storefront.</summary>
    NotAvailableInStorefront = 8,
}

/// <summary>A StoreKit call that failed, with the bridge's status for what went wrong.</summary>
public sealed class StoreKitException(StoreKitPurchaseStatus status, string? message)
    : Exception(message ?? $"App Store request failed ({status}).")
{
    public StoreKitPurchaseStatus Status { get; } = status;
}

/// <summary>
/// A product as the App Store sells it in the user's storefront: DisplayPrice is StoreKit's
/// localized price text, Price the same amount as a number, CurrencyCode its ISO 4217 code. A
/// subscription has its period (PeriodUnit "day", "week", "month" or "year", times PeriodValue) and,
/// for periods of a month or more, DisplayPricePerMonth (the price spread over its months, formatted
/// like DisplayPrice).
/// </summary>
public sealed record StoreKitProduct(
    string ProductId,
    string DisplayName,
    string Description,
    string DisplayPrice,
    decimal Price,
    string CurrencyCode,
    string ProductType,
    string? PeriodUnit,
    int PeriodValue,
    string? DisplayPricePerMonth);

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
    private static extern unsafe void skb_products(byte* productIdsJson, IntPtr context, delegate* unmanaged<IntPtr, int, byte*, void> completion);

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

    /// <summary>
    /// Re-syncs purchases with the App Store (Restore Purchases); may ask the user to sign in.
    /// Throws <see cref="StoreKitException"/> when it fails or the user cancels the sign-in.
    /// </summary>
    public static async Task SyncAsync()
    {
        var (status, text) = await InvokeAsync(StartSync);
        if (status != (int)StoreKitPurchaseStatus.Success)
        {
            throw new StoreKitException((StoreKitPurchaseStatus)status, text);
        }
    }

    /// <summary>
    /// The App Store's products of these identifiers, as the user's storefront sells them. Identifiers
    /// the App Store does not know are left out. Throws <see cref="StoreKitException"/> when the
    /// App Store cannot be asked (offline, for one).
    /// </summary>
    public static async Task<IReadOnlyList<StoreKitProduct>> GetProductsAsync(IEnumerable<string> productIds)
    {
        var ids = new System.Text.Json.Nodes.JsonArray();
        foreach (var id in productIds)
        {
            ids.Add(System.Text.Json.Nodes.JsonValue.Create(id));
        }

        var request = ids.ToJsonString();
        var (status, text) = await InvokeAsync(context => StartProducts(request, context));
        if (status != (int)StoreKitPurchaseStatus.Success)
        {
            throw new StoreKitException((StoreKitPurchaseStatus)status, text);
        }

        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        using var document = JsonDocument.Parse(text);
        return document.RootElement.EnumerateArray().Select(ParseProduct).ToList();
    }

    private static StoreKitProduct ParseProduct(JsonElement entry)
    {
        static string? ReadString(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        return new StoreKitProduct(
            ReadString(entry, "productId") ?? string.Empty,
            ReadString(entry, "displayName") ?? string.Empty,
            ReadString(entry, "description") ?? string.Empty,
            ReadString(entry, "displayPrice") ?? string.Empty,
            decimal.TryParse(ReadString(entry, "price"), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var price) ? price : 0m,
            ReadString(entry, "currencyCode") ?? string.Empty,
            ReadString(entry, "productType") ?? string.Empty,
            ReadString(entry, "periodUnit"),
            entry.TryGetProperty("periodValue", out var periodValue) && periodValue.TryGetInt32(out var value) ? value : 0,
            ReadString(entry, "displayPricePerMonth"));
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

    private static unsafe void StartProducts(string productIdsJson, IntPtr context)
    {
        var utf8 = System.Text.Encoding.UTF8.GetBytes(productIdsJson + "\0");
        fixed (byte* pointer = utf8)
        {
            skb_products(pointer, context, &OnCompleted);
        }
    }

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
