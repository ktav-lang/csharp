// Polyfills for language features that work on net8.0 out of the box
// but require a marker type on netstandard2.0.

#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices;

/// <summary>
/// Reserved by the C# compiler. Required to enable <c>init</c> setters
/// (and therefore positional records) on target frameworks that pre-date
/// .NET 5.
/// </summary>
internal static class IsExternalInit
{
}
#endif
