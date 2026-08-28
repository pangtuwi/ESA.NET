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

    /// <summary>
    /// Re-derives the engine from a definition already in hand, re-resolving the side
    /// files against <paramref name="engineFilePath"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="EngineLoadResult.Engine"/> and <see cref="EngineLoadResult.Definition"/>
    /// are two snapshots taken at load time, and editing the definition does not touch the
    /// engine the simulation reads. The original had no such split: <c>Edit.pas</c>'s OK
    /// handler converts and assigns straight onto <c>Engine2z</c>. This is how the port
    /// gets back to the same place - rebuild after an edit, rather than leave the
    /// simulation running on values the operator has already changed. See ISSUES.md C2.
    /// </para>
    /// <para>
    /// The definition instance is passed straight back out, so a reference held elsewhere
    /// - the edit window keeps one while it is open - stays live across a rebuild.
    /// </para>
    /// </remarks>
    EngineLoadResult Rebuild(EngineDefinition definition, string engineFilePath);
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
