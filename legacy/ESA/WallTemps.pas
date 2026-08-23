//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// Wall Temperatures : Engine Simulation and Analysis (ESA)
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

unit WallTemps;

interface

uses
 Dialogs, SysUtils;

const
 maxinarray = 40;

type
 TWallTemps = class
  ARpm, ATHead, ATPiston, ATULiner, ATLLiner : array of Double;
  ArrayLength : integer;
  function Load (NewFilename : String) : Boolean;
  function THead (N : Double): Double;
  function TPiston (N : Double) : Double;
  function TUliner (N : Double): Double;
  function TLLiner (N : Double) : Double;
 end; //TWallTemps

implementation

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TWallTemps.Load (NewFilename : String) : Boolean;
var
 InStr : String;
 i : integer;
 TF : TextFile;
 LineCount : Integer;
begin
 try
  AssignFile (TF, NewFilename);
  Reset (TF);
  ReadLn (TF, LineCount);
  ReadLn (TF); //Heading Line
  SetLength(Arpm, LineCount);
  SetLength(ATHead, LineCount);
  SetLength(ATPiston, LineCount);
  SetLength(ATULiner, LineCount);
  SetLength(ATLLiner, LineCount);
if LineCount > MaxInArray then
 begin
  ShowMessage ('Max No of Surf Temp values exceeded in file - max is ' + IntToStr(MaxInArray));
  Halt;
 end;
  For i := 1 to LineCount do
   ReadLn (TF, Arpm[i-1], ATHead[i-1], ATPiston [i-1], ATULiner[i-1], ATLLiner[i-1]);
  ArrayLength := LineCount;
  except
   Showmessage ('Error loading Cylinder liner Temperature data : ' + Newfilename);
  end; //Try Except
end; //TWallTemps.Load

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TWallTemps.THead (N : Double): Double;
var
  i : integer;
begin
 if N < Arpm[0] then THead := ATHead[0]
  else if N > Arpm[arrayLength-1] then THead := ATHead[ArrayLength-1]
   else
    begin
     i := 0;
     repeat inc(i); until N < Arpm[i];
     THead := ATHead[i-1] + (N-Arpm[i-1])/(Arpm[i]-Arpm[i-1])*(ATHead[i]-
              ATHead[i-1]);
    end;
end; //TWallTemps.THead

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TWallTemps.TPiston (N : Double): Double;
var
  i : integer;
begin
 if N < Arpm[0] then TPiston := ATPiston[0]
  else if N > Arpm[arrayLength-1] then TPiston := ATPiston[ArrayLength-1]
   else
    begin
     i := 0;
     repeat inc(i); until N < Arpm[i];
     TPiston := ATPiston[i-1] + (N-Arpm[i-1])/(Arpm[i]-Arpm[i-1])*(ATPiston[i]-
                ATPiston[i-1]);
    end;
end; //TWallTemps.TPiston

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TWallTemps.TULiner (N : Double): Double;
var
  i : integer;
begin
 if N < Arpm[0] then TULiner := ATULiner[0]
  else if N > Arpm[arrayLength-1] then TULiner := ATULiner[ArrayLength-1]
   else
    begin
     i := 0;
     repeat inc(i); until N < Arpm[i];
     TULiner := ATULiner[i-1] + (N-Arpm[i-1])/(Arpm[i]-Arpm[i-1])*(ATULiner[i]-
                ATULiner[i-1]);
    end;
end; //TWallTemps.TULiner

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TWallTemps.TLLiner (N : Double): Double;
var
  i : integer;
begin
 if N < Arpm[0] then TLLiner := ATLLiner[0]
  else if N > Arpm[arrayLength-1] then TLLiner := ATLLiner[ArrayLength-1]
   else
    begin
     i := 0;
     repeat inc(i); until N < Arpm[i];
     TLLiner := ATLLiner[i-1] + (N-Arpm[i-1])/(Arpm[i]-Arpm[i-1])*(ATLLiner[i]-
                ATLLiner[i-1]);
    end;
end; //TWallTemps.TLLiner

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
end.
