//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// Pipe Data :Engine Simulation and Analysis (ESA)
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

unit Pipes;

interface
uses Classes, Profiles, Dialogs, FManfA;

Type
  TPipe = Class
   AvsL : TAManf;
   InsertL, InsertAt : Double;
   Function Area (L : Double): Double;
   Function dAdL (L : Double): Double;
   Function Length : Double;
   Constructor Create;
   Destructor Destroy; override;
  end; //TPipe

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

implementation


function TPipe.Area (L : Double): Double;
begin
  Area := AvsL.GetValue(L*1000)/1e6;
end; //TPipe.Area

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TPipe.dAdL (L : Double): Double;
var AreaGrad : Double;
begin
 if (L*1000-2) < 0 then
  dAdL := (AvsL.GetValue(L*1000+2)-AvsL.GetValue(L*1000))/1e6/0.002
 else if (AvsL.GetValue(L*1000+2)) = 0 then
  dAdL := (AvsL.GetValue(L*1000)-AvsL.GetValue(L*1000-2))/1e6/0.002
 else
  dAdL := (AvsL.GetValue(L*1000+2)-AvsL.GetValue(L*1000-2))/1e6/0.004;
end; //TPipe.dAdL

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TPipe.Length : Double;
begin
 Length := AvsL.index[AvsL.xcount]/1000;
end; //TPipe.Length

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Constructor TPipe.Create;
begin
 inherited;
end; //TPipe.Create

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Destructor TPipe.Destroy;
begin
  inherited;
end; //TPipe.Destroy

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
end.
