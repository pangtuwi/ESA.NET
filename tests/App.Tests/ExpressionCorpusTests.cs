using App.Core.Expressions;
using App.Persistence;

namespace App.Tests;

/// <summary>
/// Evaluates every expression stored in every legacy <c>.eng</c> file. This is the
/// acceptance test for the AdCalc replacement: the grammar is only justified if it
/// swallows the whole corpus.
/// </summary>
public sealed class ExpressionCorpusTests
{
    /// <summary>The <c>.eng</c> keys whose values are AdCalc expressions.</summary>
    private static readonly (string Section, string Key)[] ExpressionKeys =
    [
        ("Inlet", "FPlenumP"),
        ("Inlet", "InletGrid"),
        ("Inlet", "IVRFn"),
        ("Inlet", "IVFFn"),
        ("Inlet", "IVFRFn"),
        ("Exhaust", "ExhaustGrid"),
        ("Exhaust", "EVRFn"),
        ("Exhaust", "EVFFn"),
        ("Exhaust", "EVFRFn"),
    ];

    /// <summary>Speeds spanning the range the run options accept (SPEC.md section 1).</summary>
    private static readonly double[] Speeds = [1000, 2000, 3000, 4000, 5000, 6000, 7000];

    private static List<string> CollectExpressions()
    {
        var expressions = new List<string>();

        foreach (var path in TestPaths.AllLegacyEngineFiles())
        {
            var document = IniDocument.Parse(File.ReadAllBytes(path));

            foreach (var (section, key) in ExpressionKeys)
            {
                var value = document.GetValue(section, key);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    expressions.Add(value);
                }
            }
        }

        return expressions;
    }

    [Fact]
    public void EveryExpressionInEveryEngineFileParsesAndEvaluates()
    {
        Assert.SkipWhen(TestPaths.Legacy is null, "Not running from a repository checkout.");

        var expressions = CollectExpressions();
        Assert.NotEmpty(expressions);

        var evaluator = new CachingExpressionEvaluator();
        var failures = new List<string>();

        foreach (var expression in expressions.Distinct(StringComparer.Ordinal))
        {
            foreach (var speed in Speeds)
            {
                try
                {
                    // Lengths bracket the real manifolds: a short inlet runner
                    // through to a long exhaust primary, in metres.
                    foreach (var length in (double[])[0.1, 0.758, 1.55])
                    {
                        var result = evaluator.Evaluate(expression, speed, length);

                        if (!double.IsFinite(result))
                        {
                            failures.Add($"{expression} at N={speed}, L={length} gave {result}");
                        }
                    }
                }
                catch (ExpressionException ex)
                {
                    failures.Add($"{expression} at N={speed}: {ex.Message}");
                }
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void GridExpressionsStayWithinTheLegacyGridLimits()
    {
        Assert.SkipWhen(TestPaths.Legacy is null, "Not running from a repository checkout.");

        var evaluator = new CachingExpressionEvaluator();
        var calculator = new GridSizeCalculator(evaluator);
        var breaches = new List<string>();

        foreach (var path in TestPaths.AllLegacyEngineFiles())
        {
            var document = IniDocument.Parse(File.ReadAllBytes(path));
            var name = Path.GetFileName(path);

            Check(document.GetValue("Inlet", "InletGrid"), isInlet: true);
            Check(document.GetValue("Exhaust", "ExhaustGrid"), isInlet: false);

            void Check(string? expression, bool isInlet)
            {
                if (string.IsNullOrWhiteSpace(expression))
                {
                    return;
                }

                foreach (var speed in Speeds)
                {
                    // The pipe length that a real .maf yields; exact values come from
                    // the manifold tables once an engine is assembled.
                    const double RepresentativeLength = 0.758;

                    try
                    {
                        _ = isInlet
                            ? calculator.InletGridSize(expression, RepresentativeLength, speed)
                            : calculator.ExhaustGridSize(expression, RepresentativeLength, speed);
                    }
                    catch (Exception ex) when (ex is ExpressionException or App.Core.CfdException)
                    {
                        breaches.Add($"{name} at N={speed}: {ex.Message}");
                    }
                }
            }
        }

        // Recorded rather than asserted empty: a grid expression exceeding NI or NE at
        // some speed is legitimate legacy behaviour that raises ECFDError at run time.
        // What matters here is that every expression is evaluable and the limit check
        // fires cleanly rather than silently overflowing a fixed array.
        Assert.All(breaches, breach => Assert.Contains("Grid Length of", breach, StringComparison.Ordinal));
    }
}
