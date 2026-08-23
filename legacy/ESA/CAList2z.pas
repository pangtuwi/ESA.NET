//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// CAList2z : Engine Simulation and Analysis (ESA)
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

unit CAList2z;

interface

uses
 ICEngine2z, Dialogs;

const
 NoVals = 28;

type
  TCAPoint = Class
   Value : Array[1..NoVals] of Double;
   Property V :    Double Read value[1];
   Property P :    Double Read value[2];
   Property mcyl : Double Read value[3];
   Property mb :   Double Read value[4];
   Property mu :   Double Read value[5];
   Property min :  Double Read value[6];
   Property mout : Double Read value[7];
   Property Tb :  Double Read value[10];
   Property Tu :  Double Read value[11];
   Property IVA :  Double Read value[16];
   Property EVA :  Double Read value[17];
   Property Uin :  Double Read value[18];
   Property Uex :  Double Read value[19];
   Property Pin :  Double Read value[20];
   Property Pex :  Double Read value[21];
  end;  //TCAPoint

  TCAList = Class
   CaVar : Array [-359..360] of TCAPoint;
   ColName : Array[1..NoVals] of String;
   Decimals : Array[1..NoVals] of Integer;
   k : Array[1..NoVals] of Double; //scale Factor
   Constructor Create;
   Destructor Destroy; Override;
   procedure UpdateCApoint;
   procedure SendTofile (fileName : String);
  end; //TCAList

var
  CurrCAList : TCAList;

implementation

uses SysUtils; // FloattoStrF

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

constructor TCAList.Create;
var
 LC : Integer;
begin
  inherited create;
  for LC := -359 to 360 do
    caVar[LC] := TCAPoint.Create;
  ColName [ 1] := 'Vcyl';    Decimals [ 1] := 2;   k[ 1] := 1e6;
  ColName [ 2] := 'PCyl';    Decimals [ 2] := 0;   k[ 2] := 1;
  ColName [ 3] := 'Mcyl';    Decimals [ 3] := 2;   k[ 3] := 1e6;
  ColName [ 4] := 'Mb';      Decimals [ 4] := 2;   k[ 4] := 1e6;
  ColName [ 5] := 'Mu';      Decimals [ 5] := 2;   k[ 5] := 1e6;
  ColName [ 6] := 'Min';     Decimals [ 6] := 2;   k[ 6] := 1e6;
  ColName [ 7] := 'Mout';    Decimals [ 7] := 2;   k[ 7] := 1e6;
  ColName [ 8] := 'Vb';      Decimals [ 8] := 2;   k[ 8] := 1e6;
  ColName [ 9] := 'Vu';      Decimals [ 9] := 2;   k[ 9] := 1e6;
  ColName [10] := 'Tb';      Decimals [10] := 1;   k[10] := 1;
  ColName [11] := 'Tu';      Decimals [11] := 1;   k[11] := 1;
  ColName [12] := 'Qb';      Decimals [12] := 3;   k[12] := 1;
  ColName [13] := 'Qu';      Decimals [13] := 3;   k[13] := 1;
  ColName [14] := 'Gamma';   Decimals [14] := 3;   k[14] := 1;
  ColName [15] := 'FuelM';      Decimals [15] := 1;   k[15] := 1;
  ColName [16] := 'IV A';    Decimals [16] := 1;   k[16] := 1e6;
  ColName [17] := 'EV A';    Decimals [17] := 1;   k[17] := 1e6;
  ColName [18] := 'IV V';    Decimals [18] := 3;   k[18] := 1;
  ColName [19] := 'EV V';    Decimals [19] := 3;   k[19] := 1;
  ColName [20] := 'IV P';    Decimals [20] := 0;   k[20] := 1;
  ColName [21] := 'EV P';    Decimals [21] := 0;   k[21] := 1;
  ColName [22] := 'WWork';   Decimals [22] := 3;   k[22] := 1;
  ColName [23] := 'PWork';   Decimals [23] := 3;   k[23] := 1;
  ColName [24] := 'CO';      Decimals [24] := 2;   k[24] := 1e3;
  ColName [25] := 'NO';      Decimals [25] := 2;   k[25] := 1e3;
  ColName [26] := 'CO2';     Decimals [26] := 2;   k[26] := 1e3;
  ColName [27] := 'HC';      Decimals [27] := 2;   k[27] := 1e3;
  ColName [28] := 'htLoss';   Decimals [28] := 3;   k[28] := 1;
end; // TCAList.Create

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TCAList.UpdateCApoint;
var
 CAIndex : Integer;
begin
  with Engine2z do
   begin
     CAIndex := Round(CA);
     if (CAIndex < -359) or (CAIndex > 360) then
      begin
       ShowMessage ('Incorrect CA to CAList');
       Exit;
      end;
     CaVar[CAIndex].Value[ 1] :=  Cyl.Vgas; //Cylinder Volume
     CaVar[CAIndex].Value[ 2] :=  Cyl.Pgas; //Cylinder Pressure
     CaVar[CAIndex].Value[ 3] :=  Cyl.mgas; //Total Cylinder Mass
     CaVar[CAIndex].Value[ 4] :=  Cyl.mb; //Cylinder Mass Burned
     CaVar[CAIndex].Value[ 5] :=  Cyl.mu; //Cylinder Mass Unburned
     CaVar[CAIndex].Value[ 6] :=  Min; //MassIn
     CaVar[CAIndex].Value[ 7] :=  Mout; //MassOut
     CaVar[CAIndex].Value[ 8] :=  Cyl.Vb; //Burned Volume
     CaVar[CAIndex].Value[ 9] :=  Cyl.Vu; //Unburned Volume
     CaVar[CAIndex].Value[10] :=  Cyl.Tb; //Burned Temperature
     CaVar[CAIndex].Value[11] :=  Cyl.Tu; //Unburned Temperature
     CaVar[CAIndex].Value[12] :=  Qb; //Burned Heat Transfer
     CaVar[CAIndex].Value[13] :=  Qu; //Unburned Heat Transfer
     CaVar[CAIndex].Value[14] :=  Cyl.Gamma; //Gamma
     CaVar[CAIndex].Value[15] :=  Cyl.Fuel.M; //Cylinder Fuel Mass
     CaVar[CAIndex].Value[16] :=  Manifold.IV.FlowArea(CA); //IValve Flow Area
     CaVar[CAIndex].Value[17] :=  Manifold.EV.FlowArea(CA); //EValve Flow Area
     CaVar[CAIndex].Value[18] :=  UInlet; //Inlet Gas Velocity  @ Valve
     CaVar[CAIndex].Value[19] :=  UExhaust; //Exhaust Gas Velocity @ Valve
     CaVar[CAIndex].Value[20] :=  PInlet;  //Pressure @ Inlet Valve
     CaVar[CAIndex].Value[21] :=  PExhaust; //Pressure @ Exhaust Valve
     CaVar[CAIndex].Value[22] :=  WWork; //Indicated Work
     CaVar[CAIndex].Value[23] :=  PWork; //Pumping Work
     CaVar[CAIndex].Value[24] :=  Emmissions[6]; //CO
     CaVar[CAIndex].Value[25] :=  Emmissions[7]; //NO;
     CaVar[CAIndex].Value[26] :=  Emmissions[10]; //CO2;
     CaVar[CAIndex].Value[27] :=  0; //HC
     CaVar[CaIndex].Value[28] :=  HeatLoss; //HeatLoss
  end;
end; // UpdatePoint

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TCAList.SendTofile (fileName : String);
var
 Tf : TextFile;
 LC, i : Integer;                       
begin
 Assignfile (TF, filename);
 Rewrite (TF);
 Write (TF, 'CA, ');
 for i := 1 to Novals-1 do   Write(TF, colName[i], ', ');
 WriteLn (TF, ColName[NoVals]);

 for LC := -359 to 360 do
  begin
   Write(TF, LC, ', ');
   with CaVar[LC] do
    begin
     For i := 1 to Novals-1 do Write (TF, value[i]*k[i]:10:Decimals[i], ', ');
     Writeln (TF, value[NoVals]*k[i]:10:Decimals[NoVals]);
    end;
  end;
 Close (TF);
end; //TCAList.SendTofile

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Destructor TCAList.Destroy;
var
 LC : Integer;
begin
  for LC := -359 to 360 do
   CaVar[LC].Free;
  inherited;
end; //TCAList.Destroy

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

end.
