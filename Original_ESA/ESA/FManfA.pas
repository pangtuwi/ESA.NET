//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// 1-D Interpolation for Manfold Area: Engine Simulation and Analysis (ESA)
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

unit FManfA;

interface

uses
  Windows, Messages, SysUtils, Classes, Graphics, Controls, Forms, Dialogs,
  StdCtrls, Buttons, Grids, MathDPM, PNTUseful, ComCtrls, TeEngine, Series,
  ExtCtrls, TeeProcs, Chart;

const
 maxx = 50;
 cIManf = 1;
 cEManf = 2;

type
  TFManfArea = class(TForm)
    SGArea: TStringGrid;
    BOk: TBitBtn;
    BSave: TBitBtn;
    BLoad: TBitBtn;
    OpenDialog1: TOpenDialog;
    SaveDialog1: TSaveDialog;
    Chart1: TChart;
    Series3: TFastLineSeries;
    StatusBar1: TStatusBar;
    BGraph: TButton;
    procedure BOkClick(Sender: TObject);
    procedure FormCreate(Sender: TObject);
    procedure BLoadClick(Sender: TObject);
    procedure BSaveClick(Sender: TObject);
    procedure FormDestroy(Sender: TObject);
    procedure FormShow(Sender: TObject);
    procedure BGraphClick(Sender: TObject);
  private
    Procedure LoadGrid (Filename : String);
    Procedure SaveGrid (Filename : String);
  public
    {Public declarations}
  end; //TFManfArea

  TAManf = class
    Cell : Array [1..maxx] of Double;
    Index: Array [1..maxx] of Double;
    xCount : Integer;
    AFileName : String;
    Function GetValue (inx : Double) : Double;
    Function UpdateTable : Boolean;
    Function LoadAndUpdate : Boolean;
  end; //TAMAnf

var
  FManfArea: TFManfArea;
  vCurrA : Integer;
  CurrAFilename : String;
  ACurr, AInMAnf, AExManf : TAManf;
  MaxValue : Double;
  MinValue : Double;

implementation

{$R *.DFM}

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// TAManf Implementation
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function TAManf.GetValue (inx : Double) : Double;
var
 xi : Integer;
begin
 xi := 0;
 repeat
  inc(xi);
 until (Index[xi] >= inx) or (xi = xcount);
 if xi = 1 then GetValue := Cell[1]
  else if xi = xcount then GetValue := Cell[xi]
   else Getvalue := InterpFc(inx, Index[xi-1],Cell[xi-1], Index[xi],Cell[xi]);
 if inx > Index[xi] then
  GetValue := 0;
end; //TAMAnf.GetValue

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function TAManf.LoadAndUpdate: Boolean;
begin
  FManfArea.LoadGrid(AFileName);
  LoadAndUpdate := UpdateTable;
end; //TAManf.LoadAndUpdate

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function TAManf.UpdateTable : Boolean;
var
 i : Integer;
begin
  UpdateTable := FALSE;
  xcount := 0;
  i := 1;
  repeat
   if StripString(FManfArea.SgArea.Cells [0, i], ' ') = '-'
     then xcount := i-1
   else try Index[i] := StrToFloat(FManfArea.SgArea.Cells[0,i]);
    except on EconvertError do
     begin
       ShowMessage('Interpolation table has an error in length reference - please check');
       Exit;
     end;
   end; //Try Except
   inc(i);
  until (i = maxx+1) or (xcount <> 0);

  if index[1] <> 0 then
   begin
    ShowMessage ('Manifold Length does not begin at zero');
    Exit;
   end;

  for i := 1 to Xcount-1 do
   if Index[i+1] <= Index[i] then
    begin
      ShowMessage('Interpolation table length references are not sequential ascending');
      Exit;
    end;

  For i := 1 to xcount do
   begin
    try
     Cell [i] := StrToFloat(FMAnfArea.SgArea.Cells[1, i]);
    except
     on EconvertError do
      begin
       ShowMessage('Interpolation table has an error in Area Column - please check');
       Exit;
      end;
    end; //Try except
   end;
 UpDateTable := TRUE;
end; //TAManf.UpdateTable


//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// TFManfArea Implementation
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFManfArea.BOkClick(Sender: TObject);
begin
 Close;
end; //TFManfArea.BOkClick

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFManfArea.FormCreate(Sender: TObject);
var
 LC: Integer;
begin
  // NumberRows and columns
  SGArea.Cells[0,0] := 'Length';
  SGArea.Cells[1,0] := 'Area';
  For LC := 1 to maxx do
   begin
    SGArea.Cells[0, LC] := '-';
    SGArea.Cells[1, LC] := '-';
   end;
  AInManf := TAManf.Create;
  AExManf := TAManf.Create;
end; //TFManfA.FormCreate

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFManfArea.BLoadClick(Sender: TObject);
var i : integer;
begin
  If OpenDialog1.Execute then
   begin
    LoadGrid (OpenDialog1.FileName);
    CurrAFileName := OpenDialog1.FileName;
   end;
 Series3.Clear;
 With ACurr do
  begin
   UpdateTable;
   Chart1.LeftAxis.Minimum := 0;
   Chart1.LeftAxis.Maximum := 2500;
   MaxValue := Cell[1];
   MinValue := Cell[1];
   For i := 2 to xcount do
    begin
     if Cell[i] >= MaxValue then MaxValue := Cell[i];
     if Cell[i] <= MinValue then MinValue := Cell[i];
    end;
   Chart1.LeftAxis.Minimum := MinValue - 50;
   Chart1.LeftAxis.Maximum := MaxValue + 50;
   For i := 1 to xcount do
    Series3.AddXY (index[i], cell[i], '', clteecolor);
  end; //with ACurr
end; //TFManfArea.BLoadClick

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFManfArea.BSaveClick(Sender: TObject);
begin
 If SaveDialog1.Execute then
   SaveGrid (SaveDialog1.FileName);
end; //TFManfArea.BSaveClick

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Procedure TFManfArea.LoadGrid (Filename : String);
var
 i,j, strpos : integer;
 InStr, inchar, CellStr : String;
 TF : TextFile;
begin
  try
   AssignFile (TF, Filename);
   Reset (TF);
    for i := 1 to maxx do
     begin
      if EOF (TF) then
       begin
        ShowMessage ('Attempted to read past end of file. '+
                     'Truncated File Loaded');
        CloseFile (TF);
        Exit;
       end;
      Readln (TF, instr);
      j := 2;
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
       SGArea.Cells[j-1,i] := CellStr;
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
 Statusbar1.Panels[0].Text := 'Current File : ' + CurrAFilename;
end; //TFManfArea.LoadGrid

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Procedure TFManfArea.SaveGrid (Filename : String);
var
 i,j : integer;
 TF : TextFile;
begin
  AssignFile (TF, Filename);
  Rewrite (TF);
   for i := 1 to SGArea.RowCount-1 do
    begin
     Write (TF, i);
     for j := 0 to SGArea.ColCount-1 do
      Write (TF, ',',SGArea.Cells[j,i]);
     Writeln (TF);
    end; //For i
  CloseFile(TF);
  CurrAFilename := Filename;
end; //TFManfArea.Savegrid

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFManfArea.FormDestroy(Sender: TObject);
begin
 AInManf.Free;
 AExManf.Free;
end; //TFManfArea.FormDestroy

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFManfArea.FormShow(Sender: TObject);
var
 i : integer;
begin
 Case vCurrA of
  cIMAnf : begin
          ACurr := AInManf;
          Caption := 'Length vs Area : Inlet Manifold and Head Port';
          Chart1.Title.Text.Text := 'Inlet Manifold and Head Port Area';
          Chart1.LeftAxis.Title.Caption := 'Inlet Flow Area [mm²]';
          Chart1.BottomAxis.Title.Caption := 'Distance from Plenum [mm]';
         end;
  cEManf : begin
          ACurr := AExManf;
          Caption := 'Length vs Area : Exhaust Manifold and Head Port';
          Chart1.Title.Text.Text := 'Exhaust Manifold and Head Port Area';
          Chart1.LeftAxis.Title.Caption := 'Exhaust Flow Area [mm²]';
          Chart1.BottomAxis.Title.Caption := 'Distance from Exhaust Valve [mm]';
         end;
 end; //case
 if Not fileexists(CurrAFilename) then
  begin
   CurrAFilename := 'Default.maf';
   ShowMessage ('File Does Not Exist : using Default');
  end;
 LoadGrid (CurrAFilename);
 Series3.Clear;
 With ACurr do
  begin
   UpdateTable;
   Chart1.LeftAxis.Minimum := 0;
   Chart1.LeftAxis.Maximum := 2500;
   MaxValue := Cell[1];
   MinValue := Cell[1];
   For i := 2 to xcount do
    begin
     if Cell[i] >= MaxValue then MaxValue := Cell[i];
     if Cell[i] <= MinValue then MinValue := Cell[i];
    end;
   Chart1.LeftAxis.Minimum := MinValue - 50;
   Chart1.LeftAxis.Maximum := MaxValue + 50;
   For i := 1 to xcount do
    Series3.AddXY (index[i], cell[i], '', clteecolor);
  end; //with ACurr
end; //TFManfArea.FormShow

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFManfArea.BGraphClick(Sender: TObject);
var
 i : integer;
begin
 Series3.Clear;
 With ACurr do
  begin
   UpdateTable;
   Chart1.LeftAxis.Minimum := 0;
   Chart1.LeftAxis.Maximum := 2500;
   MaxValue := Cell[1];
   MinValue := Cell[1];
   For i := 2 to xcount do
    begin
     if Cell[i] >= MaxValue then MaxValue := Cell[i];
     if Cell[i] <= MinValue then MinValue := Cell[i];
    end;
   Chart1.LeftAxis.Minimum := MinValue - 50;
   Chart1.LeftAxis.Maximum := MaxValue + 50;
   For i := 1 to xcount do
    Series3.AddXY (index[i], cell[i], '', clteecolor);
  end; //with ACurr
end; //TFManfArea.BGraphClick

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
end.

