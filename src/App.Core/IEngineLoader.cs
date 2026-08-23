using App.Core.Model;

namespace App.Core;

/// <summary>
/// Assembles a complete <see cref="Engine"/> from a <c>.eng</c> file and the side files
/// it names.
/// </summary>
/// <remarks>
/// The Delphi original had no single step like this. Cam profiles, discharge tables and
/// manifold areas were loaded at simulation-init time in <c>TEngine2z.InitVars</c>, while
/// wall temperatures, exhaust back pressure and the spark map were loaded by the Edit
/// form when the user pressed OK. Reading an engine file loaded nothing at all. Pulling
/// that scatter into one place is what lets the port report missing data once, up front,
/// instead of failing part-way through a run.
/// </remarks>
public interface IEngineLoader
{
    EngineLoadResult Load(string engineFilePath);
}

/// <summary>The outcome of loading an engine: what was built, and what could not be found.</summary>
public sealed class EngineLoadResult
{
    public EngineLoadResult(Engine engine, EngineDefinition definition, IReadOnlyList<string> problems)
    {
        Engine = engine;
        Definition = definition;
        Problems = problems;
    }

    public Engine Engine { get; }

    /// <summary>The underlying definition, still able to write itself back unchanged.</summary>
    public EngineDefinition Definition { get; }

    /// <summary>
    /// Side files that could not be resolved or parsed, described one per entry. An
    /// engine with problems is still returned: the tables that did load are usable, and
    /// the caller decides whether to show a warning or refuse to run.
    /// </summary>
    public IReadOnlyList<string> Problems { get; }

    public bool IsComplete => Problems.Count == 0;
}
