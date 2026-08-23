//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// Engine2z Class by Paul Williams
// Engine Simulation and Analysis
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// Heat transfer : burnt and Unburnt Zones : Radiation
// All variables in S.I Units
// Forced EGR read in.

unit ICEngine2z;

interface

uses
 Valves,Manifolds, Eqbm, Gasses2Z, Classes, PNTWMath, Dialogs, RKF5,
 Fuel, IPolTab, FManfA, SysUtils, WallTemps, VarSpeedList;

const
  Compression = 1;
  Combustion = 2;
  Expansion = 3;
  Exhaust = 4;
  Overlap = 5;
  Intake = 6;

Type
 TEngine2z = Class(TRKF)
   Name : ShortString;
   NoZones : Integer;
   INIT2Z, TWOZOVERLAP: Boolean;
   SAVEMANFDATA : BOOLEAN;
   PlenumP6000 : Double;
   State, OldState : Integer; //uses Compression..Intake above;
   CA : Double;       //Current State
   dCA : Double;      //Crank angle Step size
   Plenum, Exh, Cyl, Atm : TGas2z;      // Various Gasses
   PCylIVC, TCylIVC, VCylIVC : Double; // Conditions at start of Compression
   WorkDone, HeatLoss : Double;
   NCyl, Bore, Stroke, CR, ConrodLength : Double; //Geometry
   Nrpm : Double; // Engine2z Speed
   SparkAngle : TVarSpeedList;
   WallTemp : TWallTemps;
   FireOrder : ShortString; // Firing Order
   Manifold : TManifolds; // Manifolds
   VOil : Double; // Viscosity of Oil
   ForcedEgr : Double; // Exhaust Gas Recirculation
   wcrank : Double;   // rad/s Engine2z speed
   IncylEGR : Double;
   tstep : integer;
   IPt, EPt : Double; // Initial Valve Pressure Conditions
   MIn, Mout, PInlet, PExhaust, UInlet, UExhaust : Double;
   MRecirculated, NewAirMass : Double;
   dPMass : Double;
   Emmissions : EqSpecArray;
   PWork, WWork, Vd,
   FMEP, IMEP, PMEP, BMEP,
   Torque, BPower, IPower, HPower,
   SFC, MEff, Eff, ThEff, PmanIVC, HEff, MassAtm, mf, Veff : Double;
   PMax, TMax, UIMax, UEMax : Double;
   TotalMInIV, TotalMOutEV, TotalMass, MbOutInlet, MuOutExhaust : Double;
   Qb, Qu, Q : Double;
   QFuel,QHeat,QWork,QExht,QPump,QFric, FEnergy : Double; //Energy Balance
   ResidialFraction : Double;
   WoshiniCoeff : double;
   NCycles : Integer; //number of cycles to simulate
   VariableGamma : Boolean;
   Procedure Run;                               // main procedure
   Procedure Performance;                              // Results
   Procedure InitVars (var AllOK : Boolean);           //Initialisation
   Function getState (Theta : Double) : Integer;
   Constructor Create;
   Destructor Destroy; override;
   Function TFMEP : Double;
   Function hWoshini (P, T : Double) : Double;
  private
   Function VCyl (ThetaRad : Double) : Double;
   Function dVCyldTheta (ThetaRad : Double) : Double;
   Function dVCyldT(ThetaRad : Double) : Double;
   Function Capacitycc : Double;
   Function WallArea (ThetaRad : Double) : Double;
   Function dQbdtheta (x1 : Double; y1 : yarray) : Double;
   Function dQudTheta (x1 : Double; y1 : yarray) : Double;
   Function dQldTheta1z (x1 : Double; y1 : yarray) : Double;

 end; //TEngine2z

 EEngineError = class(Exception);
  
var
 Engine2z : TEngine2z;
 TF : TextFile;

implementation

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function TEngine2z.VCyl (Thetarad : Double) : Double;
Begin
 If ThetaRad > (4*Pi) then
  begin
   ShowMessage ('Vcyl called with incorrect Theta (deg not rad)');
  end;
  VCyl := ((pi/4*sqr(Bore)*Stroke)/(CR-1))*
            (1 + (CR-1)/2*(1 + 2*ConrodLength/Stroke - cos(ThetaRad)-
            sqrt(sqr(2*ConrodLength/Stroke) - sqr(sin(ThetaRad)))));
end; //TEngine2z.VCyl

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function TEngine2z.dVCyldTheta (Thetarad : Double) : Double;
begin
  dVCyldTheta := pi/8*sqr(Bore)*Stroke*sin(ThetaRad)*
             (1 + (Stroke/2/ConrodLength*cos(ThetaRad))/
             sqrt(1 - sqr(Stroke/2/ConrodLength*sin(ThetaRad))));
end; //TEngine2z.dVCyldTheta

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function TEngine2z.GetState (Theta : Double) : Integer;
begin
 if Theta < Manifold.EV.C then Getstate := Overlap
  else if Theta < Manifold.IV.C
        then Getstate := Intake
   else if Theta < Cyl.ThetaSpark
         then Getstate := Compression
    else if Theta < Cyl.ThetaSpark+Cyl.Fuel.Burnangle
          then Getstate := Combustion
     else if Theta < Manifold.EV.O
           then Getstate := Expansion
      else if Theta < Manifold.IV.O
            then Getstate := Exhaust
       else Getstate := Overlap;
end; //TEngine2z.GetState

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function TEngine2z.CapacityCC : Double;
// Returns : Engine2z Capacity in cc
begin
  CapacityCC := NCyl * (Pi*sqr(Bore)/4) * Stroke * 1E6;
end; //TEngine2z.CapacityCC

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function TEngine2z.WallArea (Thetarad : Double) : Double;
begin
 WallArea := (VCyl(Thetarad) * 4/ Bore); //Side wall area
end; //TEngine2z.WallArea

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function dWdTheta (x1 : Double; y1 : yarray) : Double;
begin
   dWdTheta := y1[2] * Engine2z.dVCyldTheta(x1);
end; //dWdTheta

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function TEngine2z.dVCyldt (ThetaRad : Double) : Double;
begin
  dVCyldt := dVCyldTheta(ThetaRad)*2*pi*(Nrpm/60);
end;  //TEngine2z.dVCyldt

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// Heat Loss...
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function TEngine2z.hWoshini (P, T : Double) : Double;
const
 C2 = 3.24E-3;
var
 C1, w : Double;
 Vswept : Double; // Swept volume
 Umeanpiston : Double; // Mean piston speed
 PRef, VRef, Pmot, VMot : Double;
begin
  case State of
   Compression, Combustion, Expansion : C1 := 2.28;
   Exhaust, Overlap, Intake : C1 :=6.18;
  end; //case
  Umeanpiston := 2*stroke*Nrpm/60;  // + Swirl factor if you like
  PRef := PCylIVC;
  VRef := VCylIVC;
  Vmot := VCyl(x);
  Pmot := Pref*pwr(Vref/Vmot,1.30); //Isentropic Expansion
  Umeanpiston := 2*stroke*Nrpm/60;
  Vswept := VCyl(Pi) * CR/(CR+1);
  w := C1 * Umeanpiston +
       C2 * (Vswept*TCylIVC) / (PCylIVC*VCylIVC) * (P-Pmot);

 { w := C1 * Umeanpiston +
       C2 * Vswept * TCylIVC *(P-PCylIVC)/(PCylIVC*VCylIVC);}
  hwoshini :=  WoshiniCoeff * Pwr(Bore, -0.2) * Pwr(P/101325, 0.8)  //131
              *Pwr (T, -0.53) * Pwr(w, 0.8); // Woshini's Equation

end; //TEngine2z.hWoshini

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function dQldTheta (x1 : Double; y1 : yarray) : Double;
var Temp : Double;
begin
 dQldTheta := -1*(Engine2z.dQbdTheta(x1,y1) + Engine2z.dQudTheta(x1,y1));
end; //dQldTheta

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function TEngine2z.dQldtheta1z (x1 : Double; y1 : yarray) : Double;
var
 LinerRatio, TavgLiner, PistonArea, WallA : Double;
 Qb : Double;
begin
  WallA := wallArea(x1);
  PistonArea := pi*sqr(Bore) / 4;
  LinerRatio := VCyl(x1) / VCyl(pi);
  TavgLiner := LinerRatio * WallTemp.TULiner(Nrpm) + (1-LinerRatio) *
         WallTemp.TLLiner(Nrpm);
  with Cyl do
   Qb := hwoshini(Pgas,Tb)*
         (PistonArea*(Tb-WallTemp.TPiston(Nrpm)+
                      Tb-WallTemp.Thead(Nrpm))
                      +WallA*(Tb-TavgLiner));
  dQldtheta1z := -Qb/wcrank;
end;  //TEngine2z.dQldtheta1z

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function TEngine2z.dQbdtheta (x1 : Double; y1 : yarray) : Double;
var
 LinerRatio, TavgLiner, PistonArea, WallA : Double;
 Qb : Double;
begin
  WallA := wallArea(x1);
  PistonArea := pi*sqr(Bore) / 4;
  LinerRatio := VCyl(x1) / VCyl(pi);
  TavgLiner := LinerRatio * WallTemp.TULiner(Nrpm) + (1-LinerRatio) *
            WallTemp.TLLiner(Nrpm);
  with Cyl do
   begin
    Case State of
     Combustion :
      Qb := hWoshini(Pgas, Tb)*Vb/Vgas*PistonArea*(Tb-WallTemp.TPiston(Nrpm)+
         Tb-WallTemp.THead(Nrpm));
     Intake : Qb := 0;
     Compression : Qb := 0;
     else
      Qb := hwoshini(Pgas,Tb)*(PistonArea*(Tb-WallTemp.TPiston(Nrpm)+
         Tb-WallTemp.Thead(Nrpm))+WallA*(Tb-TavgLiner));
    end; //case
   end; // with Cyl
  dQbdtheta := -Qb/wcrank;
end;  //TEngine2z.dQbdtheta

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function TEngine2z.dQudtheta (x1 : Double; y1 : yarray) : Double;
var
 LinerRatio, TavgLiner, PistonArea, WallA : Double;
 Qu : Double;
begin
  WallA := wallArea (x1);
  PistonArea := pi *sqr(Bore) / 4;
  LinerRatio := VCyl (x1)/ VCyl (pi);
  TavgLiner := LinerRatio * WallTemp.TULiner(Nrpm) + (1-LinerRatio) *
     WallTemp.TLLiner(Nrpm);
  with Cyl do
   begin
    Case State of
     Combustion :
        Qu := hwoshini(Pgas, Tu)*(Vu/Vgas*PistonArea*(Tu-WallTemp.TPiston(Nrpm)
          +Tu-WallTemp.Thead(Nrpm))  +WallA*(Tu-TavgLiner));
      Expansion : Qu := 0;
      Exhaust : Qu := 0;
     else
        Qu :=hwoshini(Pgas, Tu)*(PistonArea*(Tu-WallTemp.TPiston(Nrpm)+
        Tu-WallTemp.Thead(Nrpm)) +WallA*(Tu-TavgLiner))
    end; //case
   end;//with Cyl
  dQudTheta := -Qu/wcrank;
end;  //Qu

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function InitialTb (P, Tu : Double) : Double;
//iteration of isenthalpic combustion (only at initial Tb)
var
 Tb, deltaTb, hu, hb, cpb : Double;
 err, iters : integer;
begin
 Tb := 2000;
 iters := 0;
 repeat
   hu := Engine2z.Cyl.unburnt.Get_h(P, Tu, err);
   hb := Engine2z.Cyl.burnt.Get_h(P, Tb, err);
   cpb := Engine2z.Cyl.burnt.Get_Cp(P, Tb, err);
   deltaTb := (hu-hb)/cpb;
   Tb := Tb + deltaTb;
   inc(iters);
 until (abs(DeltaTb) < 0.1) or (iters > 1000);
 if iters > 1000 then
  begin
   showMessage('Error Estimating Initial Burned Gas Temp...');
   Halt;
  end;
 InitialTb := Tb;
end; //InitialTb

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// Dummy Equation : Integration
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function Zero (x1 : Double; y1 : yarray) : Double;
begin
 Zero := 0;
end; //Zero

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// Equations : Single Zone Model
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function dPdTheta1z  (x1 : Double; y1 : yarray) : Double;
var
 LocalGamma : Double;
begin
// If Engine2z.VariableGamma = FALSE then LocalGamma := 1.35
//   else LocalGamma := Engine2z.Cyl.Gamma;
LocalGamma := 1.4;
//LocalGamma := Engine2z.Cyl.Gamma;
 With Engine2z do
   dPdTheta1z := (-LocalGamma * Cyl.Pgas / Cyl.Vgas * dVCyldTheta(x1)) +
      ((LocalGamma - 1) / Cyl.Vgas *
      ((Cyl.Fuel.M * Cyl.Fuel.Q * Cyl.dxdTheta(x1))
      + dQldTheta1z(x1,y1)));

end;  //dPdTheta1z

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// Equations : UnBurned
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function dPdThetaUB (x1 : Double; y1 : yarray) : Double;
var
 dTudTh : Double;
 htransfer : Double;
begin
 with Engine2z do
  begin
   Cyl.updateUB (VCyl(x1), DVCyldTheta(x1),
                      Vcyl(x1), y1[2], y1[4]);
   if MIn > 0 then hTransfer := Plenum.hu else hTransfer := Cyl.hu;
   with Cyl do
    begin
     dTudTh := (-Pgas*dVdtheta + dQudtheta(x1,y1)+ dmindTheta*(uu-hTransfer)
                   +dmindtheta*dudPu*Pgas + mu/Vu*dudPu*Pgas*dVdTheta)/
                   (mu*dudTu + mu*dudPu*Pgas/Tu);
     dPdthetaUB := Pgas*(-dmindTheta/mu + dTudTh/Tu - dVdtheta/Vu);
    end;
  end;
end; //dPdThetaBD

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function dTudThetaUB (x1 : Double; y1 : yarray) : Double;
var
 hTransfer : double;
begin
 With Engine2z do
  begin
   if MIn > 0 then hTransfer := Plenum.hu else hTransfer := Cyl.hu;
   Cyl.updateUB (VCyl(x1), DVCyldTheta(x1), vcyl(x1), y1[2], y1[4]);
   With Cyl do
   dTudthetaUB := (-Pgas*dVdtheta + dQudtheta(x1,y1) + dmindTheta*(uu-hTransfer)
                   +dmindtheta*dudPu*Pgas + mu/Vu*dudPu*Pgas*dVdTheta)/
                   (mu*dudTu + mu*dudPu*Pgas/Tu);
  end;
 end; //dTudThetaUB

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
//  Equations : Burning
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function dVbdThetaB (x1 : Double; y1 : yarray) : Double;
var
 Q, F, L, J, H, R, B, D, S, Temp : Double;
begin
  with Engine2z.Cyl do
   begin
    UpdateB (x1, Engine2z.VCyl(x1), Engine2z.DVCyldTheta(x1),
      y1[1], y1[2], y1[3], y1[4]);
    Q := Engine2z.dQudTheta(x1, y1)/(mu*Ru);
    F := Vgas*dudTb/Rb + dudPb*mb + vu;
    L := Vu/Tu;
    J := -Vu/Pgas;
    S := dmbdtheta*Vu/mu+DVdTheta;
    R := -Pgas*dVdTheta*(1+dudTb/Rb) + Engine2z.dQbdTheta(x1,y1) -
          dmbdTheta*(ub-uu+(Ru*Tu/Rb-Tb)*dudTb);
    B := -Tu/Pgas;
    H := -(mu*Ru*(1+dudTb/Rb));
    D := 1+dudTu/Ru;
   end;
  dVbdThetaB := (-Q*F*L+Q*J*H+R*B*L-R*J*D-S*B*H+S*F*D)/(-B*H+D*F);
end; //dVbdThetaB

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function dPdThetaB (x1 : Double; y1 : yarray) : Double;
var
 H, Q, D, R, B, F, Temp : Double;
begin
  with Engine2z.Cyl do
   begin
    UpdateB (x1, Engine2z.VCyl(x1), Engine2z.DVCyldTheta(x1),
      y1[1], y1[2], y1[3], y1[4]);
    Q := Engine2z.dQudTheta(x1, y1)/(mu*Ru);
    F := Vgas*dudTb/Rb + dudPb*mb + vu;
    R := -Pgas*dVdTheta*(1+dudTb/Rb) + Engine2z.dQbdTheta(x1,y1) -
          dmbdTheta*(ub-uu+(Ru*Tu/Rb-Tb)*dudTb);
    B := -Tu/Pgas;
    H := -(mu*Ru*(1+dudTb/Rb));
    D := 1+dudTu/Ru;
   end;
  dPdThetaB := (-H*Q+D*R)/(-B*H+D*F);
end; //dPdThetaB

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function dTbdThetaB (x1 : Double; y1 : yarray) : Double;
var
 Q, F, L, J, H, R, B, D, S, N, O, M, T : Double;
begin
  with Engine2z.Cyl do
   begin
    UpdateB (x1, Engine2z.VCyl(x1), Engine2z.DVCyldTheta(x1),
      y1[1], y1[2], y1[3], y1[4]);
    Q := Engine2z.dQudTheta(x1, y1)/(mu*Ru);
    F := Vgas*dudTb/Rb + dudPb*mb + Vu;
    L := Vu/Tu;
    J := -Vu/Pgas;
    S := dmbdtheta*Vu/mu+DVdTheta;
    R := -Pgas*dVdTheta*(1+dudTb/Rb) + Engine2z.dQbdTheta(x1,y1) -
          dmbdTheta*(ub-uu+(Ru*Tu/Rb-Tb)*dudTb);
    B := -Tu/Pgas;
    H := -(mu*Ru*(1+dudTb/Rb));
    D := 1+dudTu/Ru;
    N := mb*dudPb;
    O := mb*dudTb;
    M := Pgas;
    T := Engine2z.dQbdTheta(x1, y1) + dmbdTheta*(hu-ub); //## Was T
   end;

 dTbdThetaB := -(-Q*N*H-Q*M*F*L+Q*M*H*J+R*N*D+R*M*B*L-R*M*D*J-S*M*B*H+
               S*M*D*F+T*B*H-T*D*F)/O/(-B*H+D*F);
end; //dTbdThetaB

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function dTudThetaB (x1 : Double; y1 : yarray) : Double;
var
 Q, F, R, B, H, D, Temp : Double;
begin
  With Engine2z.Cyl do
   begin
    UpdateB (x1, Engine2z.Vcyl(x1), Engine2z.DVCyldTheta(x1),
      y1[1], y1[2], y1[3], y1[4]);
    Q := Engine2z.dQudTheta(x1, y1)/(mu*Ru);
    F := Vgas*dudTb/Rb + dudPb*mb + vu;
    R := -Pgas*dVdTheta*(1+dudTb/Rb) + Engine2z.dQbdTheta(x1,y1) -
          dmbdTheta*(ub-uu+(Ru*Tu/Rb-Tb)*dudTb);
    B := -Tu/Pgas;
    H := -(mu*Ru*(1+dudTb/Rb));
    D := 1+dudTu/Ru;
   end;
  dTudThetaB := (Q*F-R*B)/(-B*H+D*F);
end; //dTudThetaB

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// Equations : Burned (Expanding)
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function dPdThetaBD (x1 : Double; y1 : yarray) : Double;
var
 dTbdTh : Double;
begin
 with Engine2z do
  begin
   Cyl.updateBD (VCyl(x1), DVCyldTheta(x1),
                      Vcyl(x1), y1[2], y1[3]);
   with Cyl do
    begin
     dTbdTh := (-Pgas*dVdtheta + dQbdtheta(x1,y1)+ dmoutdTheta*(ub-hb)
                   +dmoutdtheta*dudPb*Pgas + mb/Vb*dudPb*Pgas*dVdTheta)/
                   (mb*dudTb + mb*dudPb*Pgas/Tb);
     dPdthetaBD := Pgas*(-dmoutdTheta/mb + dTbdTh/Tb - dVdtheta/Vb);
    end;
  end;
end; //dPdThetaBD

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function dTbdThetaBD (x1 : Double; y1 : yarray) : Double;
begin
 With Engine2z do
  begin
   Cyl.updateBD (VCyl(x1), DVCyldTheta(x1), vcyl(x1), y1[2], y1[3]);
   With Cyl do
   dTbdthetaBD := (-Pgas*dVdtheta + dQbdtheta(x1,y1) + dmoutdTheta*(ub-hb)
                   +dmoutdtheta*dudPb*Pgas + mb/Vb*dudPb*Pgas*dVdTheta)/
                   (mb*dudTb + mb*dudPb*Pgas/Tb);
  end;
 end; //dTbdThetaBD

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// Gas Exchange Functions
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function dVbdThetaGE (x1 : Double; y1 : yarray) : Double;
var
 A,D,Q,E,G,R,I,J,K,S,M,N,P,T : Double;
 Temp : Double;
begin
  with Engine2z.Cyl do
   begin
    updateGE (Engine2z.VCyl(x1), Engine2z.DVCyldTheta(x1),
        y1[1], y1[2], y1[3], y1[4]);
    A := -Pgas;
    D := mu*dudTu;
    Q := Engine2z.dQudTheta(x1,y1) - Pgas*dVdTheta - dmindTheta*(uu-hin);
    E := Pgas;
    G := mb*dudTb;
    R := Engine2z.dQbdTheta(x1,y1) - dmoutdtheta*(hb-ub);
    I := 1/Vb;
    J := 1/Pgas;
    K := -1/Tb;
    S := -dmoutdtheta/mb;
    M := -1/Vu;
    N := 1/Pgas;
    P := -1/Tu;
    T := dmindTheta/mu - dVdTheta/Vu;
   end;
  Temp := -(-J*G*P*Q+D*R*K*N-N*D*G*S+J*D*G*T)/
                  (A*J*G*P-E*N*D*K+I*N*D*G-M*J*D*G);
  dVbdThetaGE := temp;
end; //dVbdThetaGE

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function dPdThetaGE (x1 : Double; y1 : yarray) : Double;
var
 A,D,Q,E,G,R,I,J,K,S,M,N,P,T : Double;
  Temp : Double;
begin
  with Engine2z.Cyl do
   begin
    updateGE (Engine2z.VCyl(x1), Engine2z.DVCyldTheta(x1),
      y1[1], y1[2], y1[3], y1[4]);
    A := -Pgas;
    D := mu*dudTu;
    Q := Engine2z.dQudTheta(x1,y1) - Pgas*dVdTheta- dmindTheta*(uu-hin);
    E := Pgas;
    G := mb*dudTb;
    R := Engine2z.dQbdTheta(x1,y1) - dmoutdtheta*(hb-ub);
    I := 1/Vb;
    J := 1/Pgas;
    K := -1/Tb;
    S := -dmoutdtheta/mb;
    M := -1/Vu;
    N := 1/Pgas;
    P := -1/Tu;
    T := dmindTheta/mu - dVdTheta/Vu;
   end;
  dPdThetaGE := (P*Q*E*K-P*Q*I*G-R*A*K*P+R*M*D*K+S*A*G*P-S*M*D*G-D*T*E*K+D*T*I*G)
                  /(A*J*G*P-E*N*D*K+I*N*D*G-M*J*D*G);
end; //dPdThetaGE

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function dTbdThetaGE (x1 : Double; y1 : yarray) : Double;
var
 A,D,Q,E,G,R,I,J,K,S,M,N,P,T : Double;
  Temp : Double;
begin
  with Engine2z.Cyl do
   begin
    updateGE (Engine2z.VCyl(x1), Engine2z.DVCyldTheta(x1),
      y1[1], y1[2], y1[3], y1[4]);
    A := -Pgas;
    D := mu*dudTu;
    Q := Engine2z.dQudTheta(x1,y1) - Pgas*dVdTheta- dmindTheta*(uu-hin);
    E := Pgas;
    G := mb*dudTb;
    R := Engine2z.dQbdTheta(x1,y1) - dmoutdtheta*(hb-ub);
    I := 1/Vb;
    J := 1/Pgas;
    K := -1/Tb;
    S := -dmoutdtheta/mb;
    M := -1/Vu;
    N := 1/Pgas;
    P := -1/Tu;
    T := dmindTheta/mu - dVdTheta/Vu;
   end;
 dTbdThetaGE := (-E*J*P*Q+R*A*J*P+R*I*D*N-R*M*D*J-E*D*N*S+E*D*J*T)/
                 (A*J*G*P-E*N*D*K+I*N*D*G-M*J*D*G);
end; //dTbdThetaGE

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function dTudThetaGE (x1 : Double; y1 : yarray) : Double;
var
 A,D,Q,E,G,R,I,J,K,S,M,N,P,T : Double;
  Temp : Double;
begin
  With Engine2z.Cyl do
   begin
    UpdateGE(Engine2z.VCyl(x1), Engine2z.DVCyldTheta(x1),
      y1[1], y1[2], y1[3], y1[4]);
    A := -Pgas;
    D := mu*dudTu;
    Q := Engine2z.dQudTheta(x1,y1) - Pgas*dVdTheta- dmindTheta*(uu-hin);
    E := Pgas;
    G := mb*dudTb;
    R := Engine2z.dQbdTheta(x1,y1) - dmoutdtheta*(hb-ub);
    I := 1/Vb;
    J := 1/Pgas;
    K := -1/Tb;
    S := -dmoutdtheta/mb;
    M := -1/Vu;
    N := 1/Pgas;
    P := -1/Tu;
    T := dmindTheta/mu - dVdTheta/Vu;
   end;
  Temp := (-Q*E*K*N+Q*I*G*N-Q*M*G*J+A*K*N*R-A*G*N*S+A*G*J*T)/
          (A*J*G*P-E*N*D*K+I*N*D*G-M*J*D*G);
  dTudThetaGE := Temp;
end; //dTudThetaGE

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// Main Program
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TEngine2z.Run;
var
 err : integer;
begin
  x := CA * Pi / 180;
  state := GetState(CA);

  if (state <> oldstate) and (NoZones = 1) then
   begin // State Initialization 1z
    OldState := State;
    if state = Compression then
      begin
       WWork := 0;   PWork := 0;
       WorkDone := 0;  HeatLoss := 0;
       FEnergy := 0;
       PMax := 0;    TMax := 0;
       UIMax := 0;   UEMax := 0;
       NewAirMass := TotalMInIV;          
       TotalMInIv := 0; TotalMOutEV := 0;
       Cyl.Fuel.M := (1/Cyl.Fuel.Lambda) * NewAirMAss / (Cyl.Fuel.AFRatio+1);
       fn[1] := Zero;
       fn[2] := dpdtheta1z;
       fn[3] := Zero;
       fn[4] := Zero;
       PMax := 0;
       TMax := 0;
      end; //Compression Initialization
   end; //State Changes (1z)

  if (state <> oldstate) and (NoZones = 2) then
   begin
    OldState := State;
    if Init2z = FALSE then
     begin
      Init2z := TRUE;
      Cyl.mu := cyl.mgas;
      Cyl.mb := 0;
     end; //2z Initialised
    Case State of
     Compression :
      begin
       fn[1] := Zero;
       fn[2] := dpdthetaUB;
       fn[3] := Zero;
       fn[4] := dTudThetaUB;
       Cyl.burnt.eqbm1.frozen := FALSE;
       WWork := 0;
       Pwork := 0;
       Heatloss := 0;
       PMax := 0;
       TMax := 0;
        FEnergy := 0;
       PMax := 0;    TMax := 0;
       UIMax := 0;   UEMax := 0;
       NewAirMass := TotalMinIV;
       TotalMInIv := 0; TotalMOutEV := 0;
       Cyl.Fuel.M := (1/Cyl.Fuel.Lambda) * NewAirMAss / (Cyl.Fuel.AFRatio+1);
      end;
     Combustion :
      begin
       fn[1] := dVbdThetaB;
       fn[2] := dPdThetaB;
       fn[3] := dTbdThetaB;
       fn[4] := dTudThetaB;
       y[3] := initialTb (Cyl.Pgas, Cyl.Tu);
      end;
     Expansion :
      begin
       fn[1] := Zero;
       fn[2] := dpdThetaBd;
       fn[3] := dTbdThetaBd;
       fn[4] := Zero;
       Cyl.burnt.eqbm1.frozen := FALSE;
       Cyl.mb := Cyl.mgas;
       Cyl.mu := 0;
       Cyl.Vb := cyl.Vgas;
       Cyl.Vu := 0;
      end;
     Exhaust : TotalMoutEV := 0;
     Overlap :
      begin
       Cyl.Burnt.eqbm1.Frozen := TRUE;
       fn[1] := Zero;
       fn[2] := dPdTheta1z;
       fn[3] := Zero;
       fn[4] := Zero;
     {  fn[1] := dVbdThetaGE;
       fn[2] := dPdThetaGE;
       fn[3] := dTbdThetaGE;
       fn[4] := dTudThetaGE;}
       MbOutInlet := 0;
       MuOutExhaust := 0;
      end; //Overlap Initialization
     Intake :
      begin
       Cyl.Tu := (Cyl.Mb*Cyl.Tb + Cyl.Mu*Plenum.tu)/Cyl.Mgas;
       Cyl.mu := Cyl.mgas;
       Cyl.Vb := 0;
       Cyl.Vu := Cyl.Vgas;

       fn[1] := Zero;
       fn[2] := dPdThetaUB;
       fn[3] := Zero;
       fn[4] := dTudThetaUB;

       {with Cyl do IncylEGR := mb/mgas;
       Cyl.Unburnt.changeParam (99,99,99,99,forcedEGR+IncylEGR,99);}
       Cyl.mb := 0;
      end; // Intake Initialization
    end; //Case
   end; // State Changes (2z)

   if (NoZones = 2) and (State = Combustion) then
    begin
     if Cyl.mb = 0 then
      begin
       fn[1] := Zero;
       fn[2] := dpdthetaUB;
       fn[3] := Zero;
       fn[4] := dTudThetaUB;
      end
     else
      begin
       fn[1] := dVbdThetaB;
       fn[2] := dPdThetaB;
       fn[3] := dTbdThetaB;
       fn[4] := dTudThetaB;
      end;
    end; //Combustion mass check

   Integrate;

   case state of
    combustion :
       Cyl.UpdateB (x, VCyl(x), DVCyldTheta(x), y[1], y[2], y[3], y[4]);
    Intake, Compression :
       Cyl.UpdateUB (VCyl(x), DVCyldTheta(x), y[1], y[2], y[4]);
    Expansion, Exhaust :
       Cyl.UpdateBD (VCyl(x), DVCyldTheta(x), y[1], y[2], y[3]);
    Overlap :
       Cyl.UpdateGE (VCyl(x), DVCyldTheta(x), y[1], y[2], y[3], y[4]);
   end; //case state

   If (NoZones = 1) then
    begin
     Cyl.mb := Cyl.mgas;
     Cyl.mu := Cyl.mgas;
     Cyl.Tb := Cyl.Pgas*Cyl.Vgas/Cyl.Rgas/Cyl.mgas;
     if Cyl.Tb < 273.15 then Cyl.Tb := 273.15;
     Cyl.Tu := Cyl.Tb;
     y[3] := Cyl.Tb;
     y[4] := Cyl.Tu;
    end; //if 1 zone

   Emmissions := Cyl.Burnt.Eqbm1.x;

  //masses
   Manifold.Main_Prog(SaveManfData, NCycles,(x*180/pi)+360,tStep,Nrpm,
                 dCA, Cyl.Pgas, Cyl.Tgas, IPt,EPt,VCyl(x),Cyl.mgas,
                 Atm.Pgas, Atm.Tgas, Manifold.IV.FlowArea(CA),
                 Manifold.EV.FlowArea(CA),
                 MIn,MOut,dPMass, PInlet,PExhaust,UInlet,UExhaust);

  Plenum.UpdateUB(0, DVCyldTheta(x), 0, PInlet, Manifold.InletT);

  //Pressure Correction 1z
   if (NoZones = 1) or (state = overlap) then
    begin
     Cyl.Pgas := Cyl.Pgas + dPMass;
     y[2] := Cyl.Pgas;
    end;

  //Mass Calculation
   Cyl.mgas := Cyl.mgas +Min -Mout;
   if Cyl.mgas < 0 then raise EEngineError.Create('Negative engine Gas Mass.');

  if (NoZones = 1) then
   begin
    TotalMInIV := TotalMInIV + MIn;
    TotalMOutEV := TotalMOutEV + Mout;
   end
  else
   begin
  // 2 Zone masses , mass flow check
   case State of
    Exhaust :
     begin
      Cyl.mb := Cyl.mb - Mout;
      Cyl.dmoutdTheta := Mout/dx;
      TotalMOutEV := TotalMOutEV + Mout;
     end;
    Intake :
     begin
      Cyl.mu := Cyl.mu + Min;
      Cyl.dmindtheta := -Min/dx;
      TotalMInIV := TotalMInIV + MIn;
     end;
    Overlap :
     begin
      if (Mout > 0) and (Cyl.mb > 0) then //burned gas out exhaust
       begin
         Cyl.Mb := Cyl.mb - mout;
         TotalMOutEV := TotalMOutEV + MOut;
       end;
      if (Mout > 0) and (Cyl.mb = 0) then //unburned gas out exhaust
       begin
        Cyl.Mu := Cyl.mu - mout;
        MuOutExhaust := MuOutExhaust + Mout;
       end;
      if (Mout < 0) and (MuOutExhaust = 0) then  //burned gas in exhaust
       begin
        Cyl.Mb := Cyl.mb - mout;     //mout is negative - reversion
        TotalMOutEV := TotalMOutEV + MOut;
       end;
      if (Mout < 0) and (MuOutExhaust > 0) then  //unburned gas in exhaust
       begin
        Cyl.Mu := Cyl.mu - mout;     //mout is negative - reversion
        MuOutExhaust := MuOutExhaust + Mout;
       end;

      //correction if burned portion <0 during previous calcs
      if Cyl.mb < 0 then
       begin
        Cyl.mu := Cyl.mu + Cyl.mb;
        MuOutExhaust := MuOutExhaust-Cyl.mb;
        Cyl.mb := 0;
       end;
      if MuOutExhaust < 0 then
       begin
        Cyl.mb := Cyl.mb - MuOutExhaust;
        MuOutExhaust := 0;
       end;

      if (Min > 0) and (MbOutInlet = 0) then  //unburned in Inlet
      begin
       Cyl.mu := Cyl.mu + min;
       TotalMInIV := TotalMInIV + MIn;
      end;
      if (Min > 0) and (MbOutInlet > 0) then   //burned in inlet
       begin
        Cyl.mb := Cyl.mb + min;
        MbOutInlet := MbOutInlet - Min;
       end;
      If (Min < 0) and (Cyl.mu > 0) then  // unburned out inlet
       begin
        Cyl.mu := Cyl.mu + min;
        TotalMInIV := TotalMInIV + MIn;
       end;
      If (Min < 0) and (Cyl.mu = 0) then  // burned out inlet
       begin
        Cyl.mb := Cyl.mb + min;
        MbOutInlet := MbOutInlet - Min;
       end;

      //correction if mu fell below zero during previous calcs
      If Cyl.mu < 0 then
       begin
        Cyl.mb := Cyl.mb + Cyl.Mu;
        MbOutInlet := MbOutInlet-Cyl.mu;
        Cyl.mu := 0;
       end;
      If MbOutInlet < 0 then
       begin
        Cyl.mu := Cyl.mu - MbOutInlet;
        MbOutInlet := 0;
       end;

     Cyl.Tu := Cyl.Pgas*Cyl.Vgas/Cyl.Rgas/Cyl.mgas;
     y[3] := cyl.Tu;
     y[4] := cyl.Tu;
    end;
   end; //case state
   end;  //if NOT 1z

   // Calculate Work
   Case State of
    Combustion, Compression, Expansion : WWork := WWork + dx*dwdTheta(x,y);
    Overlap, Intake, Exhaust :  PWork := PWork - dx*dwdTheta(x,y);
   end; //Case

   //??#### 2Z Fuel Energy
   FEnergy := FEnergy + Cyl.Fuel.M*Cyl.Fuel.Q*Cyl.dxdTheta(CA)*dx;

   if Cyl.Pgas > PMax then PMax := Cyl.Pgas;
   if Cyl.Tgas > TMax then TMax := Cyl.Tgas;

   Qb := dQbdtheta(x,Y)*dx;
   Qu := dQudTheta(x,y)*dx;
   HeatLoss := HeatLoss + Qb + Qu;

end; //TEngine2z.Run

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function TEngine2z.TFMEP : Double;
begin
    TFMEP := 1.0e5* (0.97 + (0.15 * Nrpm / 1000) + (0.05 * sqr (Nrpm/1000)));
end; //TEngine2z.TFMEP

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Procedure TEngine2z.InitVars (var AllOK : Boolean) ;
var
 AirDensity : Real;
 Terp : Double;
 err : integer;
begin
 AllOK := FALSE;
 Init2z := FALSE;
 TwoZOverlap := FALSE;

 //Integration Variables
 dCA := 1;     // Initial dtheta
 wcrank := Nrpm*pi/30;
 dx := dCA*pi/180; //Radians
 NEqns := 4;
 ForcedEGR := 0;
 Vd := Pi * Sqr(Bore) * Stroke / 4 ;
 With Atm do
  begin
   mb := 0;
   Tb := Tu;
   hin := hu;
  end; //Atm
 With Plenum do
  begin
   mb := 0;
   Pgas := Manifold.CleanAirPresFn.Result(Nrpm);  //Inlet Plenum Pressure
   Tb := Atm.Tgas;
   Tu := Atm.Tgas;
   Unburnt.Setup (0, Fuel.C, Fuel.H, Fuel.O, Fuel.N, 1/Fuel.Lambda, ForcedEgR);
   Burnt.Setup (0, Fuel.C, Fuel.H, Fuel.O, Fuel.N, 1/Fuel.Lambda, ForcedEgR);
   UpdateUB(Vcyl(pi), 0, VCyl(pi), Pgas, Tu);
   hin := hu;
  end; //Plenum
 With Cyl do
  begin
   ThetaSpark := -1* Sparkangle.GetVal(NRpm);
   Pgas := Plenum.Pgas;
   Tb := Plenum.Tgas;
   Tu := Plenum.Tgas;
   Unburnt.Setup (0, Fuel.C, Fuel.H, Fuel.O, Fuel.N, 1/Fuel.Lambda, ForcedEgR);
   Burnt.Setup (0, Fuel.C, Fuel.H, Fuel.O, Fuel.N, 1/Fuel.Lambda, forcedEgR);
   hin := hu;
  end; //Cyl
 With Exh do
  begin
   Pgas := Manifold.ExhBack.Pres(Nrpm); 
   Tu := 293.15;
   Tb := Manifold.ExhBack.Temp(Nrpm);
   Unburnt.Setup (0, Fuel.C, Fuel.H, Fuel.O, Fuel.N, 1/Fuel.Lambda, ForcedEgR);
   Burnt.Setup (0, Fuel.C, Fuel.H, Fuel.O, Fuel.N, 1/Fuel.Lambda, forcedEgR);
   hin := hu;
  end; //Exh

 with Manifold do
   begin
    IV.Profile.LoadText(IV.ProfileFile);
    EV.Profile.LoadText(EV.ProfileFile);
    IV.CdForward := CdIvIn;
    IV.CdForward.LoadAndUpdate;
    IV.CdReverse := CdIvOut;
    IV.CdReverse.LoadAndUpdate;
    EV.CdForward := CdEvOut;
    EV.CdForward.LoadAndUpdate;
    EV.CdReverse := CdEvIn;
    EV.CdReverse.LoadAndUpdate;
    IManf.AvsL := AInManf;
    IManf.AvsL.LoadAndUpdate;
    EManf.AvsL := AExManf;
    EManf.AvsL.LoadAndUpdate;
    PlenumT := Plenum.Tgas;
    GammaIn := Cyl.unburnt.get_gamma(Manifold.CleanAirPresFn.Result(Nrpm), Manifold.PlenumT,Err);
    GammaEx := Cyl.burnt.get_gamma(Exh.PGas, Exh.TGas,Err);
   end; //With Manifold

 PCylIVC := Plenum.Pgas;
 TCylIVC := Plenum.Tgas;
 VCylIVC := VCyl(Manifold.IV.C*pi/180);

 y[1] := 0;
 y[2] := Cyl.Pgas;
 y[3] := 2200;
 y[4] := Cyl.Tb;
 tStep := 0;

//Initial Masses
 TotalMInIV := 0.9 * Cyl.Pgas * Vd / 287 / Cyl.Tgas;
 TotalMass := 0.9 * Cyl.Pgas * Vd / 287 / Cyl.Tgas;
 With Cyl do Fuel.m := (1/Fuel.Lambda) * TotalMInIv / (Fuel.AFRatio+1);
 Massatm := Plenum.Pgas/Plenum.Rgas/Plenum.Tgas * VCyl(pi);
 Cyl.mgas := TotalMass;
 Cyl.mb := 0;
 Cyl.mu := totalMass;
 Cyl.Vgas := VCylIVC;
 Cyl.Vu := VCylIVC;
 Min := 0;
 MOut := 0;
 TotalMinIV := MassAtm; //This is just for initialization : set to 0 for Integration
 TotalMoutEV := 0;

//Initial Work Values
 WWork := 0;
 PWork := 0;

//Initial Peak Values
 PMax := 10000000;
 TMax := 0;
 UIMax := 0;
 UEMax := 0;

 AllOK := Manifold.IV.Profile.ProfileOK AND Manifold.EV.Profile.ProfileOK;
end; //TEngine2z.InitVars


//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Constructor TEngine2z.Create;
begin
   inherited;
   Manifold := TManifolds.Create;
   SparkAngle := TVarSpeedList.Create;
   Atm := TGas2z.Create;
   Cyl := TGas2z.Create;
   Plenum := TGas2z.Create;
   Exh := TGas2z.Create;
   WallTemp := TWallTemps.Create;
//   Assignfile (TF, 'myfile.txt');
//   Rewrite (TF);
end; //TEngine2z.Create

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Destructor TEngine2z.Destroy;
begin
  Manifold.Free;
  SparkAngle.Free;
  Atm.Free;
  Cyl.Free;
  PLenum.Free;
  Exh.Free;
  WallTemp.Free;
//  Close(TF);
  Inherited Destroy;
end; //TEngine2z.Destroy

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TEngine2z.Performance;
begin
  TotalMass := Cyl.mgas;
  Cyl.Fuel.M := (1/Cyl.Fuel.Lambda) * TotalMInIV / (Cyl.Fuel.AFRatio+1);
  ResidialFraction := (1-(TotalMInIV/TotalMass))*100;
  IMEP := WWork / Vd;
  PMEP := PWork / Vd;
  FMEP := TFMEP-PMEP;
  BMEP := IMEP - PMEP - FMEP;
  Torque := BMEP * Vd * NCyl / (2 * 2 * Pi);
  BPower := Torque * Nrpm * 2 * PI / 60;
  IPower := IMEP * Vd * NCyl / (4 * Pi) * Nrpm * 2 * Pi / 60;
  HPower := Heatloss * NCyl / (4 * Pi) * Nrpm * 2 * Pi / 60;
  VEff := TotalMInIV/MassAtm*100;
  MEff := BMEP/IMEP*100;
  mf := Cyl.Fuel.m * 2 * Nrpm *60;
  SFC := mf *1000 / BPower *1000;
  ThEff := BPower / (Cyl.fuel.Q * Cyl.fuel.m * 2 * Nrpm /60) * 100;
  QFuel := Cyl.fuel.Q * Cyl.Fuel.M;
  QHeat := -HeatLoss;
  QWork := BMEP*Vd;
  QPump := PWork;
  QFric := FMEP*Vd;
  QExht := QFuel-QHeat-QWork-QPump-QFric;
  QWork := QWork/QFuel*100;
  QHeat := QHEat/QFuel*100;
  QPump := QPump/QFuel*100;
  QFric := QFric/QFuel*100;
  QExht := QExht/QFuel*100;
end; //TEngine2z.Performance

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
end. //ICEngine2z
