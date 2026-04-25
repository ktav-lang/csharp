using System;

namespace Ktav;

/// <summary>
/// Thrown when the native library rejects an input — parse failure for
/// <see cref="Ktav.Loads"/>, render failure for <see cref="Ktav.Dumps"/>.
/// The message is the UTF-8 string returned by the native side.
/// </summary>
[Serializable]
public sealed class KtavException : Exception
{
    public KtavException() { }
    public KtavException(string message) : base(message) { }
    public KtavException(string message, Exception inner) : base(message, inner) { }

#if !NET8_0_OR_GREATER
    private KtavException(System.Runtime.Serialization.SerializationInfo info,
                          System.Runtime.Serialization.StreamingContext context)
        : base(info, context) { }
#endif
}
