using Plugin.NFC;
using SportVitaal.Shared.Services;

namespace SportVitaal.Services;

/// <summary>
/// NFC-based <see cref="IRfidReader"/> using Plugin.NFC. The phone is the reader: the member taps
/// an NFC pass (13.56 MHz / ISO&#160;14443) and we return its UID. This covers NFC tags/cards only —
/// 125&#160;kHz (LF) or UHF RFID needs an external reader and a different implementation.
///
/// Platform setup required beyond DI registration:
///  • Android — AndroidManifest.xml needs the NFC permission, and MainActivity must call
///    CrossNFC.Init(this) in OnCreate and forward OnNewIntent to CrossNFC.OnNewIntent
///    (both wired up already).
///  • iOS — needs the "Near Field Communication Tag Reading" entitlement and an
///    NFCReaderUsageDescription in Info.plist (requires a paid Apple Developer account). Reads are
///    foreground-only and the OS shows its own scan sheet.
/// </summary>
public sealed class NfcRfidReader : IRfidReader
{
    public bool IsSupported => CrossNFC.IsSupported && CrossNFC.Current.IsAvailable;

    public async Task<string?> ReadTagAsync(CancellationToken cancellationToken = default)
    {
        if (!IsSupported || !CrossNFC.Current.IsEnabled)
            return null;

        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnDiscovered(ITagInfo? tagInfo, bool format) => tcs.TrySetResult(ExtractUid(tagInfo));

        CrossNFC.Current.OnTagDiscovered += OnDiscovered;
        try
        {
            CrossNFC.Current.StartListening();
            // Complete (with null) when the caller cancels or times out so we never hang.
            await using var _ = cancellationToken.Register(() => tcs.TrySetResult(null));
            return await tcs.Task;
        }
        finally
        {
            CrossNFC.Current.OnTagDiscovered -= OnDiscovered;
            CrossNFC.Current.StopListening();
        }
    }

    /// <summary>Normalizes a tag's identifier to uppercase hex with no separators.</summary>
    private static string? ExtractUid(ITagInfo? tagInfo)
    {
        if (tagInfo is null)
            return null;

        if (!string.IsNullOrEmpty(tagInfo.SerialNumber))
            return tagInfo.SerialNumber.Replace(":", "").Replace("-", "").ToUpperInvariant();

        return tagInfo.Identifier is { Length: > 0 } id ? Convert.ToHexString(id) : null;
    }
}
