//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// 2-D Interpolation Table : Engine Simulation and Analysis (ESA)
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

unit IPolTab;

interface

uses
  Windows, Messages, SysUtils, Classes, Graphics, Controls, Forms, Dialogs,
  StdCtrls, Buttons, Grids, MathDPM, PNTUseful;

const
 maxxy = 20;
 IVIn = 1;
 IVOut = 2;
 EVIn = 3;
 EVOut = 4;

type
  TFIpol = class(TForm)
    SGIpol: TStringGrid;
    BOk: TBitBtn;
    BSave: TBitBtn;
    BLoad: TBitBtn;
    Edit1: TEdit;
    Edit2: TEdit;
    Edit3: TEdit;
    Button1: TButton;
    OpenDialog1: TOpenDialog;
    SaveDialog1: TSaveDialog;
    BShowGraph: TButton;
    procedure BOkClick(Sender: TObject);
    procedure FormCreate(Sender: TObject);
    procedure Button1Click(Sender: TObject);
    procedure BLoadClick(Sender: TObject);
    procedure BSaveClick(Sender: TObject);
    procedure FormDestroy(Sender: TObject);
    procedure BShowGraphClick(Sender: TObject);
    procedure FormShow(Sender: TObject);
  private
    Procedure LoadGrid (Filename : String);
    Procedure SaveGrid (Filename : String);
  public
    {Public declarations}
  end; //TFIpol

  TCdValve = class
    Cell : Array [1..maxxy, 1..maxxy] of Double;
    xIndex, yIndex : Array [1..maxxy] of Double;
    xCount, yCount : Integer;
    CDFileName : String;
    Function GetValue (inx, iny : Double) : Double;
    Function UpdateTable : Boolean;
    Function LoadAndUpdate : Boolean;
  end; //TCdValve

var
  FIpol: TFIpol;
  Curr : Integer;
  CurrFilename : String;
  CdIVIn, CdIVout, CdEVIn, CdEVOut, CdCurr : TCdValve;

implementation

uses Flowgraph;

{$R *.DFM}

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// TCdCurr Implementation
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function TCdValve.GetValue (inx, iny : Double) : Double;
var
 xi, yi : Integer;
 value1, value2 : Double;
begin
  xi := 0;
  repeat inc(xi) until (xIndex[xi] >= inx) or (xi = xcount);
  yi := 0;
  repeat inc(yi) until (yIndex[yi] >= iny) or (yi = ycount);
  if xi = 1 then xi := 2;
  if yi = 1 then yi := 2;
  value1 := InterpFc(inx, xIndex[xi-1],Cell[xi-1,yi],xIndex[xi],Cell[xi,yi]);
  value2 := InterpFc(inx, xIndex[xi-1],Cell[xi-1,yi-1],xIndex[xi],Cell[xi,yi-1]);
  GetValue := InterpFc(iny, yIndex[yi],Value1,yIndex[yi-1],Value2);
end; //TCdCurr.GetValue

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function TCdValve.LoadAndUpdate: Boolean;
begin
  FIpol.LoadGrid(CdFileName);
  UpdateTable;
end; //TCdValve.LoadAndUpdate

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// TFIpol Implementation
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFIpol.BOkClick(Sender: TObject);
begin
 Close;
end; //TIpol.BOkClick

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFIpol.FormCreate(Sender: TObject);
var
 LC: Integer;
begin
  // NumberRows and columns
  SGIpol.Cells[0,1] := 'Pressure Ratio';
  SGIpol.Cells[1,0] := 'Valve Lift Ratio';
  For LC := 1 to 100 do
   begin
    SGIpol.Cells[0, LC+1] := IntToStr(LC);
    SGIpol.Cells[LC+1, 0] := IntToStr(LC);
   end;
  CdIVIn := TCdValve.Create;
  CdIVOut := TCdValve.Create;
  CdEVIn := TCdValve.Create;
  CdEVOut := TCdValve.Create;
end; //TFIpol.FormCreate

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function TCdValve.UpdateTable : Boolean;
var
 LC, i, j : Integer;
begin
  //UpdateTable := FALSE;
  xcount := 0;
  LC := 2;
  repeat
   if StripString(FIPol.SgIpol.Cells [LC, 1], ' ') = '-'
     then xcount := LC-2
   else try xIndex[LC-1] := StrToFloat(FIpol.SgIpol.Cells[LC, 1]);
    except on EconvertError do
     begin
       ShowMessage('Interpolation table has an error in x reference - please check');
       Exit;
     end;
   end; //Try Except
   inc (LC);
  until (LC = 101) or (xcount <> 0);

  ycount := 0;
  LC := 2;
  repeat
   if StripString(FIpol.SgIpol.Cells [1, LC], ' ') = '-'
     then ycount := LC-2
   else try yIndex[LC-1] := StrToFloat(FIpol.SgIpol.Cells[1, LC]);
    except on EconvertError do
     begin
       ShowMessage('Interpolation table has an error in y reference- please check');
       Exit;
     end;
   end; //Try Except
   inc(LC);
  until (LC = 101) or (ycount <> 0);

  for i := 1 to Xcount-1 do
   if XIndex[i+1] <= XIndex[i] then
    begin
      ShowMessage('Interpolation table x references are not sequential ascending');
      Exit;
    end;
  for i := 1 to ycount-1 do
   if YIndex[i+1] <= YIndex[i] then
    begin
      ShowMessage('Interpolation table y references are not sequential ascending');
      Exit;
    end;

  For i := 1 to xcount do
   for j := 1 to ycount do
    begin
     try
      Cell [i,j] := StrToFloat(FIpol.SgIpol.Cells[i+1, j+1]);
     except
       on EconvertError do
        begin
         ShowMessage('Interpolation table has an error - please check');
         Exit;
        end;
     end; //Try except
    end;
 UpDateTable := TRUE;
end; //TFIpol.UpdateTable

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFIpol.Button1Click(Sender: TObject);
var
 x, y, result : double;
begin
 if not CdCurr.UpdateTable then Exit;
 x := StrToFloat (Edit1.Text);
 y := StrToFloat (Edit2.Text);
 result := CdCurr.GetValue (x,y);
 Edit3.Text := FloatToStrF(result, fffixed, 10, 2);
end; //TFIpol.Button1Click

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFIpol.BLoadClick(Sender: TObject);
begin
  If OpenDialog1.Execute then
   begin
    LoadGrid (OpenDialog1.FileName);
    CurrFileName := OpenDialog1.FileName;
    FIpol.Caption := copy (FIpol.Caption, 1, 27) + ' : ' + CurrFilename;
    CdCurr.UpDateTable;
   end; 
end; //TFIpol.BloadClick

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFIpol.BSaveClick(Sender: TObject);
begin
 If SaveDialog1.Execute then
   SaveGrid (SaveDialog1.FileName);
end; //TFIpol.BSaveClick

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Procedure TFIpol.LoadGrid (Filename : String);
var
 i,j, strpos : integer;
 InStr, inchar, CellStr : String;
 TF : TextFile;
begin
  try
   AssignFile (TF, Filename);
   Reset (TF);
    for i := 1 to SGIpol.RowCount-1 do
     begin
      if EOF (TF) then
       begin
        ShowMessage ('Attempted to read past end of file. '+
                     'Truncated File Loaded');
        CloseFile (TF);
        Exit;
       end;
      Readln (TF, instr);
      j := SGIpol.ColCount-1;
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
       SGIpol.Cells[j,i] := CellStr;
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
  
end; //TFIpol.LoadGrid

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Procedure TFIpol.SaveGrid (Filename : String);
var
 i,j : integer;
 TF : TextFile;
begin
  AssignFile (TF, Filename);
  Rewrite (TF);
   for i := 1 to SGIpol.RowCount-1 do
    begin
     for j := 1 to SGIpol.ColCount-1 do
      Write (TF, ',',SGIpol.Cells[j,i]);
     Writeln (TF);
    end; //For i
  CloseFile(TF);
  CurrFilename := Filename;
end; //TFIpol.Savegrid

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFIpol.FormDestroy(Sender: TObject);
begin
 CdIvIn.Free;
 CdIVOut.Free;
 CdEVIn.Free;
 CdEVOut.Free;
end; //FormDestroy

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFIpol.BShowGraphClick(Sender: TObject);
begin
 CdCurr.UpdateTable;
 FFlowGraph.Showmodal;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFIpol.FormShow(Sender: TObject);
begin
 Case Curr of
  IVIn : begin
          CdCurr := CdIVIn;
          FIpol.Caption := 'Cd: Inlet Valve Inflow';
         end;
  EVIn : begin
          CdCurr := CdEVIn;
          FIpol.Caption := 'Cd: Exhaust Valve Inflow';
         end;
  IVOut : begin
           CdCurr := CdIVOut;
           FIpol.Caption := 'Cd: Inlet Valve Outflow';
          end;
  EVOut : begin
           CdCurr := CdEVOut;
           FIpol.Caption := 'Cd: Exhaust Valve Outflow';
          end;
 end; //case
 if Not fileexists(CurrFilename) then
  begin
   CurrFilename := 'Default.vcd';
   ShowMessage ('File Does Not Exist : using Default');
  end;
 LoadGrid (CurrFilename);
   FIpol.Caption := copy (FIpol.Caption, 1, 27) + ' for file ' + CurrFilename;
   CdCurr.UpDateTable;
end; //TFIpol.FormShow

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
end.

