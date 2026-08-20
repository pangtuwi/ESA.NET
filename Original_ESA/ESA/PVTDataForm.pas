//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// PVT DataForm : Engine Simulation and Analysis (ESA)
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

unit PVTDataForm;

interface

uses
  Windows, Messages, SysUtils, Classes, Graphics, Controls, Forms, Dialogs,
  StdCtrls, Buttons, Grids, Menus;

type
  TFPVTData = class(TForm)
    BSaveAs: TBitBtn;
    BOK: TBitBtn;
    SaveDialog1: TSaveDialog;
    SGCAData: TStringGrid;
    procedure FormCreate(Sender: TObject);
    procedure UpdateList (CA : Double);
    procedure BSaveAsClick(Sender: TObject);
    procedure FormShow(Sender: TObject);
  private
    {Private declarations}
  public
    {Public declarations}
  end; //TFPVTData

var
  FPVTData: TFPVTData;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

implementation

uses CAList2z;
{$R *.DFM}

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFPVTData.UpdateList (CA : Double);
var
  PointNo, RowNo, i : Integer;
  CurrP :TCAPoint;
begin
 PointNo := Round (CA);
 CurrP := CurrCAList.CaVar[PointNo];
 RowNo := round(CA) + 360;
 Try
  with SGCAData do
   for i := 1 to Novals do
    Cells [i, RowNo] := FloatToStrF (CurrP.Value[i]*CurrCAList.k[i], ffFixed, 10, CurrCAList.decimals[i]);
 Except
  on EConvertError do
   begin
   end;
 end; // Try Except
end; //TFPVTData.UpdateList

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFPVTData.FormCreate(Sender: TObject);
var LC: Integer; // Loop Counter
begin

end; //TFPVTData.FormCreate

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFPVTData.BSaveAsClick(Sender: TObject);
begin
  if NOT SaveDialog1.Execute then Exit;
  CurrCaList.SendToFile (SaveDialog1.filename);
end; //TFPVTData.BSaveAsClick

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFPVTData.FormShow(Sender: TObject);
var
 LC, i : integer;
begin
  SGCAData.ColCount := NoVals+1;
  With SGCAData do
   begin
    for LC := -359 to 360 do
     Cells [0,LC+360] := IntToStr (LC);
    Cells [0,0] := 'CA';
    ColWidths [0] := 40;

    For i := 1 to NoVals do
      Cells[i,0] := CurrCaList.ColName[i];
   end; // with SGCAData
end; //TFPVTData.FormShow

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
end.
