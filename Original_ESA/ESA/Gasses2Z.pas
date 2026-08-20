//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// 2 Zone Gas Unit : Engine Simulation and Analysis (ESA)
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

unit Gasses2Z;

interface

uses Classes, GasProps, Fuel, Dialogs;
const Runiversal = 287;

type
 TGas2Z = Class
   Pgas, mgas, mb, mu, Vgas, Vu, Vb, dVdTheta, xb, Tb, Tu : Double;
   ugas, hgas, Rgas, dmbdTheta, dmindtheta, dmoutdtheta, hin : Double;
   Gamma : Double;
   Fuel : TFuel;
   Rb,hb,ub,Cpb,dudTb,dudpb,dudFb : Double;
   Ru,hu,uu,Cpu,dudTu,dudpu,dudFu : Double;
   err : integer;

   ThetaSpark : Double;
   Burnt, Unburnt : TProp;
   Function Tgas : Double;
   Procedure UpdateB (x1, V1, dVdtheta1, Vb1, P1, Tb1, Tu1 : Double);
   Procedure UpdateUB (V1, dVdtheta1, Vb1, P1, Tu1 : Double);
   Procedure UpdateBD (V1, dVdtheta1, Vb1, P1, Tb1 : Double);
   Procedure UpdateGE (V1, dVdtheta1, Vb1, P1, Tb1, Tu1 : Double);
   Function dxdTheta (Thetarad : Double) : Double;
   constructor Create;
   Destructor Destroy; override;
  private
   Function xburnt (Thetarad : Double) : Double;
 end; //TGas2z

implementation

var err : Integer; // GasProps Error

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function TGas2z.Tgas : Double;
begin
 if mb = 0 then xb := 0 else xb := mb/mgas;
 Tgas := xb*Tb + (1-xb)*Tu;
end; //TGas2z.Tgas

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Procedure TGas2z.UpdateB (x1, V1, dVdtheta1, Vb1, P1, Tb1, Tu1 : Double);
begin
 //Set Variables as passed
  Pgas := P1;
  Tb := Tb1;
  Tu := Tu1;
  Vb := Vb1;
  Vgas := V1;
  if Vb > Vgas then Vb := Vgas; //##?? iffy line???
  dVdTheta := dVdTheta1;
  Vu := Vgas-Vb;

 //Check if Burning
  xb := xburnt(x1);
  if xb < 0.01 then xb := 0.01;
  if 1-xb < 0.01 then xb := 0.99;
  if (xb>0) and (xb<1) then
   begin
    mb := xb*mgas;
    mu := (1-xb)*mgas;
   end;

   unBurnt.ReturnProps(Pgas,Tu,Ru,hu,uu,Cpu,dudTu,dudPu,dudFu,err);
   Burnt.ReturnProps(Pgas,Tb,Rb,hb,ub,Cpb,dudTb,dudPb,dudFb,err);
  ugas := xb*ub + (1-xb)*uu;
  Rgas := xb*Rb + (1-xb)*Ru;
  hgas := (1-xb)*hu + xb*hb;
  Gamma := (1-xb)*Unburnt.Get_Gamma(Pgas,Tu,err) +
            xb*Burnt.Get_Gamma(Pgas,Tb, err);

  dMbdtheta := dxdtheta(x1)*mgas;
end; //TGas2z.UpdateB

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Procedure TGas2z.UpdateBD (V1, dVdtheta1, Vb1, P1, Tb1 : Double);
begin
  Pgas := P1;
  Tb := Tb1;
  Tu := Tb1;
  Vb := V1;
  Vgas := V1;
  dVdTheta := dVdTheta1;
  Vu := 0;
  mu := 0;
  mb := mgas;

  Burnt.ReturnProps(Pgas,Tb,Rb,hb,ub,Cpb,dudTb,dudPb,dudFb,err);
  ugas := ub;
  Rgas := Rb;
  hgas := hb;
  Gamma := Burnt.Get_Gamma(Pgas,Tb, err);
  dMbdtheta := 0;
end; //TGas2z.UpdateBD

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Procedure TGas2z.UpdateUB (V1, dVdtheta1, Vb1, P1, Tu1 : Double);
begin
  Pgas := P1;
  Tb := Tu1;
  Tu := Tu1;
  Vu := V1;
  Vgas := V1;
  dVdTheta := dVdTheta1;
  Vb := 0;
  mb := 0;
  mu := mgas;

  UnBurnt.ReturnProps(Pgas,Tu,Ru,hu,uu,Cpu,dudTu,dudPu,dudFu,err);
  ugas := uu;
  Rgas := Ru;
  hgas := hu;
  Gamma := UnBurnt.Get_Gamma(Pgas,Tu, err);
  dMbdtheta := 0;
end; //TGas2z.UpdateUB

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Procedure TGas2z.UpdateGE (V1, dVdtheta1, Vb1, P1, Tb1, Tu1 : Double);
begin
 //Set Variables as passed
  Pgas := P1;
  Tb := Tu1;
  Tu := Tu1;
  Vb := 0;
  Vu := 0;
  Vgas := V1;
  dVdTheta := dVdTheta1;

  UnBurnt.ReturnProps(Pgas,Tu,Ru,hu,uu,Cpu,dudTu,dudPu,dudFu,err);
  ugas := uu;
  Rgas := Ru;
  hgas := hu;
  Gamma := UnBurnt.Get_Gamma(Pgas,Tu, err);
  dMbdtheta := 0;
end; //TGas2z.UpdateGE

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function TGas2z.xburnt (Thetarad : Double) : Double;
var
 Theta, Temp : Double;
begin
  Theta := ThetaRad*180/pi;

  if Theta <= ThetaSpark then temp := 0
  else  if (Theta > ThetaSpark) and (Theta < (ThetaSpark + Fuel.Burnangle))
       then temp := 0.5 * (1- Cos (Pi *(Theta - ThetaSpark)/Fuel.BurnAngle))
        else temp := 1;
  xburnt := temp;
end; //TGas2z.xburnt

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function TGas2z.dxdTheta (Thetarad : Double) : Double;
// Sin distribution
var
 Theta, Temp : Double;
begin
  Theta := ThetaRad*180/pi;
  if Theta <= ThetaSpark then temp := 0
  else  if (Theta > ThetaSpark) and (Theta < (ThetaSpark + Fuel.Burnangle))
       then temp := 0.5 * Pi/ (Fuel.Burnangle*pi/180) *
        Sin (Pi *(Theta - ThetaSpark)/Fuel.BurnAngle)
        else temp := 0;
  dxdtheta := temp;
end; //TGas2z.dxdTheta

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Constructor TGas2z.Create;
begin
 inherited create;
 Fuel := TFuel.Create;
 Burnt := TProp.Create(1);
 unBurnt := TProp.Create(0);
 xb := 0;
end; //TGas2z.Create

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Destructor TGas2z.Destroy;
begin
 Burnt.Free;
 Unburnt.Free;
 Fuel.Free;
 inherited Destroy;
end; //TGas2z.Destroy

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
end.
