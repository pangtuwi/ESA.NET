namespace App.Core.Manifold;

/// <summary>
/// The shared numerical helpers of the one-dimensional manifold solver. Port of the
/// free functions at the top of Manifolds.pas (lines 86-176).
/// </summary>
public static class ManifoldNumerics
{
    /// <summary>
    /// Port of <c>Manifolds.pas</c>'s own <c>Power</c> (line 86), which is a third
    /// distinct power routine, agreeing with neither <c>DelphiMath.Power</c> nor
    /// <c>DelphiMath.Pwr</c>.
    /// </summary>
    /// <remarks>
    /// It refuses a base at or below zero and a base at or above 1e20, raising rather
    /// than returning a sentinel, and otherwise always goes through
    /// <c>exp(b*ln(a))</c>. The original's third branch, <c>if (a = 0) and (b &gt; 0)</c>,
    /// is unreachable: <c>a = 0</c> has already raised two lines above. See ISSUES.md B47.
    /// </remarks>
    public static double Power(double a, double b)
    {
        if (a <= 0)
        {
            throw new CfdException($"ERROR : a <= 0 in \"Power\" !!! (a = {a})");
        }

        if (a >= 1e20)
        {
            throw new CfdException($"ERROR : a >= 1e20 in \"Power\" !!! (a = {a})");
        }

        return b == 0 ? 1 : Math.Exp(b * Math.Log(a));
    }

    /// <summary>
    /// Speed of sound from pressure and density. Port of <c>cThermo</c>.
    /// </summary>
    /// <remarks>
    /// The guard is incomplete in a way worth knowing about. When
    /// <c>gam*pres/dens</c> is not positive the original tests density and pressure for
    /// being <b>negative</b> and raises on either - but a pressure of exactly zero with a
    /// positive density satisfies neither test, and the function then falls out of its
    /// <c>else</c> branch without assigning a result at all. Delphi returns whatever was
    /// in the result register. The port returns zero for that case, which is the value
    /// the expression would have had. See ISSUES.md B48.
    /// </remarks>
    public static double SpeedOfSound(double gamma, double pressure, double density)
    {
        if (!double.IsFinite(gamma) || !double.IsFinite(pressure) || !double.IsFinite(density))
        {
            throw new CfdException("ERROR : Non-finite state in \"cThermo\" !!!");
        }

        if (gamma * pressure / density <= 0)
        {
            if (density < 0)
            {
                throw new CfdException("ERROR : Density negative in \"cThermo\" !!!");
            }

            if (pressure < 0)
            {
                throw new CfdException("ERROR : Presssure negative in \"cThermo\" !!!");
            }

            return 0;
        }

        return Math.Sqrt(gamma * pressure / density);
    }

    /// <summary>
    /// Dynamic viscosity of air as a function of temperature, in Pa s. Port of
    /// <c>Viscosity</c>: a rational fit in inverse powers of temperature.
    /// </summary>
    /// <remarks>
    /// The original's guard against a negative result raises with the message
    /// <c>'ERROR : a &gt;= 1e20 in "Power" !!!'</c>, copied from the routine above and
    /// never corrected. Reproduced, because an operator who hits it and searches the
    /// source for that string should find both places.
    /// </remarks>
    public static double Viscosity(double temperature)
    {
        var t = temperature;

        var viscosity = Math.Sqrt(t)
                        / (0.552795
                           + (2.810892e2 / t)
                           - (13.508340e4 / Power(t, 2))
                           + (39.353086e6 / Power(t, 3))
                           - (41.419387e8 / Power(t, 4)))
                        * 1e-6;

        if (viscosity < 0)
        {
            throw new CfdException("ERROR : a >= 1e20 in \"Power\" !!!");
        }

        return viscosity;
    }

    /// <summary>
    /// Fanning friction factor. Port of <c>FricFact</c>.
    /// </summary>
    /// <remarks>
    /// The transitional band, 2300 to 4000, and the first turbulent band, 4000 to 1e5,
    /// evaluate the same expression, so the boundary between them does nothing. See
    /// ISSUES.md B49.
    /// </remarks>
    public static double FanningFriction(
        double gamma, double density, double velocity, double diameter, double speedOfSound)
    {
        var temperature = speedOfSound * speedOfSound / gamma / 287;
        var reynolds = density * Math.Abs(velocity) * diameter / Viscosity(temperature);

        if (reynolds == 0)
        {
            return 0;
        }

        if (reynolds < 2300)
        {
            return 16 / reynolds;
        }

        if (reynolds < 1e5)
        {
            return 0.0791 / Power(reynolds, 0.25);
        }

        return 0.04 / Power(reynolds, 0.16);
    }

    /// <summary>
    /// Critical pressure ratio for sonic flow, found by false position. Port of
    /// <c>CritPress</c>, which brackets on [0.5, 1] and iterates to 1e-7.
    /// </summary>
    public static double CriticalPressure(double gamma, double dischargeCoefficient, double areaRatio)
    {
        const int MaxIterations = 100000;

        double F(double pressureRatio) =>
            (dischargeCoefficient * areaRatio * dischargeCoefficient * areaRatio
             * (gamma - 1) / (gamma + 1) * Power(pressureRatio, 2 / gamma))
            + (2 / (gamma + 1) * Power(pressureRatio, (1 - gamma) / gamma))
            - 1;

        var low = 0.5;
        var high = 1.0;
        var iterations = 0;
        double guess;
        double atGuess;

        do
        {
            var atLow = F(low);
            var atHigh = F(high);

            if (atLow * atHigh > 0)
            {
                throw new CfdException("ERROR : fx1*fx2 > 0 in Critical Pressure !!!");
            }

            guess = high - (atHigh * (high - low) / (atHigh - atLow));
            atGuess = F(guess);

            if ((atGuess > 0 && atLow > 0) || (atGuess < 0 && atLow < 0))
            {
                low = guess;
            }
            else
            {
                high = guess;
            }

            iterations++;
        }
        while (Math.Abs(atGuess) >= 0.0000001 && iterations <= MaxIterations);

        if (iterations > MaxIterations)
        {
            throw new CfdException("ERROR : No convergence in Critical Pressure !!!");
        }

        return guess;
    }
}
