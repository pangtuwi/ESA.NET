//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// Engine Simulation and Analysis (ESA)
// CM van Vuuren
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

program ESA;

uses
  Forms,
  Main in 'Main.pas' {FMain},
  AboutBoxUnit in 'AboutBoxUnit.pas' {AboutBox},
  Edit in 'Edit.pas' {FEdit},
  Eqbm in 'Eqbm.pas',
  ErrorLog in 'ErrorLog.pas',
  formsimul in 'formsimul.pas' {FSimulateOptions},
  Fuel in 'Fuel.pas',
  GasProps in 'gasprops.pas',
  FflowGraphOptions in 'FflowGraphOptions.pas' {FGraphOptions},
  Manifolds in 'Manifolds.pas',
  PNTWMath in 'PNTWMath.pas',
  Profiles in 'Profiles.pas',
  PVTDataForm in 'PVTDataForm.pas' {FPVTData},
  Pipes in 'Pipes.pas',
  inivalues in 'inivalues.pas',
  MultiRun in 'MultiRun.pas' {FMultiRun},
  PNTUseful in 'PNTUseful.pas',
  TorqueCurve in 'TorqueCurve.pas' {FTorqueCurve},
  PerfData in 'PerfData.pas',
  TCurveOptions in 'TCurveOptions.pas' {FTorqCurveOptions},
  FManfA in 'FManfA.pas' {FManfArea},
  Flowgraph in 'Flowgraph.pas' {FFlowGraph},
  IPolTab in 'IPolTab.pas' {FIpol},
  Welcome in 'Welcome.pas' {FWelcome},
  Valves in 'Valves.pas',
  GHeatLoss in 'GHeatLoss.pas' {FEnergyBalance},
  GValveLift in 'GValveLift.pas' {FValveLift},
  ICEngine2Z in 'ICEngine2Z.pas',
  Gasses2Z in 'Gasses2Z.pas',
  RKF5 in 'RKf5.pas',
  CAList2z in 'CAList2z.pas',
  ExhBackPandT in 'ExhBackPandT.pas',
  GridSizes in 'GridSizes.pas',
  WallTemps in 'WallTemps.pas',
  DoubleFunc in 'DoubleFunc.pas',
  VarSpeedList in 'VarSpeedList.pas';

{$R *.RES}

begin
  Application.Initialize;
  Application.Title := 'ESA';
  Application.CreateForm(TFMain, FMain);
  Application.CreateForm(TAboutBox, AboutBox);
  Application.CreateForm(TFEdit, FEdit);
  Application.CreateForm(TFSimulateOptions, FSimulateOptions);
  Application.CreateForm(TFGraphOptions, FGraphOptions);
  Application.CreateForm(TFPVTData, FPVTData);
  Application.CreateForm(TFMultiRun, FMultiRun);
  Application.CreateForm(TFTorqueCurve, FTorqueCurve);
  Application.CreateForm(TFTorqCurveOptions, FTorqCurveOptions);
  Application.CreateForm(TFManfArea, FManfArea);
  Application.CreateForm(TFFlowGraph, FFlowGraph);
  Application.CreateForm(TFIpol, FIpol);
  Application.CreateForm(TFWelcome, FWelcome);
  Application.CreateForm(TFEnergyBalance, FEnergyBalance);
  Application.CreateForm(TFValveLift, FValveLift);
  Application.Run;
end.
