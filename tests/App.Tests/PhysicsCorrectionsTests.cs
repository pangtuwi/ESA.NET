using App.Core.Model;
using App.Persistence;

namespace App.Tests;

/// <summary>
/// The Legacy/Corrected switch of <c>CORRECTIONS.md</c>, before any correction stands behind
/// it: the mode, the per-entry overrides, and where the choice is kept and recorded.
/// </summary>
public sealed class PhysicsCorrectionsTests
{
    /// <summary>A correction for these tests only; the catalogue itself is still empty.</summary>
    private static readonly Correction Example = new("B14", "The RKF5 coefficient Fehlberg published");

    [Fact]
    public void LegacyIsTheDefault()
    {
        // Agreed in CORRECTIONS.md section 5: Legacy until tier 3 is complete.
        Assert.Equal(PhysicsMode.Legacy, new PhysicsCorrections().Mode);
        Assert.Equal(PhysicsMode.Legacy, new SimulationSettings().Physics.Mode);
        Assert.False(new PhysicsCorrections().IsOn(Example));
    }

    [Fact]
    public void ACorrectionFollowsTheModeUnlessOverridden()
    {
        var physics = new PhysicsCorrections { Mode = PhysicsMode.Corrected };

        Assert.True(physics.IsOn(Example));

        physics.Overrides["b14"] = false;
        Assert.False(physics.IsOn(Example));

        physics.Mode = PhysicsMode.Legacy;
        physics.Overrides["B14"] = true;
        Assert.True(physics.IsOn(Example));
    }

    [Fact]
    public void WithNoCorrectionsImplementedCorrectedSaysItIsTheSameAsLegacy()
    {
        Assert.Empty(CorrectionCatalogue.All);
        Assert.Equal("Legacy", new PhysicsCorrections().Describe());
        Assert.Contains(
            "no corrections implemented yet",
            new PhysicsCorrections { Mode = PhysicsMode.Corrected }.Describe(),
            StringComparison.Ordinal);
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

            store.Write(target, store.Read(target));
            Assert.Equal(before, File.ReadAllBytes(target));

            Assert.DoesNotContain("[Physics]", File.ReadAllText(target), StringComparison.Ordinal);

            var settings = store.Read(target);
            settings.Physics.Mode = PhysicsMode.Corrected;
            store.Write(target, settings);

            Assert.Equal(PhysicsMode.Corrected, store.Read(target).Physics.Mode);
        }
        finally
        {
            File.Delete(target);
        }
    }
}
