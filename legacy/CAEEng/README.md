# CAEEng: the VW 1.6 L eight-valve engine of design project 1

These files are the only surviving engine definition for the thesis's **design project 1**
(section 6.5, Figures 6.10 and 6.11): a Volkswagen 1.6 L EA113 four-cylinder with one
inlet and one exhaust valve per cylinder. The thesis says its model inputs were "in the
Test Data directory of the accompanying CD", and that directory has not survived.

They come from `CAEEng/`, a 1998-99 working folder of *Engine*, the predecessor program
that ESA grew out of. The folder was recovered from the author's own backup in October
2026 (Google Drive, `CAEEng/`, 164 files). Everything else in it is that program's Delphi
source and build output, which is not copied here. Treat every file in this folder as
read-only, like the rest of `legacy/`.

| File | Bytes | Dated | What it is |
|---|---|---|---|
| `A4LowCost.eng` | 915 | 1999-06-14 | "A4LowCost 1.6 74kW Pre Development": bore 81, stroke 77.4, CR 10.2, one 39.5 mm inlet and one 31.6 mm exhaust valve, IVO/IVC/EVO/EVC 22/76/64/34, lift 10.6 mm. The older `[InManifold]`/`[ExManifold]` schema, with a fixed `SparkAngle=15.0`, constant grid sizes and constant plenum and back pressures |
| `050109113B.cam` | 2200 | 1998-07-30 | Normalised lift profile of VW camshaft profile 050 109 113 B, used for both valves. `China Torque Kick Up Investigation.xls` names the same profile ("050 B") |
| `06A133205G long.maf` | 426 | 1999-04-16 | Inlet area along the production manifold 06A 133 205 G, long runner, 658 mm |
| `06A113205G Short.maf` | 420 | 1999-04-16 | The same manifold family, short runner, 429 mm. Section 6.5.2's "two available inlet manifolds" |
| `A4LowCostEx.maf` | 430 | 1999-04-16 | Exhaust area, 958 mm |
| `A4LowCostIVIn.vcd`, `A4LowCostIVOut.vcd`, `A4LowCostEVIn.vcd`, `A4LowCostEVOut.vcd` | 1107 each | 1999-04-23 | Discharge coefficients. The two inlet tables are byte-identical in the backup too |
| `Default.exh` | 247 | 1999-06-14 | Exhaust back pressure and temperature against speed. **Malformed**: its count line says 18 rows and 17 follow, so the port's reader rejects it. Nothing needs it, since `A4LowCost.eng` also gives the back pressure inline (`ExhBackP=20`, `ExhT=400`) and the loader takes that. `TableReaderTests` pins it as the one known bad legacy file |
| `SimulDat.txt` | 3352 | 1999-05-18 | Output of a run of *Engine* titled "1999/04/22 China Final", 2000-6000 rpm. Kept as a record; nothing reads it |

Every byte count matches the backup's. Two quirks of the `.eng`: `CdIVIn` names
`A4LowCostEVIn.vcd`, not the `IVIn` table; and `PlenumP=99` and `ExhBackP=20` are the old
schema's constant pressures in kPa.

**What is not here**, and why section 6.5 is only partly reproducible: the final manifold
(the 640 or 680 mm prototype with trumpets, Figure 6.10), the inlet manifold the cam study
used (`VWChina630T.maf`, the 630 mm aluminium VW manifold), the cam study's engine files
(`VWCh101M.eng`, `VWCh101L.eng`, ...) and the lift profiles of camshafts 026 109 101M and
101L (profiles 026 109 113B and 113D). `validation/README.md` records how the suite stands
in for them.
