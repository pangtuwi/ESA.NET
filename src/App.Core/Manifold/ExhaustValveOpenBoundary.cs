using App.Core.Model;
using App.Core.Simulation;

namespace App.Core.Manifold;

/// <summary>
/// Gas leaving the cylinder through an open exhaust valve. Port of
/// <c>EXHAUST_VALVE_OPEN</c> (Manifolds.pas:1288-1531).
/// </summary>
/// <remarks>
/// <para>
/// Structurally this is the closest sibling of <see cref="InletValveReverseBoundary"/>
/// rather than of <see cref="InletValveOpenBoundary"/>, because the topology is the same:
/// a nozzle discharging from cylinder stagnation conditions into the pipe. It runs at the
/// exhaust gamma of 1.3, uses the exhaust velocity solvers, and hands over to
/// <see cref="ExhaustValveReverseBoundary"/> when the throat pressure rises to meet the
/// cylinder or the pipe end falls below the <c>EVFR</c> constant.
/// </para>
/// <para>
/// Unlike the reverse routine it writes to the new-time-level arrays, so the hand-over
/// publishes the working state into the current arrays first and reads it back
/// afterwards, exactly as the inlet pair does. See ISSUES.md B58.
/// </para>
/// </remarks>
public static class ExhaustValveOpenBoundary
{
    private const double FootTolerance = 0.001 * 0.1;
    private const double VelocityTolerance = 1 * 0.0001;
    private const double PressureTolerance = 1 * 0.001;
    private const int MaxIterations = 1000;

    /// <param name="crankAngle">Crank angle in <c>Main_Prog</c>'s 1 to 720 convention.</param>
    /// <param name="tuning">The EVF, EVFR and EVR constants, in that order.</param>
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

        const double gamma = CharacteristicSolver.ExhaustGamma;

        var criticalRatio = ManifoldNumerics.Power((gamma + 1) / 2, gamma / (gamma - 1));
        var areaRatio = Math.Min(valveFlowArea / pipeAreaAtValve, 1);

        const int boundary = 0;
        const int interior = 1;
        var x4 = current.X[boundary];

        var line = GridInterpolants.Through(current, from: boundary, to: interior);

        var waveX = current.X[interior];
        var waveU = current.Velocity[interior];
        var waveP = current.Pressure[interior];
        var waveR = current.Density[interior];

        var throatPressure = throat.Pressure;
        var throatMach = throat.MachNumber;
        var throatVelocity = throat.Velocity;
        var throatSpeedOfSound = throat.SpeedOfSound;
        var throatDensity = throat.Density;
        var dischargeCoefficient = throat.DischargeCoefficient;

        double u4 = 0, p4 = 0, r4 = 0, c4 = 0;
        double previousU = 0, previousP = 0;
        double qMinus = 0, tMinus = 0;

        var iteration = 0;
        var handedToReverse = false;
        bool converged;

        do
        {
            // ---- C- from inside the pipe ----
            while (true)
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

            if (iteration == 0)
            {
                p4 = current.Pressure[boundary];
                u4 = current.Velocity[boundary];
                r4 = current.Density[boundary];
            }

            // The original guards this with a nested u[4] >= 0 test at iteration zero and
            // then writes the same condition again unconditionally, followed by two
            // literal "Pt := Pt" statements. The unconditional form subsumes the nested
            // one, so only it survives here. See ISSUES.md B63.
            if (throatPressure >= cylinderPressure && u4 > tuning.Forward)
            {
                throatPressure = 0.999999 * cylinderPressure;
            }

            var cylinderSpeedOfSound = Math.Sqrt(gamma * 287 * cylinderTemperature);

            if (throatPressure >= cylinderPressure || u4 < tuning.ForwardReverse)
            {
                // ---- Hand over to the reverse routine ----
                current.Velocity[boundary] = u4;
                current.Pressure[boundary] = p4;
                current.Density[boundary] = r4;
                current.SpeedOfSound[boundary] = Math.Sqrt(gamma * p4 / r4);

                var reverseResult = ExhaustValveReverseBoundary.Apply(
                    current, pipe, valve, dt, cylinderPressure, cylinderTemperature, crankAngle,
                    pipeAreaAtValve, valveFlowArea,
                    new InletValveReverseBoundary.ThroatState(
                        throatMach, throatVelocity, throatSpeedOfSound, throatDensity,
                        throatPressure, dischargeCoefficient),
                    (tuning.Forward, tuning.Reverse));

                throatMach = reverseResult.MachNumber;
                throatVelocity = reverseResult.Velocity;
                throatSpeedOfSound = reverseResult.SpeedOfSound;
                throatDensity = reverseResult.Density;
                throatPressure = reverseResult.Pressure;
                dischargeCoefficient = reverseResult.DischargeCoefficient;

                u4 = current.Velocity[boundary];
                p4 = current.Pressure[boundary];
                r4 = current.Density[boundary];

                handedToReverse = true;
            }
            else
            {
                double entranceVelocity;
                double entranceMach;

                if (cylinderPressure / throatPressure >= criticalRatio)
                {
                    // ---- Choked ----
                    throatMach = 1;
                    throatPressure = cylinderPressure
                        * ManifoldNumerics.Power(2 / (gamma + 1), gamma / (gamma - 1));
                    var throatTemperature = cylinderTemperature * 2 / (gamma + 1);
                    throatSpeedOfSound = cylinderSpeedOfSound * Math.Sqrt(2 / (gamma + 1));
                    throatVelocity = throatSpeedOfSound;
                    throatDensity = throatPressure / 287 / throatTemperature;

                    dischargeCoefficient = valve.FlowCoefficient(
                        crankAngle - 360, cylinderPressure / throatPressure, reverse: false);

                    entranceVelocity = ThroatVelocitySolvers.ExhaustSonic(
                        gamma, dischargeCoefficient, areaRatio, throatVelocity, cylinderSpeedOfSound);

                    var machRatio = entranceVelocity / cylinderSpeedOfSound;

                    p4 = cylinderPressure * dischargeCoefficient * areaRatio
                         * ManifoldNumerics.Power(2 / (gamma + 1), (gamma + 1) / 2 / (gamma - 1))
                         * ((1 - ((gamma - 1) / 2 * machRatio * machRatio)) / machRatio);

                    c4 = Math.Sqrt(p4 * entranceVelocity * throatSpeedOfSound
                                   / (throatPressure * dischargeCoefficient * areaRatio));
                    entranceMach = entranceVelocity / c4;
                }
                else
                {
                    // ---- Subsonic ----
                    throatMach = Math.Sqrt(
                        2 / (gamma - 1)
                        * (ManifoldNumerics.Power(
                            cylinderPressure / throatPressure, (gamma - 1) / gamma) - 1));

                    throatSpeedOfSound = cylinderSpeedOfSound
                        * ManifoldNumerics.Power(
                            throatPressure / cylinderPressure, (gamma - 1) / gamma / 2);

                    throatVelocity = throatMach * throatSpeedOfSound;
                    throatDensity = gamma * throatPressure
                                    / (throatSpeedOfSound * throatSpeedOfSound);

                    dischargeCoefficient = valve.FlowCoefficient(
                        crankAngle - 360, cylinderPressure / throatPressure, reverse: false);

                    entranceVelocity = ThroatVelocitySolvers.ExhaustSubsonic(
                        gamma, dischargeCoefficient, areaRatio, throatVelocity,
                        throatSpeedOfSound, cylinderSpeedOfSound);

                    c4 = Math.Sqrt((cylinderSpeedOfSound * cylinderSpeedOfSound)
                                   - ((gamma - 1) / 2 * entranceVelocity * entranceVelocity));
                    entranceMach = entranceVelocity / c4;

                    var machRatio = entranceVelocity / cylinderSpeedOfSound;

                    p4 = cylinderPressure
                         * ManifoldNumerics.Power(
                             throatSpeedOfSound / cylinderSpeedOfSound, 2 / (gamma - 1))
                         * dischargeCoefficient * areaRatio
                         * (throatVelocity / entranceVelocity)
                         * (1 - ((gamma - 1) / 2 * machRatio * machRatio));
                }

                // ---- Secant on the pipe-end pressure to match the Mach number ----
                var probe = 1;
                double previousProbeP = 0, previousProbeMach = 0;

                while (true)
                {
                    u4 = (p4 - tMinus) / qMinus;
                    r4 = gamma * p4 / (c4 * c4);
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

                        // 0.99999 here, where the other three routines all probe upward
                        // with 1.001. See ISSUES.md B64.
                        p4 = 0.99999 * p4;
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

                if (cylinderPressure / throatPressure < criticalRatio)
                {
                    var implied = 1 / dischargeCoefficient / areaRatio
                                  * (u4 / throatVelocity)
                                  * (throatSpeedOfSound / c4) * (throatSpeedOfSound / c4)
                                  * p4;

                    throatPressure = (0.95 * throatPressure) + (0.05 * implied);
                }
            }

            converged = handedToReverse
                        || (iteration != 0
                            && Math.Abs(u4 - previousU) < VelocityTolerance
                            && Math.Abs(p4 - previousP) < PressureTolerance);

            previousU = u4;
            previousP = p4;
            iteration++;

            if (iteration > MaxIterations)
            {
                converged = true;
            }
        }
        while (!converged);

        target.Velocity[boundary] = u4;
        target.Pressure[boundary] = p4;
        target.Density[boundary] = r4;
        target.SpeedOfSound[boundary] = Math.Sqrt(gamma * p4 / r4);

        return new InletValveReverseBoundary.ThroatState(
            throatMach, throatVelocity, throatSpeedOfSound, throatDensity, throatPressure,
            dischargeCoefficient);
    }
}
