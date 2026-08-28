using System.Globalization;
using System.Text;
using App.Core.Model;
using App.Core.Simulation;

namespace App.Persistence;

/// <summary>
/// Builds <c>run.txt</c>: what a run was asked to do, what it did, and what it read.
/// </summary>
/// <remarks>
/// The port's own, with no legacy counterpart - the original recorded nothing about a run
/// beyond the numbers in <c>SimulDat.txt</c>, whose 24 unlabelled columns say nothing
/// about which engine or which cam profile produced them. Written for a person to read;
/// nothing reads it back, so it carries no format guarantee.
/// </remarks>
public sealed class RunManifest
{
    private const int Label = 20;

    private readonly StringBuilder _text = new();

    private bool _sectioned;

    /// <summary>Starts a manifest, headed with the time the run began.</summary>
    public RunManifest(DateTimeOffset startedAt)
    {
        _text.Append("ESA.NET run\r\n===========\r\n\r\n");

        Line("Started", startedAt.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture));
        Line("Version", typeof(RunManifest).Assembly.GetName().Version?.ToString() ?? "unknown");
    }

    /// <summary>The engine the run started from, and anything that would not load.</summary>
    public RunManifest Engine(string engineFilePath, string engineName, IReadOnlyList<string> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);

        Line("Engine file", engineFilePath);
        Line("Engine", engineName);

        foreach (var problem in problems)
        {
            Line("Load problem", problem);
        }

        return this;
    }

    /// <summary>What was asked for.</summary>
    public RunManifest Requested(double speed, SimulationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Line("Speed", Number(speed, 0) + " rev/min");
        Line("Cycles requested", settings.CycleCount.ToString(CultureInfo.InvariantCulture));
        Line("Mass balance", Number(settings.MassBalance, 3) + " mg");

        return this;
    }

    /// <summary>What the sweep was asked for, in place of a single speed.</summary>
    public RunManifest RequestedSweep(int rows, SimulationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Line("Multi-point sweep", rows.ToString(CultureInfo.InvariantCulture) + " row(s)");
        Line("Cycles requested", settings.CycleCount.ToString(CultureInfo.InvariantCulture));
        Line("Mass balance", Number(settings.MassBalance, 3) + " mg");

        return this;
    }

    /// <summary>How the run ended, and how long it took.</summary>
    public RunManifest Outcome(string outcome, TimeSpan elapsed)
    {
        // A sweep lists its rows before it knows how it ended, so the outcome needs a
        // heading of its own rather than trailing off the end of the row list.
        if (_sectioned)
        {
            Section("Result");
        }

        Line("Outcome", outcome);
        Line("Elapsed", Number(elapsed.TotalSeconds, 1) + " s");

        return this;
    }

    /// <summary>The headline figures, as the results panel shows them.</summary>
    public RunManifest Performance(Engine engine, int cyclesRun)
    {
        ArgumentNullException.ThrowIfNull(engine);

        Section("Performance");

        Line("Cycles run", cyclesRun.ToString(CultureInfo.InvariantCulture));
        Line("Torque", Number(engine.Torque, 2) + " N.m");
        Line("Power", Number(engine.BrakePower / 1e3, 2) + " kW");
        Line("IMEP", Number(engine.Imep / 1e5, 3) + " bar");
        Line("BMEP", Number(engine.Bmep / 1e5, 3) + " bar");
        Line("FMEP", Number(engine.Fmep / 1e5, 3) + " bar");
        Line("PMEP", Number(engine.Pmep / 1e5, 3) + " bar");
        Line("Volumetric eff.", Number(engine.VolumetricEfficiency, 1) + " %");
        Line("Thermal eff.", Number(engine.ThermalEfficiency, 1) + " %");
        Line("Mechanical eff.", Number(engine.MechanicalEfficiency, 1) + " %");
        Line("Fuel flow", Number(engine.FuelMassFlow, 2) + " kg/h");
        Line("SFC", Number(engine.Sfc, 1) + " g/kWh");
        Line("Trapped mass", Number(engine.TotalMass * 1e6, 2) + " mg");

        Line("Mass balance reached", Number(
            Math.Abs(engine.TotalMassInInletValve - engine.TotalMassOutExhaustValve) * 1e6, 3) + " mg");

        return this;
    }

    /// <summary>One row of a sweep, in the order the rows ran.</summary>
    public RunManifest Row(int row, double speed, string folder, MultiRunRowResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (row == 0)
        {
            Section("Rows");
        }

        var outcome = result.Result is { } completed
            ? $"{(completed.Converged ? "converged" : "not converged")} after "
              + $"{completed.CyclesRun} cycle(s), torque "
              + $"{Number(completed.Engine.Torque, 2)} N.m"
            : $"failed: {result.Failure}";

        Line($"{folder}", $"{Number(speed, 0)} rev/min, {outcome}");

        return this;
    }

    /// <summary>The input files copied into <c>inputs</c>.</summary>
    public RunManifest Inputs(IReadOnlyList<string> copied)
    {
        ArgumentNullException.ThrowIfNull(copied);

        Section("Inputs copied");

        if (copied.Count == 0)
        {
            _text.Append("  (none)\r\n");
        }

        foreach (var file in copied)
        {
            _text.Append("  ").Append(file).Append("\r\n");
        }

        return this;
    }

    /// <summary>The manifest as it will be written.</summary>
    public override string ToString() => _text.ToString();

    /// <summary>Writes the manifest, replacing anything already at <paramref name="path"/>.</summary>
    public void Write(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        File.WriteAllText(path, _text.ToString());
    }

    private void Section(string title)
    {
        _sectioned = true;

        _text.Append("\r\n").Append(title).Append("\r\n")
            .Append(new string('-', title.Length)).Append("\r\n");
    }

    private void Line(string label, string value) =>
        _text.Append(label.PadRight(Label)).Append(' ').Append(value).Append("\r\n");

    private static string Number(double value, int decimals) =>
        value.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
}
