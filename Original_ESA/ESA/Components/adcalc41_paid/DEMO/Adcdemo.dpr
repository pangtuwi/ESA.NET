program Adcdemo;

uses
  Forms,
  Main in 'Main.pas' {MainForm},
  AddF in 'AddF.pas' {AddFunct},
  AddV in 'AddV.pas' {AddVar};

begin
  Application.Initialize;
  Application.CreateForm(TMainForm, MainForm);
  Application.CreateForm(TAddFunct, AddFunct);
  Application.CreateForm(TAddVar, AddVar);
  Application.Run;
end.
