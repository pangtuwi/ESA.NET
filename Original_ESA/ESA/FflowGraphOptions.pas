//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// FflowGraphOptions : Engine Simulation and Analysis (ESA)
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

unit FflowGraphOptions;

interface

uses
  Windows, Messages, SysUtils, Classes, Graphics, Controls, Forms, Dialogs,
  StdCtrls, Buttons, ExtCtrls;

type
  TFGraphOptions = class(TForm)
    BOK: TBitBtn;
    RGFlowGraph: TRadioGroup;
    BitBtn1: TBitBtn;
    GroupBox1: TGroupBox;
    Label1: TLabel;
    Label2: TLabel;
    EGFYMax: TEdit;
    EGFYMin: TEdit;
    RGPVGraph: TRadioGroup;
    GroupBox2: TGroupBox;
    Label3: TLabel;
    Label4: TLabel;
    EPVYMax: TEdit;
    EPVYMin: TEdit;
    RGInCylGraph: TRadioGroup;
    GroupBox3: TGroupBox;
    Label5: TLabel;
    Label6: TLabel;
    EICYMax: TEdit;
    EICYMin: TEdit;
  private
    {Private declarations}
  public
    {Public declarations}
  end; //TFGraphOptions

var
  FGraphOptions: TFGraphOptions;

implementation

{$R *.DFM}

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
end.
