//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// MultiRun : Engine Simulation and Analysis (ESA)
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

unit MultiRun;

interface

uses
  Windows, Messages, SysUtils, Classes, Graphics, Controls, Forms, Dialogs,
  StdCtrls, Grids, Buttons, PNTUseful, ComCtrls;

const
 MaxNoRuns = 100;

type
  TFMultiRun = class(TForm)
    SG1: TStringGrid;
    LFilename: TLabel;
    BOk: TBitBtn;
    BCancel: TBitBtn;
    BLoad: TBitBtn;
    BSave: TBitBtn;
    OpenDialog1: TOpenDialog;
    SaveDialog1: TSaveDialog;
    CBShowGraphs: TCheckBox;
    StatusBar1: TStatusBar;
    procedure FormCreate(Sender: TObject);
    procedure BCancelClick(Sender: TObject);
    procedure BOkClick(Sender: TObject);
    procedure BSaveClick(Sender: TObject);
    procedure BLoadClick(Sender: TObject);
    procedure SG1DrawCell(Sender: TObject; Col, Row: Integer; Rect: TRect;
      State: TGridDrawState);
  private
    Procedure SaveGrid (Filename : String);
    Procedure LoadGrid (Filename : String);
  public
    NoRuns : Integer;
    Function GetMultiRunVar(i,j : integer; var Invar : Double):Boolean;
    Function GetMultiRunStr(i,j : integer; var InStr : String):Boolean;

  end; //TFMultiRun

var
  FMultiRun: TFMultiRun;
  OkToMultiRun : Boolean;

implementation

uses Main;

{$R *.DFM}

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFMultiRun.FormCreate(Sender: TObject);
var
 i,j : Integer;
begin
 SG1.Cells[0,0] := 'No';
 SG1.Cells[1,0] := 'Speed';
 SG1.Cells[2,0] := 'Iters';
 SG1.Cells[3,0] := 'IManfFile';
 SG1.Cells[4,0] := 'EManfFile';
 SG1.Cells[5,0] := 'ICamFile';
 SG1.Cells[6,0] := 'ECamFile';
 SG1.Cells[7,0] := 'IVO';
 SG1.Cells[8,0] := 'IVC';
 SG1.Cells[9,0] := 'EVO';
 SG1.Cells[10,0] := 'EVC';
 SG1.Cells[11,0] := 'IValveLift';
 SG1.Cells[12,0] := 'EValveLift';
 SG1.Cells[13,0] := 'Spark °BTDC';
 SG1.Cells[14,0] := 'Burn Angle°';
 for j := 1 to 14 do
  For i := 1 to MaxNoRuns do
   SG1.Cells[j,i] := '-';

 For i := 1 to MaxNoRuns do
  SG1.Cells[0,i] := IntToStr (i);

end; //TFMultiRun.FormCreate

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFMultiRun.BCancelClick(Sender: TObject);
begin
 OkTOMultiRun := FALSE;
 Close;
end; //TFMultiRun.BCancelClick

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFMultiRun.BOkClick(Sender: TObject);
var
 i : integer;
begin
 i := 0;
 repeat  // find noRuns
  inc(i);
 until (StripString(SG1.Cells[1,i], ' ') = '-') or (i=MaxNoRuns+1);
 NoRuns := i-1;
 OkToMultiRun := TRUE;
 Close;
end; //TFMultiRun.BOkClick

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function TFMultiRun.GetMultiRunVar(i,j : integer; var Invar : Double): Boolean;
var
 instr : string;
begin
 GetMultiRunVar := FALSE;
 instr := StripString(SG1.Cells[i,j], ' ');
 if instr = '-' then exit;
 invar := StrToFloat(instr);
 GetMultiRunVar := TRUE;
end; //TFMultiRun.GetMultiRunVar

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function TFMultiRun.GetMultiRunStr(i,j : integer; var InStr : String) : Boolean;
var
 Tempstr : string;
begin
 GetMultiRunStr := FALSE;
 Tempstr := StripString(SG1.Cells[i,j], ' ');
 if Tempstr = '-' then exit;
 inStr := TempStr;
 GetMultiRunStr := TRUE;
end; //TFMultiRun.GetMultiRunStr

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Procedure TFMultiRun.SaveGrid (Filename : String);
var
 i,j : integer;
 TF : TextFile;
begin
  AssignFile (TF, Filename);
  Statusbar1.Panels[1].Text := Filename;
  Rewrite (TF);
   for i := 1 to SG1.RowCount-1 do
    begin
     Write (TF, i);
     for j := 1 to SG1.ColCount-1 do
      Write (TF, ',',SG1.Cells[j,i]);
     Writeln (TF);
    end; //For i
  CloseFile(TF);
end; //TFMultiRun.SaveGrid

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Procedure TFMultiRun.LoadGrid (Filename : String);
var
 i,j, strpos : integer;
 InStr, inchar, CellStr : String;
 TF : TextFile;
begin
  try
   AssignFile (TF, Filename);
   Statusbar1.Panels[1].Text := Filename;
   Reset (TF);
    for i := 1 to SG1.RowCount-1 do
     begin
      if EOF (TF) then
       begin
        ShowMessage ('Attempted to read past end of file. '+
                     'Truncated File Loaded');
        CloseFile (TF);
        Exit;             
       end;
      Readln (TF, instr);
      j := SG1.ColCount-1;
      strpos := length(instr);
      repeat
       CellStr := '';
       repeat
        inchar := copy(instr, strpos, 1);
        if inchar <> ',' then CellStr := inchar + CellStr;
        dec (strpos);
       until (inchar = ',') or (strpos = 0);
       CellStr := StripString (CellStr, ' ');
       if Cellstr = '' then CellStr := '-';
       SG1.Cells[j,i] := CellStr;
       dec(j);
      until (j = 0) or (strpos = 0);
     end; //For i
   CloseFile(TF);
  Except
   on EInOutError do
    begin
     ShowMessage ('Error in File : ' + Filename + #13 +
                  ' File could not be loaded.');
     Exit;
    end;//OnEInOutError
  end; //Try Except
end; //TFMultiRun.LoadGrid

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFMultiRun.BSaveClick(Sender: TObject);
begin
 If SaveDialog1.Execute then
   SaveGrid (SaveDialog1.FileName);
end; //TFMultiRun.BSaveClick

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFMultiRun.BLoadClick(Sender: TObject);
begin
 If OpenDialog1.Execute then
   LoadGrid (OpenDialog1.FileName);
end; //TFMultiRun.BLoadClick

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFMultiRun.SG1DrawCell(Sender: TObject; Col, Row: Integer;
  Rect: TRect; State: TGridDrawState);
begin
    Statusbar1.Panels[0].Text := SG1.Cells[SG1.Col,Sg1.Row];
end; //TFMultiRun.SG1DrawCell

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
end.
