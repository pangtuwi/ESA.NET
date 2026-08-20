//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// VarSpeedList : Engine Simulation and Analysis (ESA)
// Loads and returns interpolated values from a file of RPM vs a variable
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

unit VarSpeedList;

interface

uses
 Dialogs, SysUtils;

type
 TVarSpeedList = class
  Arpm, AVals : array of Double;
  ArrayLength : integer;
  function Load (NewFilename : String) : Boolean;
  function GetVal (NSearch : Double): Double;
 end; //TVarSpeedList

implementation

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TVarSpeedList.Load (NewFilename : String) : Boolean;
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
  SetLength(AVals, LineCount);
  For i := 1 to LineCount do
   ReadLn (TF, Arpm[i-1], AVals [i-1]);
  ArrayLength := LineCount;
  except
   Showmessage ('Error loading data from file : ' + Newfilename);
  end; //Try Except
end; //TVarSpeedList.Load

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TVarSpeedList.GetVal (NSearch : Double): Double;
var
  i : integer;
  TempV : Double;
begin
 if NSearch < Arpm[0]
  then
   TempV := AVals[0]
  else
   if NSearch >= Arpm[arrayLength-1]
    then TempV := AVals[ArrayLength-1]
    else
     begin
      i := 0;
      repeat inc(i); until Arpm[i] > NSearch;
      TempV := AVals[i-1] + (NSearch-Arpm[i-1])/(Arpm[i]-Arpm[i-1])*(AVals[i]-
               AVals[i-1]);
     end;
   Result := TempV;
end; //TVarSpeedList.GetVal

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
end.
