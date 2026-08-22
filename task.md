The directory Original_ESA contains a Borland Delphi application I wrote years ago.
Do not write any new code yet.

There is a user manual for the app in that folder and a subfolder ESA containing all the code.

Read every .pas and .dfm file and produce SPEC.md containing:

1. Form inventory — each form, its controls, layout, and what
   each event handler actually does. Quote the Pascal where the
   logic is non-obvious.
2. Data structures — every record/class type, with exact field
   layouts. Flag any `packed record`, ShortString, or file-of-record
   usage, since those define on-disk formats I must stay
   compatible with.
3. Persistence — file formats, INI/registry keys, database access
   (BDE, ADO, direct file I/O). Document the byte layout of any
   binary format.
4. External dependencies — third-party VCL components, DLLs,
   direct Win32 API calls. For each, note whether a .NET
   equivalent exists or if it needs reimplementing.
5. Business rules — any calculation, validation, or state machine
   buried in the UI code. This is the part I care most about
   preserving.
6. Dead code — units or handlers that appear unreachable.

Where the intent of the original code is genuinely ambiguous,
list it under "Questions for Paul" rather than guessing.