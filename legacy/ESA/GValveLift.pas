//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// Valve Lift Graph : Engine Simulation and Analysis (ESA)
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

unit GValveLift;

interface

uses
  Windows, Messages, SysUtils, Classes, Graphics, Controls, Forms, Dialogs,
  TeEngine, Series, ExtCtrls, TeeProcs, Chart, StdCtrls, Buttons, PerfData,
  Menus, ICEngine2z;

type
  TFValveLift = class(TForm)
    BOk: TBitBtn;
    Chart1: TChart;
    Series1: TFastLineSeries;
    Series2: TFastLineSeries;
    Panel1: TPanel;
    Panel2: TPanel;
    Panel3: TPanel;
    procedure BOkClick(Sender: TObject);
    procedure FormShow(Sender: TObject);
  private
    {Private declarations}
  public
    {Public declarations}
  end; //TFValveLift

var
  FValveLift: TFValveLift;

implementation

{$R *.DFM}

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFValveLift.BOkClick(Sender: TObject);
begin
 Close;
end; //TFValveLift.BOkClick

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFValveLift.FormShow(Sender: TObject);
var
 i,ca : Double;
begin
 Series1.Clear;
 Series2.Clear;
 with Engine2z.Manifold do
  begin
   i := -360;
   repeat
    i := i + 1;
    ca := i + 360;
    if ca > 360 then ca := ca - 720;
    if IV.MaxLift > EV.MaxLift then
     Chart1.LeftAxis.Maximum := IV.MaxLift*1e3 + 0.5
    else
     Chart1.LeftAxis.Maximum := EV.MaxLift*1e3 + 0.5;
    Series1.AddXY (i, IV.Lift(ca)*1e3, '', clteecolor);
    Series2.AddXY (i, EV.Lift(ca)*1e3, '', clteecolor);
   until i > 359;
  end;
end; //TFValveLift.FormShow

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
end.
