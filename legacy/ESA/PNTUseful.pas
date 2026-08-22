//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// Useful Delphi Routines
// P.N.T. Williams
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

unit PNTUseful;

interface

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function StripString (instr, Stripchr : String) : String;

implementation

Function StripString (instr, Stripchr : String) : String;
var
 TempStr, TempChr : String;
 i : integer;
begin
 tempStr := '';
 for i := 1 to length (instr) do
  begin
   TempChr := Copy(instr, i, 1);
   if TempChr <> Stripchr then TempStr := TempStr + TempChr;
  end;
 StripString := TempStr;
end; //StripString

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
end.
