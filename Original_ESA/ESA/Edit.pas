//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// Egine Data Edit Form  : Engine Simulation and Analysis (ESA)
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

unit Edit;

interface

uses
  Windows, Messages, SysUtils, Classes, Graphics, Controls, Forms, Dialogs,
  StdCtrls, ComCtrls, Menus, Grids, Buttons, ICEngine2z, IniFiles, IPolTab,
  jpeg, ExtCtrls;

type
  TFEdit = class(TForm)
    EngineData: TPageControl;
    TabSheet1: TTabSheet;
    PCylinders: TTabSheet;
    Exhaust: TTabSheet;
    Cams: TTabSheet;
    Bore: TLabel;
    Stroke: TLabel;
    Capacity: TLabel;
    EBore: TEdit;
    EStroke: TEdit;
    ECapacity: TEdit;
    Label2: TLabel;
    Label3: TLabel;
    Label4: TLabel;
    Label5: TLabel;
    EConrodLength: TEdit;
    Label6: TLabel;
    Label15: TLabel;
    ENoCylS: TEdit;
    PValves: TTabSheet;
    GroupBox2: TGroupBox;
    Label16: TLabel;
    ETAtm: TEdit;
    Label17: TLabel;
    Label18: TLabel;
    EPAtm: TEdit;
    Label19: TLabel;
    GroupBox3: TGroupBox;
    Label20: TLabel;
    EAFRatio: TEdit;
    Label21: TLabel;
    Label22: TLabel;
    EQFuel: TEdit;
    Label23: TLabel;
    GroupBox4: TGroupBox;
    GroupBox6: TGroupBox;
    Label28: TLabel;
    Label29: TLabel;
    Label32: TLabel;
    EIVProfile: TEdit;
    BBrouseInProfile: TButton;
    Label33: TLabel;
    EIVO: TEdit;
    EIVC: TEdit;
    Label34: TLabel;
    Label35: TLabel;
    EIVLift: TEdit;
    GroupBox5: TGroupBox;
    Label36: TLabel;
    Label37: TLabel;
    Label38: TLabel;
    Label39: TLabel;
    EEVProfile: TEdit;
    BBrouseExProfile: TButton;
    EEVO: TEdit;
    EEVC: TEdit;
    EEVLift: TEdit;
    EIVNo: TEdit;
    EIVDiam: TEdit;
    GroupBox7: TGroupBox;
    Label40: TLabel;
    Label41: TLabel;
    EEVNo: TEdit;
    EEVDiam: TEdit;
    BOK: TBitBtn;
    BLoad: TBitBtn;
    BSave: TBitBtn;
    OpenDialog: TOpenDialog;
    SaveDialog: TSaveDialog;
    Label27: TLabel;
    ETFuel: TEdit;
    Label50: TLabel;
    LCR: TLabel;
    ECR: TEdit;
    Label53: TLabel;
    EName: TEdit;
    Label54: TLabel;
    Label51: TLabel;
    EVoil: TEdit;
    GroupBox13: TGroupBox;
    Label56: TLabel;
    Label57: TLabel;
    Label58: TLabel;
    Label59: TLabel;
    EC: TEdit;
    EH: TEdit;
    EO: TEdit;
    EN: TEdit;
    GroupBox8: TGroupBox;
    Label44: TLabel;
    EIManfAFileName: TEdit;
    BIManfAEdit: TButton;
    BCdIVOut: TButton;
    Label30: TLabel;
    ECdIVOut: TEdit;
    Label42: TLabel;
    ECdEVIn: TEdit;
    BCdEVIn: TButton;
    Label61: TLabel;
    ECdIVIn: TEdit;
    BCdIvIn: TButton;
    Label62: TLabel;
    ECdEVOut: TEdit;
    BCdEVOut: TButton;
    Label46: TLabel;
    ELambda: TEdit;
    OpenDialog2: TOpenDialog;
    Model: TTabSheet;
    TabSheet2: TTabSheet;
    EWallTempFileName: TEdit;
    Image1: TImage;
    EWoshiniCoeff: TEdit;
    TabSheet3: TTabSheet;
    GroupBox9: TGroupBox;
    Label48: TLabel;
    Label63: TLabel;
    EEManfAFileName: TEdit;
    BEManfAEdit: TButton;
    EExhBackPFile: TEdit;
    GroupBox10: TGroupBox;
    Label64: TLabel;
    EInletGrid: TEdit;
    Label8: TLabel;
    EIVR: TEdit;
    Label9: TLabel;
    Label10: TLabel;
    EIVF: TEdit;
    EIVFR: TEdit;
    GroupBox11: TGroupBox;
    Label66: TLabel;
    EExhaustGrid: TEdit;
    Label11: TLabel;
    EEVR: TEdit;
    Label12: TLabel;
    EEVF: TEdit;
    Label13: TLabel;
    EEVFR: TEdit;
    RGIntegrator: TRadioGroup;
    Label14: TLabel;
    ECleanAirPresDropFn: TEdit;
    GroupBox12: TGroupBox;
    Label24: TLabel;
    Label25: TLabel;
    Label26: TLabel;
    EBurnAngle: TEdit;
    ESparkAngle: TEdit;
    CBSaveManfData: TCheckBox;
    GroupBox1: TGroupBox;
    Label7: TLabel;
    OpenDialog1: TOpenDialog;
    Button1: TButton;
    Button2: TButton;
    OpenDialog3: TOpenDialog;
    EIVD: TEdit;
    Label1: TLabel;
    EEVD: TEdit;
    Label31: TLabel;
    RadioGroup1: TRadioGroup;
    RB2Zone: TRadioButton;
    RB1Zone: TRadioButton;
    CBVariableGamma: TCheckBox;
    GroupBox14: TGroupBox;
    Label43: TLabel;
    EPerfSave: TEdit;
    BrowseExh: TButton;
    OpenDialog4: TOpenDialog;
    procedure ECCChanged(Sender: TObject);
    procedure BLoadClick(Sender: TObject);
    procedure BSaveClick(Sender: TObject);
    procedure BOKClick(Sender: TObject);
    Function ReadFromEdits : Boolean;
    Function LoadTextFile (FileName : String) : String;
    Function SaveTextFile (FileName : String) : String;
    procedure BCdIVOutClick(Sender: TObject);
    procedure BCdEVInClick(Sender: TObject);
    procedure BCdEVOutClick(Sender: TObject);
    procedure BCdIvInClick(Sender: TObject);
    procedure BIManfAEditClick(Sender: TObject);
    procedure BEManfAEditClick(Sender: TObject);
    procedure BBrouseInProfileClick(Sender: TObject);
    procedure BBrouseExProfileClick(Sender: TObject);
    procedure FormCreate(Sender: TObject);
    procedure FormShow(Sender: TObject);
    procedure Button1Click(Sender: TObject);
    procedure Button2Click(Sender: TObject);
    procedure EIVDChanged(Sender: TObject);
    procedure EEVDChanged(Sender: TObject);
    procedure RB1ZoneClick(Sender: TObject);
    procedure RB2ZoneClick(Sender: TObject);
    procedure OpenDialog2CanClose(Sender: TObject; var CanClose: Boolean);
    procedure BrowseExhClick(Sender: TObject);
  private
  public
  end; // TFEdit

var
  FEdit: TFEdit;
  ECode : Integer;
  EditsOK : Boolean;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

implementation

uses ErrorLog, FManfA;

{$R *.DFM}

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function StrToFloatf (St: String) : real;
begin
 try
  StrToFloatf := StrToFloat (St);
 except
  on EConvertError do
   begin
    MessageDlg ('An Error Occurred while trying to Convert' + #13
                 + 'the string "'+ St + '" to a real value',
                  mtWarning, [MBOK], 0);
    StrToFloatf := 0;
   end; // on EConvertError
  end; // Except

  end; //StrToFloatf

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function TFEdit.LoadTextFile (FileName : String) : String;
var InFile : TIniFile;
begin
  InFile := TIniFile.Create(FileName);
  With Infile do
  Try
  //Engine
   EName.Text := ReadString('Cylinders','Name','No Name Found');
   ENoCyls.Text := ReadString('Cylinders','NoCyls', '0');
   EBore.Text := ReadString('Cylinders','Bore', '0');
   EStroke.Text := ReadString('Cylinders','Stroke', '0');
   ECR.Text := ReadString('Cylinders','CR', '0');
   EConrodLength.Text := ReadString('Cylinders','ConrodLength', '0');
  // Heat Transfer
   EWallTempFileName.Text := ReadString('HeatTransfer','TempFile', 'Default.cwt');
   EWoshiniCoeff.Text := ReadString('HeatTransfer','CWoshini', '131');
  //Inlet
   EIManfAFileName.Text := ReadString('Inlet','AreaFile', 'Default.maf');
   ECleanAirPresDropFn.Text := ReadString('Inlet','FPlenumP', '99.0');
   EInletGrid.Text := ReadString('Inlet','InletGrid', '50');
   EIVR.Text := ReadString('Inlet','IVRFn', '0');
   EIVF.Text := ReadString('Inlet','IVFFn', '0');
   EIVFR.Text := ReadString('Inlet','IVFRFn', '0');
  //Exhaust
   EEManfAFileName.Text := ReadString('Exhaust','AreaFile', 'Default.maf');
   EExhBackPFile.Text := ReadString ('Exhaust','ExhBackFile', 'Default.exh');
   EExhaustGrid.Text := ReadString('Exhaust','ExhaustGrid', '50');
   EEVR.Text := ReadString('Exhaust','EVRFn', '0');
   EEVF.Text := ReadString('Exhaust','EVFFn', '0');
   EEVFR.Text := ReadString('Exhaust','EVFRFn', '0');
  //Cams
   EIVO.Text := ReadString('Cams','IVO', '0');
   EIVC.Text := ReadString('Cams','IVC', '0');
   EEVO.Text := ReadString('Cams','EVO', '0');
   EEVC.Text := ReadString('Cams','EVC', '0');
   EIVLift.Text := ReadString('Cams','IVLift', '0');
   EEVLift.Text := ReadString('Cams','EVLift', '0');
   EIVProfile.Text := ReadString('Cams','IVProfile', '');
   EEVProfile.Text := ReadString('Cams','EVProfile', '');
  //Valves
   EIVNo.Text := ReadString('Valves','IVNo', '0');
   EEVNo.Text := ReadString('Valves','EVNo', '0');
   EIVDiam.Text := ReadString('Valves','IVDiam', '0');
   EEVDiam.Text := ReadString('Valves','EVDiam', '0');
   ECdIVIn.Text := ReadString('Valves','CdIvIn', 'Default.vcd');
   ECdIVOut.Text := ReadString('Valves','CdIvOut', 'Default.vcd');
   ECdEVIn.Text := ReadString('Valves','CdEvIn', 'Default.vcd');
   ECdEVOut.Text := ReadString('Valves','CdEvOut', 'Default.vcd');
  //Fuel
   EBurnAngle.Text := ReadString('Fuel','BurnAngle', '55');
   ESparkAngle.Text := ReadString('Fuel','SparkAngle', 'Default.spk');
   EAFRatio.Text := ReadString('Fuel','AFRatio', '0');
   ETFuel.Text := ReadString('Fuel','TFuel', '0');
   EQFuel.Text := ReadString('Fuel','QFuel', '0');
   ELambda.Text := ReadString('Fuel','Lambda', '0.96');
   ETAtm.Text := ReadString('Conditions','TAtm', '0');
   EPAtm.Text := ReadString('Conditions','PAtm', '0');
   EvOil.Text := ReadString('Conditions','vOil', '0');
  //Calculation
   CBVariableGamma.Checked := (ReadString('Calculation','VariableGamma', '0') = '1');
   CBSaveManfData.Checked := (ReadString('Calculation','SaveManfData', '0') = '1');
   RGIntegrator.ItemIndex := StrToInt(ReadString('Calculation','Integrator', '0'));
   EPerfSave.Text := ReadString('Calculation','PerfDataSave', 'SimulDat.txt');
  Finally
   InFile.Free;
  end; // Try ... Finally
end; //TFEdit.LoadTextFile

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function TFEdit.SaveTextFile (FileName : String) : String;
var InFile : TIniFile;
begin
  InFile := TIniFile.Create(FileName);
  With Infile do
  Try
   WriteString('Cylinders', 'Name', EName.Text);
   WriteString('Cylinders', 'NoCyls', ENoCyls.Text);
   WriteString('Cylinders', 'Bore', EBore.Text);
   WriteString('Cylinders', 'Stroke', EStroke.Text);
   WriteString('Cylinders', 'CR', ECR.Text);
   WriteString('Cylinders', 'ConrodLength', EConrodLength.Text);

   WriteString('HeatTransfer','TempFile', EWallTempFileName.Text);
   WriteString('HeatTransfer', 'CWoshini', EWoshiniCoeff.Text);

   WriteString('Inlet', 'AreaFile', EIManfAFileName.Text);
   WriteString('Inlet', 'FPlenumP', ECleanAirPresDropFn.Text);
   WriteString('Inlet', 'InletGrid', EInletGrid.Text);
   WriteString('Inlet', 'IVRFn', EIVR.Text);
   WriteString('Inlet', 'IVFFn', EIVF.Text);
   WriteString('Inlet', 'IVFRFn', EIVFR.Text);

   WriteString('Exhaust', 'AreaFile', EEManfAFileName.Text);
   WriteString('Exhaust', 'ExhBackFile',EExhBackPFile.Text);
   WriteString('Exhaust', 'ExhaustGrid', EExhaustGrid.Text);
   WriteString('Exhaust', 'EVRFn', EEVR.Text);
   WriteString('Exhaust', 'EVFFn', EEVF.Text);
   WriteString('Exhaust', 'EVFRFn', EEVFR.Text);

   WriteString('Cams','IVO', EIVO.Text);
   WriteString('Cams','IVC', EIVC.Text);
   WriteString('Cams','EVO', EEVO.Text);
   WriteString('Cams','EVC', EEVC.Text);
   WriteString('Cams','IVLift', EIVLift.Text);
   WriteString('Cams','EVLift', EEVLift.Text);
   WriteString('Cams','IVProfile', EIVProfile.Text);
   WriteString('Cams','EVProfile', EEVProfile.Text);

   WriteString('Valves','IVNo', EIVNo.Text);
   WriteString('Valves','EVNo', EEVNo.Text);
   WriteString('Valves','IVDiam', EIVDiam.Text);
   WriteString('Valves','EVDiam', EEVDiam.Text);

   WriteString('Valves','CdIVIn', ECdIVIn.Text);
   WriteString('Valves','CdEVIn', ECdEVIn.Text);
   WriteString('Valves','CdIVOut', ECdIVOut.Text);
   WriteString('Valves','CdEVOut', ECdEVOut.Text);

   WriteString('Fuel','BurnAngle', EBurnAngle.Text);
   WriteString('Fuel','SparkAngle', ESparkAngle.Text);
   WriteString('Fuel','AFRatio', EAFRatio.Text);
   WriteString('Fuel','TFuel', ETFuel.Text);
   WriteString('Fuel','QFuel', EQFuel.Text);
   WriteString('Fuel', 'Lambda', ELambda.Text);

   WriteString('Conditions','TAtm', ETAtm.Text);
   WriteString('Conditions','PAtm', EPAtm.Text);
   WriteString('Conditions','vOil', EvOil.Text);

   if CBVariableGamma.Checked
     then WriteString('Calculation','VariableGamma', '1')
      else WriteString('Calculation','VariableGamma', '0');
   if CBSaveManfData.Checked
     then WriteString('Calculation','SaveManfData', '1')
      else WriteString('Calculation','SaveManfData', '0');
   WriteString('Calculation','Integrator', IntToStr(RGIntegrator.ItemIndex));
   WriteString('Calculation','PerfDataSave', EPerfSave.Text);
  Finally
   InFile.Free;
  end; // Try ... Finally
end; //TFEdit.SaveTextFile

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFEdit.ECCChanged(Sender: TObject);
var
 Bore, Stroke, Cylinders : Real;
 CCString : String;
begin
 Val (EBore.Text, Bore, ECode);
 Val (EStroke.Text, Stroke, ECode);
 Val (ENoCylS.Text, Cylinders, ECode);
 Str ((Cylinders * (Pi*sqr(Bore)/4) * Stroke /1000):4:0, CCString);
 ECapacity.Text := CCString;
end; //TFEdit.ECCChanged

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TFEdit.ReadFromEdits : Boolean;
var
 ErrorString : String;
 AllOK : Boolean;
begin
 AllOK := TRUE;
 With Engine2z do
  begin
   Try
    Atm.Tu := StrToFloatf (ETAtm.Text)+273.15;
    Atm.Pgas  := StrToFloatf (EPAtm.Text)*1000;
    Voil := StrToFloatf (EvOil.Text);

    Name := EName.Text;
    Bore := StrToFloatf(EBore.Text)/1000;
    Stroke := StrToFloatf(EStroke.Text)/1000;
    ConrodLength := StrToFloatf(EConrodlength.Text)/1000;
    NCyl := StrToInt(ENoCyls.Text);
    CR := StrToFloatf(ECR.Text);
    WallTemp.Load (EWallTempFileName.Text);
    WoshiniCoeff := StrToFloatf(EWoshiniCoeff.Text);

    ELog.Log ('Loading Manifolds');

    with Manifold do
     begin

      Plenum.Tu := StrToFloatf (ETAtm.Text)+273.15;
      ExhBack.Load(EExhBackPFile.Text);
      ExhBack.Patm := Atm.PGas;
      AInMAnf.AFileName := EIManfAFileName.Text;
      AExManf.AFileName := EEManfAFileName.Text;
      IGrid.FunctionString := EInletGrid.Text;
      EGrid.FunctionString := EExhaustGrid.Text;
      CleanAirPresFn.FunctionString := ECleanAirPresDropFn.Text;

      IVRFunc.FunctionString := EIVR.Text;
      IVFFunc.FunctionString := EIVF.Text;
      IVFRFunc.FunctionString := EIVFR.Text;

      EVRFunc.FunctionString := EEVR.Text;
      EVFFunc.FunctionString := EEVF.Text;
      EVFRFunc.FunctionString := EEVFR.Text;

     ELog.Log ('Loading Valves');
      IV.O  := 360 - StrToFloatf(EIVO.Text);
      IV.C  := -180 + StrToFloatf(EIVC.Text);
      EV.O  := 180 - StrToFloatf(EEVO.Text);
      EV.C  := -360 + StrToFloatf(EEVC.Text);

      IV.MaxLift  := StrToFloatf(EIVLift.Text)/1000;
      EV.MaxLift := StrToFloatf(EEVLift.Text)/1000;
      IV.ProfileFile := EIVProfile.Text;
      EV.ProfileFile := EEVProfile.Text;

      CdIVIn.CdFileName := ECdIVIn.Text;
      CdIVOut.CdFileName := ECdIVOut.Text;
      CdEVIn.CdFileName := ECdEVIn.Text;
      CdEVOut.CdFileName := ECdEVOut.Text;

      IV.No := StrToInt (EIVNo.Text);
      EV.No := StrToInt (EEVNo.Text);
      IV.D := StrToFloatf (EIVDiam.Text)/1000;
      EV.D  := StrToFloatf (EEVDiam.Text)/1000;
    end;

    ELog.Log ('Loading Fuel');
    Plenum.Fuel.Burnangle  := StrToFloatf (EBurnAngle.Text);
    Plenum.Fuel.AFRatio := StrToFloatf (EAFRatio.Text);
    Plenum.Fuel.T := StrToFloatf (ETFuel.Text)+273.15;
    Plenum.Fuel.Q := StrToFloat (EQFuel.Text)*1E6;
    Plenum.Fuel.Lambda := StrToFloatf (ELambda.Text);
    Plenum.Fuel.C := StrToInt (EC.Text);
    Plenum.Fuel.H := StrToInt (EH.Text);
    Plenum.Fuel.O := StrToInt (EO.Text);
    Plenum.Fuel.N := StrToInt (EN.Text);
    SparkAngle.Load(ESparkAngle.Text);

    Cyl.Fuel.Burnangle := Plenum.Fuel.Burnangle;
    Cyl.Fuel.C := Plenum.Fuel.C;
    Cyl.Fuel.H := Plenum.Fuel.H;
    Cyl.Fuel.O := Plenum.Fuel.O;
    Cyl.Fuel.N := Plenum.Fuel.N;
    Cyl.Fuel.Lambda := Plenum.Fuel.Lambda;
    Cyl.Fuel.T := Plenum.Fuel.T;
    Cyl.Fuel.Q := Plenum.Fuel.Q;
    Cyl.Fuel.AFRatio := Plenum.Fuel.AFRatio;

    Exh.Fuel.Burnangle := Plenum.Fuel.Burnangle;
    Exh.Fuel.C := Plenum.Fuel.C;
    Exh.Fuel.H := Plenum.Fuel.H;
    Exh.Fuel.O := Plenum.Fuel.O;
    Exh.Fuel.N := Plenum.Fuel.N;
    Exh.Fuel.Lambda := Plenum.Fuel.Lambda;
    Exh.Fuel.T := Plenum.Fuel.T;
    Exh.Fuel.Q := Plenum.Fuel.Q;

    VariableGamma := CBVariableGamma.Checked;
    SaveManfData := CBSaveManfData.Checked;
    Integrator := RGIntegrator.ItemIndex;
   except
    on EConvertError do
     begin
      ErrorString := 'Error Reading Data' + #13 + #13 +
                     'One of the Edit boxes has an incorrect or absent value';
      MessageDlg (ErrorString, mtWarning, [mbOk], 0);
      AllOK := FALSE;
     end; // on EConvertError
   end; // Try Except
  end; // With Engine
  ELog.Log ('All Edits Read');
  ReadFromEdits := AllOK;
end;  //TFEdit.ReadFromEdits

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFEdit.BLoadClick(Sender: TObject);
begin
 If OpenDialog.Execute then
   LoadTextFile (OpenDialog.FileName);
end; //TFEdit.BLoadClick

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFEdit.BSaveClick(Sender: TObject);
begin
 If SaveDialog.Execute then
   SaveTextFile(SaveDialog.FileName)
end; //TFEdit.BSaveClick

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFEdit.BOKClick(Sender: TObject);
begin
  Close;
end; //TFEdit.BOKClick

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFEdit.BCdIVOutClick(Sender: TObject);
begin
 Curr := IVOut;
 CurrFilename := ECdIVOut.Text;
 FIPol.ShowModal;
 ECdIVOut.Text := CurrFilename;
end;  //TFEdit.BCdIVOutClick

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFEdit.BCdEVInClick(Sender: TObject);
begin
  Curr := EVIn;
  CurrFilename := ECdEVIn.Text;
  FIPol.ShowModal;
  ECdEVIn.Text := CurrFilename;
end;  //TFEdit.BCdEVInClick

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFEdit.BCdEVOutClick(Sender: TObject);
begin
 Curr := EVOut;
 CurrFilename := ECdEVOut.Text;
 FIpol.ShowModal;
 ECdEVOut.Text := CurrFilename;
end;  //TFEdit.BCdEVOutClick

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFEdit.BCdIvInClick(Sender: TObject);
begin
  Curr := IVIn;
  CurrFilename := ECdIVIn.Text;
  FIpol.ShowModal;
  ECdIVIn.Text := CurrFilename;
end;  //TFEdit.BCdIvInClick

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFEdit.BIManfAEditClick(Sender: TObject);
begin
 vCurrA := cIManf;
 CurrAFilename := EIManfAFileName.Text;
 FManfArea.ShowModal;
 EIManfAFileName.Text := CurrAFileName;
end; //TFEdit.BIManfAEditClick

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFEdit.BEManfAEditClick(Sender: TObject);
begin
 vCurrA := cEManf;
 CurrAFileNAme := EEManfAFileName.Text;
 FManfArea.ShowModal;
 EEManfAFileName.Text := CurrAFileName;
end; //TFEdit.BEManfAEditClick

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFEdit.BBrouseInProfileClick(Sender: TObject);
begin
 OpenDialog2.FileName := EIVProfile.Text;
 If OpenDialog2.Execute then
  EIVProfile.Text := OpenDialog2.FileName;
end; //TFEdit.BBrouseInProfileClick

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFEdit.BBrouseExProfileClick(Sender: TObject);
begin
 OpenDialog2.FileName := EEVProfile.Text;
 If OpenDialog2.Execute then
  EEVProfile.Text := OpenDialog2.FileName;
end; //TFEdit.BBrouseExProfileClick

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFEdit.FormCreate(Sender: TObject);
begin

end; //TFEdit.FormCreate

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFEdit.FormShow(Sender: TObject);
begin
   EngineData.ActivePage := PCylinders;
end; //TFEdit.FormShow

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFEdit.Button1Click(Sender: TObject);
begin
  OpenDialog1.FileName := EWallTempFileName.Text;
 If OpenDialog1.Execute then
  EWallTempFileName.Text := OpenDialog1.FileName;
end; //TFEdit.Button1Click

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFEdit.Button2Click(Sender: TObject);
begin
 OpenDialog3.FileName := ESparkAngle.Text;
 If OpenDialog3.Execute then
   ESparkAngle.Text := OpenDialog3.FileName;
end; //TFEdit.Button2Click

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFEdit.EIVDChanged(Sender: TObject);
var
 IVO, IVC : Real;
 IVDString : String;
begin
 Val (EIVO.Text, IVO, ECode);
 Val (EIVC.Text, IVC, ECode);
 Str ((IVO + 180 + IVC):4:0, IVDString);
 EIVD.Text := IVDString;
end; //TFEdit.EIVDChanged

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFEdit.EEVDChanged(Sender: TObject);
var
 EVO, EVC : Real;
 EVDString : String;
begin
 Val (EEVO.Text, EVO, ECode);
 Val (EEVC.Text, EVC, ECode);
 Str ((EVO + 180 + EVC):4:0, EVDString);
 EEVD.Text := EVDString;
end; //TFEdit.EEVDChanged

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
procedure TFEdit.RB1ZoneClick(Sender: TObject);
begin
 CBVariableGamma.Enabled := TRUE;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFEdit.RB2ZoneClick(Sender: TObject);
begin
 CBVariableGamma.Enabled := FALSE;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFEdit.OpenDialog2CanClose(Sender: TObject;
  var CanClose: Boolean);
begin

end;
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFEdit.BrowseExhClick(Sender: TObject);
begin
  OpenDialog4.FileName := EExhBackPFile.Text;
 If OpenDialog4.Execute then
  EExhBackPFile.Text := OpenDialog4.FileName;
end;

end.
