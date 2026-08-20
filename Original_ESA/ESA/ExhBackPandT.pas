//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// Exhaust Back Pressure and Temperature : Engine Simulation and Analysis (ESA)
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

unit ExhBackPandT;

interface

uses
 Dialogs, SysUtils;

const
 maxinarray = 40;

type
 TExhaustPandT = class
  Arpm, APExh, ATExh : array of Double;
  ArrayLength : integer;
  PAtm : Double;
  function Load (NewFilename : String) : Boolean;
  function Pres (N : Double): Double;
  function Temp (N : Double) : Double;
 end; // TExhaustPandT

implementation

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TExhaustPandT.Load (NewFilename : String) : Boolean;
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
  SetLength(ATExh, LineCount);
  SetLength(APExh, LineCount);
if LineCount > MaxInArray then
 begin
  ShowMessage ('Max No of Exhaust P and T values exceeded in file - max is ' + IntToStr(MaxInArray));
  Halt;
 end;
  For i := 1 to LineCount do
   ReadLn (TF, Arpm[i-1], ATExh [i-1], APExh[i-1]);
  ArrayLength := LineCount;
  except
   Showmessage ('Error loading Exhaust P and T data : ' + Newfilename);
  end; //Try Except
end; //TExhaustPandT.Load

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TExhaustPandT.Pres (N : Double): Double;
var
  i : integer;
  TempP : Double;
begin
 if N < Arpm[0] then TempP := APExh[0]
  else if N >= Arpm[arrayLength-1] then TempP := APExh[ArrayLength-1]
   else
    begin
     i := 0;
     repeat inc(i); until Arpm[i] > N;
     TempP := APExh[i-1] + (N-Arpm[i-1])/(Arpm[i]-Arpm[i-1])*(APExh[i]-APExh[i-1]);
    end;
  Pres := TempP*1000 + PAtm;
end; //TExhaustPandT.Pres

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TExhaustPandT.Temp (N : Double): Double;
var
  i : integer;
  TempT : Double;
begin
 if N < Arpm[0] then TempT := ATExh[0]
  else if N >= Arpm[arrayLength-1] then TempT := ATExh[ArrayLength-1]
   else
    begin
     i := 0;
     repeat inc(i); until Arpm[i]>N;
     TempT := ATExh[i-1] + (N-Arpm[i-1])/(Arpm[i]-Arpm[i-1])*(ATExh[i]-ATExh[i-1]);
    end;
  Result := TempT;  
end; //TExhaustPandT.Temp

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
end.
