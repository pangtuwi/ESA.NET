//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// Valve Flow Coefficient Graph : Engine Simulation and Analysis (ESA)
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
unit Flowgraph;

interface

uses
  Windows, Messages, SysUtils, Classes, Graphics, Controls, Forms, Dialogs,
  StdCtrls, Buttons, TeEngine, TeeSurfa, ExtCtrls, TeeProcs, Chart, IpolTab;

type
  TFFlowGraph = class(TForm)
    Chart1: TChart;
    Series1: TSurfaceSeries;
    BitBtn1: TBitBtn;
    Label1: TLabel;
    Label2: TLabel;
    Label3: TLabel;
    Label4: TLabel;
    Label5: TLabel;
    Label6: TLabel;
    procedure FormShow(Sender: TObject);
  private
    {Private declarations}
  public
    {Public declarations}
  end; //TFFlowGraph

var
  FFlowGraph: TFFlowGraph;

implementation

{$R *.DFM}

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFFlowGraph.FormShow (Sender: TObject);
var
 i, j : integer;
 ii,jj : integer;
begin
 Series1.Clear;
 Series1.NumxValues := CdCurr.xcount;
 Series1.Numzvalues := CdCurr.ycount;
 Chart1.LeftAxis.Maximum := 1;
 Chart1.BottomAxis.SetMinMax (1,CdCurr.xcount);
 Chart1.DepthAxis.SetMinMax (0,CdCurr.ycount);
 if (Curr = IVIn) or (Curr = EVIn) then
  Chart1.LeftAxis.Minimum := 0.7
 else if (Curr = IVOut) or (Curr = EVOut) then
  Chart1.LeftAxis.Minimum := 0.4;

 For i := 1 to CdCurr.xcount do
  For j := 1 to CdCurr.ycount do
   begin
    Series1.AddXYZ (i, CdCurr.cell[i,j], CdCurr.ycount -j+1,
                    FIpol.SGIpol.Cells[i+1,1], clteecolor);
   end;
end; //TFFlowGraph.FormShow

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
end.
