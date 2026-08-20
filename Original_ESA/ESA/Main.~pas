//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// Main : Engine Simulation and Analysis (ESA)
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

unit Main;

interface

uses
  Windows, Messages, SysUtils, Classes, Graphics, Controls, Forms, Dialogs,
  Menus, ComCtrls, IniValues, Printers, ExtCtrls, TeEngine, Series, TeeProcs,
  Chart, StdCtrls, Grids, ErrorLog, FormSimul, PerfData, MPlayer, GasProps,
  Manifolds, eqbm, caList2z;

const
  WM_UpDateScreen = WM_USER + 0;

type
  TFMain = class(TForm)
    MainMenu1: TMainMenu;
    File1: TMenuItem;
    Load1: TMenuItem;
    SaveAs1: TMenuItem;
    N2: TMenuItem;
    Exit1: TMenuItem;
    Graphs1: TMenuItem;
    OutPut1: TMenuItem;
    Help1: TMenuItem;
    Contents1: TMenuItem;
    About1: TMenuItem;
    Edit1: TMenuItem;
    Chart1: TChart;
    Chart2: TChart;
    Run1: TMenuItem;
    SinglePointSimulation1: TMenuItem;
    MultiPointSimulation1: TMenuItem;
    LoadDefault1: TMenuItem;
    N3: TMenuItem;
    STOP1: TMenuItem;
    PVTTrace1: TMenuItem;
    PopupMenu1: TPopupMenu;
    Options1: TMenuItem;
    GraphOptions1: TMenuItem;
    Timer1: TTimer;
    Series1: TFastLineSeries;
    Series6: TFastLineSeries;
    Chart3: TChart;
    Series4: TFastLineSeries;
    Series7: TFastLineSeries;
    RunTimeGraph1: TMenuItem;
    N5: TMenuItem;
    Series11: TFastLineSeries;
    Series12: TFastLineSeries;
    Series3: TFastLineSeries;
    Series2: TFastLineSeries;
    SaveDialog1: TSaveDialog;
    ShowTorqueCurve1: TMenuItem;
    N7: TMenuItem;
    ValveOpening1: TMenuItem;
    HeatLoss1: TMenuItem;
    Series13: TFastLineSeries;
    Series9: TFastLineSeries;
    Series10: TFastLineSeries;
    Pause1: TMenuItem;
    QuickRun: TMenuItem;
    StatusBar: TStatusBar;
    InCylPropdCA: TPanel;
    Memo: TMemo;
    Panel1: TPanel;
    MFileName: TPanel;
    MRunTime: TPanel;
    MNoCycl: TPanel;
    MMassBal: TPanel;
    MSpeed: TPanel;
    MTorq: TPanel;
    MPower: TPanel;
    MVolEff: TPanel;
    MFuelCons: TPanel;
    MSFC: TPanel;
    MCylMass: TPanel;
    MIMEP: TPanel;
    MBMEP: TPanel;
    MFMEP: TPanel;
    Label1: TLabel;
    MPMEP: TPanel;
    MWork: TPanel;
    MHeatL: TPanel;
    MPump: TPanel;
    MFric: TPanel;
    MExh: TPanel;
    MFuel: TPanel;
    Label2: TLabel;
    Label3: TLabel;
    Label4: TLabel;
    Label5: TLabel;
    Label6: TLabel;
    Label7: TLabel;
    Label8: TLabel;
    Label9: TLabel;
    Label10: TLabel;
    Label11: TLabel;
    Label12: TLabel;
    Label13: TLabel;
    Label14: TLabel;
    Label15: TLabel;
    Label16: TLabel;
    Label17: TLabel;
    Label18: TLabel;
    Label19: TLabel;
    Label20: TLabel;
    Label21: TLabel;
    Label22: TLabel;
    Label23: TLabel;
    Panel23: TPanel;
    Panel24: TPanel;
    Label24: TLabel;
    Label25: TLabel;
    Label26: TLabel;
    Label27: TLabel;
    Label28: TLabel;
    Label29: TLabel;
    Label30: TLabel;
    Label31: TLabel;
    Label32: TLabel;
    Label33: TLabel;
    Label34: TLabel;
    Label36: TLabel;
    Label38: TLabel;
    Label35: TLabel;
    Label37: TLabel;
    Label39: TLabel;
    Label40: TLabel;
    Label41: TLabel;
    Label42: TLabel;
    Label43: TLabel;
    GasFlowPressdCA: TPanel;
    Series5: TLineSeries;
    Series8: TLineSeries;
    PIVO: TPanel;
    PIVC: TPanel;
    PEVO: TPanel;
    PEVC: TPanel;
    Panel2: TPanel;
    Panel3: TPanel;
    Panel4: TPanel;
    Panel5: TPanel;
    Label44: TLabel;
    MSREngName: TPanel;
    Label45: TLabel;
    MSRRunT: TPanel;
    Label46: TLabel;
    MSRNoCyc: TPanel;
    Label47: TLabel;
    MSRMassB: TPanel;
    Label48: TLabel;
    MSRRuns: TPanel;
    Label49: TLabel;
    Panel11: TPanel;
    Label50: TLabel;
    Label51: TLabel;
    Label52: TLabel;
    Label53: TLabel;
    Label54: TLabel;
    Label55: TLabel;
    Label56: TLabel;
    procedure Edit1Click(Sender: TObject);
    procedure FormClose(Sender: TObject; var Action: TCloseAction);
    procedure Exit1Click(Sender: TObject);
    procedure About1Click(Sender: TObject);
    procedure Load1Click(Sender: TObject);
    procedure SaveAs1Click(Sender: TObject);
    procedure FormCreate(Sender: TObject);
    procedure FormResize(Sender: TObject);
    procedure FormShow(Sender: TObject);
    procedure SinglePointSimulation1Click(Sender: TObject);
    procedure LoadDefault1Click(Sender: TObject);
    procedure STOP1Click(Sender: TObject);
    procedure PVTTrace1Click(Sender: TObject);
    procedure Options1Click(Sender: TObject);
    procedure Timer1Timer(Sender: TObject);
    procedure Chart2Zoom(Sender: TObject);
    procedure MultiPointSimulation1Click(Sender: TObject);
    procedure SaveText1Click(Sender: TObject);
    procedure PlotResults1Click(Sender: TObject);
    procedure ShowTorqueCurve1Click(Sender: TObject);
    procedure ValveOpening1Click(Sender: TObject);
    procedure HeatLoss1Click(Sender: TObject);
    procedure Pause1Click(Sender: TObject);
    procedure QuickRunClick(Sender: TObject);
    procedure Contents1Click(Sender: TObject);
  private
    procedure Status (st : String);
    Procedure Simulate;
    Procedure ClearFlowGraphs;
    Procedure RedrawGraphs;
    Procedure ClearCylinderGraphs;
    Procedure UpDateGraphs (CA : Double);
    Procedure ShowResults;
    Function GetSimulTime : String;
    Procedure RuntimeMenus (STATUS : Boolean);
    Procedure WriteRunFile;
  public

  end;

var
  FMain : TFMain;
  CurrEng : TObject;
  Paused : Boolean;

 procedure Comment (St : String);

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

implementation

uses
   ICEngine2z, // Engine Simulation
   Edit, //FEdit
   AboutBoxUnit, //AboutBox
   PVTDataForm, //Text Display / Print / Save PVT Data
   FFlowGraphOptions, MultiRun, TorqueCurve, Welcome, FManfA,
   GValveLift, GHeatLoss;

var
  CommentsON : Boolean;
  Convergence : Boolean;
  Running : Boolean;
  OKToRun : Boolean;
  StartTime, EndTime : TDatetime;
  ChartAutoRescale : Boolean;
  ShowGraphs : Boolean;
  ShowFlowGraphs : Boolean;
  ShowPVGraphs : Boolean;
  ShowCylGraphs : Boolean;
  nsimuls : Integer;
  Stepping : Integer;
{$R *.DFM}

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Procedure Comment (St : String);
begin
 if CommentsOn then FMain.Memo.Lines.Add (St);
end; //Comment

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Procedure TFMain.Status (st : String);
begin
 StatusBar.Panels[0].Text := 'Simulation Status: ' + st;
end; //TFMain.Status

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function IntToStrDgts (I, Digits : Integer) : String;
var
 st : string;
begin
 st := IntToStr (I);
 while length (st) < Digits do st := '0' + st;
 intToStrDgts := st;
end; //IntToStrDgts

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function TFMain.GetSimulTime : String;
var
  SimulTime : TDateTime;
  sH, sM, ss, sMs : Word;
begin
  if EndTime < Now then EndTime := Now;
  SimulTime := EndTime-StartTime;
  DecodeTime (SimulTime, sH, sM, sS, sMS);
  GetSimulTime := IntToStrDgts (sH,2) + ':' + IntToStrDgts (sM,2) + ':'
                                            + IntToStrDgts (ss,2);
end; //TFMain.GetSimulTime

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Procedure TFMain.Simulate;
var
 i : integer;
 CA1 : Double;
begin
 ClearFlowGraphs;
 ClearCylinderGraphs;
 if FEdit.CBSaveManfData.Checked then
  Engine2z.SAVEMANFDATA := TRUE;
 if NoCycles < 3 then NoCycles := 3;
 Engine2z.NoZones := 1;
 for i := 1 to NoCycles do
 with Engine2z do
  begin
   MNoCycl.Caption := IntToStr(i) + ' / ' + IntToStr (NoCycles);
   MSRNoCyc.Caption := MNoCycl.Caption;
   if i <> 1 then
    begin
     MMassBal.Caption := FloatToStrF(abs(TotalMInIV-TotalMOutEV)*1e6, fffixed, 10,1);
     MSRMassB.Caption := MMassBal.Caption;
    end;
   if (i >= No1zCycles + 1) and (FEdit.RB2Zone.Checked = TRUE) then
    NoZones := 2
   else if FEdit.RB1Zone.Checked = TRUE then NoZones := 1;
   if abs(TotalMInIV-TotalMOutEV)*1e6 < MassBalance then
    begin
     NoCycles := i;
     Running := FALSE;
     FGraphOptions.RGFlowGraph.ItemIndex := 1;
     FGraphOptions.RGPVGraph.ItemIndex := 1;
     FGraphOptions.RGInCylGraph.ItemIndex := 1;
     ShowResults;
     RunTimeMenus (TRUE);
     Exit;
    end;
   CA := Manifold.IV.C;
   repeat
     Application.ProcessMessages;
     if not Paused then
      begin
       Try
         CA1 := CA;
         if CA1 < 0 then CA1 := 720 + CA1;
         if CA = 0 then ClearFlowGraphs;
         if CA = Manifold.IV.C then ClearCylinderGraphs;
         Run;
         CurrCAList.UpdateCAPoint;
         if ShowGraphs or ShowFlowGraphs or ShowPVGraphs or ShowCylGraphs then
          if (abs(round(CA)) mod 5) < 1 then UpdateGraphs (CA);
         FPVTData.UpDateList (CA);
         MRunTime.Caption := GetSimulTime;
         MSRRunT.Caption := MRunTime.Caption;
         statusBar.Panels[2].Text := GetSimulTime;
         InCylPropdCA.Caption := floatToStrF(CA, fffixed,5,0) + ' deg ';
         GasFlowPressdCA.Caption := floatToStrF(CA1, fffixed,5,0) + ' deg ';
         CA := CA+dCA;
         if CA > 360 then CA := CA - 720;
         CA1 := CA;
        Except
         on E: EEqbmError do
          begin
            MessageDlg(E.Message, mtInformation, [mbOK], 0);
            Running := NOT Running;
            Status ('Terminated on Eqbm Error');
          end;//on eGasPropsError
         on E: EGasPropsError do
          begin
            MessageDlg(E.Message, mtInformation, [mbOK], 0);
            Running := NOT Running;
            Status ('Terminated on Gas Props Error');
          end;//on eGasPropsError
         on E: ECFDError do
          begin
            MessageDlg(E.Message, mtInformation, [mbOK], 0);
            Running := NOT Running;
            Status ('Terminated on CFD Error');
          end;//on eGasPropsError
         on E: EEngineError do
          begin
            MessageDlg(E.Message, mtInformation, [mbOK], 0);
            Running := NOT Running;
            Status ('Terminated on Engine Error');
          end;//on eGasPropsError
         else
          begin
           MessageDlg('Terminated on Unknown Error', mtInformation, [mbOK], 0);
           Running := NOT Running;
           Status ('Terminated on Unknown Error');
          end;
        end; //Try Except
      end; //if Not Paused then Begin...
      If Not running then Exit;
    until Abs(CA-Manifold.IV.C)< dCA;
    if (abs(TotalMInIV-TotalMOutEV)*1e6 < 0.5) and (FMultiRun.NoRuns > 1) then
     Exit;
  end; // with Engine2z for i
end; //TFMain.Simulate

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Procedure TFMain.ClearFlowGraphs;
var
  FlowAngle : Double;
  GraphMin : Double;
  GraphMax : Double;
begin
 PIVO.Visible := True;
 PIVO.Left := StrtoInt(FloattoStr(Engine2z.Manifold.IV.O))div 2 + 46;
 PIVC.Visible := True;
 PIVC.Left := StrtoInt(FloattoStr(Engine2z.Manifold.IV.C+720))div 2 + 47;
 PEVO.Visible := True;
 PEVO.Left := StrtoInt(FloattoStr(Engine2z.Manifold.EV.O))div 2 + 45;
 PEVC.Visible := True;
 PEVC.Left := StrtoInt(FloattoStr(Engine2z.Manifold.EV.C+720))div 2 + 46;
 Series1.Clear;  //Inlet
 Series3.Clear;  //Cylinder
 Series4.Clear;  //IValve
 Series5.Clear;  //IVC
 Series6.Clear;  //Exhaust
 Series7.Clear;  //EValve
 Series8.Clear;  //EVC
 Series9.Clear;  //Burned
 Series10.Clear; //UnBurned

  case FGraphOptions.RGFlowGraph.ItemIndex of
   1: begin
       Series1.Active := TRUE;
       Series3.Active := TRUE;
       Series6.Active := TRUE;
       Series9.Active := FALSE;
       Series10.Active := FALSE;
       if StrToFloat(FGraphOptions.EGFYMax.Text) > 0 then
        Chart1.LeftAxis.Maximum := StrToFloat(FGraphOptions.EGFYMax.Text)
       else if (MSRRunT.Caption = '') or (MRunTime.Caption = '') or
               (MSRRunT.Caption = '-') or (MRunTime.Caption = '-') then
        Chart1.LeftAxis.Maximum := 2.75
       else
        Chart1.LeftAxis.Maximum := 0.00025*Engine2z.Nrpm + 1.55;
       if StrToFloat(FGraphOptions.EGFYMin.Text) > 0 then
        Chart1.LeftAxis.Minimum := StrToFloat(FGraphOptions.EGFYMin.Text)
       else if (MSRRunT.Caption = '') or (MRunTime.Caption = '') or
               (MSRRunT.Caption = '-') or (MRunTime.Caption = '-') then
        Chart1.LeftAxis.Minimum := 0
       else
        Chart1.LeftAxis.Minimum := -0.0001*Engine2z.Nrpm + 0.6;
       Chart1.LeftAxis.AxisValuesFormat := '#,##0.00';
       Chart1.LeftAxis.Title.Caption := 'Pressure [bar]';
       if Chart1.LeftAxis.Maximum > 5 then Chart1.LeftAxis.Maximum := 5;
       if Chart1.LeftAxis.Minimum < 0 then Chart1.LeftAxis.Minimum := 0;
      end;
   2: begin
       Series1.Active := TRUE;
       Series3.Active := FALSE;
       Series6.Active := TRUE;
       Series9.Active := FALSE;
       Series10.Active := FALSE;
       if StrToFloat(FGraphOptions.EGFYMax.Text) > 0 then
        Chart1.LeftAxis.Maximum := StrToFloat(FGraphOptions.EGFYMax.Text)
       else Chart1.LeftAxis.Maximum := 450;
       if StrToFloat(FGraphOptions.EGFYMin.Text) > 0 then
        Chart1.LeftAxis.Minimum := StrToFloat(FGraphOptions.EGFYMin.Text)
       else Chart1.LeftAxis.Minimum := -150;
       Chart1.LeftAxis.AxisValuesFormat := '#,##0';
       Chart1.LeftAxis.Title.Caption := 'Velocity [m/s]';
      end;
   3: begin
       Series1.Active := TRUE;
       Series3.Active := TRUE;
       Series6.Active := TRUE;
       Series9.Active := TRUE;
       Series10.Active := TRUE;
       if StrToFloat(FGraphOptions.EGFYMax.Text) > 0 then
        Chart1.LeftAxis.Maximum := StrToFloat(FGraphOptions.EGFYMax.Text)
       else Chart1.LeftAxis.Maximum := 100;
       if StrToFloat(FGraphOptions.EGFYMin.Text) > 0 then
        Chart1.LeftAxis.Minimum := StrToFloat(FGraphOptions.EGFYMin.Text)
       else Chart1.LeftAxis.Minimum := -40;
       Chart1.LeftAxis.AxisValuesFormat := '#,##0';
       Chart1.LeftAxis.Title.Caption := 'Mass / MassFlow [kg / kg/deg]';       
      end;
   end; //case
   GraphMin := Chart1.LeftAxis.Minimum;
   GraphMax := Chart1.LeftAxis.Maximum;
 //ValveEvents
 with Engine2z.Manifold do
  begin
   if (IV.O < 0) then FlowAngle := IV.O+720 Else FlowAngle := IV.O;
    Series4.AddXY (FlowAngle, GraphMin, '', clteecolor);
    Series4.AddXY (FlowAngle, GraphMax, '', clteecolor);
   if EV.O < 0 then FlowAngle := EV.O+720 Else FlowAngle := EV.O;
    Series7.AddXY (FlowAngle, GraphMin, '', clteecolor);
    Series7.AddXY (FlowAngle, GraphMax, '', clteecolor);
   if IV.C < 0 then FlowAngle := IV.C+720 Else FlowAngle := IV.C;
    Series5.AddXY (FlowAngle, GraphMin, '', clteecolor);
    Series5.AddXY (FlowAngle, GraphMax, '', clteecolor);
   if EV.C < 0 then FlowAngle := EV.C+720 Else FlowAngle := EV.C;
    Series8.AddXY (FlowAngle, GraphMin, '', clteecolor);
    Series8.AddXY (FlowAngle, GraphMax, '', clteecolor);
  end;
end; //TFMain.ClearFlowGraphs

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Procedure TFMain.ClearCylinderGraphs;
var
  FlowAngle  : Double;
begin
 if (Engine2z.NoZones = 1) and (FEdit.RB2Zone.Checked = TRUE) then
  Stepping := 1
 else if (FEdit.RB2Zone.Checked = FALSE) then
  Stepping := 4;
 Series2.Clear;  // PV Diagram
 Series11.Clear; // Cyl Pressure
 Series12.Clear; // Tburned
 Series13.Clear; // Tunburned

 case FGraphOptions.RGPVGraph.ItemIndex of
   1: begin
       Chart2.BottomAxis.Minimum := 0;
       Chart2.BottomAxis.Maximum := Round(Engine2z.Vd*1e6)*1.2;
       if (Engine2z.NoZones = 1) then Chart2.LeftAxis.Maximum := 100;
       if ChartAutoRescale and (Stepping = 4) then
        begin
         Chart2.LeftAxis.Maximum := Round(Engine2z.PMax/1e5) + 5;
         Chart2.BottomAxis.Maximum := Round(Engine2z.Vd*1e6)*1.2;
        end;
       if StrToFloat(FGraphOptions.EPVYMax.Text) > 0 then
        Chart2.LeftAxis.Maximum := StrToFloat(FGraphOptions.EPVYMax.Text);
       if StrToFloat(FGraphOptions.EPVYMin.Text) > 0 then
        Chart2.LeftAxis.Minimum := StrToFloat(FGraphOptions.EPVYMin.Text);
      end;
 end;
 case FGraphOptions.RGInCylGraph.ItemIndex of
   1: begin
       Chart3.BottomAxis.Minimum := Engine2z.Manifold.IV.C;
       Chart3.BottomAxis.Maximum := Engine2z.Manifold.EV.O;
       if (Engine2z.NoZones = 1) then
        begin
         Chart3.LeftAxis.Maximum := 100;
         Chart3.RightAxis.Maximum := 6000;
        end;
       if ChartAutoRescale and (Stepping = 4) then
        begin
         Chart3.LeftAxis.Maximum := Round(Engine2z.PMax/1e5) + 5;
         Chart3.RightAxis.Maximum := Chart3.LeftAxis.Maximum/100*6000;
        end;
       if StrToFloat(FGraphOptions.EICYMax.Text) > 0 then
        begin
         Chart3.LeftAxis.Maximum := StrToFloat(FGraphOptions.EICYMax.Text);
         Chart3.RightAxis.Maximum := Chart3.LeftAxis.Maximum/100*6000;
        end;
       if StrToFloat(FGraphOptions.EICYMin.Text) > 0 then
        begin
         Chart3.LeftAxis.Minimum := StrToFloat(FGraphOptions.EICYMin.Text);
         Chart3.RightAxis.Minimum := Chart3.LeftAxis.Minimum/100*6000;
        end;
      end;
 end;
 if Stepping < 4 then
  Stepping := Stepping + 1;
end; //TFMain.ClearCylinderGraphs

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Procedure TFMain.RedrawGraphs;
var
 i : integer;
begin
 ClearFlowGraphs;
 ClearCylinderGraphs;
 for i := -359 to 360 do
  if i mod 2 = 0 then UpDateGraphs (i);
end; //TFMain.RedrawGraphs

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Procedure TFMain.ShowResults;
begin
 RedrawGraphs;
 With Engine2z do
  begin
   Performance;
   MFileName.Caption := Name;
   MNoCycl.Caption := IntToStr(NoCycles) + ' / ' + IntToStr (NoCycles);
   MSpeed.Caption := FloatToStrF(Nrpm,fffixed,6,0);
   MRunTime.Caption := GetSimulTime;
   MTorq.Caption := FloatToStrF(Torque, fffixed, 10,1);
   MPower.Caption := FloatToStrF(BPower/1e3, fffixed, 10,1);
   MVolEff.Caption := FloatToStrF(VEff, fffixed, 10,1);
   MFuelCons.Caption := FloatToStrF(mf, fffixed, 10,1);
   MSFC.Caption := FloatToStrF(SfC, fffixed, 10,1);
   MCylMass.Caption := FloatToStrF(TotalMass*1e6, fffixed, 10,1);
   MMassBal.Caption := FloatToStrF(abs(TotalMInIV-TotalMOutEV)*1e6, fffixed, 10,1);
   MWork.Caption := FloatToStrF(QWork, fffixed,3,1);
   MHeatL.Caption := FloatToStrF(QHeat, fffixed,3,1);
   MPump.Caption := FloatToStrF(QPump, fffixed,3,1);
   MFric.Caption := FloatToStrF(QFric, fffixed,3,1);
   MExh.Caption := FloatToStrF(QExht, fffixed,3,1);
   MFuel.Caption := '100';
   MIMEP.Caption := FloatToStrF(IMEP/1e5, fffixed, 10,3);
   MFMEP.Caption := FloatToStrF(FMEP/1e5, fffixed, 10,3);
   MPMEP.Caption := FloatToStrF(PMEP/1e5, fffixed, 10,3);
   MBMEP.Caption := FloatToStrF(BMEP/1e5, fffixed, 10,3);
   WriteRunFile;
   beep;
  end; //with Engine2z
end; //TFMain.ShowResults

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Procedure TFMain.UpDateGraphs (CA : Double);
var
 Current : TCAPoint;
 FlowAngle : Double;
begin
 Current := CurrCAList.CaVar [round(CA)];
 if CA < 0 then FlowAngle := CA+720 Else FlowAngle := CA;

   if Showgraphs or ShowPVGraphs or Not Running then //PVGraph
    if FGraphOptions.RGPVGraph.Itemindex = 1 then
     Series2.Addxy (Current.Value[1]*1E6, Current.Value[2]/1E5, '', clTeeColor);

   if Showgraphs or ShowFlowGraphs or Not Running then //FlowGraph
    begin
    if FGraphOptions.RGFlowGraph.Itemindex = 1 then
     begin
      Series3.AddXY (flowangle, Current.P/1E5, '', clTeeColor);
      Series1.AddXY (flowangle, Current.Pin/1E5, '', clTeeColor);
      Series6.AddXY (flowangle, Current.PEx/1E5, '', clTeeColor);
     end; // Pressure Graphs
    if FGraphOptions.RGFlowGraph.Itemindex = 2 then
     begin
      Series1.AddXY (flowangle, Current.UIn, '', clTeeColor);
      Series6.AddXY (flowangle, Current.UEx, '', clTeeColor);
     end; // Velocity Graphs
    if FGraphOptions.RGFlowGraph.Itemindex = 3 then
     begin
      Series9.AddXY (flowangle, Current.mb*1E5, '', clTeeColor);
      Series10.AddXY (flowangle, Current.mu*1E5, '', clTeeColor);
      Series1.Addxy (flowangle, Current.Min*1E7, '', clTeeColor);
      Series6.Addxy (flowangle, Current.Mout*1E7, '', clTeeColor);
      Series3.Addxy (flowangle, Current.mcyl*1E5, '', clTeeColor);
     end; // Pressure Graphs
    end;

   if Showgraphs or ShowCylGraphs or Not Running then //CylinderGraph
    if FGraphOptions.RGInCylGraph.Itemindex = 1 then
     begin
      if (Current.IVA = 0) and (current.EVA = 0) then
       Series11.AddXY (CA, Current.P/1e5, '', clTeeColor);
      if (Current.IVA = 0) and (current.EVA = 0) then
       Series12.AddXY (CA, Current.Tb, '', clTeeColor);
      if (Current.IVA = 0) and (current.EVA = 0) then
       Series13.AddXY (CA, Current.Tu, '', clTeeColor);
     end;
end; //TFMain.UpDateGraphs

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFMain.Edit1Click(Sender: TObject);
begin
 FEdit.Show;
end; //TFMain.Edit1Click

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFMain.FormClose(Sender: TObject;
                           var Action: TCloseAction);
// var DlgStr : String;
begin
  if running then
   begin
    if MessageDlg ('Still Running : Terminate and Close?', mtwarning,
                   [mbyes, mbno], 0)= mrno then
    begin
     Action := CANone;
     Exit;
    end;
   end;
  Engine2z.Free;
  ELog.Free;
  CurrCAList.Free;
  PerformanceData.Free;
  Action := CAFree;
  Halt;
end; //TFMain.FormClose

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFMain.Exit1Click(Sender: TObject);
begin
 Close;
end; //TFMain.Exit1Click

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFMain.About1Click(Sender: TObject);
begin
 AboutBox.Show;
end; //TFMain.About1Click

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFMain.Load1Click(Sender: TObject);
begin
  Status ('Loading...');
  If FEdit.OpenDialog.Execute then
  begin
   FEdit.SaveDialog.Filename := FEdit.OpenDialog.Filename;
   FEdit.LoadTextFile (FEdit.OpenDialog.FileName);
   Status ('Engine File Loaded');
   statusBar.Panels[1].Text := 'Current Engine : ' + FEdit.OpenDialog.FileName;
   FMultiRun.LFilename.Caption := 'Base File : ' + FEdit.OpenDialog.FileName;
   if NOT SaveAs1.Enabled then
    SaveAs1.Enabled := TRUE;
   if NOT Edit1.Enabled then
    Edit1.Enabled := TRUE;
   if NOT Run1.Enabled then
    Run1.Enabled := TRUE;
   if NOT Graphs1.Enabled then
    Graphs1.Enabled := TRUE;
   if NOT Output1.Enabled then
    Output1.Enabled := TRUE;
   if NOT SinglePointSimulation1.Enabled then
    SinglePointSimulation1.Enabled := TRUE;
   if NOT MultiPointSimulation1.Enabled then
    MultiPointSimulation1.Enabled := TRUE;
   if NOT MultiPointSimulation1.Enabled then
    MultiPointSimulation1.Enabled := TRUE;
   if NOT ValveOpening1.Enabled then
    ValveOpening1.Enabled := TRUE;
   if NOT Options1.Enabled then
    Options1.Enabled := TRUE;
   if NOT GraphOptions1.Enabled then
    GraphOptions1.Enabled := TRUE;
   MFileName.Caption := FEdit.EName.Text;
   MNoCycl.Caption := '-';
   MSpeed.Caption := '-';
   MRunTime.Caption := '-';
   MTorq.Caption := '-';
   MPower.Caption := '-';
   MVolEff.Caption := '-';
   MFuelCons.Caption := '-';
   MSFC.Caption := '-';
   MCylMass.Caption := '-';
   MMassBal.Caption := '-';
   MWork.Caption := '-';
   MHeatL.Caption := '-';
   MPump.Caption := '-';
   MFric.Caption := '-';
   MExh.Caption := '-';
   MFuel.Caption := '-';
   MIMEP.Caption := '-';
   MFMEP.Caption := '-';
   MPMEP.Caption := '-';
   MBMEP.Caption := '-';
   Series1.Clear;  //Inlet
   Series2.Clear;  // PV Diagram
   Series3.Clear;  //Cylinder
   Series4.Clear;  //IValve
   Series5.Clear;  //IVC
   Series6.Clear;  //Exhaust
   Series7.Clear;  //EValve
   Series8.Clear;  //EVC
   Series9.Clear;  //Burned
   Series10.Clear; //UnBurned
   Series11.Clear; // Cyl Pressure
   Series12.Clear; // Tburned
   Series13.Clear; // Tunburned
  end;
end; //TFMain.Load1Click

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFMain.SaveAs1Click(Sender: TObject);
begin
 Status ('Saving...');
 If FEdit.SaveDialog.Execute then
  begin
   FEdit.SaveTextFile(FEdit.SaveDialog.FileName);
   Status ('File Saved');
   statusBar.Panels[1].Text := 'Saved Engine : ' + FEdit.SaveDialog.FileName;
  end;
end; //TFMain.SaveAs1Click

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFMain.FormCreate(Sender: TObject);
begin
  // Initialization Routines
  Status ('Initializing');
  LoadIniValues;
  ELog := TErrorLog.Init (iniErrorLogFileName, 'Simulation Log');
  CommentsON := TRUE; // Comments wanted
  Running := FALSE; // Not Running Yet
  CurrCAList := TCAList.Create;
  PerformanceData := TPerfData.Create;
  status ('Ready');
  Paused := FALSE;
end; //TFMain.FormCreate

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFMain.FormResize(Sender: TObject);
begin
  if WindowState = wsMaximized then
  begin
   VertScrollBar.Visible := FALSE;
   HorzScrollBar.Visible := FALSE;
  end
 else
  begin
    VertScrollBar.Visible := TRUE;
    HorzScrollBar.Visible := TRUE;
  end;
end; //TFMain.FormResize

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFMain.FormShow(Sender: TObject);
begin
   VertScrollBar.Visible := FALSE;
   HorzScrollBar.Visible := FALSE;
   ChartAutoRescale := TRUE;
   FWelcome.ShowModal;
end; //FMain.FormShow

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFMain.SinglePointSimulation1Click(Sender: TObject);
var
 Result : Integer;
begin
  Engine2z := TEngine2z.Create;
  MFileName.Caption := FEdit.EName.Text;
  MNoCycl.Caption := '-';
  MSpeed.Caption := '-';
  MRunTime.Caption := '-';
  MTorq.Caption := '-';
  MPower.Caption := '-';
  MVolEff.Caption := '-';
  MFuelCons.Caption := '-';
  MSFC.Caption := '-';
  MCylMass.Caption := '-';
  MMassBal.Caption := '-';
  MWork.Caption := '-';
  MHeatL.Caption := '-';
  MPump.Caption := '-';
  MFric.Caption := '-';
  MExh.Caption := '-';
  MFuel.Caption := '-';
  MIMEP.Caption := '-';
  MFMEP.Caption := '-';
  MPMEP.Caption := '-';
  MBMEP.Caption := '-';
  PIVO.Visible := FALSE;
  PIVC.Visible := FALSE;
  PEVO.Visible := FALSE;
  PEVC.Visible := FALSE;
  Series1.Clear;  //Inlet
  Series2.Clear;  // PV Diagram
  Series3.Clear;  //Cylinder
  Series4.Clear;  //IValve
  Series5.Clear;  //IVC
  Series6.Clear;  //Exhaust
  Series7.Clear;  //EValve
  Series8.Clear;  //EVC
  Series9.Clear;  //Burned
  Series10.Clear; //UnBurned
  Series11.Clear; // Cyl Pressure
  Series12.Clear; // Tburned
  Series13.Clear; // Tunburned
  Panel1.Show;
  Memo.Clear;
  Memo.Hide;
  ELog.Log ('Reading from Edits');
  Result := FSimulateOPtions.Showmodal;
  if Result <> MrOK then Exit;
  if FSimulateOptions.RBGraphsOn.Checked then
   begin
    FGraphOptions.RGFlowGraph.ItemIndex := 1;
    FGraphOptions.RGPVGraph.ItemIndex := 1;
    FGraphOptions.RGInCylGraph.ItemIndex := 1;
   end;
  if FSimulateOptions.RBGraphsOff.Checked then
   begin
    FGraphOptions.RGFlowGraph.ItemIndex := 0;
    FGraphOptions.RGPVGraph.ItemIndex := 0;
    FGraphOptions.RGInCylGraph.ItemIndex := 0;
   end;
  if FSimulateOptions.RBSelection.Checked then
   begin
    if FSimulateOptions.CBGasFlow.Checked then
     FGraphOptions.RGFlowGraph.ItemIndex := 1
    else
     FGraphOptions.RGFlowGraph.ItemIndex := 0;
    if FSimulateOptions.CBPV.Checked then
     FGraphOptions.RGPVGraph.ItemIndex := 1
    else
     FGraphOptions.RGPVGraph.ItemIndex := 0;
    if FSimulateOptions.CBInCyl.Checked then
     FGraphOptions.RGInCylGraph.ItemIndex := 1
    else
     FGraphOptions.RGInCylGraph.ItemIndex := 0;
   end;
  MFileName.Caption := FEdit.EName.Text;
  MNoCycl.Caption := '1' + ' / ' + IntToStr (NoCycles);
  MSpeed.Caption := FloatToStrF(Engine2z.Nrpm,fffixed,6,0);
  MRunTime.Caption := '00:00:00';
  MFuel.Caption := '100';

  FEdit.ReadfromEdits;
  ELog.Log ('Edits OK');
  Engine2z.InitVars (OKtoRun);
  Engine2z.NCycles := NoCycles;
  ELog.Log ('Engine Initialized');
  nsimuls := 1;
  FMultiRun.NoRuns := 1;
  if OKtoRun then
   begin
    Load1.Enabled := FALSE;
    SaveAs1.Enabled := FALSE;
    Edit1.Enabled := FALSE;
    LoadDefault1.Enabled := FALSE;
    SinglePointSimulation1.Enabled := FALSE;
    MultiPointSimulation1.Enabled := FALSE;
    Stop1.Enabled := TRUE;
    Pause1.Enabled := TRUE;
    Graphs1.Enabled := FALSE;
    Output1.Enabled := FALSE;
    Help1.Enabled := FALSE;
    QuickRun.Enabled := FALSE;
    status ('Running Simulation');
    RuntimeMenus (FALSE);
    StartTime := Now;
    ShowGraphs := FSimulateOptions.RBGraphsOn.Checked;
    ShowFlowGraphs := FSimulateOptions.CBGasFlow.Checked;
    ShowPVGraphs := FSimulateOptions.CBPV.Checked;
    ShowCylGraphs := FSimulateOptions.CBInCyl.Checked;
    Simulate;
    if Running then
     begin
      Running := FALSE;
      FGraphOptions.RGFlowGraph.ItemIndex := 1;
      FGraphOptions.RGPVGraph.ItemIndex := 1;
      FGraphOptions.RGInCylGraph.ItemIndex := 1;
      ShowResults;
      Status ('Simulation Complete');
      statusBar.Panels[2].Text := GetSimulTime;
      EndTime := Now;
     end;
    RuntimeMenus (TRUE);
//    Engine2z.Free;
   end;
end; //TFMain.SinglePointSimulation1Click

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFMain.LoadDefault1Click(Sender: TObject);
begin
   // Default File loaded
   FEdit.LoadTextFile (iniEngineFileName);
   Status ('Loading...');
   FEdit.SaveDialog.Filename := iniEngineFileName;
   Status ('Default File Loaded');
   statusBar.Panels[1].Text := 'Current Engine : ' + iniEngineFileName;
   FMultiRun.LFilename.Caption := 'Base File : ' + iniEngineFileName;
   if NOT SaveAs1.Enabled then
    SaveAs1.Enabled := TRUE;
   if NOT Edit1.Enabled then
    Edit1.Enabled := TRUE;
   if NOT Run1.Enabled then
    Run1.Enabled := TRUE;
   if NOT Graphs1.Enabled then
    Graphs1.Enabled := TRUE;
   if NOT Output1.Enabled then
    Output1.Enabled := TRUE;
   if NOT SinglePointSimulation1.Enabled then
    SinglePointSimulation1.Enabled := TRUE;
   if NOT MultiPointSimulation1.Enabled then
    MultiPointSimulation1.Enabled := TRUE;
   if NOT ValveOpening1.Enabled then
    ValveOpening1.Enabled := TRUE;
   if NOT Options1.Enabled then
    Options1.Enabled := TRUE;
   if NOT GraphOptions1.Enabled then
    GraphOptions1.Enabled := TRUE;
  MFileName.Caption := FEdit.EName.Text;
  MNoCycl.Caption := '-';
  MSpeed.Caption := '-';
  MRunTime.Caption := '-';
  MTorq.Caption := '-';
  MPower.Caption := '-';
  MVolEff.Caption := '-';
  MFuelCons.Caption := '-';
  MSFC.Caption := '-';
  MCylMass.Caption := '-';
  MMassBal.Caption := '-';
  MWork.Caption := '-';
  MHeatL.Caption := '-';
  MPump.Caption := '-';
  MFric.Caption := '-';
  MExh.Caption := '-';
  MFuel.Caption := '-';
  MIMEP.Caption := '-';
  MFMEP.Caption := '-';
  MPMEP.Caption := '-';
  MBMEP.Caption := '-';
 Series1.Clear;  //Inlet
 Series2.Clear;  // PV Diagram
 Series3.Clear;  //Cylinder
 Series4.Clear;  //IValve
 Series5.Clear;  //IVC
 Series6.Clear;  //Exhaust
 Series7.Clear;  //EValve
 Series8.Clear;  //EVC
 Series9.Clear;  //Burned
 Series10.Clear; //UnBurned
 Series11.Clear; // Cyl Pressure
 Series12.Clear; // Tburned
 Series13.Clear; // Tunburned
end; //TFMain.LoadDefault1Click

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFMain.STOP1Click(Sender: TObject);
begin
  Load1.Enabled := TRUE;
  SaveAs1.Enabled := TRUE;
  Edit1.Enabled := TRUE;
  LoadDefault1.Enabled := TRUE;
  SinglePointSimulation1.Enabled := TRUE;
  MultiPointSimulation1.Enabled := TRUE;
  Stop1.Enabled := FALSE;
  Running := FALSE;
  Pause1.Enabled := FALSE;
  Status ('Simulation Terminated');
  EndTime := Now;
  statusBar.Panels[2].Text := GetSimulTime;
  CurrCAList.SendToFile (IniTextSaveFileName);
  Memo.Lines.Add ('Premature Run Termination...');
end; //TFMain.STOP1Click

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFMain.PVTTrace1Click(Sender: TObject);
begin
 FPVTData.ShowModal;
end; //TFMain.PVTTrace1Click

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFMain.Options1Click(Sender: TObject);
var
 Index : integer;
 i : integer;
begin
 if (MSRRunT.Caption = '') or (MRunTime.Caption = '') or
    (MSRRunT.Caption = '-') or (MRunTime.Caption = '-') then
  begin
   Engine2z := TEngine2z.Create;
   FEdit.ReadfromEdits;
   ELog.Log ('Edits OK');
   Engine2z.InitVars (OKtoRun);
  end;
 FGraphOptions.Showmodal;
 Series1.Clear;  //Inlet
 Series2.Clear;  // PV Diagram
 Series3.Clear;  //Cylinder
 Series6.Clear;  //Exhaust
 Series9.Clear;  //Burned
 Series10.Clear; //UnBurned
 Series11.Clear; // Cyl Pressure
 Series12.Clear; // Tburned
 Series13.Clear; // Tunburned
 Case FGraphOptions.RGFlowGraph.ItemIndex of
  1 : //Pressures
   begin
    Chart1.Title.Text[0] := 'Gas Flow : Pressures';
    Chart1.LeftAxis.Title.Caption := 'Pressure [bar]';
    if StrToFloat(FGraphOptions.EGFYMax.Text) > 0 then
     Chart1.LeftAxis.Maximum := StrToFloat(FGraphOptions.EGFYMax.Text);
    if StrToFloat(FGraphOptions.EGFYMin.Text) > 0 then
     Chart1.LeftAxis.Minimum := StrToFloat(FGraphOptions.EGFYMin.Text);
    Series9.ShowInlegend := FALSE;
    Series10.ShowInLegend := FALSE;
   end;
  2 : //Velocities
   begin
    Chart1.Title.Text[0] := 'Gas Flow : Velocities';
    Chart1.LeftAxis.Title.Caption := 'Velocity [m/s]';
    Series9.ShowInlegend := FALSE;
    Series10.ShowInLegend := FALSE;
   end;
  3 : //Masses
   begin
    Chart1.Title.Text[0] := 'Gas Flow : Masses';
    Chart1.LeftAxis.Title.Caption := 'Mass / MassFlow [kg / kg/deg]';
    Series9.ShowInlegend := TRUE;
    Series10.ShowInLegend := TRUE;
   end;
 end;

 if (FGraphOptions.RGFlowGraph.ItemIndex <> 0) and
    (FGraphOptions.RGPVGraph.ItemIndex <> 0) and
    (FGraphOptions.RGInCylGraph.ItemIndex <> 0) then
   begin
    ShowFlowGraphs := TRUE;
    ShowPVGraphs := TRUE;
    ShowCylGraphs := TRUE;
    FSimulateOptions.RBGraphsOn.Checked := TRUE;
    FSimulateOptions.CBGasFlow.Checked := TRUE;
    FSimulateOptions.CBPV.Checked := TRUE;
    FSimulateOptions.CBInCyl.Checked := TRUE;
   end;
 if (FGraphOptions.RGFlowGraph.ItemIndex = 0) and
    (FGraphOptions.RGPVGraph.ItemIndex <> 0) and
    (FGraphOptions.RGInCylGraph.ItemIndex <> 0) then
   begin
    ShowFlowGraphs := FALSE;
    ShowPVGraphs := TRUE;
    ShowCylGraphs := TRUE;
    FSimulateOptions.RBSelection.Checked := TRUE;
    FSimulateOptions.CBPV.Checked := TRUE;
    FSimulateOptions.CBInCyl.Checked := TRUE;
   end;
 if (FGraphOptions.RGFlowGraph.ItemIndex <> 0) and
    (FGraphOptions.RGPVGraph.ItemIndex = 0) and
    (FGraphOptions.RGInCylGraph.ItemIndex <> 0) then
   begin
    ShowFlowGraphs := TRUE;
    ShowPVGraphs := FALSE;
    ShowCylGraphs := TRUE;
    FSimulateOptions.RBSelection.Checked := TRUE;
    FSimulateOptions.CBGasFlow.Checked := TRUE;
    FSimulateOptions.CBInCyl.Checked := TRUE;
   end;
 if (FGraphOptions.RGFlowGraph.ItemIndex <> 0) and
    (FGraphOptions.RGPVGraph.ItemIndex <> 0) and
    (FGraphOptions.RGInCylGraph.ItemIndex = 0) then
   begin
    ShowFlowGraphs := TRUE;
    ShowPVGraphs := TRUE;
    ShowCylGraphs := FALSE;
    FSimulateOptions.RBSelection.Checked := TRUE;
    FSimulateOptions.CBGasFlow.Checked := TRUE;
    FSimulateOptions.CBPV.Checked := TRUE;
   end;
 if (FGraphOptions.RGFlowGraph.ItemIndex = 0) and
    (FGraphOptions.RGPVGraph.ItemIndex = 0) and
    (FGraphOptions.RGInCylGraph.ItemIndex <> 0) then
   begin
    ShowFlowGraphs := FALSE;
    ShowPVGraphs := FALSE;
    ShowCylGraphs := TRUE;
    FSimulateOptions.RBSelection.Checked := TRUE;
    FSimulateOptions.CBInCyl.Checked := TRUE;
   end;
 if (FGraphOptions.RGFlowGraph.ItemIndex = 0) and
    (FGraphOptions.RGPVGraph.ItemIndex <> 0) and
    (FGraphOptions.RGInCylGraph.ItemIndex = 0) then
   begin
    ShowFlowGraphs := FALSE;
    ShowPVGraphs := TRUE;
    ShowCylGraphs := FALSE;
    FSimulateOptions.RBSelection.Checked := TRUE;
    FSimulateOptions.CBGasFlow.Checked := TRUE;
    FSimulateOptions.CBInCyl.Checked := TRUE;
   end;
 if (FGraphOptions.RGFlowGraph.ItemIndex <> 0) and
    (FGraphOptions.RGPVGraph.ItemIndex = 0) and
    (FGraphOptions.RGInCylGraph.ItemIndex = 0) then
   begin
    ShowFlowGraphs := TRUE;
    ShowPVGraphs := FALSE;
    ShowCylGraphs := FALSE;
    FSimulateOptions.RBSelection.Checked := TRUE;
    FSimulateOptions.CBGasFlow.Checked := TRUE;
   end;

 ClearFlowGraphs;
 ClearCylinderGraphs;
 if Not Running then
  begin
   for i := -359 to 360 do
    if i mod 2 = 0 then UpDateGraphs (i);
  end;  
end; //TFMain.Options1Click

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFMain.Timer1Timer(Sender: TObject);
var
 TempStr : String;
begin
 Comment ('Timer');
 repeat
  TempStr := ELog.GetLoggedString;
  if TempStr <> '' then Comment (TempStr);
 until TempStr = '';
end; //TFMain.Timer1Timer

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFMain.Chart2Zoom(Sender: TObject);
begin
  ChartAutoRescale := FALSE;
end; //TFMain.Chart2Zoom

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFMain.RunTimeMenus (Status : Boolean);
begin
 Running := NOT STATUS;
 Load1.Enabled := STATUS;
 SaveAs1.Enabled := STATUS;
 Edit1.Enabled := STATUS;
 LoadDefault1.Enabled := STATUS;
 SinglePointSimulation1.Enabled := STATUS;
 MultiPointSimulation1.Enabled := STATUS;
 Stop1.Enabled := NOT STATUS;
 Pause1.Enabled := NOT Status;
 Graphs1.Enabled := STATUS;
 Output1.Enabled := STATUS;
 Help1.Enabled := STATUS;
 QuickRun.Enabled := STATUS;
end; //TFMain.RunTimeMenus

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFMain.WriteRunFile;
var
 OutputF : TextFile;
 StrFile : String;
begin
  StrFile := FEdit.EPerfSave.Text;
  assignfile(OutputF,StrFile);
  if fileExists(StrFile) then append(OutPutF) else
   begin
    Rewrite(OutputF);
    write(OutputF,'Speed','  ','IMEP','     ','PMEP','    ','FMEP','   ','BMEP','   ',
                  'MEff','  ','VEff','   ','ThEff','  ','Torque','  ','Power',
                  '    ','mf','    ','SFC','  ','TMass','  ','MassIn','  ',
                  'MassOut','  ','Lambda','  ','Spark','  ','BackP','  ',
                  'QHeat','  ','QWork','  ','QExht','  ','QPump','  ','QFric');
   end;
  writeln(OutputF);
  with Engine2z do
  write(OutputF,Nrpm:4:0,'  ',IMEP/1e5:6:3,'  ',PMEP/1e5:6:3,'  ',FMEP/1e5:6:3,'  '
       ,BMEP/1e5:6:3,'  ',MEff:4:1,'  ',VEff:4:1,'  ',ThEff:4:1,'  '
       ,Torque:6:2,'  ',BPower/1e3:6:3,'  ',mf:5:2,'  ',SFC:5:1,'  '
       ,TotalMass*1e6:6:2,'  ',TotalMInIV*1e6:6:2,'  ',TotalMOutEV*1e6:6:2, '  '
       ,Cyl.Fuel.Lambda:5:2, '  ', Cyl.ThetaSpark*(-1):5:1, '  '
       ,Manifold.ExhBack.Pres(Nrpm)/1e3-101.325:5:1, '  '
       ,QHeat:5:1, '  ', QWork:5:1, '  ', QExht:5:1, '  ',QPump:5:1, '  '
       ,QFric:5:1);
 closefile(outputF);
end; //TFMain.WriteRunFile

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFMain.MultiPointSimulation1Click(Sender: TObject);
var
 i : integer;
 memostr : String;
 OutputF : TextFile;
 No2z : Integer;
 Space1Str, Space2Str, Space3Str : String;
begin
 MSREngName.Caption := '-';
 MSRRunT.Caption := '-';
 MSRNoCyc.Caption := '-';
 MSRRuns.Caption := '-';
 MSRMassB.Caption := '-';
 PIVO.Visible := FALSE;
 PIVC.Visible := FALSE;
 PEVO.Visible := FALSE;
 PEVC.Visible := FALSE;
 Series1.Clear;  //Inlet
 Series2.Clear;  // PV Diagram
 Series3.Clear;  //Cylinder
 Series4.Clear;  //IValve
 Series5.Clear;  //IVC
 Series6.Clear;  //Exhaust
 Series7.Clear;  //EValve
 Series8.Clear;  //EVC
 Series9.Clear;  //Burned
 Series10.Clear; //UnBurned
 Series11.Clear; // Cyl Pressure
 Series12.Clear; // Tburned
 Series13.Clear; // Tunburned
 Panel1.Hide;
 Memo.Clear;
 Memo.Show;
 MSREngName.Caption := FEdit.EName.Text;
 FMultiRun.ShowModal;
 if Not OkToMultiRun then Exit;
 status ('Running Multi Variable Simulation');
 StartTime := Now;
 RunTimeMenus (FALSE);
 if FMultiRun.CBShowGraphs.Checked then
  begin
   FSimulateOptions.RBGraphsOn.Checked := TRUE;
   FSimulateOptions.RBGraphsOff.Checked := FALSE;
   FSimulateOptions.RBSelection.Checked := FALSE;
  end
 else
  begin
   FSimulateOptions.RBGraphsOn.Checked := FALSE;
   FSimulateOptions.RBGraphsOff.Checked := TRUE;
   FSimulateOptions.RBSelection.Checked := FALSE;
  end;
  if FSimulateOptions.RBGraphsOn.Checked then
   begin
    FGraphOptions.RGFlowGraph.ItemIndex := 1;
    FGraphOptions.RGPVGraph.ItemIndex := 1;
    FGraphOptions.RGInCylGraph.ItemIndex := 1;
   end;
  if FSimulateOptions.RBGraphsOff.Checked then
   begin
    FGraphOptions.RGFlowGraph.ItemIndex := 0;
    FGraphOptions.RGPVGraph.ItemIndex := 0;
    FGraphOptions.RGInCylGraph.ItemIndex := 0;
   end;
  {if NOT FSimulateOptions.RBSelection.Checked and then
   begin
    FGraphOptions.RGFlowGraph.ItemIndex := 0;
    FGraphOptions.RGPVGraph.ItemIndex := 0;
    FGraphOptions.RGInCylGraph.ItemIndex := 0;
   end;}
 ShowGraphs := FMultiRun.CBShowGraphs.Checked;
 PerformanceData.Clear;

 For nsimuls := 1 to FMultiRun.NoRuns do
  begin
   try
    MSRRuns.Caption:= IntToStr(nsimuls) + ' of ' + IntToStr(FMultiRun.NoRuns);
    MSRMassB.Caption := '-';
    Engine2z := TEngine2z.Create;
    FEdit.ReadfromEdits;
//    Engine2z.InitVars (OKtoRun);
    Engine2z.Nrpm := StrToFloat(FMultiRun.SG1.Cells[1,nsimuls]);
    NoCycles := StrToInt(FMultiRun.SG1.Cells[2,nsimuls]);
    No2z := NoCycles-1;
    if No2z < NoCycles then No1zCycles := NoCycles - No2z else
    No1zCycles := 1;
    Engine2z.NCycles := NoCycles;

    FMultiRun.GetMultiRunStr(3,nsimuls, AInManf.AFileName);
    FMultiRun.GetMultiRunStr(4,nsimuls, AExManf.AFileName);
    FMultiRun.GetMultiRunstr(5,nsimuls, Engine2z.Manifold.IV.ProfileFile);
    FMultiRun.GetMultiRunStr(6,nsimuls, Engine2z.Manifold.EV.ProfileFile);

    FMultiRun.GetMultiRunVar(7,nsimuls, Engine2z.Manifold.IV.O);
    FMultiRun.GetMultiRunVar(8,nsimuls, Engine2z.Manifold.IV.C);
    FMultiRun.GetMultiRunVar(9,nsimuls, Engine2z.Manifold.EV.O);
    FMultiRun.GetMultiRunVar(10,nsimuls, Engine2z.Manifold.EV.C);
    if FMultiRun.GetMultiRunVar(11,nsimuls, Engine2z.Manifold.IV.MaxLift)
    then With Engine2z.Manifold do
     begin
       IV.MaxLift := IV.MaxLift / 1000;
       EV.MaxLift := IV.MaxLift;
     end;
    FMultiRun.GetMultiRunVar(12,nsimuls, Engine2z.Manifold.EV.MaxLift);
    Engine2z.InitVars (OKtoRun);
    if FMultiRun.GetMultiRunVar(13,nsimuls, Engine2z.Cyl.ThetaSpark)
     then Engine2z.Cyl.ThetaSpark := Engine2z.Cyl.ThetaSpark*-1;
    FMultiRun.GetMultiRunVar(14,nsimuls, Engine2z.Cyl.Fuel.Burnangle);

//    Engine2z.InitVars (OKtoRun);
    if Running then Memo.Lines.Add('  ' + FloatToStrF(Engine2z.Nrpm, fffixed,4,0));
    Simulate;
    if Running then
    with Engine2z do
     begin
      Memo.Lines.Delete(nsimuls-1);
      Performance;
      ClearFlowGraphs;
      ClearCylinderGraphs;
      Running := FALSE;
      for i := -359 to 360 do
       if i mod 2 = 0 then UpDateGraphs (i);
      Running := TRUE;
      if VEff >= 100 then Space1Str := ''+ '           '
      else Space1Str := ''+ '             ';
      if (TotalMInIV-TotalMOutEV)*1e6 < 0 then Space2Str := ''+ '               '
      else Space2Str := ''+ '                ';
      if Torque >= 100 then Space3Str := ''+ '           '
      else Space3Str := ''+ '             ';
      MemoStr := '  ' + FloatToStrF(Nrpm, fffixed,4,0) + Space3Str +
                 FloatToStrF(Torque, fffixed, 6, 1) + '          '+
                 FloatToStrF(BPower/1e3, fffixed, 6, 1) + Space1Str +
                 FloatToStrF(VEff, fffixed, 4, 1) + '        '+
                 FloatToStrF(SFC, fffixed, 5, 1) + '         '+
                 FloatToStrF(TotalMass*1e6, fffixed, 6, 1) + Space2Str +
                 FloatToStrF((TotalMInIV-TotalMOutEV)*1e6, fffixed, 6, 1);
       PerformanceData.AddDataPoint (Nrpm, Torque, BPower/1e3, VEff);
       MSRMassB.Caption := FloatToStrF((TotalMInIV-TotalMOutEV)*1e6, fffixed, 6, 1);
       WriteRunFile;
       Memo.Lines.Add (MemoStr);
      end;
   except
    On EConvertError do
     begin
      assignfile(OutputF,'SimulDat.txt');
      append(OutputF);
      writeln(OutputF);
      writeln (OutputF, 'Error in Multirun Command Line ', nsimuls);
      closefile(outputF);
     end; //EConvertError
   end; // Try .. except
//   Engine2z.Free;
  end;
 Running := FALSE;
 ShowGraphs := TRUE;
 FGraphOptions.RGFlowGraph.ItemIndex := 1;
 FGraphOptions.RGPVGraph.ItemIndex := 1;
 FGraphOptions.RGInCylGraph.ItemIndex := 1;
 RedrawGraphs;
 RunTimeMenus (TRUE);
 Status ('Simulation Complete');
 statusBar.Panels[2].Text := GetSimulTime;
 EndTime := Now;
 Status ('Ready...');
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFMain.SaveText1Click(Sender: TObject);
var
 TF : TextFile;
 i : Integer;
begin
 if NOT SaveDialog1.Execute then Exit;
 AssignFile (TF, SaveDialog1.Filename);
 Rewrite (TF);
 For i := 0 to Memo.Lines.Count-1 do
  WriteLn (TF, Memo.Lines[i]);
 CloseFIle(TF);
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFMain.PlotResults1Click(Sender: TObject);
begin
 FTorqueCurve.ShowModal;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFMain.ShowTorqueCurve1Click(Sender: TObject);
begin
 FTorqueCurve.Showmodal;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//


procedure TFMain.ValveOpening1Click(Sender: TObject);
begin
 if (MSRRunT.Caption = '') or (MRunTime.Caption = '') or
    (MSRRunT.Caption = '-') or (MRunTime.Caption = '-') then
  begin
   Engine2z := TEngine2z.Create;
   FEdit.ReadfromEdits;
   ELog.Log ('Edits OK');
   Engine2z.InitVars (OKtoRun);
  end;
 if Not FEdit.ReadFromEdits then Exit;
 Engine2z.Manifold.IV.Profile.LoadText(Engine2z.Manifold.IV.ProfileFile);
 Engine2z.Manifold.EV.Profile.LoadText(Engine2z.Manifold.EV.ProfileFile);
 FValveLift.ShowModal;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFMain.HeatLoss1Click(Sender: TObject);
begin
 FEnergyBalance.ShowModal;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFMain.Pause1Click(Sender: TObject);
begin
 Paused := NOT Paused;
 if Paused then status('Paused') else Status('Running Simulation');
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFMain.QuickRunClick(Sender: TObject);
begin
  LoadDefault1Click(Sender);
  SinglePointSimulation1Click(Sender);
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFMain.Contents1Click(Sender: TObject);
begin
 MessageDlg('Open "User_Manual.doc" or "User_Manual.pdf" in C:\ESA\Help',
 mtInformation,[mbOk], 0);
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
end.
