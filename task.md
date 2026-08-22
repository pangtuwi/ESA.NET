Read SPEC.md in full before doing anything.

Phase 2: build the project skeleton. No business logic, no ported
forms — that's phase 3. I want a solution that compiles, runs,
tests, and proves it can read my legacy data.

Structure:

  src/App.Core        — domain models, business rules, interfaces.
                        No reference to any UI framework. Ever.
  src/App.Persistence — legacy file/INI readers and writers,
                        implementing interfaces defined in Core.
  src/App.Wpf         — WPF app, .NET 10, MVVM. Views and
                        ViewModels only; no logic beyond binding.
  tests/App.Tests     — xUnit, referencing Core and Persistence.
  legacy/             — the original Delphi source, untouched,
                        as reference material.

Conventions:
- Nullable reference types on, warnings as errors.
- CommunityToolkit.Mvvm for observable properties and commands.
- Microsoft.Extensions.DependencyInjection with a generic host;
  ViewModels resolved from the container.
- No static mutable state.

Deliverables:
1. The solution above, building clean with zero warnings.
2. A shell MainWindow that launches to an empty window with the
   menu structure from SPEC.md section 1 — menu items present but
   wired to no-op handlers.
3. Domain model types in Core for every record/class in SPEC.md
   section 2. Data only, no behaviour yet.
4. Persistence: a reader for [name the primary data format] with a
   round-trip test proving it reads legacy/samples/[file] and
   writes a byte-identical copy. This test must pass before you
   consider the phase done.
5. CLAUDE.md at the repo root covering: what the app does, the
   layering rules above, where the Delphi originals live, naming
   conventions, how to run build and tests, and the phase plan
   with phase 2 marked complete.

Then stop and give me a summary of what you built, anything in
SPEC.md that turned out to be wrong or underspecified, and what
you'd tackle first in phase 3. Don't start phase 3.
