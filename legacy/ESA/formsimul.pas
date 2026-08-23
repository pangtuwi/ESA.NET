//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// Engine Simulation and Analysis (ESA)
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

unit FormSimul;

interface

uses
  Windows, Messages, SysUtils, Classes, Graphics, Controls, Forms, Dialogs,
  StdCtrls, Buttons, IniValues, ICEngine2z, ExtCtrls;

type
  TFSimulateOptions = class(TForm)
    Label1: TLabel;
    ERPM: TEdit;
    BitBtn1: TBitBtn;
    BitBtn2: TBitBtn;
    CBGasFlow: TCheckBox;
    Label3: TLabel;
    ENoCycles: TEdit;
    Bevel1: TBevel;
    Label2: TLabel;
    Label4: TLabel;
    EMassBalance: TEdit;
    Label5: TLabel;
    CBPV: TCheckBox;
    CBInCyl: TCheckBox;
    Bevel2: TBevel;
    Label6: TLabel;
    RBGraphsOn: TRadioButton;
    RBGraphsOff: TRadioButton;
    RBSelection: TRadioButton;
    procedure FormCreate(Sender: TObject);
    procedure FormClose(Sender: TObject; var Action: TCloseAction);
    procedure RBGraphsOnClick(Sender: TObject);
    procedure RBGraphsOffClick(Sender: TObject);
    procedure RBSelectionClick(Sender: TObject);
  private
    { Private declarations }
  public
    { Public declarations }
  end;

var
  FSimulateOptions: TFSimulateOptions;
  NoCycles : Integer;
  No1zCycles : Integer;
  MassBalance : Double;

implementation

uses Main, Edit;

{$R *.DFM}

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFSimulateOptions.FormCreate(Sender: TObject);
begin
 EnoCycles.Text := iniNoCycles;
 Erpm.Text := iniEngineSpeed;
 EMassBalance.Text := iniMassBalance;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFSimulateOptions.FormClose(Sender: TObject;
  var Action: TCloseAction);
begin
 try
  NoCycles := StrToInt(EnoCycles.Text);
  No1zCycles := 1;
  MassBalance := StrToFloat(EMassBalance.Text);
  Engine2z.Nrpm := StrToFloat(Erpm.Text);
    if Engine2z.Nrpm < 1250 then
   begin
    Action := caNone;
    Erpm.Text := '1250';
   end;
  if Engine2z.Nrpm > 7000 then
   begin
    Action := caNone;
    Erpm.Text := '7000';
   end;
 Except

 end; //Try Except
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFSimulateOptions.RBGraphsOnClick(Sender: TObject);
begin
 CBGasFlow.Checked := True;
 CBPV.Checked := True;
 CBInCyl.Checked := True;
 CBGasFlow.Enabled := False;
 CBPV.Enabled := False;
 CBInCyl.Enabled := False;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFSimulateOptions.RBGraphsOffClick(Sender: TObject);
begin
 CBGasFlow.Checked := False;
 CBPV.Checked := False;
 CBInCyl.Checked := False;
 CBGasFlow.Enabled := False;
 CBPV.Enabled := False;
 CBInCyl.Enabled := False;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TFSimulateOptions.RBSelectionClick(Sender: TObject);
begin
 CBGasFlow.Checked := False;
 CBPV.Checked := False;
 CBInCyl.Checked := False;
 CBGasFlow.Enabled := True;
 CBPV.Enabled := True;
 CBInCyl.Enabled := True;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
end.
