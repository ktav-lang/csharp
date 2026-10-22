using System.Collections.Generic;
using System.IO;

using NUnit.Framework;

namespace Ktav.Tests;

/// <summary>
/// The FULL five-category spec conformance suite replayed against the
/// pure managed backend — the exact mirror of <see cref="SpecConformance"/>
/// (which runs native). Reuses the same shared cores and the same
/// oracles; no assertion is weakened and no expected category differs.
/// Raw-byte InvalidUtf8 fixtures go through <c>Ktav.LoadsUtf8(.., Pure)</c>
/// so the pure backend's byte-accurate error spans are exercised the
/// same way the native envelope is.
/// </summary>
[TestFixture]
public class PureSpecConformance
{
    [TestCaseSource(typeof(SpecConformance), nameof(SpecConformance.ValidCases))]
    public void Valid(string ktavPath) => SpecConformance.ValidCore(ktavPath, KtavBackend.Pure);

    [TestCaseSource(typeof(SpecConformance), nameof(SpecConformance.ValidCanonicalCases))]
    public void ValidCanonical(string ktavPath) =>
        SpecConformance.ValidCanonicalCore(ktavPath, KtavBackend.Pure);

    [TestCaseSource(typeof(SpecConformance), nameof(SpecConformance.InvalidCases))]
    public void Invalid(string ktavPath) => SpecConformance.InvalidCore(ktavPath, KtavBackend.Pure);

    [TestCaseSource(typeof(SpecConformance), nameof(SpecConformance.UnrepresentableCases))]
    public void Unrepresentable(string jsonPath) =>
        SpecConformance.UnrepresentableCore(jsonPath, KtavBackend.Pure);

    [TestCaseSource(typeof(SpecConformance), nameof(SpecConformance.ParseableUnrepresentableCases))]
    public void ParseableUnrepresentable(string ktavPath) =>
        SpecConformance.ParseableUnrepresentableCore(ktavPath, KtavBackend.Pure);

    [TestCaseSource(typeof(SpecConformance), nameof(SpecConformance.StrictLossyCases))]
    public void StrictLossy(string ktavPath) =>
        SpecConformance.StrictLossyCore(ktavPath, KtavBackend.Pure);
}
