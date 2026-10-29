using App.Core.Model;
using App.Persistence;

namespace App.Tests;

/// <summary>
/// The Legacy/Corrected switch of <c>CORRECTIONS.md</c>, independent of the corrections behind
/// it: the mode, the per-entry overrides, and where the choice is kept and recorded.
/// </summary>
public sealed class PhysicsCorrectionsTests
{
    /// <summary>A correction for these tests only, independent of what the catalogue holds.</summary>
    private static readonly Correction Example = new("B99", "A correction for testing the switch");

    [Fact]
    public void CorrectedIsTheDefault()
    {
        // Agreed in CORRECTIONS.md section 5: Corrected once tier 3 is complete, with Legacy
        // asked for by name.
        Assert.Equal(PhysicsMode.Corrected, PhysicsCorrections.DefaultMode);
        Assert.Equal(PhysicsMode.Corrected, new PhysicsCorrections().Mode);
        Assert.Equal(PhysicsMode.Corrected, new SimulationSettings().Physics.Mode);
        Assert.True(new PhysicsCorrections().IsOn(Example));
        Assert.Equal(CorrectionCatalogue.All.Count(c => c.InCorrected), new PhysicsCorrections().Active.Count);
    }

    [Fact]
    public void ACorrectionFollowsTheModeUnlessOverridden()
    {
        var physics = new PhysicsCorrections { Mode = PhysicsMode.Corrected };

        Assert.True(physics.IsOn(Example));

        physics.Overrides["b99"] = false;
        Assert.False(physics.IsOn(Example));

        physics.Mode = PhysicsMode.Legacy;
        physics.Overrides["B99"] = true;
        Assert.True(physics.IsOn(Example));
    }

    [Fact]
    public void TheCatalogueHoldsTheCorrectionsThatHaveLandedAndDescribeNamesThem()
    {
        // B14 was the first. Each tier 3 correction adds itself here when it lands.
        Assert.Contains(CorrectionCatalogue.Rkf5Coefficient, CorrectionCatalogue.All);
        Assert.Equal("B14", CorrectionCatalogue.Rkf5Coefficient.Entry);
        Assert.Contains(CorrectionCatalogue.ClosedCylinderMassFlow, CorrectionCatalogue.All);
        Assert.Equal("B46", CorrectionCatalogue.ClosedCylinderMassFlow.Entry);
        Assert.Equal("B32", CorrectionCatalogue.WoschniMotoredAngle.Entry);
        Assert.Equal("B33", CorrectionCatalogue.WoschniSweptVolume.Entry);
        Assert.Equal("B31", CorrectionCatalogue.WoschniNegativeVelocity.Entry);
        Assert.Equal("B38", CorrectionCatalogue.IvcReference.Entry);
        Assert.Equal("B75", CorrectionCatalogue.WoschniCombustionTerm.Entry);
        Assert.Equal("B50", CorrectionCatalogue.ManifoldGammas.Entry);
        Assert.Equal("B54", CorrectionCatalogue.ClosedValveWallVelocity.Entry);
        Assert.Equal("B55", CorrectionCatalogue.OpenEndDensityConvergence.Entry);
        Assert.Equal("B56", CorrectionCatalogue.SonicEntranceBracket.Entry);
        Assert.Equal("B59", CorrectionCatalogue.InletReverseStall.Entry);
        Assert.Equal("B61", CorrectionCatalogue.ExhaustReverseSingleRelaxation.Entry);
        Assert.Equal("B62", CorrectionCatalogue.ExhaustReverseStagnationChoke.Entry);
        Assert.Equal("B64", CorrectionCatalogue.SecantProbe.Entry);
        Assert.Equal("B65", CorrectionCatalogue.LiveInletTemperature.Entry);
        Assert.Equal("B4", CorrectionCatalogue.AreaClamp.Entry);
        Assert.Equal("B16", CorrectionCatalogue.AnalyticPressureDerivative.Entry);
        Assert.Equal("B18", CorrectionCatalogue.ResidualMolecularWeight.Entry);
        Assert.Equal("B35", CorrectionCatalogue.SingleZoneGamma.Entry);
        Assert.Equal("B76", CorrectionCatalogue.SingleZoneTrialState.Entry);
        Assert.Equal("B77", CorrectionCatalogue.EndOfStepState.Entry);
        Assert.Equal("B78", CorrectionCatalogue.BurntVolumeReset.Entry);
        Assert.Equal("B37", CorrectionCatalogue.GasExchangeZones.Entry);
        Assert.Equal("B79", CorrectionCatalogue.OverlapValveTotals.Entry);
        Assert.Equal("B81", CorrectionCatalogue.ExhaustValveLatch.Entry);
        Assert.Equal("B82", CorrectionCatalogue.InletValveLatch.Entry);
        Assert.Equal("B83", CorrectionCatalogue.AreaJunctions.Entry);
        Assert.Equal("B84", CorrectionCatalogue.FreshValveThroat.Entry);
        Assert.Equal("B85", CorrectionCatalogue.ThroatSolveFromZero.Entry);
        Assert.Equal("B86", CorrectionCatalogue.WarmUpConvergence.Entry);
        Assert.Equal("B87", CorrectionCatalogue.NetWorkTorque.Entry);
        Assert.Equal("B88", CorrectionCatalogue.MechanicalFriction.Entry);
        Assert.Equal("B89", CorrectionCatalogue.WiebeBurn.Entry);
        Assert.Equal("B91", CorrectionCatalogue.SonicMachBracket.Entry);
        Assert.Equal("B90", CorrectionCatalogue.FlameDevelopmentDelay.Entry);
        Assert.Equal("B34", CorrectionCatalogue.CombustionWallShare.Entry);
        Assert.Equal("B92", CorrectionCatalogue.CombustionEfficiency.Entry);
        Assert.Equal("B93", CorrectionCatalogue.ValveFlowContinuity.Entry);
        Assert.Equal("B94", CorrectionCatalogue.ChokedDischargeContinuity.Entry);
        Assert.Equal(40, CorrectionCatalogue.All.Count);

        Assert.Equal("Legacy, no corrections on", new PhysicsCorrections { Mode = PhysicsMode.Legacy }.Describe());
        Assert.Equal(
            $"Corrected, all {CorrectionCatalogue.All.Count - 2} of its corrections on; off: B90, B34",
            new PhysicsCorrections().Describe());

        var corrected = new PhysicsCorrections { Mode = PhysicsMode.Corrected };
        Assert.Equal(
            $"Corrected, all {CorrectionCatalogue.All.Count - 2} of its corrections on; off: B90, B34", corrected.Describe());

        // An override that switches B14 back off is named, not hidden.
        corrected.Overrides["B14"] = false;
        Assert.DoesNotContain(CorrectionCatalogue.Rkf5Coefficient, corrected.Active);
    }

    /// <summary>
    /// ISSUES.md A40: B90 fails the measured best-torque spark at full throttle, and B34 replaces
    /// the thesis's own wall split (Table 2.1), so Corrected runs neither; ESA.ini can still turn
    /// either on, in either mode.
    /// </summary>
    [Fact]
    public void B90AndB34AreOffInCorrectedUnlessTurnedOn()
    {
        Assert.False(CorrectionCatalogue.FlameDevelopmentDelay.InCorrected);
        Assert.False(CorrectionCatalogue.CombustionWallShare.InCorrected);
        Assert.All(
            CorrectionCatalogue.All.Where(c => c != CorrectionCatalogue.FlameDevelopmentDelay && c != CorrectionCatalogue.CombustionWallShare),
            c => Assert.True(c.InCorrected));

        var corrected = new PhysicsCorrections { Mode = PhysicsMode.Corrected };
        Assert.False(corrected.IsOn(CorrectionCatalogue.FlameDevelopmentDelay));
        Assert.False(corrected.IsOn(CorrectionCatalogue.CombustionWallShare));
        Assert.True(corrected.IsOn(CorrectionCatalogue.WiebeBurn));

        corrected.Overrides["B90"] = true;
        corrected.Overrides["B34"] = true;
        Assert.True(corrected.IsOn(CorrectionCatalogue.FlameDevelopmentDelay));
        Assert.True(corrected.IsOn(CorrectionCatalogue.CombustionWallShare));
        Assert.Equal($"Corrected, all {CorrectionCatalogue.All.Count} corrections on", corrected.Describe());

        var legacy = new PhysicsCorrections { Mode = PhysicsMode.Legacy };
        legacy.Overrides["B90"] = true;
        Assert.Equal("Legacy, corrections on: B90", legacy.Describe());
    }

    [Fact]
    public void ACloneIsIndependent()
    {
        var physics = new PhysicsCorrections { Mode = PhysicsMode.Corrected };
        physics.Overrides["B46"] = false;

        var copy = physics.Clone();
        physics.Mode = PhysicsMode.Legacy;
        physics.Overrides["B46"] = true;

        Assert.Equal(PhysicsMode.Corrected, copy.Mode);
        Assert.False(copy.Overrides["B46"]);
    }

    /// <summary>A copy of the shipped <c>ESA.ini</c>, which has every key the store writes.</summary>
    private static string ShippedEsaIniCopy()
    {
        Assert.SkipWhen(TestPaths.Legacy is null, "Not running from a repository checkout.");

        var target = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".ini");
        File.Copy(Path.Combine(TestPaths.Legacy!, "ESA", "ESA.ini"), target);

        return target;
    }

    [Fact]
    public void TheChoiceIsReadFromAndWrittenToEsaIni()
    {
        var target = ShippedEsaIniCopy();
        var store = new SimulationSettingsStore();

        try
        {
            // Lower-case mode, two overrides and one value that is neither 0 nor 1.
            File.AppendAllText(target, "\r\n[Physics]\r\nMode=corrected\r\nB14=1\r\nB46=0\r\nB50=maybe");

            var settings = store.Read(target);

            Assert.Equal(PhysicsMode.Corrected, settings.Physics.Mode);
            Assert.True(settings.Physics.Overrides["B14"]);
            Assert.False(settings.Physics.Overrides["B46"]);
            Assert.False(settings.Physics.Overrides.ContainsKey("B50"));

            // Nothing changed, so nothing is rewritten - not even Mode=corrected's case.
            var before = File.ReadAllBytes(target);
            store.Write(target, settings);
            Assert.Equal(before, File.ReadAllBytes(target));

            // A change rewrites only what changed.
            settings.Physics.Mode = PhysicsMode.Legacy;
            store.Write(target, settings);
            Assert.Equal(PhysicsMode.Legacy, store.Read(target).Physics.Mode);
            Assert.Contains("B14=1", File.ReadAllText(target), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(target);
        }
    }

    [Fact]
    public void AnEsaIniWithoutTheSwitchGainsNoSectionUntilItSaysSomething()
    {
        var target = ShippedEsaIniCopy();
        var store = new SimulationSettingsStore();

        try
        {
            var before = File.ReadAllBytes(target);

            // An ESA.ini that predates the switch runs Corrected, the default, and keeps
            // its bytes while it does.
            Assert.Equal(PhysicsMode.Corrected, store.Read(target).Physics.Mode);
            store.Write(target, store.Read(target));
            Assert.Equal(before, File.ReadAllBytes(target));

            Assert.DoesNotContain("[Physics]", File.ReadAllText(target), StringComparison.Ordinal);

            // Choosing Legacy is what it then says.
            var settings = store.Read(target);
            settings.Physics.Mode = PhysicsMode.Legacy;
            store.Write(target, settings);

            Assert.Contains("Mode=Legacy", File.ReadAllText(target), StringComparison.Ordinal);
            Assert.Equal(PhysicsMode.Legacy, store.Read(target).Physics.Mode);
        }
        finally
        {
            File.Delete(target);
        }
    }

    /// <summary>
    /// Legacy has to be asked for by name. A mode the store cannot read is the default,
    /// Corrected, as a missing one is; a missing ESA.ini is the default too.
    /// </summary>
    [Theory]
    [InlineData("Mode=Legacy", PhysicsMode.Legacy)]
    [InlineData("Mode= legacy ", PhysicsMode.Legacy)]
    [InlineData("Mode=Corrected", PhysicsMode.Corrected)]
    [InlineData("Mode=Legcay", PhysicsMode.Corrected)]
    [InlineData("Mode=", PhysicsMode.Corrected)]
    [InlineData("B14=0", PhysicsMode.Corrected)]
    public void OnlyANamedLegacyReadsAsLegacy(string line, PhysicsMode expected)
    {
        var target = ShippedEsaIniCopy();
        var store = new SimulationSettingsStore();

        try
        {
            File.AppendAllText(target, "\r\n[Physics]\r\n" + line + "\r\n");

            Assert.Equal(expected, store.Read(target).Physics.Mode);
        }
        finally
        {
            File.Delete(target);
        }

        Assert.Equal(PhysicsMode.Corrected, store.Read(target).Physics.Mode);
    }
}
