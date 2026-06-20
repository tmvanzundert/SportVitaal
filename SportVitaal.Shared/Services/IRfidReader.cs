namespace SportVitaal.Shared.Services;

/// <summary>
/// Reads an RFID/NFC tag UID from the device. Implemented per-platform in the MAUI app
/// (the phone acts as the reader: the member taps an NFC pass at the door) and consumed by
/// the check-in flow to identify the pass.
/// </summary>
public interface IRfidReader
{
    /// <summary>
    /// Whether this device can read tags right now: NFC hardware present, supported OS,
    /// and the radio enabled. When <c>false</c> the check-in flow falls back to a simulated scan.
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// Waits for a single tag tap and returns its UID as uppercase hex with no separators
    /// (e.g. <c>04A1B2C3</c>), or <c>null</c> if cancelled, timed out, or no tag was read.
    /// </summary>
    Task<string?> ReadTagAsync(CancellationToken cancellationToken = default);
}
