# Questions for Paul

1. Is the burnt-volume clamp in Gasses2Z.pas an intended numerical safeguard, or should exceeding cylinder volume be reported as an error?
- it is an intended numerical safeguard

2. What operating conditions and expected cycle count normally satisfy the 1 microgram mass-balance tolerance? Is three cycles always a valid minimum?
- yes, 3 cycles is a valid minimum

3. Is the simplified, frozen-equilibrium overlap model intentional, or should the commented gas-exchange equations be restored in a compatible implementation?
- use the simplified frozen-equilibrium overlap model

4. Is the combustion fuel-mass formula's AFRatio + 1 intentional?
- yes.   AF ratio is stated as X:1.   Total mass is therefore  X+1

5. Are the state-specific Woshini constants empirically validated calibration values?
- yes

6. Does AdCalc parse/compile expression strings once or on every evaluation? This affects a .NET replacement's performance design.
- unsure.  PLease review code in more detail

7. Should equilibrium emissions freeze below a particular temperature, and if so what threshold is authoritative?
- In a standard spark-ignition (SI) 4-stroke engine, \(NO_{x}\) typically freezes around 1800 K to 2000 K, while Carbon Monoxide (CO) freezes at a lower window of roughly 1500 K to 1700 K

8. Which engine values are intended to accumulate across cycles and which should reset each cycle?
- general intent is that engine values at the end of a cycle are used as the starting conditions for the next cycle.

9. Are old TProfile linked lists freed when a new engine definition is loaded?
- yes they should be,.

10. How do user-configured inlet/exhaust grid sizes relate to the fixed capacities 68 and 38?
- these grid sizes were put in place as suitable for most standard industry engine applications.  Ideally these would be varibale rather than fixed.  Used these same fixed values for the new software

11. Can multi-run count exceed MaxNoPoints = 100, and what user-visible behavior is expected?
- theoretically yes.  The user visible behaviour was not built into the original software so would have failed without warning.

12. Does SAVEMANFDATA produce a file? If so, what filename, encoding, and record/column layout must remain compatible?
- propoese something aligned with other outputs

13.  Are the sample .maf, .vcd, .cam, .spk, .cwt, and .exh files the complete authoritative format examples, or are there additional legacy variants?
- yes, use those samples as authoritative

14.  Should .eng and exported text files remain ANSI-compatible with the original Delphi TIniFile and file readers, or may the .NET implementation standardize on UTF-8?
- Ok to standardise on UTF-8