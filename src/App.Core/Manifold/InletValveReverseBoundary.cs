using App.Core.Model;
using App.Core.Simulation;

namespace App.Core.Manifold;

/// <summary>
/// Gas driven back out of the cylinder through the inlet valve. Port of
/// <c>INLET_VALVE_REVERSE</c> (Manifolds.pas:441-676).
/// </summary>
/// <remarks>
/// <para>
/// The valve is treated as a converging nozzle discharging from cylinder stagnation
/// conditions. Three regimes are possible and the routine picks between them on the
/// pressure ratio: no flow at all when the cylinder is at or below the throat pressure,
/// choked flow above the critical ratio, and subsonic flow between. Each fixes the throat
/// state, and an inner secant iteration then moves the pipe-end pressure until the Mach
/// number there matches the one the nozzle delivers.
/// </para>
/// <para>
/// <b>This routine writes into the current-time-level arrays, not the new ones.</b> Every
/// other boundary routine writes to the <c>...New</c> arrays;
/// <c>INLET_VALVE_REVERSE</c> overwrites <c>uInlet[Q]</c>, <c>PInlet[Q]</c> and
/// <c>RInlet[Q]</c> in place. It is called from inside <c>INLET_VALVE_OPEN</c>, which
/// then continues from the values it left. See ISSUES.md B58.
/// </para>
/// </remarks>
public static class InletValveReverseBoundary
{
    private const double FootTolerance = 0.001 * 0.1;
    private const double VelocityTolerance = 1 * 0.0001;
    private const double PressureTolerance = 1 * 0.001;
    private const int MaxIterations = 1000;

    /// <summary>The throat state this boundary leaves behind, which <c>MassFlow</c> reads.</summary>
    /// <param name="MachNumber">Delphi <c>Mt</c>.</param>
    /// <param name="Velocity">Delphi <c>ut</c>, negative for reverse flow.</param>
    /// <param name="SpeedOfSound">Delphi <c>ct</c>.</param>
    /// <param name="Density">Delphi <c>Rt</c>.</param>
    /// <param name="Pressure">Delphi <c>Pt</c>, carried between steps.</param>
    /// <param name="DischargeCoefficient">Delphi <c>Cd</c>.</param>
    public readonly record struct ThroatState(
        double MachNumber,
        double Velocity,
        double SpeedOfSound,
        double Density,
        double Pressure,
        double DischargeCoefficient);

    /// <summary>
    /// Solves the inlet pipe's valve end for reverse flow, updating <paramref name="grid"/>
    /// in place at its last point.
    /// </summary>
    /// <param name="throat">
    /// The throat state carried in from the previous step; its <c>Pressure</c> is the
    /// starting guess and is relaxed rather than recomputed outright.
    /// </param>
    /// <param name="crankAngle">
    /// Crank angle in <c>Main_Prog</c>'s convention, which runs 1 to 720 rather than -359
    /// to 360: it is passed as <c>x*180/pi + 360</c>. This routine subtracts the 360 again
    /// before looking up valve lift, so passing a trace-convention angle here silently
    /// reads the lift 360 degrees away - which for an inlet valve means shut, a discharge
    /// coefficient of zero, and a division by zero inside the velocity solver.
    /// </param>
    /// <param name="reverseTuning">Delphi <c>IVR</c>, from the <c>.eng</c> file's IVRFn.</param>
    public static ThroatState Apply(
        PipeGrid grid,
        PipeGeometry pipe,
        ValveMotion valve,
        double dt,
        double cylinderPressure,
        double cylinderTemperature,
        double crankAngle,
        double pipeAreaAtValve,
        double valveFlowArea,
        ThroatState throat,
        double reverseTuning)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(pipe);
        ArgumentNullException.ThrowIfNull(valve);

        const double gamma = CharacteristicSolver.InletGamma;

        var criticalRatio = ManifoldNumerics.Power((gamma + 1) / 2, gamma / (gamma - 1));
        var areaRatio = Math.Min(valveFlowArea / pipeAreaAtValve, 1);

        var q = grid.ActiveCount - 1;
        var interior = q - 1;
        var x4 = grid.X[q];

        var line = GridInterpolants.Through(grid, from: interior, to: q);

        var footX = grid.X[interior];
        var footU = grid.Velocity[interior];
        var footP = grid.Pressure[interior];
        var footR = grid.Density[interior];

        var throatPressure = throat.Pressure;
        var throatMach = throat.MachNumber;
        var throatVelocity = throat.Velocity;
        var throatSpeedOfSound = throat.SpeedOfSound;
        var throatDensity = throat.Density;
        var dischargeCoefficient = throat.DischargeCoefficient;

        double u4 = 0, p4 = 0, r4 = 0, c4 = 0;
        double previousU = 0, previousP = 0;
        double qPlus = 0, tPlus = 0;

        var iteration = 0;
        bool converged;

        do
        {
            // ---- The C+ characteristic arriving from inside the pipe ----
            for (var guard = 0; guard <= 100; guard++)
            {
                if (iteration == 0)
                {
                    (u4, p4, r4) = (footU, footP, footR);
                }

                var meanVelocity = (footU + u4) / 2;
                var meanPressure = (footP + p4) / 2;
                var meanDensity = (footR + r4) / 2;
                var c = ManifoldNumerics.SpeedOfSound(gamma, meanPressure, meanDensity);

                var x = x4 - (dt / (1 / (meanVelocity + c)));

                if (x > pipe.Length)
                {
                    x = pipe.Length;
                }

                if (Math.Abs(x - footX) < FootTolerance)
                {
                    var diameter = Math.Sqrt(4 * pipe.Area(x) / Math.PI);
                    var friction = ManifoldNumerics.FanningFriction(gamma, footR, footU, diameter, c);

                    qPlus = meanDensity * c;

                    var source = (-footR * footU * c * c / pipe.Area(x) * pipe.AreaGradient(x))
                                 + (((gamma - 1) * footU - c)
                                    * (footR * footU * Math.Abs(footU) * 2 * friction / diameter));

                    tPlus = footP + (qPlus * footU) + (source * dt);
                    break;
                }

                footX = x;
                footU = line.VelocityAt(x);
                footP = line.PressureAt(x);
                footR = line.DensityAt(x);
            }

            // The reverse tuning constant only ever appears here, nudging the throat
            // pressure down when the cylinder has fallen below it and the pipe end is
            // still running backwards faster than the constant allows.
            if (iteration > 0 && cylinderPressure <= throatPressure && u4 < reverseTuning)
            {
                throatPressure = 0.999999 * cylinderPressure;
            }

            if (iteration == 0)
            {
                p4 = grid.Pressure[q];
                u4 = grid.Velocity[q];
                r4 = grid.Density[q];
                c4 = Math.Sqrt(gamma * p4 / r4);

                if (cylinderPressure <= throatPressure)
                {
                    throatPressure = 0.999999 * cylinderPressure;
                }
            }

            var cylinderSpeedOfSound = Math.Sqrt(gamma * 287 * cylinderTemperature);

            if (cylinderPressure <= throatPressure)
            {
                // ---- Stalled: the cylinder cannot push anything out ----
                throatPressure = (0.5 * throatPressure) + (0.5 * cylinderPressure);
                throatMach = 0;
                throatSpeedOfSound = cylinderSpeedOfSound;
                throatVelocity = 0;
                throatDensity = gamma * throatPressure / (throatSpeedOfSound * throatSpeedOfSound);

                u4 = 0;
                p4 = (0.5 * throatPressure) + (0.5 * p4);
                r4 = gamma * p4 / (c4 * c4);

                converged = true;
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
                        crankAngle - 360, cylinderPressure / throatPressure, reverse: true);

                    entranceVelocity = ThroatVelocitySolvers.InletSonic(
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
                        crankAngle - 360, cylinderPressure / throatPressure, reverse: true);

                    entranceVelocity = ThroatVelocitySolvers.InletSubsonic(
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

                // ---- Secant iteration on the pipe-end pressure to match the Mach number ----
                // Uncapped in the original, like the characteristic foot loops: see B51.
                var probe = 1;
                double previousProbeP = 0, previousProbeMach = 0;

                for (var guard = 0; guard <= 100; guard++)
                {
                    u4 = (p4 - tPlus) / qPlus;
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

                // Reverse flow runs the other way along the pipe.
                u4 = -u4;

                if (cylinderPressure / throatPressure < criticalRatio)
                {
                    // Relaxed rather than replaced: five per cent of the newly implied
                    // throat pressure per pass. The choked branch leaves it alone, through
                    // a branch the original writes out as "Pt := Pt".
                    var implied = 1 / dischargeCoefficient / areaRatio
                                  * (u4 / -throatVelocity)
                                  * (throatSpeedOfSound / c4) * (throatSpeedOfSound / c4)
                                  * p4;

                    throatPressure = (0.95 * throatPressure) + (0.05 * implied);
                }

                converged = iteration != 0
                            && Math.Abs(u4 - previousU) < VelocityTolerance
                            && Math.Abs(p4 - previousP) < PressureTolerance;
            }

            previousU = u4;
            previousP = p4;
            iteration++;

            if (iteration > MaxIterations)
            {
                converged = true;
            }
        }
        while (!converged);

        throatVelocity = -throatVelocity;

        // In place, on the current arrays. See B58.
        grid.Velocity[q] = u4;
        grid.Pressure[q] = p4;
        grid.Density[q] = r4;
        grid.SpeedOfSound[q] = Math.Sqrt(gamma * p4 / r4);

        return new ThroatState(
            throatMach, throatVelocity, throatSpeedOfSound, throatDensity, throatPressure,
            dischargeCoefficient);
    }
}
