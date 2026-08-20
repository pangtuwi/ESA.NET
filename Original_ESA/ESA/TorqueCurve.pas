//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// Torque Curve : Engine Simulation and Analysis (ESA)
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

unit TorqueCurve;

interface

uses
  Windows, Messages, SysUtils, Classes, Graphics, Controls, Forms, Dialogs,
  TeEngine, Series, ExtCtrls, TeeProcs, Chart, StdCtrls, Buttons, PerfData,
  Menus;

type
  TFTorqueCurve = class(TForm)
    BOk: TBitBtn;
    Chart1: TChart;
    Series1: TFastLineSeries;
    Series2: TFastLineSeries;
    Series3: TFastLineSeries;
    Button1: TButton;
    procedure BOkClick(Sender: TObject);
    procedure FormShow(Sender: TObject);
    procedure Button1Click(Sender: TObject);
  private
    {Private declarations}
  public
    {Public declarations}
  end; //TFTorqueCurve

var
  FTorqueCurve: TFTorqueCurve;

implementation

uses TCurveOptions;

{$R *.DFM}

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFTorqueCurve.BOkClick(Sender: TObject);
begin
 Close;
end; //TFTorqueCurve.BOkClick

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFTorqueCurve.FormShow(Sender: TObject);
var
 i : integer;
begin
 Series1.Clear;
 Series2.Clear;
 Series3.Clear;
 with PerformanceData do
  begin
   For i := 1 to NoPoints do
    begin
     Series1.AddXY (Point[i].Speed, Point[i].Torque, '', clteecolor);
     Series2.AddXY (Point[i].Speed, Point[i].Power, '', clteecolor);
     Series3.AddXY (Point[i].Speed, Point[i].VolEff, '', clteecolor);
    end;
  end;
end; //TFTorqueCurve.FormShow

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFTorqueCurve.Button1Click(Sender: TObject);
begin
 With FTorqCurveOptions do
  begin
   ETMin.Text := FloatToStrF(Chart1.LeftAxis.Minimum, fffixed, 6,0);
   ETMax.Text := FloatToStrF(Chart1.LeftAxis.Maximum, fffixed, 6,0);
   EPMin.Text := FloatToStrF(Chart1.RightAxis.Minimum, fffixed, 6,0);
   EPMax.Text := FloatToStrF(Chart1.RightAxis.Maximum, fffixed, 6,0);
   ErpmMin.Text := FloatToStrF(Chart1.BottomAxis.Minimum, fffixed, 6,0);
   ErpmMax.Text := FloatToStrF(Chart1.BottomAxis.Maximum, fffixed, 6,0);
   ShowModal;
   Chart1.LeftAxis.Minimum := StrToFloat (ETMin.Text);
   Chart1.LeftAxis.Maximum := StrToFloat (ETMax.Text);
   Chart1.RightAxis.Minimum := StrToFloat (EPMin.Text);
   Chart1.RightAxis.Maximum := StrToFloat (EPMax.Text);
   Chart1.BottomAxis.Minimum := StrToFloat (ErpmMin.Text);
   Chart1.BottomAxis.Maximum := StrToFloat (ErpmMax.Text);
  end; //TFTorqueCurve.Button1Click
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
end.
