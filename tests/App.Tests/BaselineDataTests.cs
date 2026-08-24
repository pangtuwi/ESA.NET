using System.Globalization;
using App.Core.Expressions;
using App.Core.Interpolation;
using App.Persistence;
using App.Persistence.Tables;

namespace App.Tests;

/// <summary>
/// Guards the phase 4 validation baseline in <c>data/baseline</c>.
/// </summary>
/// <remarks>
/// <para>
/// The baseline is a captured run of the original Delphi application: an engine,
/// every side file it names, and the full-cycle trace it produced. BASELINE.md
/// documents it. Phase 4 is measured against it, so these tests make sure it stays
/// loadable and intact rather than quietly rotting.
/// </para>
/// <para>
/// They also pin the cross-checks that the baseline confirmed about the phase 3
/// port: the C7H17 fuel default, the crossed exhaust discharge tables, and
/// <c>FPlenumP</c> winning over <c>PlenumP</c>.
/// </para>
/// </remarks>
public sealed class BaselineDataTests
{
    private static string? Baseline { get; } = FindBaseline();

    private static void RequireBaseline() =>
        Assert.SkipWhen(Baseline is null, "Not running from a repository checkout.");

    private static string File(string name) => Path.Combine(Baseline!, name);

    private static EngineLoader CreateLoader() => new(
        new EngineDefinitionStore(),
        new CamProfileReader(),
        new SpeedKeyedTableReader(),
        new WallTemperatureTableReader(),
        new ExhaustBackPressureTableReader(),
        new ManifoldAreaTableStore(),
        new DischargeCoefficientTableStore());

    private static string? FindBaseline()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "data", "baseline");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    [Fact]
    public void EveryFileTheBaselineNeedsIsPresent()
    {
        RequireBaseline();

        string[] required =
        [
            "A2China.eng",
            "A2ChinaInlet_M758.maf",
            "A2ChinaExhaust_M.maf",
            "A2China Inlet Profile.cam",
            "A2China Exhaust Profile.cam",
            "A2China IVIn.vcd",
            "A2China IVOut.vcd",
            "A2ChinaVar.spk",
            "A2China.cwt",
            "A2China.exh",
            "A2China.txt",
        ];

        var missing = required.Where(name => !System.IO.File.Exists(File(name))).ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void TheBaselineEngineLoadsWithNothingUnresolved()
    {
        RequireBaseline();

        // Every path in A2China.eng is a dead C:\CAEEng\... absolute path from the
        // machine that produced the run, so this only passes because
        // LegacyPathResolver falls back to the file name beside the .eng.
        var result = CreateLoader().Load(File("A2China.eng"));

        Assert.True(result.IsComplete, string.Join("; ", result.Problems));
        Assert.Equal("A2 China Jetta 1.6L 5V Baseline", result.Engine.Name);
    }

    [Fact]
    public void LoadedValuesMatchTheScreenshottedSettings()
    {
        RequireBaseline();

        var engine = CreateLoader().Load(File("A2China.eng")).Engine;

        // Cylinders tab: the form displays 1595 cc, computed from these.
        Assert.Equal(4, engine.CylinderCount);
        Assert.Equal(81.0, engine.Bore);
        Assert.Equal(77.4, engine.Stroke);
        Assert.Equal(9.2, engine.CompressionRatio);

        var capacity = engine.CylinderCount * Math.PI / 4.0 * engine.Bore * engine.Bore * engine.Stroke / 1000.0;
        Assert.Equal(1595, capacity, 0);

        // Cams tab: durations are Open + 180 + Close, shown as 279 and 281.
        var inlet = engine.Manifold.InletValve;
        var exhaust = engine.Manifold.ExhaustValve;
        Assert.Equal(279, inlet.OpenAngle + 180 + inlet.CloseAngle);
        Assert.Equal(281, exhaust.OpenAngle + 180 + exhaust.CloseAngle);

        // Fuel tab: C 7, H 17, O 0, N 0. No .eng file stores composition, so the
        // screenshot is the only record that the original ran on these.
        Assert.Equal(7, engine.Cylinder.Fuel.C);
        Assert.Equal(17, engine.Cylinder.Fuel.H);
        Assert.Equal(0, engine.Cylinder.Fuel.O);
        Assert.Equal(0, engine.Cylinder.Fuel.N);

        // Inlet tab shows (99000): FPlenumP wins over the older PlenumP=98.0, which
        // this file also carries.
        Assert.Equal("(99000)", engine.Manifold.PlenumPressureFunction.Expression);
    }

    [Fact]
    public void ExhaustDischargeTablesAreCrossedAsTheValvesTabShows()
    {
        RequireBaseline();

        var definition = new EngineDefinitionStore().Read(File("A2China.eng"));

        // The Valves tab shows the exhaust Forward Flow Cd box holding IVOut.vcd and
        // Reverse holding IVIn.vcd, because forward flow through an exhaust valve is
        // outward.
        Assert.Equal("C:\\CAEEng\\A2China IVOut.vcd", definition.ExhaustValveCdOutwardFile);
        Assert.Equal("C:\\CAEEng\\A2China IVIn.vcd", definition.ExhaustValveCdInwardFile);
    }

    [Fact]
    public void TheGridExpressionsStayWithinTheLegacyLimitsAtTheBaselineSpeed()
    {
        RequireBaseline();

        var engine = CreateLoader().Load(File("A2China.eng")).Engine;
        var calculator = new GridSizeCalculator(new CachingExpressionEvaluator());

        var inletAreas = engine.Manifold.InletPipe.AreaVersusLength;
        var inletLength = inletAreas.Position[inletAreas.Count - 1] / 1000.0;

        var exhaustAreas = engine.Manifold.ExhaustPipe.AreaVersusLength;
        var exhaustLength = exhaustAreas.Position[exhaustAreas.Count - 1] / 1000.0;

        // The run was at 4000 rpm. Neither grid may exceed NI or NE or the solver
        // would have raised ECFDError instead of producing the trace.
        var inletPoints = calculator.InletGridSize(engine.Manifold.InletGrid.Expression, inletLength, 4000);
        var exhaustPoints = calculator.ExhaustGridSize(engine.Manifold.ExhaustGrid.Expression, exhaustLength, 4000);

        Assert.InRange(inletPoints, 1, App.Core.EsaLimits.InletGridPoints);
        Assert.InRange(exhaustPoints, 1, App.Core.EsaLimits.ExhaustGridPoints);
    }

    /// <summary>
    /// The chain from the trace to every headline number on the results screen, as
    /// tabulated in BASELINE.md. Each link is checkable on its own, so when phase 4
    /// starts producing numbers the first failing row localises the fault.
    /// </summary>
    [Fact]
    public void TheReportedPerformanceDerivesFromTheTrace()
    {
        RequireBaseline();

        var (header, rows) = ReadTrace();

        double Field(string row, string column) => double.Parse(
            row.Split(',')[header.IndexOf(column)], CultureInfo.InvariantCulture);

        var volumes = rows.Select(r => Field(r, "Vcyl")).ToList();

        // Swept volume per cylinder, in cubic metres. Vcyl is written in cc.
        var sweptVolume = (volumes.Max() - volumes.Min()) / 1e6;
        Assert.Equal(398.84e-6, sweptVolume, 8);

        // The accumulators reset at inlet valve closing, so the cycle-complete
        // values are the ones immediately before it, not those on the last row.
        var crankAngles = rows.Select(r => Field(r, "CA")).ToList();
        var beforeReset = crankAngles.IndexOf(-101);
        Assert.True(beforeReset >= 0, "The trace should contain crank angle -101.");

        var work = Field(rows[beforeReset], "WWork");
        var pumpWork = Field(rows[beforeReset], "PWork");

        // Sanity: the row after really is the reset. The accumulator is zeroed and
        // then immediately accrues that step's own contribution, so it lands near
        // zero rather than exactly on it.
        Assert.Equal(-100, Field(rows[beforeReset + 1], "CA"));
        Assert.True(
            Math.Abs(Field(rows[beforeReset + 1], "WWork")) < 1,
            $"Expected WWork to reset at -100, found {Field(rows[beforeReset + 1], "WWork")}.");

        const double Rpm = 4000;
        const int Cylinders = 4;

        var imep = work / sweptVolume;
        var pmep = pumpWork / sweptVolume;
        var totalFmep = 1.0e5 * (0.97 + (0.15 * Rpm / 1000) + (0.05 * Math.Pow(Rpm / 1000, 2)));
        var fmep = totalFmep - pmep;
        var bmep = imep - pmep - fmep;
        var torque = bmep * sweptVolume * Cylinders / (2 * 2 * Math.PI);
        var power = torque * Rpm * 2 * Math.PI / 60;

        // Against the results screen, at its displayed precision.
        Assert.Equal(14.291, imep / 1e5, 3);
        Assert.Equal(-0.392, pmep / 1e5, 3);
        Assert.Equal(2.370, totalFmep / 1e5, 3);
        Assert.Equal(2.762, fmep / 1e5, 3);
        Assert.Equal(11.921, bmep / 1e5, 3);
        Assert.Equal(151.3, torque, 1);
        Assert.Equal(63.4, power / 1000, 1);

        // IMEP - FMEP is not BMEP; the PMEP term genuinely participates.
        Assert.NotEqual(bmep / 1e5, (imep - fmep) / 1e5, 1);
    }

    [Fact]
    public void TraceLandmarksMatchTheChartsAndTheValveTiming()
    {
        RequireBaseline();

        var (header, rows) = ReadTrace();

        double Field(string row, string column) => double.Parse(
            row.Split(',')[header.IndexOf(column)], CultureInfo.InvariantCulture);

        var crankAngles = rows.Select(r => Field(r, "CA")).ToList();
        var pressures = rows.Select(r => Field(r, "PCyl")).ToList();
        var burntTemperatures = rows.Select(r => Field(r, "Tb")).ToList();

        // Peak pressure just after firing TDC; the P-V diagram peaks a little over
        // 70 bar and the in-cylinder chart tops out near 3000 K.
        var peakPressure = pressures.Max();
        Assert.Equal(70.1, peakPressure / 1e5, 1);
        Assert.Equal(14, crankAngles[pressures.IndexOf(peakPressure)]);

        var peakTemperature = burntTemperatures.Max();
        Assert.Equal(3015, peakTemperature, 0);
        Assert.Equal(-7, crankAngles[burntTemperatures.IndexOf(peakTemperature)]);

        // The accumulator reset sits at inlet valve closing: IVC is 80 degrees after
        // bottom dead centre, and the trace counts from firing TDC, so -180 + 80.
        var engine = CreateLoader().Load(File("A2China.eng")).Engine;
        var expectedReset = -180 + engine.Manifold.InletValve.CloseAngle;
        Assert.Equal(-100, expectedReset);

        // At the reset each accumulator drops to zero and then picks up that step's
        // own contribution, so it lands near zero rather than exactly on it.
        var resetRow = crankAngles.IndexOf(expectedReset);

        foreach (var accumulator in (string[])["WWork", "PWork", "htLoss"])
        {
            var atReset = Field(rows[resetRow], accumulator);
            var before = Field(rows[resetRow - 1], accumulator);

            Assert.True(
                Math.Abs(atReset) < 1,
                $"Expected {accumulator} to reset at {expectedReset}, found {atReset}.");
            Assert.True(
                Math.Abs(before) > 10,
                $"Expected {accumulator} to have accumulated before the reset, found {before}.");
        }
    }

    /// <summary>
    /// The performance data file the run wrote. It carries more precision than the
    /// results screen and several quantities the screen never shows.
    /// </summary>
    [Fact]
    public void ThePerformanceFileMatchesTheResultsScreen()
    {
        RequireBaseline();

        var (header, rows) = ReadPerformance();

        // One row per run: the original capture and the re-run with manifold output
        // enabled. Identical to the last digit, so the simulation is deterministic.
        Assert.Equal(2, rows.Count);
        Assert.Equal(rows[0], rows[1]);

        double Value(string column) => double.Parse(
            rows[0][header.IndexOf(column)], CultureInfo.InvariantCulture);

        Assert.Equal(4000, Value("Speed"));
        Assert.Equal(14.291, Value("IMEP"));
        Assert.Equal(-0.392, Value("PMEP"));
        Assert.Equal(2.762, Value("FMEP"));
        Assert.Equal(11.921, Value("BMEP"));
        Assert.Equal(151.34, Value("Torque"));
        Assert.Equal(63.395, Value("Power"));
        Assert.Equal(273.6, Value("SFC"));
        Assert.Equal(109.7, Value("VEff"));

        // Mechanical efficiency is BMEP over IMEP.
        Assert.Equal(Value("BMEP") / Value("IMEP") * 100, Value("MEff"), 1);

        // The 0.3 mg the results screen reports is the gap between the two mass
        // totals, which is the convergence metric the run stopped on.
        Assert.Equal(0.27, Math.Abs(Value("MassIn") - Value("MassOut")), 2);
    }

    /// <summary>
    /// Speed-keyed lookups, checkable without running any physics: the spark angle
    /// and exhaust back pressure the run reported are what the tables interpolate to
    /// at 4000 rpm.
    /// </summary>
    [Fact]
    public void SpeedKeyedLookupsMatchTheReportedValues()
    {
        RequireBaseline();

        var (header, rows) = ReadPerformance();
        double Value(string column) => double.Parse(
            rows[0][header.IndexOf(column)], CultureInfo.InvariantCulture);

        var engine = CreateLoader().Load(File("A2China.eng")).Engine;

        var spark = LegacyInterpolation.AtSpeed(engine.SparkAngle.Rpm, engine.SparkAngle.Values, 4000);
        var backPressure = LegacyInterpolation.AtSpeed(
            engine.Manifold.ExhaustBack.Rpm, engine.Manifold.ExhaustBack.Pressure, 4000);

        Assert.Equal(Value("Spark"), spark, 1);
        Assert.Equal(Value("BackP"), backPressure, 1);
    }

    /// <summary>
    /// Pins the four-cylinder assumption baked into the fuel flow and thermal
    /// efficiency formulas, so that porting them verbatim in phase 4 is a recorded
    /// decision rather than an accident.
    /// </summary>
    [Fact]
    public void FuelFlowAndThermalEfficiencyAssumeFourCylinders()
    {
        RequireBaseline();

        var (header, rows) = ReadPerformance();
        double Value(string column) => double.Parse(
            rows[0][header.IndexOf(column)], CultureInfo.InvariantCulture);

        var definition = new EngineDefinitionStore().Read(File("A2China.eng"));

        const double Rpm = 4000;
        var massIn = Value("MassIn") / 1e6;                 // mg -> kg
        var fuelMass = 1 / definition.Lambda * massIn / (definition.AirFuelRatio + 1);

        // Delphi: mf := Cyl.Fuel.m * 2 * Nrpm * 60. No cylinder count anywhere.
        var delphiFuelFlow = fuelMass * 2 * Rpm * 60;

        // Physically: per-cylinder per-cycle mass, times cylinders, times cycles/hr.
        var physicalFuelFlow = fuelMass * definition.CylinderCount * (Rpm / 2) * 60;

        Assert.Equal(Value("mf"), delphiFuelFlow, 2);

        // They agree here only because this engine has four cylinders.
        Assert.Equal(4, definition.CylinderCount);
        Assert.Equal(physicalFuelFlow, delphiFuelFlow, 6);

        // At any other count the Delphi formula is wrong by 4 / NCyl.
        foreach (var cylinders in (int[])[3, 6, 8])
        {
            var physical = fuelMass * cylinders * (Rpm / 2) * 60;
            Assert.Equal(4.0 / cylinders, delphiFuelFlow / physical, 6);
        }
    }

    [Fact]
    public void EveryShippedEngineIsFourCylinder()
    {
        Assert.SkipWhen(TestPaths.Legacy is null, "Not running from a repository checkout.");

        // Which is why the hard-coded cylinder count above was never caught.
        var store = new EngineDefinitionStore();
        var counts = TestPaths.AllLegacyEngineFiles()
            .Select(path => store.Read(path).CylinderCount)
            .Distinct()
            .ToList();

        Assert.Equal([4], counts);
    }

    /// <summary>
    /// The strongest validation of the phase 3 expression work available: the
    /// original wrote one column per manifold grid point, so the width of its own
    /// output files is the grid size it computed.
    /// </summary>
    /// <remarks>
    /// Matching it exercises the whole chain at once — the parser, left-associative
    /// <c>^</c>, <c>DelphiMath.Power</c>'s integer fast path, round-half-to-even, the
    /// <c>.maf</c> reader and the pipe length derived from it. A slip in any one of
    /// them would very likely shift the rounded point count.
    /// </remarks>
    [Theory]
    [InlineData("InlPress.m", true)]
    [InlineData("InlVel.m", true)]
    [InlineData("ExhPress.m", false)]
    [InlineData("ExhVel.m", false)]
    public void ManifoldFieldWidthMatchesTheComputedGridSize(string fileName, bool isInlet)
    {
        RequireBaseline();

        var engine = CreateLoader().Load(File("A2China.eng")).Engine;
        var calculator = new GridSizeCalculator(new CachingExpressionEvaluator());

        var areas = isInlet
            ? engine.Manifold.InletPipe.AreaVersusLength
            : engine.Manifold.ExhaustPipe.AreaVersusLength;

        var pipeLength = areas.Position[areas.Count - 1] / 1000.0;

        var expected = isInlet
            ? calculator.InletGridSize(engine.Manifold.InletGrid.Expression, pipeLength, 4000)
            : calculator.ExhaustGridSize(engine.Manifold.ExhaustGrid.Expression, pipeLength, 4000);

        var columns = System.IO.File.ReadLines(File(fileName))
            .First()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Length;

        Assert.Equal(expected, columns);
    }

    /// <summary>
    /// The manifold output and the PVT trace come from <b>adjacent cycles</b>, not the
    /// same one.
    /// </summary>
    /// <remarks>
    /// Through the closed period the two agree to the printed precision, because that
    /// part of the cycle is fixed by the mass trapped at inlet valve closing, which has
    /// converged. Through gas exchange they diverge by up to about 0.07 bar, because
    /// that part depends on the manifold wave state, which is still settling from cycle
    /// to cycle. Phase 4 must not assume the two files describe the same cycle.
    /// </remarks>
    [Fact]
    public void TheManifoldTracesComeFromAnAdjacentCycleToThePvtTrace()
    {
        RequireBaseline();

        // Pcyl.txt writes crank angle offset by 360, so its CA 360 is the trace's
        // firing top dead centre.
        var cylinderPressures = System.IO.File.ReadLines(File("Pcyl.txt"))
            .Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(p => p.Length == 2)
            .ToDictionary(
                p => (int)double.Parse(p[0], CultureInfo.InvariantCulture),
                p => double.Parse(p[1], CultureInfo.InvariantCulture));

        var (header, rows) = ReadTrace();
        var traceIndex = header.IndexOf("PCyl");
        var tracePressures = rows
            .Select(r => r.Split(','))
            .ToDictionary(
                f => (int)double.Parse(f[0], CultureInfo.InvariantCulture),
                f => double.Parse(f[traceIndex], CultureInfo.InvariantCulture) / 1e5);

        // 620 crank angles: 360 through 720, then 1 through 259.
        Assert.Equal(620, cylinderPressures.Count);

        var closedPeriod = new List<double>();
        var gasExchange = new List<double>();

        foreach (var (manifoldAngle, pressure) in cylinderPressures)
        {
            var traceAngle = manifoldAngle - 360;

            if (!tracePressures.TryGetValue(traceAngle, out var expected))
            {
                continue;
            }

            // Exhaust valve opening is 64 degrees before bottom dead centre, so the
            // cylinder is sealed from firing top dead centre until then.
            var difference = Math.Abs(expected - pressure);
            (traceAngle is >= 0 and <= 250 ? closedPeriod : gasExchange).Add(difference);
        }

        Assert.True(closedPeriod.Count > 200, $"Expected a full closed period, got {closedPeriod.Count}.");
        Assert.NotEmpty(gasExchange);

        // Through the closed period the two are the same to the printed precision:
        // the trace writes whole pascals, which is 1e-5 bar.
        Assert.True(
            closedPeriod.Max() <= 0.0002,
            $"Closed period should match; largest difference was {closedPeriod.Max():F4} bar.");

        // Through gas exchange they do not, because the manifold wave state differs
        // between cycles. This is recorded, not tolerated: if it ever collapses to
        // zero the two files have started describing the same cycle and the note in
        // BASELINE.md needs revisiting.
        Assert.True(
            gasExchange.Max() > 0.01,
            $"Expected cycle-to-cycle divergence in gas exchange, largest was {gasExchange.Max():F4} bar.");
        Assert.True(
            gasExchange.Max() < 0.5,
            $"Gas exchange divergence of {gasExchange.Max():F4} bar is larger than a settling manifold explains.");
    }

    [Fact]
    public void EveryManifoldOutputFileHasTheSameRowCount()
    {
        RequireBaseline();

        string[] outputs =
        [
            "Inlet.txt", "Exhaust.txt", "Pcyl.txt", "Tcyl.txt", "MassFlow.txt",
            "InlPress.m", "InlVel.m", "ExhPress.m", "ExhVel.m",
        ];

        var missing = outputs.Where(f => !System.IO.File.Exists(File(f))).ToList();
        Assert.Empty(missing);

        // All nine are written from the same block, one row per crank angle.
        var rowCounts = outputs
            .Select(f => System.IO.File.ReadAllLines(File(f)).Count(l => l.Trim().Length > 0))
            .Distinct()
            .ToList();

        Assert.Equal([620], rowCounts);
    }

    private static (List<string> Header, List<string[]> Rows) ReadPerformance()
    {
        var lines = System.IO.File.ReadAllLines(File("SimulDat.txt"))
            .Where(l => l.Trim().Length > 0)
            .ToList();

        var header = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(h => h.Trim())
            .ToList();

        var rows = lines.Skip(1)
            .Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToList();

        return (header, rows);
    }

    private static (List<string> Header, List<string> Rows) ReadTrace()
    {
        var lines = System.IO.File.ReadAllLines(File("A2China.txt"))
            .Where(l => l.Trim().Length > 0)
            .ToList();

        return (lines[0].Split(',').Select(h => h.Trim()).ToList(), lines.Skip(1).ToList());
    }

    [Fact]
    public void TheReferenceTraceHasTheShapeBaselineMdDescribes()
    {
        RequireBaseline();

        var lines = System.IO.File.ReadAllLines(File("A2China.txt"))
            .Where(l => l.Trim().Length > 0)
            .ToList();

        var header = lines[0].Split(',').Select(h => h.Trim()).ToList();
        var rows = lines.Skip(1).ToList();

        // Crank angle plus the 28 captured values, matching ColName in CAList2z.pas.
        Assert.Equal(29, header.Count);
        Assert.Equal("CA", header[0]);
        Assert.Equal("PCyl", header[2]);
        Assert.Equal("FuelM", header[15]);
        Assert.Equal("htLoss", header[^1]);

        // One row per crank angle over [-359, 360].
        Assert.Equal(720, rows.Count);

        double Field(string row, int index) =>
            double.Parse(row.Split(',')[index], CultureInfo.InvariantCulture);

        Assert.Equal(App.Core.EsaLimits.FirstCrankAngle, Field(rows[0], 0));
        Assert.Equal(App.Core.EsaLimits.LastCrankAngle, Field(rows[^1], 0));

        // The trace validates its own geometry: Vcyl is written in cc, so the swept
        // volume across four cylinders is the 1595 cc the form displays, and the
        // ratio of the extremes is the 9.2 compression ratio in the .eng.
        var volumes = rows.Select(r => Field(r, 1)).ToList();
        var swept = (volumes.Max() - volumes.Min()) * 4;
        var compressionRatio = volumes.Max() / volumes.Min();

        Assert.Equal(1595, swept, 0);
        Assert.Equal(9.2, compressionRatio, 1);
    }
}
