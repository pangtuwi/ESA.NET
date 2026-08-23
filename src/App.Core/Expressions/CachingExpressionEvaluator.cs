using System.Collections.Concurrent;

namespace App.Core.Expressions;

/// <summary>
/// An <see cref="IExpressionEvaluator"/> that parses each distinct expression once and
/// reuses the tree.
/// </summary>
/// <remarks>
/// The Delphi original did the opposite: <c>GetExtendedResult</c> built a fresh parser,
/// evaluated, and destroyed it on every call, and the manifold solver calls these
/// expressions once per timestep. SPEC.md sections 4 and 6 explicitly permit compiling
/// and caching provided the results and error behaviour match, so this does.
/// </remarks>
public sealed class CachingExpressionEvaluator : IExpressionEvaluator
{
    private readonly ConcurrentDictionary<string, ExpressionNode> _cache = new(StringComparer.Ordinal);

    public double Evaluate(string expression, double engineSpeed, double length = 0)
    {
        ArgumentNullException.ThrowIfNull(expression);

        return _cache.GetOrAdd(expression, ExpressionParser.Parse).Evaluate(engineSpeed, length);
    }
}
