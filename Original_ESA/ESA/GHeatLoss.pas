//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// Heat Loss Graph : Engine Simulation and Analysis (ESA)
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

unit GHeatLoss;

interface

uses
  Windows, Messages, SysUtils, Classes, Graphics, Controls, Forms, Dialogs,
  TeEngine, Series, ExtCtrls, TeeProcs, Chart, StdCtrls, Buttons, PerfData,
  Menus, CAList2z;

type
  TFEnergyBalance = class(TForm)
    BOk: TBitBtn;
    Chart1: TChart;
    Series1: TFastLineSeries;
    Series2: TFastLineSeries;
    Series3: TFastLineSeries;
    Panel1: TPanel;
    Panel2: TPanel;
    Panel3: TPanel;
    procedure BOkClick(Sender: TObject);
    procedure FormShow(Sender: TObject);
  private
    {Private declarations}
  public
    {Public declarations}
  end; //TFEnergyBalance

var
  FEnergyBalance: TFEnergyBalance;

implementation

{$R *.DFM}

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFEnergyBalance.BOkClick(Sender: TObject);
begin
 Close;
end; //TFEnergyBalance.BOkClick

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFEnergyBalance.FormShow(Sender: TObject);
var
 i : integer;
begin
 Series1.Clear;
 Series2.Clear;
 Series3.Clear;
 Chart1.LeftAxis.Minimum := CurrCAList.caVar[-180].Value[28]-100;
 Chart1.LeftAxis.Maximum := CurrCAList.caVar[-180].Value[22]+50;
 for i := -359 to 360 do
  begin
   with CurrCAList.caVar[i] do
    begin
     Series1.AddXY (i, Value[28], '', clteecolor); //HeatLoss
     Series2.AddXY (i, Value[22], '', clteecolor); //WWork
     Series3.AddXY (i, value[23], '', clteecolor); //PumpWork
    end;//with
  end; //for i
end; //TFEnergyBalance.FormShow

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
end.
