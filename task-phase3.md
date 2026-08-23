Read SPEC.md and CLAUDE.md in full before doing anything, and read the
phase 2 code — App.Core's models, App.Persistence's IniDocument, and the
tests. Phase 3 builds on them, it does not replace them.

Phase 3: make the app able to read a complete engine. Phase 2 reads the
.eng file that names the side files; phase 3 reads the side files
themselves, evaluates the expressions embedded in .eng, and gives the
user a form to edit all of it. Still no simulation — that is phase 4.

Scope:

  1. Readers and writers for the six table formats: .cam, .spk, .cwt,
     .exh, .maf, .vcd. Plus ESA.ini for application defaults.
  2. An expression evaluator to replace the proprietary TAdCalc.
  3. The engine Edit form.

Do not start the integrator, the gas model, the equilibrium model or the
manifold solver.

--------------------------------------------------------------------
What I already know about the formats. Verify it, don't trust it.
--------------------------------------------------------------------

The authoritative loaders are in legacy/ESA/. Read them; the notes below
are a starting point, not a specification.

.cam — Profiles.pas, TProfile.LoadText. Delphi `Readln(TF, x, y)`: two
whitespace-separated doubles per line, no header, no row count, read
until EOF. Sample files are fixed-width but the reader does not care.

.spk, .cwt, .exh — VarSpeedList.pas, WallTemps.pas, ExhBackPandT.pas,
all three `Load` methods. Same shape: line 1 is the row count, line 2 is
a heading that is read and discarded, then that many rows of numerics
separated by whitespace or tabs. Columns:
  .spk  RPM, spark angle
  .cwt  RPM, THead, TPiston, TULiner, TLLiner
  .exh  RPM, temperature, pressure
Note the .exh column order: temperature comes before pressure. SPEC.md
section 3 says "RPM, exhaust pressure, and exhaust-temperature" and is
wrong. Confirm against Nissan.exh, whose heading row reads
SPEED / TEMP[C] / P[kPa].
.cwt and .exh both enforce MaxInArray = 40 and `Halt` the application if
a file exceeds it. .spk enforces no limit at all. Decide what the port
should do instead of Halt, and say why.

.maf — parsed by TFManfArea.LoadGrid in FManfA.pas, validated by
TAManf.UpdateTable. Comma-delimited with a leading row-number column, so
a row reads `3,290,1026` meaning row 3, position 290, area 1026. `-`
marks an unused cell. Max 50 rows. Validation rules worth keeping: the
first position must be 0, positions must be strictly ascending, and a
`-` in the first column ends the table.

.vcd — parsed by TFIpol.LoadGrid in IPolTab.pas. Comma-delimited with a
leading empty field on every line, so lines begin with `,`. The first
row is the x-axis header, the first column the y-axis index, the rest
the coefficient grid. Max 20 by 20, bilinearly interpolated. `-` marks
unused.

Two things about .maf and .vcd matter architecturally. Both parsers walk
each line right-to-left, which is why a malformed line degrades the way
it does — reproduce the observable result, not the technique. More
importantly, both live in **form code-behind** in the original and write
straight into a TStringGrid. In this port that parsing belongs in
App.Persistence. The Edit form must not parse anything.

ESA.ini — IniValues.pas. The real file at legacy/ESA/ESA.ini does not
match the example quoted in SPEC.md section 3: ErrorLog is CAEEng.err
not ESA2z1z.err, MassBalance is 0.5 not 1, and the file ends without a
trailing newline. Trust the file. Reuse IniDocument rather than writing
a second INI parser; App.Core.Model.SimulationSettings already exists
for the values.

--------------------------------------------------------------------
The expression evaluator
--------------------------------------------------------------------

.eng files store live expressions in InletGrid, ExhaustGrid, FPlenumP
and the six valve-flow functions. TGridSize evaluates these to decide
the manifold grid-point count, so nothing downstream can be trusted
until they evaluate to the same numbers as the original. This is the one
phase 3 component with no legacy source to port from: AdCalc is
third-party and stays in legacy/ as reference only.

I surveyed every expression across all 65 .eng files. The entire corpus
uses only:

  - numeric literals, including scientific notation such as 1.0293E-19
  - two variables, N (engine speed) and L (pipe length)
  - the operators + - * / ^ and parentheses
  - spaces

No function calls, no comparisons, no logical operators, no strings.
AdCalc itself supports 30-plus functions, comparison and logical
operators and string expressions. **Do not build all of that.** Build
what the data actually needs, put it behind an interface in App.Core so
phase 4 depends on the abstraction, and make anything outside the
supported grammar a clear, testable error rather than a silent wrong
answer.

Two semantics you must pin down rather than assume, because getting them
wrong is silent and poisons phase 4:

  - is ^ left- or right-associative
  - how tightly does unary minus bind, so what does -2^2 evaluate to

Determine both from ADCALC.PAS, and write tests that state the answer.
SPEC.md sections 4 and 6 explicitly permit compiling and caching
expressions, so cache them.

The acceptance test is the corpus itself: evaluate every expression in
every .eng file at a range of N and L and assert none of them throws.

--------------------------------------------------------------------
The Edit form
--------------------------------------------------------------------

Edit.pas and the binary Edit.dfm. Eight tab sheets; captions I recovered
include Cylinders, Heat Trans, Spark Angle, Inlet, Exhaust, Cams,
Valves, Fuel and Model. Re-extract them yourself — Edit.dfm is binary
DFM and needs string extraction, not a text editor.

Details worth carrying over:

  - Capacity is recalculated live as Cyl * Pi/4 * Bore^2 * Stroke / 1000.
  - The cam angle conventions are on the labels and are not obvious:
    IVO is °BTDC, IVC is °ABDC, EVO is °BBDC, EVC is °ATDC.
  - SPEC.md section 6 records that BOKClick catches EConvertError and
    shows the user nothing. Do not reproduce that. Decide what good
    validation looks like and tell me what you chose.

Two open questions I could not answer from the data, which need a
decision before or during this phase:

  - The Fuel tab has C, H, N and O composition fields, and TFuel has
    those integers, but no .eng file contains keys for them. Work out
    where the original got them and how the port should persist them.
  - Five Example1 engines (Nissan1-5.eng) use the older undocumented
    schema: [InManifold] and [ExManifold] sections, and keys PlenumP,
    IVMinA, EVMinA, FireOrder, THead, TPiston, TULiner, TLLiner, ExhT
    and ExhBackP. Those files carry wall temperatures inline instead of
    naming a .cwt file. Phase 2 round-trips them byte for byte but gives
    them no typed surface. Decide how the Edit form and the readers
    should treat them, rather than quietly ignoring them.

Also decide whether Inlet.grd and Exhaust.grd in Example1 are live data
or dead scratch files. They contain raw Pascal fragments mixed with
expressions and are described nowhere in SPEC.md.

--------------------------------------------------------------------
Constraints
--------------------------------------------------------------------

The phase 2 rules still hold and are not negotiable:

  - App.Core references no UI framework. LayeringTests enforces it.
  - Parsing lives in App.Persistence, behind interfaces declared in
    App.Core. Views bind and nothing else.
  - No static mutable state. Services come from the container.
  - Nullable on, warnings as errors, zero warnings in a Release build.
  - Package versions stay centralised in Directory.Packages.props.

--------------------------------------------------------------------
Deliverables
--------------------------------------------------------------------

  1. Readers for all six table formats plus ESA.ini, with round-trip or
     load-and-compare tests over the real files in legacy/ESA/Data.
     Where a format is genuinely lossy, say so and test what you can
     guarantee instead of pretending to byte exactness.
  2. The expression evaluator, with the whole .eng expression corpus as
     its test bed and explicit tests for ^ associativity and unary minus.
  3. The Edit form, loading and saving a real engine end to end, with
     the .eng file still round-tripping byte for byte when nothing was
     changed. EngRoundTripTests must stay green — if editing a value
     starts reformatting the rest of the file, that is a regression.
  4. CLAUDE.md updated: phase 3 marked complete, phase 4 next, and any
     new port caveats added to the caveats section.

Then stop and give me a summary of what you built, anything in SPEC.md
that turned out wrong or underspecified, what you decided on the open
questions above, and what you'd tackle first in phase 4. Don't start
phase 4.
