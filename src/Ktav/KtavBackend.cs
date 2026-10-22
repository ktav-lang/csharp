namespace Ktav;

/// <summary>
/// Selects the implementation backing the <see cref="Ktav"/> operations.
/// <see cref="Native"/> is the Rust-backed <c>ktav_cabi</c> library (the
/// process-wide default, byte-identical to pre-0.8.1 behaviour);
/// <see cref="Pure"/> is the managed implementation — it never loads,
/// downloads, or invokes a native binary.
/// </summary>
/// <remarks>
/// Pass a backend explicitly per call (authoritative, race-free), or flip the
/// process-wide default once at startup with
/// <see cref="Ktav.UsePureBackend"/> / <see cref="Ktav.UseNativeBackend"/>.
/// </remarks>
public enum KtavBackend
{
    /// <summary>The Rust-backed native library (default).</summary>
    Native = 0,

    /// <summary>The managed pure implementation — no native binary involved.</summary>
    Pure = 1,
}
