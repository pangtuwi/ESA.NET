//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// ErrorLog : Engine Simulation and Analysis
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

unit ErrorLog;

interface
uses Classes, SysUtils;

Type
 TErrorLog = Class
  EList : TStringList;
  DisplayedCount : Integer;
  FileName : String;
  TF : TextFile;
  Constructor Init (FileN : String;  LogType : String);
  Destructor Destroy; Override;
  Procedure Log (InStr : String);
  Function GetLoggedString : String;
 end;  // TErrorLog

var ELog : TErrorLog;

implementation

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Procedure TErrorLog.Log (InStr : String);
begin
  EList.Add (DateTimeToStr (Now) + '   ' + InStr);
  Try
  WriteLn (TF, EList.Strings[EList.Count -1]);
  Except on EInOutError do DisplayedCount := -1;
  end; //TErrorLog.Log
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function TErrorLog.GetLoggedString : String;
begin
 if (EList.Count < 1) or (EList.Count = DisplayedCount) then GetLoggedString := ''
  else
   begin
    Inc (DisplayedCount);
    GetLoggedString := EList.Strings[DisplayedCount-1];
   end;
end; //TErrorLog.GetLoggedString

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Constructor TErrorLog.Init (FileN : String;  LogType : String);
begin
  FileName := FileN;
  Try
    Assign (TF, FileName);
    If FileExists (FileName) then
     begin
      Append (TF);
      WriteLn (TF);
      WriteLn (TF, '-----------------------------------------');
      WriteLn (TF);
     end
    else rewrite (TF);
    WriteLn (TF, 'OPening Log File : ', LogType, ' :    ', DateTimeToStr (Now));
    WriteLn (TF);
    LogType := 'Ok';
  Except
   on EInOutError do
    begin
     LogType := 'File Error';
     DisplayedCount := -1;
    end;
  end; // Try Except
  DisplayedCount := 0;
  EList := TStringList.Create;
end; //TErrorLog.Init

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Destructor TErrorLog.Destroy;
begin
  if Elist <> nil then EList.Free;
  Close (TF);
  inherited;
end; //TErrorLog.Destroy

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
end.
