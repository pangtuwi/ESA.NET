//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// Valves : Engine Simulation and Analysis (ESA)
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

unit Valves;

interface
uses Classes, Profiles, Dialogs, IpolTab;

Type
  TValve = Class
   No : Integer; // Number
   O, C : Double; // Open, Close
   D : Double; // Diameter
   CdForward, CdReverse : TCdValve;
   MaxLift : Double;
   ProfileFile : String;
   Profile : TProfile;
   Function Lift (Theta : Double) : Double;
   Function Open (Theta : Double) : Boolean;
   Function FlowArea (Theta : Double) : Double;
   Function FlowCoeff (Theta, PRatio : Double;
                           Reverse : Boolean) : Double;
   Constructor Create;
   Destructor Destroy; override;
  end; // TValve

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

implementation

Function TValve.FlowCoeff (Theta, PRatio : Double;
                           Reverse : Boolean) : Double;
var
 LRatio,Coeff : Double;
begin
  if PRatio > 5 then
   begin
    FlowCoeff := 0.7;
    Exit;
   end;
  LRatio := Lift(Theta) / MaxLift;
  if LRatio = 0 then FlowCoeff := 0 else
  if Reverse
   then Coeff := CdReverse.GetValue (PRatio, LRatio)
    else Coeff := CdForward.GetValue (PRatio, LRatio);
  FlowCoeff := Coeff;
end; //TValve.FlowCoeff

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function TValve.FlowArea (Theta : Double) : Double;
begin
 FlowArea := Lift(Theta) * pi * D * No;
end; //TValve.FlowArea

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function TValve.Lift (Theta : Double) : Double;
var
 UnitAngle, UnitLift : Double;
 Cnew, ONew : Double;
begin
  if O > C then
   begin
    Cnew := C + 720;
    if Theta < 0 then Theta := Theta + 720;
   end;
  if (Theta < O) or (Theta > CNew) then
   begin
    UnitLift := 0;
   end
  else
   begin
    UnitAngle := (Theta - O) / (CNew - O);
    UnitLift := Profile.Gety (UnitAngle);
   end; // if then else
  Lift := UnitLift * MaxLift;
end; //TValve.Lift

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function TValve.Open (Theta : Double) : Boolean;
begin
 if Lift (Theta) > 0 then Open := True else OPen := false;
end; //TValve.Open

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Constructor TValve.Create;
begin
 inherited;
 Profile := TProfile.Create;
end; //TValve.Create

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Destructor TValve.Destroy;
begin
  Profile.Free;
  inherited;
end; //TValve.Destroy

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
end.
