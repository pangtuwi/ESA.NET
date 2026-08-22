unit Adcreg;

interface

uses
  Classes, AdCalc;

procedure Register;

implementation

procedure Register;
begin
  RegisterComponents('Math', [TAdCalc]);
end;

end.