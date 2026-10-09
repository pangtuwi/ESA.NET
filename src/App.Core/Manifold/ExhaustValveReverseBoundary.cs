using App.Core.Model;
using App.Core.Simulation;

namespace App.Core.Manifold;

/// <summary>
/// Gas driven back into the cylinder through the exhaust valve. Port of
/// <c>EXHAUST_VALVE_REVERSE</c> (Manifolds.pas:989-1284).
/// </summary>
/// <remarks>
/// <para>
/// Despite the name this is not the mirror of <see cref="InletValveReverseBoundary"/>.
/// The exhaust pipe end is the source and the cylinder the sink, so the nozzle is fed
/// from the <b>pipe's</b> stagnation state, and the routine carries a substitution branch
/// that hands the problem back to normal outward flow when the cylinder turns out to be
/// the higher of the two.
/// </para>
/// <para>
/// Note also that the critical ratio here is the plain isentropic
/// <c>((gam+1)/2)^(gam/(gam-1))</c>, not the discharge-coefficient-aware value
/// <c>INLET_VALVE_OPEN</c> derives from <c>CritPress</c>. For a pipe-fed nozzle that
/// is right against the pipe's stagnation pressure and wrong against its static one,
/// which is the pressure the original tests. See ISSUES.md B60 and B62.
/// </para>
/// <para>
/// Its stagnation guard latches as the open routine's throat guard does, the other way
/// round. While the pipe end flows in faster than <c>EVR</c>, a stagnation pressure that
/// has fallen to the cylinder's is pinned to <c>1.000001*Pcyl</c>, and that pin alone
/// drives an inflow of around a metre per second; past <c>EVR</c> it sustains itself, and
/// outward flow never resumes however far the cylinder rises above the pipe. See ISSUES.md
/// B81.
/// </para>
/// </remarks>
public static class ExhaustValveReverseBoundary
{
    private const double FootTolerance = 0.001 * 0.1;
    private const double VelocityTolerance = 1 * 0.0001;
    private const double PressureTolerance = 1 * 0.001;
    private const double DensityTolerance = 1 * 0.0001;
    private const int MaxIterations = 1000;

    /// <param name="crankAngle">Crank angle in <c>Main_Prog</c>'s 1 to 720 convention.</param>
    /// <param name="tuning">The EVF and EVR constants, in that order.</param>
    public static InletValveReverseBoundary.ThroatState Apply(
        PipeGrid grid,
        PipeGeometry pipe,
        ValveMotion valve,
        double dt,
        double cylinderPressure,
        double cylinderTemperature,
        double crankAngle,
        double pipeAreaAtValve,
        double valveFlowArea,
        InletValveReverseBoundary.ThroatState throat,
        (double Forward, double Reverse) tuning,
        double gamma = CharacteristicSolver.ExhaustGamma,
        bool singleRelaxation = false,
        bool stagnationChoke = false,
        bool releaseLatch = false,
        ManifoldDiagnostics? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(pipe);
        ArgumentNullException.ThrowIfNull(valve);

        var criticalRatio = ManifoldNumerics.Power((gamma + 1) / 2, gamma / (gamma - 1));
        var areaRatio = Math.Min(valveFlowArea / pipeAreaAtValve, 1);

        const int boundary = 0;
        const int interior = 1;
        var x4 = grid.X[boundary];

        var line = GridInterpolants.Through(grid, from: boundary, to: interior);

        var waveX = grid.X[interior];
        var waveU = grid.Velocity[interior];
        var waveP = grid.Pressure[interior];
        var waveR = grid.Density[interior];

        var pathX = grid.X[boundary];
        var pathU = grid.Velocity[boundary];
        var pathP = grid.Pressure[boundary];
        var pathR = grid.Density[boundary];

        var throatPressure = throat.Pressure;
        var throatMach = throat.MachNumber;
        var throatVelocity = throat.Velocity;
        var throatSpeedOfSound = throat.SpeedOfSound;
        var throatDensity = throat.Density;
        var dischargeCoefficient = throat.DischargeCoefficient;
        var throatTemperature = 0.0;

        double u4 = 0, p4 = 0, r4 = 0, c4 = 0;
        double entrancePressure = 0, stagnationTest = 0;
        double previousU = 0, previousP = 0, previousR = 0;
        double qMinus = 0, tMinus = 0, a0 = 0, t0 = 0;

        var iteration = 0;
        bool converged;
        var substituted = false;
        var releasing = false;

        do
        {
            // ---- C- from inside the pipe ----
            for (var guard = 0; guard <= 100; guard++)
            {
                if (iteration == 0)
                {
                    (u4, p4, r4) = (waveU, waveP, waveR);
                }

                var meanVelocity = (waveU + u4) / 2;
                var meanPressure = (waveP + p4) / 2;
                var meanDensity = (waveR + r4) / 2;
                var c = ManifoldNumerics.SpeedOfSound(gamma, meanPressure, meanDensity);

                var x = Math.Max(x4 - (dt / (1 / (meanVelocity - c))), 0);

                if (Math.Abs(x - waveX) < FootTolerance)
                {
                    var diameter = Math.Sqrt(4 * pipe.Area(x) / Math.PI);
                    var friction = ManifoldNumerics.FanningFriction(gamma, waveR, waveU, diameter, c);

                    qMinus = meanDensity * c;

                    var source = (-waveR * waveU * c * c / pipe.Area(x) * pipe.AreaGradient(x))
                                 + (((gamma - 1) * waveU + c)
                                    * (waveR * waveU * Math.Abs(waveU) * 2 * friction / diameter));

                    tMinus = waveP - (qMinus * waveU) + (source * dt);
                    break;
                }

                waveX = x;
                waveU = line.VelocityAt(x);
                waveP = line.PressureAt(x);
                waveR = line.DensityAt(x);
            }

            // ---- The path line ----
            for (var guard = 0; guard <= 100; guard++)
            {
                if (iteration == 0)
                {
                    (u4, p4, r4) = (pathU, pathP, pathR);
                }

                var meanVelocity = (pathU + u4) / 2;
                var meanPressure = (pathP + p4) / 2;
                var meanDensity = (pathR + r4) / 2;
                var c = ManifoldNumerics.SpeedOfSound(gamma, meanPressure, meanDensity);

                var x = Math.Abs(meanVelocity) < 1E-8 ? x4 : x4 - (dt / (1 / meanVelocity));
                x = Math.Max(x, 0);

                if (Math.Abs(x - pathX) < FootTolerance)
                {
                    var diameter = Math.Sqrt(4 * pipe.Area(x) / Math.PI);
                    var friction = ManifoldNumerics.FanningFriction(gamma, pathR, pathU, diameter, c);

                    a0 = c * c;
                    var b0 = (gamma - 1) * (pathR * pathU * Math.Abs(pathU) * 2 * friction / diameter);
                    t0 = (b0 * (x4 - x)) + pathP - (a0 * pathR);
                    break;
                }

                pathX = x;
                pathU = line.VelocityAt(x);
                pathP = line.PressureAt(x);
                pathR = line.DensityAt(x);
            }

            if (iteration == 0)
            {
                p4 = grid.Pressure[boundary];
                u4 = grid.Velocity[boundary];
                r4 = grid.Density[boundary];

                // From the throat speed of sound carried in from the previous step.
                throatTemperature = throatSpeedOfSound * throatSpeedOfSound / gamma / 287;
                entrancePressure = p4;
                c4 = Math.Sqrt(gamma * p4 / r4);

                // The original negates the velocity inside the square, which changes
                // nothing; written straight here.
                stagnationTest = p4 * ManifoldNumerics.Power(
                    1 + ((gamma - 1) / 2 * (u4 / c4) * (u4 / c4)), gamma / (gamma - 1));
            }

            if (stagnationTest <= cylinderPressure && u4 < tuning.Reverse)
            {
                stagnationTest = 1.000001 * cylinderPressure;
            }

            double stagnationPressure;
            double staticTemperature;
            double stagnationTemperature;

            var pinned = false;

            if (stagnationTest <= cylinderPressure || u4 > tuning.Forward || releasing)
            {
                stagnationPressure = cylinderPressure;
                staticTemperature = c4 * c4 / 287 / gamma;
                stagnationTemperature = staticTemperature
                    * ManifoldNumerics.Power(stagnationPressure / p4, (gamma - 1) / gamma);
            }
            else
            {
                stagnationPressure = p4 * ManifoldNumerics.Power(
                    1 + ((gamma - 1) / 2 * (u4 / c4) * (u4 / c4)), gamma / (gamma - 1));

                stagnationTest = stagnationPressure;

                if (stagnationTest <= cylinderPressure && u4 < 0)
                {
                    stagnationPressure = 1.000001 * cylinderPressure;
                    pinned = true;
                }

                staticTemperature = c4 * c4 / 287 / gamma;
                stagnationTemperature = staticTemperature
                    * ManifoldNumerics.Power(stagnationPressure / p4, (gamma - 1) / gamma);
            }

            if (stagnationPressure <= cylinderPressure)
            {
                // ---- Substitution for normal outward flow ----
                // The cylinder is the higher of the two, so nothing can come back in. The
                // throat is relaxed halfway towards cylinder conditions and the pipe end
                // held still. Unlike the inlet's equivalent branch this does not stop the
                // outer loop: it falls through to the convergence test, which passes on
                // the next pass because nothing moves. See ISSUES.md B61.
                throatPressure = (0.5 * cylinderPressure) + (0.5 * throatPressure);
                throatTemperature = (0.5 * cylinderTemperature) + (0.5 * throatTemperature);
                throatDensity = throatPressure / 287 / throatTemperature;
                throatSpeedOfSound = Math.Sqrt(gamma * 287 * throatTemperature);
                throatVelocity = 0;
                throatMach = 0;

                u4 = 0;
                c4 = Math.Sqrt(gamma * p4 / r4);
                substituted = true;
            }
            else
            {
                double entranceMach;

                // The throat state below is built from the pipe's stagnation pressure, but
                // the choked test is made on the static pipe-end pressure against the
                // cylinder's. The original carries its own "???????????" on this line.
                // See ISSUES.md B62.
                // stagnationChoke tests the pressure the throat is built from instead.
                var chokingPressure = stagnationChoke ? stagnationPressure : p4;

                if (chokingPressure / cylinderPressure >= criticalRatio)
                {
                    // ---- Choked ----
                    throatMach = 1;
                    throatPressure = stagnationPressure
                        * ManifoldNumerics.Power(2 / (gamma + 1), gamma / (gamma - 1));
                    throatTemperature = stagnationTemperature * 2 / (gamma + 1);
                    throatSpeedOfSound = Math.Sqrt(gamma * 287 * throatTemperature);
                    throatVelocity = throatSpeedOfSound;
                    throatDensity = throatPressure / 287 / throatTemperature;

                    dischargeCoefficient = valve.FlowCoefficient(
                        crankAngle - 360, p4 / cylinderPressure, reverse: true);

                    entranceMach = ThroatVelocitySolvers.ExhaustSonicMach(
                        gamma, dischargeCoefficient, areaRatio);
                }
                else
                {
                    // ---- Subsonic: the throat sits at cylinder pressure ----
                    throatPressure = cylinderPressure;
                    throatTemperature = stagnationTemperature
                        * ManifoldNumerics.Power(
                            throatPressure / stagnationPressure, (gamma - 1) / gamma);

                    throatSpeedOfSound = Math.Sqrt(gamma * 287 * throatTemperature);
                    throatMach = Math.Sqrt(
                        2 / (gamma - 1)
                        * (ManifoldNumerics.Power(
                            stagnationPressure / throatPressure, (gamma - 1) / gamma) - 1));

                    throatVelocity = throatSpeedOfSound * throatMach;
                    throatDensity = throatPressure / 287 / throatTemperature;

                    var entranceTemperature = stagnationTemperature
                        * ManifoldNumerics.Power(p4 / stagnationPressure, (gamma - 1) / gamma);
                    var entranceSpeedOfSound = Math.Sqrt(gamma * 287 * entranceTemperature);

                    dischargeCoefficient = valve.FlowCoefficient(
                        crankAngle - 360, p4 / cylinderPressure, reverse: true);

                    // Directly from continuity, with no root find: the exhaust subsonic
                    // branch is the only one of the four that does not need one.
                    var entranceVelocity = dischargeCoefficient * areaRatio * throatVelocity
                        * ManifoldNumerics.Power(
                            throatSpeedOfSound / entranceSpeedOfSound, 2 / (gamma - 1));

                    entranceMach = entranceVelocity / entranceSpeedOfSound;
                }

                p4 = stagnationPressure
                     / ManifoldNumerics.Power(
                         1 + ((gamma - 1) / 2 * entranceMach * entranceMach), gamma / (gamma - 1));

                // ---- Secant on the pipe-end pressure to match the Mach number ----
                var probe = 1;
                double previousProbeP = 0, previousProbeMach = 0;

                for (var guard = 0; guard <= 100; guard++)
                {
                    u4 = (tMinus - p4) / qMinus;
                    r4 = (p4 - t0) / a0;
                    c4 = ManifoldNumerics.SpeedOfSound(gamma, p4, r4);

                    var mach = u4 / c4;

                    if (Math.Abs(mach - entranceMach) <= 0.000000001)
                    {
                        break;
                    }

                    if (probe == 1)
                    {
                        probe = 2;
                        previousProbeP = p4;
                        previousProbeMach = mach;
                        p4 = 1.001 * p4;
                    }
                    else
                    {
                        var slope = (mach - previousProbeMach) / (p4 - previousProbeP);
                        previousProbeP = p4;
                        previousProbeMach = mach;
                        p4 += 0.8 * (entranceMach - mach) / slope;
                    }
                }

                c4 = Math.Sqrt(gamma * p4 / r4);
                entrancePressure = (0.95 * entrancePressure) + (0.05 * p4);
                p4 = entrancePressure;
                u4 = -u4;

                stagnationTest = p4 * ManifoldNumerics.Power(
                    1 + ((gamma - 1) / 2 * (u4 / c4) * (u4 / c4)), gamma / (gamma - 1));
            }

            // singleRelaxation stops on the substitution, as the inlet's equivalent branch
            // does, so its throat relaxation is applied once rather than twice (B61).
            converged = (singleRelaxation && substituted)
                        || (iteration != 0
                            && Math.Abs(u4 - previousU) < VelocityTolerance
                            && Math.Abs(r4 - previousR) < DensityTolerance
                            && Math.Abs(p4 - previousP) < PressureTolerance);

            // Settled with the stagnation pinned: the inflow is the pin's own, not the
            // pipe's, which sits at or below the cylinder. B81 substitutes outward flow.
            if (converged && pinned)
            {
                if (releaseLatch)
                {
                    releasing = true;
                    converged = false;

                    if (diagnostics is not null)
                    {
                        diagnostics.ExhaustValveLatchReleases++;
                    }
                }
                else if (diagnostics is not null)
                {
                    diagnostics.ExhaustValveLatches++;
                }
            }

            previousU = u4;
            previousP = p4;
            previousR = r4;
            iteration++;

            if (iteration > MaxIterations)
            {
                converged = true;
            }
        }
        while (!converged);

        throatVelocity = -throatVelocity;

        // In place on the current arrays, as its inlet counterpart does. See B58.
        grid.Velocity[boundary] = u4;
        grid.Pressure[boundary] = p4;
        grid.Density[boundary] = r4;
        grid.SpeedOfSound[boundary] = Math.Sqrt(gamma * p4 / r4);

        return new InletValveReverseBoundary.ThroatState(
            throatMach, throatVelocity, throatSpeedOfSound, throatDensity, throatPressure,
            dischargeCoefficient);
    }
}
