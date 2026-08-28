using App.Core.Model;
using App.Core.Simulation;

namespace App.Core.Manifold;

/// <summary>
/// Air drawn through an open inlet valve into the cylinder. Port of
/// <c>INLET_VALVE_OPEN</c> (Manifolds.pas:680-985).
/// </summary>
/// <remarks>
/// <para>
/// The forward case treats the valve as a nozzle fed from the pipe end's stagnation
/// state, choosing between choked and subsonic flow on the pressure ratio against a
/// critical ratio derived from the discharge coefficient and area ratio. An inner secant
/// iteration then moves the pipe-end pressure until its Mach number matches the nozzle's.
/// </para>
/// <para>
/// When the pipe end falls to or below the throat pressure, or its velocity drops below
/// the <c>IVFR</c> tuning constant, the flow has reversed and the whole problem is handed
/// to <see cref="InletValveReverseBoundary"/>. That routine works on the current-time-level
/// arrays (ISSUES.md B58), so this one publishes the working state into them first and
/// reads the answer back out afterwards.
/// </para>
/// <para>
/// Three tuning constants from the <c>.eng</c> file steer the switching: <c>IVF</c>
/// (forward), <c>IVFR</c> (forward-reverse threshold) and <c>IVR</c>, which is passed
/// through to the reverse routine. They have no physical derivation in the source.
/// </para>
/// </remarks>
public static class InletValveOpenBoundary
{
    private const double FootTolerance = 0.001 * 0.1;
    private const double VelocityTolerance = 1 * 0.0001;
    private const double PressureTolerance = 1 * 0.001;
    private const double DensityTolerance = 1 * 0.0001;
    private const int MaxIterations = 1000;

    /// <param name="crankAngle">
    /// Crank angle in <c>Main_Prog</c>'s 1 to 720 convention, as for
    /// <see cref="InletValveReverseBoundary.Apply"/>.
    /// </param>
    /// <param name="tuning">The IVF, IVFR and IVR constants, in that order.</param>
    public static InletValveReverseBoundary.ThroatState Apply(
        PipeGrid current,
        PipeGrid target,
        PipeGeometry pipe,
        ValveMotion valve,
        double dt,
        double cylinderPressure,
        double cylinderTemperature,
        double crankAngle,
        double pipeAreaAtValve,
        double valveFlowArea,
        InletValveReverseBoundary.ThroatState throat,
        (double Forward, double ForwardReverse, double Reverse) tuning)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(pipe);
        ArgumentNullException.ThrowIfNull(valve);

        const double gamma = CharacteristicSolver.InletGamma;

        var q = current.ActiveCount - 1;
        var interior = q - 1;
        var x4 = current.X[q];

        var areaRatio = Math.Min(valveFlowArea / pipeAreaAtValve, 1);

        // The critical ratio is the reciprocal of CritPress here - the only place that
        // routine is used - rather than the plain isentropic value the commented-out line
        // beside it would have given.
        var dischargeCoefficient = valve.FlowCoefficient(
            crankAngle - 360, current.Pressure[q] / cylinderPressure, reverse: false);
        var criticalRatio = 1 / ManifoldNumerics.CriticalPressure(
            gamma, dischargeCoefficient, areaRatio);

        var line = GridInterpolants.Through(current, from: interior, to: q);

        var waveX = current.X[interior];
        var waveU = current.Velocity[interior];
        var waveP = current.Pressure[interior];
        var waveR = current.Density[interior];

        var pathX = current.X[q];
        var pathU = current.Velocity[q];
        var pathP = current.Pressure[q];
        var pathR = current.Density[q];

        var throatPressure = throat.Pressure;
        var throatMach = throat.MachNumber;
        var throatVelocity = throat.Velocity;
        var throatSpeedOfSound = throat.SpeedOfSound;
        var throatDensity = throat.Density;

        double u4 = 0, p4 = 0, r4 = 0, c4 = 0;
        double entrancePressure = 0, pressureDifference = 0;
        double previousU = 0, previousP = 0, previousR = 0;
        double qPlus = 0, tPlus = 0, a0 = 0, t0 = 0;

        var iteration = 0;
        var handedToReverse = false;
        bool converged;

        do
        {
            // ---- C+ from inside the pipe ----
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

                var x = Math.Min(x4 - (dt / (1 / (meanVelocity + c))), pipe.Length);

                if (Math.Abs(x - waveX) < FootTolerance)
                {
                    var diameter = Math.Sqrt(4 * pipe.Area(x) / Math.PI);
                    var friction = ManifoldNumerics.FanningFriction(gamma, waveR, waveU, diameter, c);

                    qPlus = meanDensity * c;

                    var source = (-waveR * waveU * c * c / pipe.Area(x) * pipe.AreaGradient(x))
                                 + (((gamma - 1) * waveU - c)
                                    * (waveR * waveU * Math.Abs(waveU) * 2 * friction / diameter));

                    tPlus = waveP + (qPlus * waveU) + (source * dt);
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
                x = Math.Min(x, pipe.Length);

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
                p4 = current.Pressure[q];
                u4 = current.Velocity[q];
                r4 = current.Density[q];
                c4 = Math.Sqrt(gamma * p4 / r4);
                entrancePressure = p4;
                pressureDifference = 0;

                if (p4 <= cylinderPressure)
                {
                    pressureDifference = cylinderPressure - throatPressure;
                }

                if ((throatVelocity >= 0 && throatVelocity < c4)
                    || (u4 <= 0 && u4 > tuning.ForwardReverse))
                {
                    throatPressure = cylinderPressure;
                }
            }

            // A one-shot nudge that lifts the pipe end back above the throat when it has
            // sagged below it while still flowing forwards.
            if (p4 <= throatPressure && u4 > tuning.Forward)
            {
                p4 += pressureDifference * 0.5;

                if (p4 <= throatPressure)
                {
                    p4 = 1.000001 * throatPressure;
                }

                entrancePressure = p4;
                u4 = 0.5 * (u4 + tuning.Forward);
                pressureDifference = 0;
            }

            var reversed = p4 <= throatPressure || u4 < tuning.ForwardReverse;

            double stagnationPressure;
            double stagnationTemperature = 0;

            if (reversed)
            {
                stagnationPressure = cylinderPressure;
            }
            else
            {
                stagnationPressure = p4
                    * ManifoldNumerics.Power(
                        1 + ((gamma - 1) / 2 * (u4 / c4) * (u4 / c4)), gamma / (gamma - 1));

                var staticTemperature = c4 * c4 / 287 / gamma;
                stagnationTemperature = staticTemperature
                    * ManifoldNumerics.Power(stagnationPressure / p4, (gamma - 1) / gamma);
            }

            if (reversed)
            {
                // ---- Hand over to the reverse routine ----
                if (u4 >= 0)
                {
                    throatPressure = (0.5 * throatPressure) + (0.5 * p4);
                }

                // The reverse routine reads and writes the current arrays in place, so the
                // working state is published into them and read back. See ISSUES.md B58.
                current.Velocity[q] = u4;
                current.Density[q] = r4;
                current.Pressure[q] = p4;
                current.SpeedOfSound[q] = Math.Sqrt(gamma * p4 / r4);

                var reverseResult = InletValveReverseBoundary.Apply(
                    current, pipe, valve, dt, cylinderPressure, cylinderTemperature, crankAngle,
                    pipeAreaAtValve, valveFlowArea,
                    new InletValveReverseBoundary.ThroatState(
                        throatMach, throatVelocity, throatSpeedOfSound, throatDensity,
                        throatPressure, dischargeCoefficient),
                    tuning.Reverse);

                throatMach = reverseResult.MachNumber;
                throatVelocity = reverseResult.Velocity;
                throatSpeedOfSound = reverseResult.SpeedOfSound;
                throatDensity = reverseResult.Density;
                throatPressure = reverseResult.Pressure;
                dischargeCoefficient = reverseResult.DischargeCoefficient;

                u4 = current.Velocity[q];
                r4 = current.Density[q];
                p4 = current.Pressure[q];
                c4 = Math.Sqrt(gamma * p4 / r4);

                handedToReverse = true;
            }
            else
            {
                double entranceMach;

                if (p4 / throatPressure >= criticalRatio)
                {
                    // ---- Choked ----
                    throatMach = 1;
                    var throatTemperature = stagnationTemperature * 2 / (gamma + 1);
                    throatSpeedOfSound = Math.Sqrt(gamma * 287 * throatTemperature);
                    throatVelocity = throatSpeedOfSound;
                    throatPressure = p4 * ManifoldNumerics.Power(
                        throatSpeedOfSound / c4, 2 * gamma / (gamma - 1));
                    throatDensity = throatPressure / 287 / throatTemperature;

                    dischargeCoefficient = valve.FlowCoefficient(
                        crankAngle - 360, p4 / cylinderPressure, reverse: false);

                    entranceMach = dischargeCoefficient * areaRatio
                                   * ManifoldNumerics.Power(
                                       throatSpeedOfSound / c4, (gamma + 1) / (gamma - 1));

                    entranceMach = Math.Min(entranceMach, 1);
                }
                else
                {
                    // ---- Subsonic: the throat simply sits at cylinder pressure ----
                    throatPressure = cylinderPressure;
                    var throatTemperature = stagnationTemperature
                        * ManifoldNumerics.Power(
                            throatPressure / stagnationPressure, (gamma - 1) / gamma);

                    throatSpeedOfSound = Math.Sqrt(gamma * 287 * throatTemperature);
                    throatMach = Math.Sqrt(
                        2 / (gamma - 1)
                        * (ManifoldNumerics.Power(
                            stagnationPressure / throatPressure, (gamma - 1) / gamma) - 1));

                    throatVelocity = throatSpeedOfSound * throatMach;
                    throatDensity = throatPressure / 287 / throatTemperature;

                    var entranceTemperature = throatTemperature
                        * ManifoldNumerics.Power(p4 / throatPressure, (gamma - 1) / gamma);
                    var entranceSpeedOfSound = Math.Sqrt(gamma * 287 * entranceTemperature);

                    dischargeCoefficient = valve.FlowCoefficient(
                        crankAngle - 360, p4 / cylinderPressure, reverse: false);

                    double entranceVelocity;

                    if (entranceSpeedOfSound < throatSpeedOfSound)
                    {
                        entranceVelocity = 0;
                    }
                    else
                    {
                        var cdArea = dischargeCoefficient * areaRatio;

                        entranceVelocity = Math.Sqrt(
                            2 / (gamma - 1)
                            * ((entranceSpeedOfSound * entranceSpeedOfSound)
                               - (throatSpeedOfSound * throatSpeedOfSound))
                            / ((1 / (cdArea * cdArea)
                                * ManifoldNumerics.Power(
                                    entranceSpeedOfSound / throatSpeedOfSound, 4 / (gamma - 1)))
                               - 1));
                    }

                    entranceVelocity = Math.Min(entranceVelocity, throatVelocity);
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
                    // Opposite sign to the reverse routine, and the density comes from the
                    // path line rather than from the local speed of sound.
                    u4 = (tPlus - p4) / qPlus;
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
            }

            // The reverse hand-over stops the outer loop outright, and the convergence
            // test is skipped with it.
            converged = handedToReverse
                        || (iteration != 0
                            && Math.Abs(u4 - previousU) < VelocityTolerance
                            && Math.Abs(r4 - previousR) < DensityTolerance
                            && Math.Abs(p4 - previousP) < PressureTolerance);

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

        target.Velocity[q] = u4;
        target.Pressure[q] = p4;
        target.Density[q] = r4;
        target.SpeedOfSound[q] = Math.Sqrt(gamma * p4 / r4);

        return new InletValveReverseBoundary.ThroatState(
            throatMach, throatVelocity, throatSpeedOfSound, throatDensity, throatPressure,
            dischargeCoefficient);
    }
}
