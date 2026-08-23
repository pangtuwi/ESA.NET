// Engine Unit for Simulation of Pressure Pulse Flow in Inlet/Exhaust Manifolds
// Christie M. van Vuuren
// MSc.Ing (Meg)
//
// Adapted for Object Delphi4,
// Modified for 2Z model by P.N.T. Williams

// Engine Simulation and Analysis (ESA)


UNIT Manifolds;

INTERFACE

USES  Valves, Dialogs, Pipes, SysUtils, GridSizes, ExhBackPandT, DoubleFunc;

CONST  NI = 68;   //Define amount of points in Inletgrid to a max of 68
       NE = 38;   //Define amount of points in Exhaustgrid to a max of 38
       H = 4;     //Amount of Calculated points
       E1 = 1;    //Convergence factor for Velocity
       E2 = 1;    //Convergence factor for Pressure
       E3 = 1;    //Convergence factor for Density
       E4 = 0.1;  //Convergence factor for Calculated Points

VAR  Choice  : Integer;  //Choose between Exhaust- and Inlet Internal Pipe
     QI,QE,W : Integer;  //Inlet & Exhaust Grid Parameters

TYPE TInletCalcArray = ARRAY[1..NI] of Double;    //Array for Inlet Variables
     TExhaustCalcArray = ARRAY[1..NE] of Double;  //Array for Exhaust Variables

     TManifolds = Class
       IV, EV       : TValve;             //Inlet & Exhaust Valves Type
       IManf,EManf  : TPipe;              //Inlet & Exhaust Pipes Type
       ExhBack      : TExhaustPandT;      //Exhaust Back Pressure and Temperature
       CleanAirPresFn : TDoubFunc;
       IGrid, EGrid : TGridSize;
       IVRFunc, IVFFunc, IVFRFunc,
       EVRFunc, EVFFunc, EVFRFunc : TDoubFunc;
       IVR, IVF, IVFR, EVR, EVF, EVFR : Double;
       GammaIn,                           //Gamma value for Inlet
       GammaEx,                           //Gamma value for Exhaust
       GammaCyl     : Double;             //Gamma value for Cylinder
       PlenumT      : Double;             //Plenum Temperature
       XInlet       : TInletCalcArray;    //X-coordinate Array for Inlet
       uInlet       : TInletCalcArray;    //Velocity-coordinate for Inlet
       PInlet       : TInletCalcArray;    //Pressure-coordinate for Inlet
       RInlet       : TInletCalcArray;    //Density-coordinate for Inlet
       cInlet       : TInletCalcArray;    //Speed of Sound-coordinate for Inlet
       TempInlet    : TInletCalcArray;    //Temperature-coordinate for Inlet
       XExhaust     : TExhaustCalcArray;  //X-coordinate Array for Exhaust
       uExhaust     : TExhaustCalcArray;  //Velocity-coordinate for Exhaust
       PExhaust     : TExhaustCalcArray;  //Pressure-coordinate for Exhaust
       RExhaust     : TExhaustCalcArray;  //Density-coordinate for Exhaust
       cExhaust     : TExhaustCalcArray;  //Speed of Sound-coordinate for Exhaust
       TempExhaust  : TExhaustCalcArray;  //Temperature-coordinate for Exhaust
       InletT,
       ExhaustT     : Double;             //Temp at first and last Points
       ICd,ECd      : Double;             //Inlet&Exhaust Valve Discharge Coeff
       Iut,Ict,IRt  : Double;             //Inlet Valve Throat Vel, SOS & dens
       Eut,Ect,ERt  : Double;             //Exhaust Valve Throat Vel, SOS & dens

   Procedure Main_Prog(SaveManifoldData            : Boolean;
                       NoCycles                    : Integer;
                       CA                          : Double;
                       var tStep                   : Integer;
                       Speed,dCrankA               : Double;
                       Pcyl,Tcyl                   : Double;
                       var IPt,EPt                 : Double;
                       CylVol,MassCyl,Patm,Tatm    : Double;
                       IValveArea,EValveArea       : Double;
                       var MassIn, MassOut, dPMass : Double;
                       var InletP,ExhaustP,
                           InletU,ExhaustU         : Double);

       Constructor Create;
       Destructor Destroy; Override;

     End;  //TManifolds

   ECFDError = class(exception);

IMPLEMENTATION

//******************************************************************************

Function Power(a,b : Double) : Double;
  //Power = a^b
Begin
 if (a <= 0) or (a >= 1e20) then
  begin
   if a <= 0 then raise ECFDError.Create('ERROR : a <= 0 in "Power" !!!');
   if a >= 1e20 then raise ECFDError.Create('ERROR : a >= 1e20 in "Power" !!!');
  end;
 if b = 0 then
  Power := 1
 else if (a = 0) and (b > 0) then
  Power := 0
 else Power := exp(b*ln(a));
End;  //Power

//******************************************************************************

Function cThermo(gam,pres,dens : Double) : Double;  //Speed of Sound,"c"
Begin
 if gam*pres/dens <= 0 then
  begin
   if dens < 0 then raise ECFDError.Create('ERROR : Density negative in "cThermo" !!!');
   if pres < 0 then raise ECFDError.Create('ERROR : Presssure negative in "cThermo" !!!');
  end
 else cThermo := sqrt(gam*pres/dens);
End;  //cThermo

//******************************************************************************

Function Viscosity(T : Double) : Double;  //Viscosity of Air as a f(Temp.)
Var Visc : Double;
Begin
 Visc := sqrt(T)/(0.552795 + 2.810892e2/T - 13.508340e4/power(T,2)
                      + 39.353086e6/power(T,3) - 41.419387e8/power(T,4))*1e-6;
 if Visc < 0 then raise ECFDError.Create('ERROR : a >= 1e20 in "Power" !!!')
 else Viscosity := Visc;
End;  //Viscosity

//******************************************************************************

Function FricFact(gam,rho,V,d,c : Double) : Double;
  //Fanning Friction Factor, "f"
Var Re : Double;       //Reynolds number
    f  : Double;       //Fanning Friction Factor
    T  : Double;       //Temperature
Begin
 T := sqr(c)/gam/287;
 Re := rho*abs(V)*d/Viscosity(T);
 if Re = 0 then
  f := 0
 else if (Re > 0) and (Re < 2300) then       //Laminer Flow
  f := 16/Re
 else if (Re >= 2300) and (Re < 4000) then   //Transitional Flow
  f := 0.0791/power(Re,0.25)
 else if (Re >= 4000) and (Re < 1e5) then   //Turbulent Flow
  f := 0.0791/power(Re,0.25)
 else if Re >= 1e5 then                     //Turbulent Flow
  f := 0.04/power(Re,0.16);
 FricFact := f;
End;  //FricFact

//******************************************************************************

Function CritPress(gam,Cd,Aratio : Double) : Double;
  //Critical Pressure for Sonic Normal Flow at Inlet and Reverse Flow at Exhaust
Const MaxNoIters = 100000;
Var Pc1,Pc2,Pc3 : Double;
    fx1,fx2,fx3 : Double;
    iters       : Integer;
Begin
 Pc1 := 0.5;
 Pc2 := 1;
 iters := 0;
 Repeat
  fx1 := sqr(Cd*Aratio)*(gam-1)/(gam+1)*power(Pc1,2/gam) +
                                           2/(gam+1)*power(Pc1,(1-gam)/gam) - 1;
  fx2 := sqr(Cd*Aratio)*(gam-1)/(gam+1)*power(Pc2,2/gam) +
                                           2/(gam+1)*power(Pc2,(1-gam)/gam) - 1;
 if fx1*fx2 > 0 then
  raise ECFDError.Create('ERROR : fx1*fx2 > 0 in Critical Pressure !!!');
  Pc3 := Pc2 - fx2*(Pc2-Pc1)/(fx2-fx1);
  fx3 := sqr(Cd*Aratio)*(gam-1)/(gam+1)*power(Pc3,2/gam) +
                                           2/(gam+1)*power(Pc3,(1-gam)/gam) - 1;
  if ((fx3 > 0) and (fx1 > 0)) or ((fx3 < 0) and (fx1 < 0)) then
   Pc1 := Pc3
  else
   Pc2 := Pc3;
  inc (iters);
 Until (abs(fx3) < 0.0000001)  or (iters > MaxNoIters);
 if iters > MaxNoIters then
  raise ECFDError.Create('ERROR : No convergence in Critical Pressure !!!');
 CritPress := Pc3;
End;  //CritPress

//******************************************************************************

Function InlSonicVelSolve(gam,Cd,Aratio,ut,cCyl : Double) : Double;
  //Sonic Velocity at Inlet Valve for Reverse Flow
Const MaxNoIters = 100000;
Var u1,u2,u3    : Double;
    fx1,fx2,fx3 : Double;
    iters       : Integer;
Begin
 u1 := 0.000001*ut;
 u2 := 0.6*ut;
 iters := 0;
 Repeat
  fx1 := sqr(u1) - power(2/(gam+1),3/2)*(1/Cd/Aratio + gam)*u1*cCyl
                                                        + sqr(cCyl)*(2/(gam+1));
  fx2 := sqr(u2) - power(2/(gam+1),3/2)*(1/Cd/Aratio + gam)*u2*cCyl
                                                        + sqr(cCyl)*(2/(gam+1));
 if fx1*fx2 > 0 then
  raise ECFDError.Create('ERROR : fx1*fx2 > 0 in Inlet Sonic Velocity Solve(Reverse Flow) !!!');
  u3 := u2 - fx2*(u2-u1)/(fx2-fx1);
  fx3 := sqr(u3) - power(2/(gam+1),3/2)*(1/Cd/Aratio + gam)*u3*cCyl
                                                        + sqr(cCyl)*(2/(gam+1));
  if ((fx3 > 0) and (fx1 > 0)) or ((fx3 < 0) and (fx1 < 0)) then
   u1 := u3
  else
   u2 := u3;
  inc (iters);
 Until (abs(fx3) < 0.0000001) or (iters > MaxNoIters);
 if iters > MaxNoIters then
 raise ECFDError.Create('ERROR : No convergence in Inlet Sonic Velocity Solve(Reverse Flow) !!!');
 InlSonicVelSolve := u3;
End;  //InlSonicVelSolve

//******************************************************************************

Function InlSubSonicVelSolve(gam,Cd,Aratio,ut,ct,cCyl : Double) : Double;
  //Subsonic Speed at Inlet Valve for Reverse Flow
Const MaxNoIters = 100000;
Var u1,u2,u3    : Double;
    fx1,fx2,fx3 : Double;
    iters       : Integer;
Begin
 u1 := 0.000001*ut;
 u2 := 0.99999*ut;
 iters := 0;
 Repeat
  fx1 := sqr(u1) - 2/(gam+1)*(sqr(ct)/ut/Cd/Aratio+gam*ut)*u1
                                                        + sqr(cCyl)*(2/(gam+1));
  fx2 := sqr(u2) - 2/(gam+1)*(sqr(ct)/ut/Cd/Aratio+gam*ut)*u2
                                                        + sqr(cCyl)*(2/(gam+1));
 if fx1*fx2 > 0 then
  raise ECFDError.Create('ERROR : fx1*fx2 > 0 in Inlet SubSonic Velocity Solve(Reverse Flow) !!!');
  u3 := u2-fx2*(u2-u1)/(fx2-fx1);
  fx3 := sqr(u3) - 2/(gam+1)*(sqr(ct)/ut/Cd/Aratio+gam*ut)*u3
                                                        + sqr(cCyl)*(2/(gam+1));
  if ((fx3 > 0) and (fx1 > 0)) or ((fx3 < 0) and (fx1 < 0)) then
   u1 := u3
  else
   u2 := u3;
 inc(iters);
 Until (abs(fx3) < 0.0000001) or (iters > MaxNoIters);
 if iters > MaxNoIters then
  raise ECFDError.Create('ERROR : No convergence in Inlet Subsonic Velocity Solve(Reverse Flow) !!!');
 InlSubSonicVelSolve := u3;
End; //InlSubSonicVelSolve

//******************************************************************************

Function ExhSonicVelSolve(gam,Cd,Aratio,ut,cCyl : Double) : Double;
  //Sonic Speed at Exhaust Valve Exit
Const MaxNoIters = 100000;
Var u1,u2,u3    : Double;
    fx1,fx2,fx3 : Double;
    iters       : Integer;
Begin
 u1 := 0.000001*ut;
 u2 := 0.8*ut;
 iters := 0;
 Repeat
  fx1 := sqr(u1) - power(2/(gam+1),3/2)*(1/Cd/Aratio + gam)*u1*cCyl
                                                        + sqr(cCyl)*(2/(gam+1));
  fx2 := sqr(u2) - power(2/(gam+1),3/2)*(1/Cd/Aratio + gam)*u2*cCyl
                                                        + sqr(cCyl)*(2/(gam+1));
 if fx1*fx2 > 0 then
 raise ECFDError.Create('ERROR : fx1*fx2 > 0 in Exhaust Sonic Velocity Solve !!!');
  u3 := u2 - fx2*(u2-u1)/(fx2-fx1);
  fx3 := sqr(u3) - power(2/(gam+1),3/2)*(1/Cd/Aratio + gam)*u3*cCyl
                                                        + sqr(cCyl)*(2/(gam+1));
  if ((fx3 > 0) and (fx1 > 0)) or ((fx3 < 0) and (fx1 < 0)) then
   u1 := u3
  else
   u2 := u3;
  inc (iters);
 Until (abs(fx3) < 0.0000001) or (iters > MaxNoIters);
 if iters > MaxNoIters then
  raise ECFDError.Create('ERROR : No convergence in Exhaust Sonic Velocity Solve !!!');
 ExhSonicVelSolve := u3;
End;  //ExhSonicVelSolve

//******************************************************************************

Function ExhSubSonicVelSolve(gam,Cd,Aratio,ut,ct,cCyl : Double) : Double;
  //Subsonic Speed at Exhaust Valve Exit
Const MaxNoIters = 100000;
Var u1,u2,u3    : Double;
    fx1,fx2,fx3 : Double;
    iters       : Integer;
Begin
 u1 := 0.000001*ut;
 u2 := 0.99999*ut;
 iters := 0;
 Repeat
  fx1 := sqr(u1) - 2/(gam+1)*(sqr(ct)/ut/Cd/Aratio+gam*ut)*u1
                                                        + sqr(cCyl)*(2/(gam+1));
  fx2 := sqr(u2) - 2/(gam+1)*(sqr(ct)/ut/Cd/Aratio+gam*ut)*u2
                                                        + sqr(cCyl)*(2/(gam+1));
 if fx1*fx2 > 0 then
  raise ECFDError.Create('ERROR : fx1*fx2 > 0 in Exhaust SubSonic Velocity Solve !!!');
  u3 := u2-fx2*(u2-u1)/(fx2-fx1);
  fx3 := sqr(u3) - 2/(gam+1)*(sqr(ct)/ut/Cd/Aratio+gam*ut)*u3
                                                        + sqr(cCyl)*(2/(gam+1));
  if ((fx3 > 0) and (fx1 > 0)) or ((fx3 < 0) and (fx1 < 0)) then
   u1 := u3
  else
   u2 := u3;
 inc(iters);
 Until (abs(fx3) < 0.0000001) or (iters > MaxNoIters);
 if iters > MaxNoIters then
 raise ECFDError.Create('ERROR : No convergence in Exhaust Subsonic Velocity Solve !!!');
 ExhSubSonicVelSolve := u3;
End; //ExhSubSonicVelSolve

//******************************************************************************

Function ExhSonicMachSolve(gam,Cd,Aratio : Double) : Double;
  //Sonic Mach Number at Exhaust Valve Entrance
Const MaxNoIters = 100000;
Var M1,M2,M3    : Double;
    fx1,fx2,fx3 : Double;
    iters       : Integer;
Begin
 M1 := 0.45*Aratio;
 M2 := 0.75*Aratio;
 iters := 0;
 Repeat
  fx1 := 1/Cd/Aratio - 1/M1*power(2/(gam+1)*
                                     (1 + (gam-1)/2*sqr(M1)),(gam+1)/2/(gam-1));
  fx2 := 1/Cd/Aratio - 1/M2*power(2/(gam+1)*
                                     (1 + (gam-1)/2*sqr(M2)),(gam+1)/2/(gam-1));
 if fx1*fx2 > 0 then
  raise ECFDError.Create('ERROR : fx1*fx2 > 0 in Exhaust Sonic Mach Solve(Reverse Flow) !!!');
  M3 := M2 - fx2*(M2-M1)/(fx2-fx1);
  fx3 := 1/Cd/Aratio - 1/M3*power(2/(gam+1)*
                                     (1 + (gam-1)/2*sqr(M3)),(gam+1)/2/(gam-1));
  if ((fx3 > 0) and (fx1 > 0)) or ((fx3 < 0) and (fx1 < 0)) then
   M1 := M3
  else
   M2 := M3;
  inc (iters);
 Until (abs(fx3) < 0.0000001)  or (iters > MaxNoIters);
 if iters > MaxNoIters then
   raise ECFDError.Create('ERROR : No convergence in Exhaust Sonic Mach Solve(Reverse Flow) !!!');
 ExhSonicMachSolve := M3;
End;  //ExhSonicMachSolve

//******************************************************************************

Procedure CalcX(IManf,EManf  : TPipe;
                var XInlet   : TInletCalcArray;
                var XExhaust : TExhaustCalcArray);
  //X-Coordinates of Inlet & Exhaust Pipes
Var i                 : Integer;
    dxInlet,dxExhaust : Double;
Begin
 dxInlet := IManf.Length/(QI-1);
 XInlet[1] := 0;
 for i := 2 to QI do XInlet[i] := XInlet[i-1] + dxInlet;
 dxExhaust := EManf.Length/(QE-1);
 XExhaust[1] := 0;
 for i := 2 to QE do XExhaust[i] := XExhaust[i-1] + dxExhaust;
End;  //CalcX

//******************************************************************************

Procedure CalcVel(var uInlet   : TInletCalcArray;
                  var uExhaust : TExhaustCalcArray);
  //Velocity in Inlet & Exhaust Pipes
Var i : Integer;
Begin
 for i := 1 to QI do uInlet[i] := 0;
 for i := 1 to QE do uExhaust[i] := 0;
End;  //CalcVel

//******************************************************************************

Procedure CalcPress(InletPress,ExhaustPress : Double;
                    var PInlet              : TInletCalcArray;
                    var PExhaust            : TExhaustCalcArray);
  //Pressure in Inlet & Exhaust Pipes
Var i : Integer;
Begin
 for i := 1 to QI do PInlet[i] := InletPress;
 for i := 1 to QE do PExhaust[i] := ExhaustPress;
End;  //CalcPress

//******************************************************************************

Procedure CalcDens(PInlet,TempInlet     : TInletCalcArray;
                   var RInlet           : TInletCalcArray;
                   PExhaust,TempExhaust : TExhaustCalcArray;
                   var RExhaust         : TExhaustCalcArray);
  //Density in Inlet & Exhaust Pipes
Var i : Integer;
Begin
 for i := 1 to QI do RInlet[i] := PInlet[i]/287/TempInlet[i];
 for i := 1 to QE do RExhaust[i] := PExhaust[i]/287/TempExhaust[i];
End;  //CalcDens

//******************************************************************************

Procedure CalcSOS(gam          : Double;
                  TempInlet    : TInletCalcArray;
                  var cInlet   : TInletCalcArray;
                  TempExhaust  : TExhaustCalcArray;
                  var cExhaust : TExhaustCalcArray);
  //Speed of Sound in Inlet & Exhaust Pipes
Var i : Integer;
Begin
 for i := 1 to QI do cInlet[i] := sqrt(gam*287*TempInlet[i]);
 for i := 1 to QE do cExhaust[i] := sqrt(gam*287*TempExhaust[i]);
End;  //CalcSOS

//******************************************************************************

Procedure MassFlow(gam,dt                    : Double;
                   Iut,Ict,IRt,ICd           : Double;
                   Eut,Ect,ERt,ECd           : Double;
                   IValveArea,EValveArea     : Double;
                   cCyl,CylVol               : Double;
                   var MassIn,MassOut,dPMass : Double);
  //Cylinder Pressure Correction Calculation & Mass Transfer Caculation
Var cStag : Double;
Begin
//MassFlow into Cylinder through Inlet Valve
 MassIn := Iut*IRt*(ICd*IValveArea)*dt;
//MassFlow out of Cylinder through Exhaust Valve
 MassOut := Eut*ERt*(ECd*EValveArea)*dt;
//Inlet Valve Stagnation Conditions
 if Iut > 0 then cStag := sqrt(sqr(Ict) + (gam-1)/2*sqr(Iut))
// else cStag := cCyl;  //Valve
 else cStag := sqrt(sqr(Ict) + (gam-1)/2*sqr(abs(Iut)));  //Pipe
//Exhaust Valve Stagnation Conditions
// if Eut >= 0 then cCyl := cCyl  //Valve
 if Eut >= 0 then cCyl := sqrt(sqr(Ect) + (gam-1)/2*sqr(abs(Eut)))  //Pipe
 else cCyl := sqrt(sqr(Ect) + (gam-1)/2*sqr(Eut));
//Pressure Correction
 dPMass := (sqr(cStag)*MassIn - sqr(cCyl)*MassOut)/CylVol;
End;  //MassFlow

//******************************************************************************

Procedure INLET_VALVE_REVERSE(dt,gam,PCyl,TCyl,CA       : Double;
                              var Mt,ut,ct,Rt,Pt        : Double;
                              AreaInlet,IValveArea      : Double;
                              var XInlet,uInlet,PInlet,
                                  RInlet,cInlet         : TInletCalcArray;
                              IV                        : TValve;
                              var Cd                    : Double;
                              IManf                     : TPipe;
                              IVR, IVF, IVFR            : Double);
  //Gasflow through the Inlet Valve from the Cylinder
Type array_var = Array[1..H] of Double;  //Array for x-values
Var  I,iter,stop : Integer;    //Parameters
     ue          : Double;     //Velocity at valve entrance
     cCyl        : Double;     //Speed of sound
     ce,c4       : Double;     //Speed of sound at Valve Entrance & Point4
     PRcr        : Double;     //Critical pressure ratio for sonic flow
     Me,M4       : Double;     //Mach number at point in nozzle throat
     Aratio      : Double;     //Ratio between nozzle throat and entrance
     Tt          : Double;     //Throat Temperature
     dx          : Double;     //x-step
     xs          : array_var;  //x-coördinates for initial reference points
     x           : array_var;  //x-coördinates at temperary points
     u           : array_var;  //Velocities at temperery points
     P           : array_var;  //Pressure at temperery points
     R           : array_var;  //Density at temperery points
     MuL,MPL,MRL : Double;     //Gradient at left
     BuL,BPL,BRL : Double;     //Constant at left
     a           : Double;     //Velocity to determine s.o.s
     c           : Double;     //Speed of sound
     pres        : Double;     //Pressure to determine s.o.s
     dens        : Double;     //Density to determine s.o.s
     Lplus       : Double;     //lambdaplus, lambdaminus
     Qplus       : Double;     //Q+
     Tplus       : Double;     //T+
     Splus       : Double;     //S+
     uD,PD       : Double;     //Covergence test for velocity,pressure
     P4I         : Double;     //New Pressure
     M4I         : Double;     //New Mach Number
     SL          : Double;     //Gradient
     Ptt         : Double;     //Throat Correction Pressures
     Q           : Integer;    //Grid Parameter
     D1          : Double;     //Hydroulic Diameter
     f1          : Double;     //Fanning Friction Factor
Begin
//DEFINE INITIAL PROPERTIES AND DETERMINE INTERPOLATING POLYNOMIALS
  gam := 1.3994;
  iter := 0;
  Q := QI;
  PRcr := power((gam+1)/2,gam/(gam-1));
  Aratio := IValveArea/AreaInlet;
  if Aratio >= 1 then Aratio := 1;
 //Initial x-values
  xs[1] := xInlet[Q-1];
  x[4] := xInlet[Q];
 //Initial velocity-values
  u[1] := uInlet[Q-1];
 //Initial pressure-values
  P[1] := PInlet[Q-1];
 //Initial density-values
  R[1] := RInlet[Q-1];
 //Determine Interpolating Polynomials for Left Two Points
  dx := xInlet[Q-1] - xInlet[Q];
  //velocity
  MuL := (uInlet[Q-1]-uInlet[Q])/dx;
  BuL := uInlet[Q] - MuL*xInlet[Q];
  //pressure
  MPL := (PInlet[Q-1]-PInlet[Q])/dx;
  BPL := PInlet[Q] - MPL*xInlet[Q];
  //density
  MRL := (RInlet[Q-1]-RInlet[Q])/dx;
  BRL := RInlet[Q] - MRL*xInlet[Q];
//DO ITERATION UNTIL CONVERGENCE IS REACHED
REPEAT
//LOCATE POINT 1 AND DETERMINE COEFFICIENTS ALONG LINE 14(C+)
 REPEAT
  stop := 0;
  if iter = 0 then
   begin
    u[4] := u[1];
    P[4] := P[1];
    R[4] := R[1];
   end;
  a := (u[1]+u[4])/2;
  pres := (P[1]+P[4])/2;
  dens := (R[1]+R[4])/2;
  if (pres < 0) or (dens < 0) then
  showMessage('ERROR : Press/Dens negative in Point 1 at "Inlet_Valve_Reverse" !!!');
  c := cThermo(gam,pres,dens);
  Lplus := 1/(a+c);
  x[1] := x[4] - dt/Lplus;
  if x[1] > IManf.Length then x[1] := IManf.Length;
  if ABS(x[1]-xs[1]) < 0.001*E4 then
   begin
    D1 := sqrt(4*IManf.Area(x[1])/Pi);
    f1 := FricFact(gam,R[1],u[1],D1,c);
    Qplus := dens*c;
    Splus := - R[1]*u[1]*sqr(c)/IManf.Area(x[1])*IManf.dAdL(x[1])
             + ((gam-1)*u[1] - c)*(R[1]*u[1]*abs(u[1])*2*f1/D1);
    Tplus := P[1] + Qplus*u[1] + Splus*dt;
    stop := 1;
   end
  else
   begin
    xs[1] := x[1];
    u[1] := MuL*x[1] + BuL;
    P[1] := MPL*x[1] + BPL;
    R[1] := MRL*x[1] + BRL;
   end;
 UNTIL stop = 1;
 //POINT 1 IS FIXED
 //****************
//CALCULATE THE PROPERTIES AT POINT 4 AND TEST FOR CONVERGENCE
 if (iter > 0) and (Pcyl <= Pt) and (u[4] < IVR) then
  Pt := 0.999999*Pcyl;
 if iter = 0 then
  begin
   P[4] := PInlet[Q];
   u[4] := uInlet[Q];
   R[4] := RInlet[Q];
   c4 := sqrt(gam*P[4]/R[4]);
   if (Pcyl <= Pt) then
    Pt := 0.999999*Pcyl
   else
    Pt := Pt;
  end;
 cCyl := sqrt(gam*287*Tcyl);
 if Pcyl <= Pt then
  begin
  //Throat
   Pt := 0.5*Pt + 0.5*Pcyl;
   Mt := 0;
   ct := cCyl;
   ut := 0;
   Rt := gam*Pt/sqr(ct);
  //Entrance
   u[4] := 0;
   P[4] := 0.5*Pt + 0.5*P[4];
   R[4] := gam*P[4]/sqr(c4);
   stop := 1;
  end
else
 begin
  if (Pcyl/Pt) >= PRcr then
  //**SONIC FLOW**
   begin
    //Throat
    Mt := 1;
    Pt := Pcyl*power(2/(gam+1),gam/(gam-1));
    Tt := Tcyl*2/(gam+1);
    ct := cCyl*sqrt(2/(gam+1));
    ut := ct;
    Rt := Pt/287/Tt;
    //Entrance
    Cd := IV.FlowCoeff(CA-360,Pcyl/Pt,TRUE);
    ue := InlSonicVelSolve(gam,Cd,Aratio,ut,cCyl);
    P[4] := Pcyl*Cd*Aratio*power(2/(gam+1),(gam+1)/2/(gam-1))
                                      *((1 - (gam-1)/2*sqr(ue/cCyl))/(ue/cCyl));
    ce := sqrt((P[4]*ue*ct)/(Pt*Cd*Aratio));
    c4 := ce;
    Me := ue/ce;
   end
  else
   begin
   //**SUBSONIC FLOW**
    //Throat
    Mt := sqrt(2/(gam-1)*(power(Pcyl/Pt,(gam-1)/gam) - 1));
    ct := cCyl*power(Pt/Pcyl,(gam-1)/gam/2);
    ut := Mt*ct;
    Rt := gam*Pt/sqr(ct);
    //Entrance
    Cd := IV.FlowCoeff(CA-360,Pcyl/Pt,TRUE);
    ue := InlSubSonicVelSolve(gam,Cd,Aratio,ut,ct,cCyl);
    ce := sqrt(sqr(cCyl) - (gam-1)/2*sqr(ue));
    c4 := ce;
    Me := ue/ce;
    P[4] := Pcyl*power(ct/cCyl,2/(gam-1))*Cd*Aratio*(ut/ue)*
                                                     (1-(gam-1)/2*sqr(ue/cCyl));
   end;
   I := 1;
  Repeat
   u[4] := (P[4]-Tplus)/Qplus;
   R[4] := gam*P[4]/sqr(c4);
   pres := P[4];
   dens := R[4];
   if (pres < 0) or (dens < 0) then
   showMessage('ERROR : Press/Dens negative in "Inlet_Valve_Reverse" !');
   c4 := cThermo(gam,pres,dens);
   M4 := u[4]/c4;
   if ABS(M4-Me) >  0.000000001 then
    begin
     if I = 1 then
      begin
       I := I+1;
       P4I := P[4];
       M4I := M4;
       P[4] := 1.001*P[4];
       stop := 0;
      end
     else
      begin
       SL := (M4-M4I)/(P[4]-P4I);
       P4I := P[4];
       M4I := M4;
       P[4] := P[4] + 0.8*(Me-M4)/SL;
       stop := 0;
      end;
    end
    else stop := 1;
  Until stop = 1;
  stop := 0;
  u[4] := -u[4];
  if (Pcyl/Pt) >= PRcr then
   Pt := Pt
  else
   begin
    Ptt := (1/Cd/Aratio)*(u[4]/(-ut))*sqr(ct/c4)*P[4];
    Pt := 0.95*Pt + 0.05*Ptt;
   end;
 end;
 if iter <> 0 then
  if (ABS(u[4]-uD) < (E1*0.0001)) then
   if (ABS(P[4]-PD) < (E2*0.001)) then
    stop := 1;
  uD := u[4];
  PD := P[4];
  iter := iter + 1;
  if iter > 1000 then stop := 1;
 UNTIL stop = 1;
 //POINT 4 IS FIXED
 //****************
 ut := -ut;
 uInlet[Q] := u[4];
 PInlet[Q] := P[4];
 RInlet[Q] := R[4];
 cInlet[Q] := sqrt(gam*PInlet[Q]/RInlet[Q]);
End;  //INLET_VALVE_REVERSE

//******************************************************************************

Procedure INLET_VALVE_OPEN(dt,gam,PCyl,TCyl,CA      : Double;
                           var Mt,ut,ct,Rt,Pt       : Double;
                           AreaInlet,IValveArea     : Double;
                           var XInlet,uInlet,
                               PInlet,RInlet,cInlet : TInletCalcArray;
                           var uInletNew,PInletNew,
                               RInletNew,cInletNew  : TInletCalcArray;
                           IV                       : TValve;
                           var Cd                   : Double;
                           IManf                    : TPipe;
                           IVR, IVF, IVFR            : Double);
  //Airflow through the Inlet Valve into the Cylinder
Type array_var = Array[1..H] of Double;  //Array for x-values
Var I,iter,stop : Integer;   //Parameters
    dx          : Double;    //x-step
    xs          : array_var; //x-coördinates for initial reference points
    x           : array_var; //x-coördinates at temporarily points
    ue          : Double;    //Entrance Pressures
    u           : array_var; //Velocities at temporarily points
    Pstag,Pe    : Double;    //Stagnation & Entrance Pressures
    P           : array_var; //Pressure at temporarily points
    R           : array_var; //Density at temporarily points
    MuL,MPL,MRL : Double;    //Gradient at left
    BuL,BPL,BRL : Double;    //Constant at left
    a           : Double;    //Velocity to determine S.o.S
    c           : Double;    //Speed of sound
    ce,c4       : Double;    //Speed of sound: Entrance,Point4
    pres        : Double;    //Pressure to determine S.o.S
    dens        : Double;    //Density to determine S.o.S
    Lplus       : Double;    //lambdaplus
    Qplus       : Double;    //Q+
    Tplus       : Double;    //T+
    Splus       : Double;    //S+
    Lo,Ao,Bo,T0 : Double;    //lambdao,Ao,Bo,To
    uD,PD,RD    : Double;    //Covergence test for velocity,pressure,density
    PRcr        : Double;    //Critical pressure ratio for sonic flow
    Me          : Double;    //Mach number at point in nozzle throat
    Aratio      : Double;    //Ratio between nozzle throat and entrance
    P4I         : Double;    //New Pressure
    M4,M4I      : Double;    //New Mach Number
    SL          : Double;    //Gradient
    Tstat,Tt,                //Static & Throat Temperature
    Tstag,Te    : Double;    //Stagnation & Entrance Temperature
    Q           : Integer;   //Grid Parameter
    D1,D3       : Double;    //Hydroulic Diameter at each point
    f1,f3       : Double;    //Fanning Friction Factor
    PressDiff   : Double;    //Press diff (Pcyl-Pt)
Begin
//DEFINE INITIAL PROPERTIES AND DETERMINE INTERPOLATING POLYNOMIALS
  gam := 1.3994;
  iter := 0;
  Q := QI;
  Aratio := IValveArea/AreaInlet;
  if Aratio > 1 then
   Aratio := 1;
     //if (ut >= 0) then Pt := Pcyl;
  Cd := IV.FlowCoeff(CA-360,PInlet[Q]/Pcyl,FALSE);
  PRcr := 1/CritPress(gam,Cd,Aratio);
     //PRcr := power((gam+1)/2,gam/(gam-1));
 //Initial x-values
  xs[1] := xInlet[Q-1];
  xs[3] := xInlet[Q];
  x[4] := xInlet[Q];
 //Initial velocity-values
  u[1] := uInlet[Q-1];
  u[3] := uInlet[Q];
 //Initial pressure-values
  P[1] := PInlet[Q-1];
  P[3] := PInlet[Q];
 //Initial density-values
  R[1] := RInlet[Q-1];
  R[3] := RInlet[Q];
 //Determine Interpolating Polynomials for Left Two Points
  dx := xInlet[Q-1] - xInlet[Q];
  //velocity
  MuL := (uInlet[Q-1]-uInlet[Q])/dx;
  BuL := uInlet[Q] - MuL*xInlet[Q];
  //pressure
  MPL := (PInlet[Q-1]-PInlet[Q])/dx;
  BPL := PInlet[Q] - MPL*xInlet[Q];
  //density
  MRL := (RInlet[Q-1]-RInlet[Q])/dx;
  BRL := RInlet[Q] - MRL*xInlet[Q];
//DO ITERATION UNTIL CONVERGENCE IS REACHED
REPEAT
//LOCATE POINT 1 AND DETERMINE COEFFICIENTS ALONG LINE 14(C+)
 REPEAT
  stop := 0;
  if iter = 0 then
   begin
    u[4] := u[1];
    P[4] := P[1];
    R[4] := R[1];
   end;
  a := (u[1]+u[4])/2;
  pres := (P[1]+P[4])/2;
  dens := (R[1]+R[4])/2;
  if (pres < 0) or (dens < 0) then
  showMessage('ERROR : Press/Dens negative in Point 1 at "Inlet_Valve_Open" !!!');
  c := cThermo(gam,pres,dens);
  Lplus := 1/(a+c);
  x[1] := x[4] - dt/Lplus;
  if x[1] > IManf.Length then x[1] := IManf.Length;
  if ABS(x[1]-xs[1]) < 0.001*E4 then
   begin
    D1 := sqrt(4*IManf.Area(x[1])/Pi);
    f1 := FricFact(gam,R[1],u[1],D1,c);
    Qplus := dens*c;
    Splus := - R[1]*u[1]*sqr(c)/IManf.Area(x[1])*IManf.dAdL(x[1])
             + ((gam-1)*u[1] - c)*(R[1]*u[1]*abs(u[1])*2*f1/D1);
    Tplus := P[1] + Qplus*u[1] + Splus*dt;
    stop := 1;
   end
  else
   begin
    xs[1] := x[1];
    u[1] := MuL*x[1] + BuL;
    P[1] := MPL*x[1] + BPL;
    R[1] := MRL*x[1] + BRL;
   end;
 UNTIL stop = 1;
 //POINT 1 IS FIXED
 //****************
//LOCATE POINT 3 AND DETERMINE COEFFICIENTS ALONG LINE 34(Co)
 REPEAT
  stop := 0;
  if iter = 0 then
   begin
    u[4] := u[3];
    P[4] := P[3];
    R[4] := R[3];
   end;
  a := (u[3]+u[4])/2;
  pres := (P[3]+P[4])/2;
  dens := (R[3]+R[4])/2;
  if (pres < 0) or (dens < 0) then
  showMessage('ERROR : Press/Dens negative in Point 3 at "Inlet_Valve_Open" !!!');
  c := cThermo(gam,pres,dens);
  if ABS(a) < 1E-8 then x[3] := x[4]
  else
   begin
    Lo := 1/a;
    x[3] := x[4] - dt/Lo;
   end;
  if x[3] > IManf.Length then x[3] := IManf.Length;
  if ABS(x[3]-xs[3]) < 0.001*E4 then
   begin
    D3 := sqrt(4*IManf.Area(x[3])/Pi);
    f3 := FricFact(gam,R[3],u[3],D3,c);
    Ao := sqr(c);
    Bo := (gam-1)*(R[3]*u[3]*abs(u[3])*2*f3/D3);
    T0 := Bo*(x[4]-x[3]) + P[3] - Ao*R[3];
    stop := 1;
   end
  else
   begin
    xs[3] := x[3];
    u[3] := MuL*x[3] + BuL;
    P[3] := MPL*x[3] + BPL;
    R[3] := MRL*x[3] + BRL;
   end;
 UNTIL stop = 1;
 //POINT 3 IS FIXED
 //****************
//CALCULATE THE PROPERTIES AT POINT 4 AND TEST FOR CONVERGENCE
  if iter = 0 then
   begin
    P[4] := PInlet[Q];
    u[4] := uInlet[Q];
    R[4] := RInlet[Q];
    c4 := sqrt(gam*P[4]/R[4]);
    Pe := P[4];
    PressDiff := 0;
    //if (u[4] < IVF) and (u[4] > IVFR) then
    // u[4] := u[4];
    if P[4] <= Pcyl then PressDiff := Pcyl - Pt;
    if ((ut >= 0) and (ut < c4)) or ((u[4] <= 0) and (u[4] > IVFR)) then
     Pt := Pcyl;
   end;
  if (P[4] <= Pt) and (u[4] > IVF) then
   begin
    P[4] := P[4] + PressDiff*0.5;
    if P[4] <= Pt then P[4] := 1.000001*Pt;
    Pe := P[4];
    u[4] := 0.5*(u[4] + IVF);
    PressDiff := 0;
   end;
  if (P[4] <= Pt) or (u[4] < IVFR) then
   Pstag := Pcyl
  else
   begin
    Pstag := P[4]*power((1 + (gam-1)/2*sqr(u[4]/c4)),gam/(gam-1));
    Tstat := sqr(c4)/287/gam;
    Tstag := Tstat*power(Pstag/P[4],(gam-1)/gam);
   end;
  //**Reverse Flow at Inlet Valve**
  if (P[4] <= Pt) or (u[4] < IVFR) then
   begin
    if u[4] >= 0 then
     Pt := 0.5*Pt + 0.5*P[4];
    uInlet[Q] := u[4];
    RInlet[Q] := R[4];
    PInlet[Q] := P[4];
    cInlet[Q] := sqrt(gam*PInlet[Q]/RInlet[Q]);
    INLET_VALVE_REVERSE(dt,gam,PCyl,TCyl,CA,Mt,ut,ct,Rt,Pt,AreaInlet,
                        IValveArea,XInlet,uInlet,PInlet,RInlet,cInlet,IV,Cd,
                        IManf,IVR, IVF, IVFR);
    u[4] := uInlet[Q];
    R[4] := RInlet[Q];
    P[4] := PInlet[Q];
    c4 := sqrt(gam*PInlet[Q]/RInlet[Q]);
    stop := 1;
   end
 else
 begin
  if (P[4]/Pt) >= PRcr then
  //**SONIC FLOW**
   begin
    //Throat
    Mt := 1;
    Tt := Tstag*2/(gam+1);
    ct := sqrt(gam*287*Tt);
    ut := ct;
    Pt := P[4]*power(ct/c4,2*gam/(gam-1));
    Rt := Pt/287/Tt;
    //Entrance
    Cd := IV.FlowCoeff(CA-360,P[4]/Pcyl,FALSE);
    Me := Cd*Aratio*power(ct/c4,(gam+1)/(gam-1));
    if Me > 1 then Me := 1;
    P[4] := Pstag/power((1 + (gam-1)/2*sqr(Me)),gam/(gam-1));
   end
  else
  //**SUBSONIC FLOW**
   begin
    //Throat
    Pt := Pcyl;
    Tt := Tstag*power(Pt/Pstag,(gam-1)/gam);
    ct := sqrt(gam*287*Tt);
    Mt := sqrt(2/(gam-1)*(power(Pstag/Pt,(gam-1)/gam)-1));
    ut := ct*Mt;
    Rt := Pt/287/Tt;
    //Entrance
    Te := Tt*power(P[4]/Pt,(gam-1)/gam);
    ce := sqrt(gam*287*Te);
    Cd := IV.FlowCoeff(CA-360,P[4]/Pcyl,FALSE);
    if ce < ct then ue := 0
    else ue := sqrt(2/(gam-1)*(sqr(ce)-sqr(ct))/(1/sqr(Cd*Aratio)*
                                                     power(ce/ct,4/(gam-1))-1));
    if ue >= ut then ue := ut;
    Me := ue/ce;
    P[4] := Pstag/power((1 + (gam-1)/2*sqr(Me)),gam/(gam-1));
   end;
  I := 1;
  Repeat
   u[4] := (Tplus-P[4])/Qplus;
   R[4] := (P[4]-T0)/Ao;
   pres := P[4];
   dens := R[4];
   if (pres < 0) or (dens < 0) then
   showMessage('ERROR : Press/Dens negative in "Inlet_Valve_Open" !!!');
   c4 := cThermo(gam,pres,dens);
   M4 := u[4]/c4;
   if ABS(M4-Me) > 0.000000001 then
    begin
     if I = 1 then
      begin
       I := I+1;
       P4I := P[4];
       M4I := M4;
       P[4] := 1.001*P[4];
       stop := 0;
      end
     else
      begin
       SL := (M4-M4I)/(P[4]-P4I);
       P4I := P[4];
       M4I := M4;
       P[4] := P[4] + 0.8*(Me-M4)/SL;
       stop := 0;
      end;
    end
    else stop := 1;
  Until stop = 1;
  stop := 0;
  c4 := sqrt(gam*P[4]/R[4]);
  Pe := 0.95*Pe + 0.05*P[4];
  P[4] := Pe;
 end;
  if (iter <> 0) and (stop = 0)then
   if (ABS(u[4]-uD) < (E1*0.0001)) then
    if (ABS(R[4]-RD) < (E3*0.0001)) then
     if (ABS(P[4]-PD) < (E2*0.001)) then
      stop := 1;
   uD := u[4];
   PD := P[4];
   RD := R[4];
   iter := iter + 1;
   if iter > 1000 then stop := 1;
  UNTIL stop = 1;
  //POINT 4 IS FIXED
  //****************
  uInletNew[Q] := u[4];
  PInletNew[Q] := P[4];
  RInletNew[Q] := R[4];
  cInletNew[Q] := sqrt(gam*PInletNew[Q]/RInletNew[Q]);
End;  //INLET_VALVE_OPEN

//******************************************************************************

Procedure EXHAUST_VALVE_REVERSE(dt,gam,PCyl,TCyl,CA         : Double;
                             var Mt,ut,ct,Rt,Pt             : Double;
                             AreaExhaust,EValveArea         : Double;
                             var XExhaust,uExhaust,
                                 PExhaust,RExhaust,cExhaust : TExhaustCalcArray;
                             EV                             : TValve;
                             var Cd                         : Double;
                             EManf                          : TPipe;
                             EVR, EVF, EVFR                 : Double);
  //Airflow through the Exhaust Valve into the Cylinder
Type array_var = Array[1..H] of Double;  //Array for x-values
Var I,iter,stop : Integer;    //Parameters
    dx          : Double;     //x-step
    xs          : array_var;  //x-coördinates for initial reference points
    x           : array_var;  //x-coördinates at temporarily points
    ue          : Double;     //Velocity at Entrance
    u           : array_var;  //Velocities at temporarily points
    Pstag,Pe    : Double;     //Stagnation,Entrance Pressures
    P           : array_var;  //Pressure at temporarily points
    R           : array_var;  //Density at temporarily points
    MuR,MPR,MRR : Double;     //Gradient at right
    BuR,BPR,BRR : Double;     //Constant at right
    a           : Double;     //Velocity to determine S.o.S
    c           : Double;     //Speed of sound
    ce,c4       : Double;     //Speed of sound at Entrance and Point4
    pres        : Double;     //Pressure to determine S.o.S
    dens        : Double;     //Density to determine S.o.S
    Lminus      : Double;     //lambdaminus
    Qminus      : Double;     //Q-
    Tminus      : Double;     //T-
    Sminus      : Double;     //S-
    Lo,Ao,Bo,T0 : Double;     //lambdao, Ao, To
    uD,PD,RD    : Double;     //Covergence test for velocity,pressure,density
    PRcr        : Double;     //Critical pressure ratio for sonic flow
    Me          : Double;     //Mach number at point in nozzle entrance
    Aratio      : Double;     //Ratio between nozzle throat and entrance
    P4I         : Double;     //New Pressure
    M4,M4I      : Double;     //New Mach Number
    SL          : Double;     //Gradient
    Tstat,Tt,                 //Static & Throat Temperature
    Tstag,Te    : Double;     //Stagnation & Entrance Temperature
    Q           : Integer;    //Grid Parameter
    PstagTest   : Double;     //Pressure Test Parameter
    D2,D3       : Double;     //Hydroulic Diameter
    f2,f3       : Double;     //Fanning Friction Factor
Begin
//DEFINE INITIAL PROPERTIES AND DETERMINE INTERPOLATING POLYNOMIALS
  gam := 1.3;
  iter := 0;
  Aratio := EValveArea/AreaExhaust;
  if Aratio > 1 then Aratio := 1;
  PRcr := power((gam+1)/2,gam/(gam-1));
 //Initial x-values
  xs[2] := xExhaust[2];
  xs[3] := xExhaust[1];
  x[4] := xExhaust[1];
 //Initial velocity-values
  u[2] := uExhaust[2];
  u[3] := uExhaust[1];
 //Initial pressure-values
  P[2] := PExhaust[2];
  P[3] := PExhaust[1];
 //Initial density-values
  R[2] := RExhaust[2];
  R[3] := RExhaust[1];
 //Determine Interpolating Polynomials for Right Two Points
  dx := xExhaust[1] - xExhaust[2];
  //velocity
  MuR := (uExhaust[1]-uExhaust[2])/dx;
  BuR := uExhaust[2] - MuR*xExhaust[2];
  //pressure
  MPR := (PExhaust[1]-PExhaust[2])/dx;
  BPR := PExhaust[2] - MPR*xExhaust[2];
  //density
  MRR := (RExhaust[1]-RExhaust[2])/dx;
  BRR := RExhaust[2] - MRR*xExhaust[2];
//DO ITERATION UNTIL CONVERGENCE IS REACHED
REPEAT
//LOCATE POINT 2 AND DETERMINE COEFFICIENTS ALONG LINE 24(C-)
 REPEAT
  stop := 0;
  if iter = 0 then
   begin
    u[4] := u[2];
    P[4] := P[2];
    R[4] := R[2];
   end;
  a := (u[2]+u[4])/2;
  pres := (P[2]+P[4])/2;
  dens := (R[2]+R[4])/2;
  if (pres < 0) or (dens < 0) then
  showMessage('ERROR : Press/Dens negative in Point 2 at "Exhaust_Valve_Reverse"!');
  c := cThermo(gam,pres,dens);
  Lminus := 1/(a-c);
  x[2] := x[4] - dt/Lminus;
  if x[2] < 0 then x[2] := 0;
  if ABS(x[2]-xs[2]) < 0.001*E4 then
   begin
    D2 := sqrt(4*EManf.Area(x[2])/Pi);
    f2 := FricFact(gam,R[2],u[2],D2,c);
    Qminus := dens*c;
    Sminus := - R[2]*u[2]*sqr(c)/EManf.Area(x[2])*EManf.dAdL(x[2])
              + ((gam-1)*u[2] + c)*(R[2]*u[2]*abs(u[2])*2*f2/D2);
    Tminus := P[2] - Qminus*u[2] + Sminus*dt;
    stop := 1;
   end
  else
   begin
    xs[2] := x[2];
    u[2] := MuR*x[2] + BuR;
    P[2] := MPR*x[2] + BPR;
    R[2] := MRR*x[2] + BRR;
   end;
 UNTIL stop = 1;
 //POINT 2 IS FIXED
 //****************
//LOCATE POINT 3 AND DETERMINE COEFFICIENTS ALONG LINE 34(Co)
 REPEAT
  stop := 0;
  if iter = 0 then
   begin
    u[4] := u[3];
    P[4] := P[3];
    R[4] := R[3];
   end;
  a := (u[3]+u[4])/2;
  pres := (P[3]+P[4])/2;
  dens := (R[3]+R[4])/2;
  if (pres < 0) or (dens < 0) then
  showMessage('ERROR : Press/Dens negative in Point 3 at "Exhaust_Valve_Reverse"');
  c := cThermo(gam,pres,dens);
  if ABS(a) < 1E-8 then x[3] := x[4]
  else
   begin
    Lo := 1/a;
    x[3] := x[4] - dt/Lo;
   end;
  if x[3] < 0 then x[3] := 0;
  if ABS(x[3]-xs[3]) < 0.001*E4 then
   begin
    D3 := sqrt(4*EManf.Area(x[3])/Pi);
    f3 := FricFact(gam,R[3],u[3],D3,c);
    Ao := sqr(c);
    Bo := (gam-1)*(R[3]*u[3]*abs(u[3])*2*f3/D3);
    T0 := Bo*(x[4]-x[3]) + P[3] - Ao*R[3];
    stop := 1;
   end
  else
   begin
    xs[3] := x[3];
    u[3] := MuR*x[3] + BuR;
    P[3] := MPR*x[3] + BPR;
    R[3] := MRR*x[3] + BRR;
   end;
 UNTIL stop = 1;
 //POINT 3 IS FIXED
 //****************
//CALCULATE THE PROPERTIES AT POINT 4 AND TEST FOR CONVERGENCE
  if iter = 0 then
   begin
    P[4] := PExhaust[1];
    u[4] := uExhaust[1];
    R[4] := RExhaust[1];
    Tt := sqr(ct)/gam/287;
    Pe := P[4];
    c4 := sqrt(gam*P[4]/R[4]);
    PstagTest := P[4]*power((1 + (gam-1)/2*sqr(-u[4]/c4)),gam/(gam-1));
   end;
  if (PstagTest <= Pcyl) and (u[4] < EVR) then
   PstagTest := 1.000001*Pcyl;
  if (PstagTest <= Pcyl) or (u[4] > EVF) then
   begin
    Pstag := Pcyl;
    Tstat := sqr(c4)/287/gam;
    Tstag := Tstat*power(Pstag/P[4],(gam-1)/gam);
   end
  else
   begin
    Pstag := P[4]*power((1 + (gam-1)/2*sqr(u[4]/c4)),gam/(gam-1));
    PstagTest := Pstag;
    if (PstagTest <= Pcyl) and (u[4] < 0) then
     begin
      Pstag := 1.000001*Pcyl;
     end;
    Tstat := sqr(c4)/287/gam;
    Tstag := Tstat*power(Pstag/P[4],(gam-1)/gam);
   end;
  //**Substitution for Normal Flow at Exhaust Valve**
  if Pstag <= Pcyl then
   begin
    //Throat
    Pt := 0.5*Pcyl + 0.5*Pt;
    Tt := 0.5*Tcyl + 0.5*Tt;
    Rt := Pt/287/Tt;
    ct := sqrt(gam*287*Tt);
    ut := 0;
    Mt := 0;
    //Entrance
    Te := Tstat;
    ce := sqrt(gam*287*Te);
    u[4] := 0;
    c4 := sqrt(gam*P[4]/R[4]);
    stop := 0;
   end
 else
 begin
  //**SONIC FLOW**
  if (P[4]/Pcyl) >= PRcr then           //???????????
   begin
   //Throat
    Mt := 1;
    Pt := Pstag*power(2/(gam+1),gam/(gam-1));
    Tt := Tstag*2/(gam+1);
    ct := sqrt(gam*287*Tt);
    ut := ct;
    Rt := Pt/287/Tt;
   //Entrance
    Cd := EV.FlowCoeff(CA-360,P[4]/Pcyl,TRUE);
    Me := ExhSonicMachSolve(gam,Cd,Aratio);
    P[4] := Pstag/power((1 + (gam-1)/2*sqr(Me)),gam/(gam-1));
   end
  else
   //**SUBSONIC FLOW**
   begin
    //Throat
    Pt := Pcyl;
    Tt := Tstag*power(Pt/Pstag,(gam-1)/gam);
    ct := sqrt(gam*287*Tt);
    Mt := sqrt(2/(gam-1)*(power(Pstag/Pt,(gam-1)/gam)-1));
    ut := ct*Mt;
    Rt := Pt/287/Tt;
    //Entrance
    Te := Tstag*power(P[4]/Pstag,(gam-1)/gam);
    ce := sqrt(gam*287*Te);
    Cd := EV.FlowCoeff(CA-360,P[4]/Pcyl,TRUE);
    ue := Cd*Aratio*ut*power(ct/ce,2/(gam-1));
    Me := ue/ce;
    P[4] := Pstag/power((1 + (gam-1)/2*sqr(Me)),gam/(gam-1));
   end;
  I := 1;
  Repeat
   u[4] := (Tminus-P[4])/Qminus;
   R[4] := (P[4]-T0)/Ao;
   pres := P[4];
   dens := R[4];
   if (pres < 0) or (dens < 0) then
   showMessage('ERROR : Pressure negative in "Exhaust_Valve_Reverse" !!!');
   c4 := cThermo(gam,pres,dens);
   M4 := u[4]/c4;
   if ABS(M4-Me) > 0.000000001 then
    begin
     if I = 1 then
      begin
       I := I+1;
       P4I := P[4];
       M4I := M4;
       P[4] := 1.001*P[4];
       stop := 0;
      end
     else
      begin
       SL := (M4-M4I)/(P[4]-P4I);
       P4I := P[4];
       M4I := M4;
       P[4] := P[4] + 0.8*(Me-M4)/SL;
       stop := 0;
      end;
    end
    else stop := 1;
  Until stop = 1;
  stop := 0;
  c4 := sqrt(gam*P[4]/R[4]);
  Pe := 0.95*Pe + 0.05*P[4];
  P[4] := Pe;
  u[4] := -u[4];
  PstagTest := P[4]*power((1 + (gam-1)/2*sqr(u[4]/c4)),gam/(gam-1));
 end;
  if iter <> 0 then
   if (ABS(u[4]-uD) < (E1*0.0001)) then
    if (ABS(R[4]-RD) < (E3*0.0001)) then
     if (ABS(P[4]-PD) < (E2*0.001)) then
      stop := 1;
   uD := u[4];
   PD := P[4];
   RD := R[4];
   iter := iter + 1;
   if iter > 1000 then stop := 1;
  UNTIL stop = 1;
 //POINT 4 IS FIXED
 //****************
  ut := -ut;
  uExhaust[1] := u[4];
  PExhaust[1] := P[4];
  RExhaust[1] := R[4];
  cExhaust[1] := sqrt(gam*PExhaust[1]/RExhaust[1]);
End;  //EXHAUST_VALVE_REVERSE

//******************************************************************************

Procedure EXHAUST_VALVE_OPEN(dt,gam,PCyl,TCyl,CA            : Double;
                            var Mt,ut,ct,Rt,Pt              : Double;
                            AreaExhaust,EValveArea          : Double;
                            var XExhaust,uExhaust,PExhaust,
                                RExhaust,cExhaust           : TExhaustCalcArray;
                            var uExhaustNew,PExhaustNew,
                                RExhaustNew,cExhaustNew     : TExhaustCalcArray;
                            EV                              : TValve;
                            var Cd                          : Double;
                            EManf                           : TPipe;
                            EVR, EVF, EVFR                 : Double);
  //Gasflow through the Exhaust Valve from the Cylinder
Type array_var = Array[1..H] of Double;  //Array for x-values
Var  I,iter,stop  : Integer;   //Parameters
     ue           : Double;    //Entrance Velocity
     cCyl         : Double;    //Speed of sound in Cylinder
     ce,c4        : Double;    //Speed of sound at Entrance & Point4
     PRcr         : Double;    //Critical pressure ratio for sonic flow
     Me,M4        : Double;    //Mach number at point in nozzle throat
     Aratio       : Double;    //Ratio between nozzle throat and entrance
     Tt           : Double;    //Throat Temperature
     dx           : Double;    //x-step
     xs           : array_var; //x-coördinates for initial reference points
     x            : array_var; //x-coördinates at temperary points
     u            : array_var; //Velocities at temperery points
     P            : array_var; //Pressure at temperery points
     R            : array_var; //Density at temperery points
     MuR,MPR,MRR  : Double;    //Gradient at right
     BuR,BPR,BRR  : Double;    //Constant at right
     a            : Double;    //Velocity to determine s.o.s
     c            : Double;    //Speed of sound
     pres         : Double;    //Pressure to determine s.o.s
     dens         : Double;    //Density to determine s.o.s
     Lminus       : Double;    //lambdaminus
     Qminus       : Double;    //Q-
     Tminus       : Double;    //T-
     Sminus       : Double;    //S-
     Lo,Ao,T0     : Double;    //lambdao, Ao, To
     uD,PD,RD     : Double;    //Covergence test for velocity,pressure,density
     P4I          : Double;    //New Pressure
     M4I          : Double;    //New Mach Number
     SL           : Double;    //Gradient
     Ptt          : Double;    //Throat Correction Pressures
     D2           : Double;    //Hydroulic Diameter
     f2           : Double;    //Fanning Friction Factor
Begin
//DEFINE INITIAL PROPERTIES AND DETERMINE INTERPOLATING POLYNOMIALS
  gam := 1.3;
  iter := 0;
  PRcr := power((gam+1)/2,gam/(gam-1));
  Aratio := EValveArea/AreaExhaust;
  if Aratio >= 1 then Aratio := 1;
 //Initial x-values
  xs[2] := xExhaust[2];
  x[4] := xExhaust[1];
 //Initial velocity-values
  u[2] := uExhaust[2];
  u[3] := uExhaust[1];
 //Initial pressure-values
  P[2] := PExhaust[2];
  P[3] := PExhaust[1];
 //Initial density-values
  R[2] := RExhaust[2];
  R[3] := RExhaust[1];
 //Determine Interpolating Polynomials for Right Two Points
  dx := xExhaust[1] - xExhaust[2];
  //velocity
  MuR := (uExhaust[1]-uExhaust[2])/dx;
  BuR := uExhaust[2] - MuR*xExhaust[2];
  //pressure
  MPR := (PExhaust[1]-PExhaust[2])/dx;
  BPR := PExhaust[2] - MPR*xExhaust[2];
  //density
  MRR := (RExhaust[1]-RExhaust[2])/dx;
  BRR := RExhaust[2] - MRR*xExhaust[2];
//DO ITERATION UNTIL CONVERGENCE IS REACHED
REPEAT
//LOCATE POINT 2 AND DETERMINE COEFFICIENTS ALONG LINE 24(C-)
 REPEAT
  stop := 0;
  if iter = 0 then
   begin
    u[4] := u[2];
    P[4] := P[2];
    R[4] := R[2];
   end;
  a := (u[2]+u[4])/2;
  pres := (P[2]+P[4])/2;
  dens := (R[2]+R[4])/2;
  if (pres < 0) or (dens < 0) then
  showMessage('ERROR : Press/Dens negative in Point 2 at "Exhaust_Valve_Open" !!!');
  c := cThermo(gam,pres,dens);
  Lminus := 1/(a-c);
  x[2] := x[4] - dt/Lminus;
  if x[2] < 0 then x[2] := 0;
  if ABS(x[2]-xs[2]) < 0.001*E4 then
   begin
    D2 := sqrt(4*EManf.Area(x[2])/Pi);
    f2 := FricFact(gam,R[2],u[2],D2,c);
    Qminus := dens*c;
    Sminus := - R[2]*u[2]*sqr(c)/EManf.Area(x[2])*EManf.dAdL(x[2])
              + ((gam-1)*u[2] + c)*(R[2]*u[2]*abs(u[2])*2*f2/D2);
    Tminus := P[2] - Qminus*u[2] + Sminus*dt;
    stop := 1;
   end
  else
   begin
    xs[2] := x[2];
    u[2] := MuR*x[2] + BuR;
    P[2] := MPR*x[2] + BPR;
    R[2] := MRR*x[2] + BRR;
   end;
 UNTIL stop = 1;
 //POINT 2 IS FIXED
 //****************
//CALCULATE THE PROPERTIES AT POINT 4 AND TEST FOR CONVERGENCE
  if iter = 0 then
   begin
    P[4] := PExhaust[1];
    u[4] := uExhaust[1];
    R[4] := RExhaust[1];
    if u[4] >= 0 then
     if (Pt >= Pcyl) and (u[4] > EVF) then
      Pt := 0.999999*Pcyl
     else
      Pt := Pt
    else
     Pt := Pt;
   end;
 if (Pt >= Pcyl) and (u[4] > EVF) then
  Pt := 0.999999*Pcyl;
 if (Pt >= Pcyl) and (u[4] >= 0) and (u[4] < EVF) then
  Pt := Pt;
 cCyl := sqrt(gam*287*Tcyl);
 //**Reverse Flow at Exhaust Valve**
 if (Pt >= Pcyl) or (u[4] < EVFR) then
  begin
   uExhaust[1] := u[4];
   PExhaust[1] := P[4];
   RExhaust[1] := R[4];
   cExhaust[1] := sqrt(gam*PExhaust[1]/RExhaust[1]);
   EXHAUST_VALVE_REVERSE(dt,gam,PCyl,TCyl,CA,Mt,ut,ct,Rt,Pt,AreaExhaust,
                         EValveArea,XExhaust,uExhaust,PExhaust,RExhaust,
                         cExhaust,EV,Cd,EManf,EVR, EVF, EVFR);
   u[4] := uExhaust[1];
   P[4] := PExhaust[1];
   R[4] := RExhaust[1];
   stop := 1;
  end
 else
 begin
  if (Pcyl/Pt) >= PRcr then
  //**SONIC FLOW**
   begin
    //Throat
    Mt := 1;
    Pt := Pcyl*power(2/(gam+1),gam/(gam-1));
    Tt := Tcyl*2/(gam+1);
    ct := cCyl*sqrt(2/(gam+1));
    ut := ct;
    Rt := Pt/287/Tt;
    //Entrance
    Cd := EV.FlowCoeff(CA-360,Pcyl/Pt,FALSE);
    ue := ExhSonicVelSolve(gam,Cd,Aratio,ut,cCyl);
    P[4] := Pcyl*Cd*Aratio*power(2/(gam+1),(gam+1)/2/(gam-1))
                                      *((1 - (gam-1)/2*sqr(ue/cCyl))/(ue/cCyl));
    ce := sqrt((P[4]*ue*ct)/(Pt*Cd*Aratio));
    c4 := ce;
    Me := ue/ce;
   end
  else
  begin
   //**SUBSONIC FLOW**
    //Throat
    Mt := sqrt(2/(gam-1)*(power(Pcyl/Pt,(gam-1)/gam) - 1));
    ct := cCyl*power(Pt/Pcyl,(gam-1)/gam/2);
    ut := Mt*ct;
    Rt := gam*Pt/sqr(ct);
    //Entrance
    Cd := EV.FlowCoeff(CA-360,Pcyl/Pt,FALSE);
    ue := ExhSubSonicVelSolve(gam,Cd,Aratio,ut,ct,cCyl);
    ce := sqrt(sqr(cCyl) - (gam-1)/2*sqr(ue));
    c4 := ce;
    Me := ue/ce;
    P[4] := Pcyl*power(ct/cCyl,2/(gam-1))*Cd*Aratio*(ut/ue)
                                                    *(1-(gam-1)/2*sqr(ue/cCyl));
   end;
  I := 1;
  Repeat
   u[4] := (P[4]-Tminus)/Qminus;
   R[4] := gam*P[4]/sqr(c4);
   pres := P[4];
   dens := R[4];
   if (pres < 0) or (dens < 0) then
   showMessage('ERROR : Press/Dens negative in "Exhaust_Valve_Open" !!!');
   c4 := cThermo(gam,pres,dens);
   M4 := u[4]/c4;
   if ABS(M4-Me) >  0.000000001 then
    begin
     if I = 1 then
      begin
       I := I+1;
       P4I := P[4];
       M4I := M4;
       P[4] := 0.99999*P[4];
       stop := 0;
      end
     else
      begin
       SL := (M4-M4I)/(P[4]-P4I);
       P4I := P[4];
       M4I := M4;
       P[4] := P[4] + 0.8*(Me-M4)/SL;
       stop := 0;
      end;
    end
   else stop := 1;
  Until stop = 1;
  stop := 0;
  c4 := sqrt(gam*P[4]/R[4]);
  if (Pcyl/Pt) >= PRcr then
   Pt := Pt
  else
   begin
    Ptt := (1/Cd/Aratio)*(u[4]/ut)*sqr(ct/c4)*P[4];
    Pt := 0.95*Pt + 0.05*Ptt;
   end;
 end;
 if iter <> 0 then
  if (ABS(u[4]-uD) < (E1*0.0001)) then
   if (ABS(P[4]-PD) < (E2*0.001)) then
    stop := 1;
  uD := u[4];
  PD := P[4];
  iter := iter + 1;
  if iter > 1000 then stop := 1;
 UNTIL stop = 1;
 //POINT 4 IS FIXED
 //****************
 uExhaustNew[1] := u[4];
 PExhaustNew[1] := P[4];
 RExhaustNew[1] := R[4];
 cExhaustNew[1] := sqrt(gam*PExhaustNew[1]/RExhaustNew[1]);
End;  //EXHAUST_VALVE_OPEN

//******************************************************************************

Procedure INFLOW_INLET_PIPE(dt,gam,Pplenum,Tplenum    : Double;
                            var XInlet,uInlet,PInlet,
                                RInlet,cInlet         : TInletCalcArray;
                            var uInletNew,PInletNew,
                                RInletNew,cInletNew   : TInletCalcArray;
                            IManf                     : TPipe);
  //Inflow of Air from the Atmosphere(Plenum) at the Inlet Pipe
Type array_var = Array[1..H] of Double;  //Array for x-values
Var iter,stop   : Integer;    //Parameters
    dx          : Double;     //x-step
    xs          : array_var;  //x-coördinates for initial reference points
    x           : array_var;  //x-coördinates at temporarily points
    u           : array_var;  //Velocities at temporarily points
    P           : array_var;  //Pressure at temporarily points
    R           : array_var;  //Density at temporarily points
    MuR,MPR,MRR : Double;     //Gradient at right
    BuR,BPR,BRR : Double;     //Constant at right
    c           : Double;     //Speed of sound
    T4          : Double;     //Temperature at point4
    a           : Double;     //Velocity to determine s.o.s
    pres        : Double;     //Pressure to determine s.o.s
    dens        : Double;     //Density to determine s.o.s
    Lminus      : Double;     //lambdaminus
    Qminus      : Double;     //Q-
    Tminus      : Double;     //T-
    Sminus      : Double;     //S-
    Lo,Ao,Bo,T0 : Double;     //lambdao,Ao,Bo,To
    uD,PD,RD    : Double;     //Covergence test for velocity,pressure,density
    D2,D3       : Double;     //Hydroulic Diameter
    f2,f3       : Double;     //Fanning Friction Factor
Begin
//DEFINE INITIAL PROPERTIES AND DETERMINE INTERPOLATING POLYNOMIALS
  gam := 1.3994;
  iter := 0;
 //Initial x-values
  xs[2] := xInlet[2];
  xs[3] := xInlet[1];
  x[4] := xInlet[1];
 //Initial velocity-values
  u[2] := uInlet[2];
  u[3] := uInlet[1];
 //Initial pressure-values
  P[2] := PInlet[2];
  P[3] := PInlet[1];
 //Initial density-values
  R[2] := RInlet[2];
  R[3] := RInlet[1];
 //Determine Interpolating Polynomials for Right Two Points
  dx := xInlet[1] - xInlet[2];
  //velocity
  MuR := (uInlet[1]-uInlet[2])/dx;
  BuR := uInlet[2] - MuR*xInlet[2];
  //pressure
  MPR := (PInlet[1]-PInlet[2])/dx;
  BPR := PInlet[2] - MPR*xInlet[2];
  //density
  MRR := (RInlet[1]-RInlet[2])/dx;
  BRR := RInlet[2] - MRR*xInlet[2];
//DO ITERATION UNTIL CONVERGENCE IS REACHED
REPEAT
//LOCATE POINT 2 AND DETERMINE COEFFICIENTS ALONG LINE 24(C-)
 REPEAT
  stop := 0;
  if iter = 0 then
   begin
    u[4] := u[2];
    P[4] := P[2];
    R[4] := R[2];
   end;
  a := (u[2]+u[4])/2;
  pres := (P[2]+P[4])/2;
  dens := (R[2]+R[4])/2;
  if (pres < 0) or (dens < 0) then
  showMessage('ERROR : Press/Dens negative in Point 2 at "Inflow_Inlet_Pipe" !!!');
  c := cThermo(gam,pres,dens);
  Lminus := 1/(a-c);
  x[2] := x[4] - dt/Lminus;
  if x[2] < 0 then x[2] := 0;
  if ABS(x[2]-xs[2]) < 0.001*E4 then
   begin
    D2 := sqrt(4*IManf.Area(x[2])/Pi);
    f2 := FricFact(gam,R[2],u[2],D2,c);
    Qminus := dens*c;
    Sminus := - R[2]*u[2]*sqr(c)/IManf.Area(x[2])*IManf.dAdL(x[2])
              + ((gam-1)*u[2] + c)*(R[2]*u[2]*abs(u[2])*2*f2/D2);
    Tminus := P[2] - Qminus*u[2] + Sminus*dt;
    stop := 1;
   end
  else
   begin
    xs[2] := x[2];
    u[2] := MuR*x[2] + BuR;
    P[2] := MPR*x[2] + BPR;
    R[2] := MRR*x[2] + BRR;
   end;
 UNTIL stop = 1;
 //POINT 2 IS FIXED
 //****************
//LOCATE POINT 3 AND DETERMINE COEFFICIENTS ALONG LINE 34(Co)
 REPEAT
  stop := 0;
  if iter = 0 then
   begin
    u[4] := u[3];
    P[4] := P[3];
    R[4] := R[3];
   end;
  a := (u[3]+u[4])/2;
  pres := (P[3]+P[4])/2;
  dens := (R[3]+R[4])/2;
  if (pres < 0) or (dens < 0) then
  showMessage('ERROR : Press/Dens negative in Point 3 at "Inflow_Inlet_Pipe" !!!');
  c := cThermo(gam,pres,dens);
  if ABS(a) < 1E-8 then x[3] := x[4]
  else
   begin
    Lo := 1/a;
    x[3] := x[4] - dt/Lo;
   end;
  if x[3] < 0 then x[3] := 0;
  if ABS(x[3]-xs[3]) < 0.001*E4 then
   begin
    D3 := sqrt(4*IManf.Area(x[3])/Pi);
    f3 := FricFact(gam,R[3],u[3],D3,c);
    Ao := sqr(c);
    Bo := (gam-1)*(R[3]*u[3]*abs(u[3])*2*f3/D3);
    T0 := Bo*(x[4]-x[3]) + P[3] - Ao*R[3];
    stop := 1;
   end
  else
   begin
    xs[3] := x[3];
    u[3] := MuR*x[3] + BuR;
    P[3] := MPR*x[3] + BPR;
    R[3] := MRR*x[3] + BRR;
   end;
 UNTIL stop = 1;
 //POINT 3 IS FIXED
 //****************
//CALCULATE THE PROPERTIES AT POINT 4 AND TEST FOR CONVERGENCE
 stop := 0;
 if (uInlet[1] < 0) and (iter = 0) or (u[4] < 0) and (iter > 0) then
  begin
   P[4] := Pplenum;
   u[4] := (P[4]-Tminus)/Qminus;
   R[4] := (P[4]-T0)/Ao;
  end
 else
  begin
   if iter = 0 then u[4] := uInlet[1];
   T4 := Tplenum - ((gam-1)/2/gam*sqr(u[4])/287);
   P[4] := Pplenum*power((T4/Tplenum),(gam/(gam-1)));
   R[4] := P[4]/(287*T4);
   u[4] := (P[4]-Tminus)/Qminus;
  end;
 if iter <> 0 then
  if (ABS(u[4]-uD) < (E1*0.0001)) then
   if (ABS(P[4]-PD) < (E2*0.001)) then
    stop := 1;
  uD := u[4];
  PD := P[4];
  iter := iter + 1;
  if iter > 1000 then stop := 1;
 UNTIL stop = 1;
 //POINT 4 IS FIXED
 //****************
 uInletNew[1] := u[4];
 PInletNew[1] := P[4];
 RInletNew[1] := R[4];
 cInletNew[1] := sqrt(gam*PInletNew[1]/RInletNew[1]);
End;  //INFLOW_INLET_PIPE

//******************************************************************************

Procedure OUTFLOW_EXHAUST_PIPE(dt,gam,Pback,Tback           : Double;
                               var XExhaust,uExhaust,PExhaust,
                                   RExhaust,cExhaust        : TExhaustCalcArray;
                               var uExhaustNew,PExhaustNew,
                                   RExhaustNew,cExhaustNew  : TExhaustCalcArray;
                               EManf                        : TPipe);
  //Exhaust Gas Outflow to Atmosphere(Back Pressure) in Exhaust Pipe
Type array_var = Array[1..H] of Double;  //Array for x-values
Var iter,stop    : Integer;    //Parameters
    dx           : Double;     //x-step
    xs           : array_var;  //x-coördinates for initial reference points
    x            : array_var;  //x-coördinates at temporarily points
    u            : array_var;  //Velocities at temporarily points
    P            : array_var;  //Pressure at temporarily points
    R            : array_var;  //Density at temporarily points
    MuL,MPL,MRL  : Double;     //Gradient at left
    BuL,BPL,BRL  : Double;     //Constant at left
    a            : Double;     //Velocity to determine s.o.s
    c            : Double;     //Speed of sound
    pres         : Double;     //Pressure to determine s.o.s
    dens         : Double;     //Density to determine s.o.s
    Lplus        : Double;     //lambdaplus
    Qplus        : Double;     //Q+
    Tplus        : Double;     //T+
    Splus        : Double;     //S+
    Lo,Ao,Bo,T0  : Double;     //lambdao,Ao,Bo,To
    uD,PD,RD     : Double;     //Covergence test for velocity,pressure,density
    Q            : Integer;    //Grid Parameter
    T4           : Double;     //Temperature at Point4
    D1,D3        : Double;     //Hydroulic Diameter
    f1,f3        : Double;     //Fanning Friction Factor
Begin
//DEFINE INITIAL PROPERTIES AND DETERMINE INTERPOLATING POLYNOMIALS
  gam := 1.3;
  iter := 0;
  Q := QE;
 //Initial x-values
  xs[1] := xExhaust[Q-1];
  xs[3] := xExhaust[Q];
  x[4] := xExhaust[Q];
 //Initial velocity-values
  u[1] := uExhaust[Q-1];
  u[3] := uExhaust[Q];
 //Initial pressure-values
  P[1] := PExhaust[Q-1];
  P[3] := PExhaust[Q];
 //Initial density-values
  R[1] := RExhaust[Q-1];
  R[3] := RExhaust[Q];
 //Determine Interpolating Polynomials for Left Two Points
  dx := xExhaust[Q-1] - xExhaust[Q];
  //velocity
  MuL := (uExhaust[Q-1]-uExhaust[Q])/dx;
  BuL := uExhaust[Q] - MuL*xExhaust[Q];
  //pressure
  MPL := (PExhaust[Q-1]-PExhaust[Q])/dx;
  BPL := PExhaust[Q] - MPL*xExhaust[Q];
  //density
  MRL := (RExhaust[Q-1]-RExhaust[Q])/dx;
  BRL := RExhaust[Q] - MRL*xExhaust[Q];
//DO ITERATION UNTIL CONVERGENCE IS REACHED
REPEAT
//LOCATE POINT 1 AND DETERMINE COEFFICIENTS ALONG LINE 14(C+)
 REPEAT
  stop := 0;
  if iter = 0 then
   begin
    u[4] := u[1];
    P[4] := P[1];
    R[4] := R[1];
   end;
  a := (u[1]+u[4])/2;
  pres := (P[1]+P[4])/2;
  dens := (R[1]+R[4])/2;
  if (pres < 0) or (dens < 0) then
  showMessage('ERROR : Press/Dens negative in Point 1 at "Outflow_Exhaust_Pipe" !!!');
  c := cThermo(gam,pres,dens);
  Lplus := 1/(a+c);
  x[1] := x[4] - dt/Lplus;
  if x[1] > EManf.Length then x[1] := EManf.Length;
  if ABS(x[1]-xs[1]) < 0.001*E4 then
   begin
    D1 := sqrt(4*EManf.Area(x[1])/Pi);
    f1 := FricFact(gam,R[1],u[1],D1,c);
    Qplus := dens*c;
    Splus := - R[1]*u[1]*sqr(c)/EManf.Area(x[1])*EManf.dAdL(x[1])
             + ((gam-1)*u[1] - c)*(R[1]*u[1]*abs(u[1])*2*f1/D1);
    Tplus := P[1] + Qplus*u[1] + Splus*dt;
    stop := 1;
   end
  else
   begin
    xs[1] := x[1];
    u[1] := MuL*x[1] + BuL;
    P[1] := MPL*x[1] + BPL;
    R[1] := MRL*x[1] + BRL;
   end;
 UNTIL stop = 1;
 //POINT 1 IS FIXED
 //****************
//LOCATE POINT 3 AND DETERMINE COEFFICIENTS ALONG LINE 34(Co)
 REPEAT
  stop := 0;
  if iter = 0 then
   begin
    u[4] := u[3];
    P[4] := P[3];
    R[4] := R[3];
   end;
  a := (u[3]+u[4])/2;
  pres := (P[3]+P[4])/2;
  dens := (R[3]+R[4])/2;
  if (pres < 0) or (dens < 0) then
  showMessage('ERROR : Press/Dens negative in Point 3 at "Outflow_Exhaust_Pipe" !!!');
  c := cThermo(gam,pres,dens);
  if ABS(a) < 1E-8 then x[3] := x[4]
  else
   begin
    Lo := 1/a;
    x[3] := x[4] - dt/Lo;
   end;
  if x[3] > EManf.Length then x[3] := EManf.Length;
  if ABS(x[3]-xs[3]) < 0.001*E4 then
   begin
    D3 := sqrt(4*EManf.Area(x[3])/Pi);
    f3 := FricFact(gam,R[3],u[3],D3,c);
    Ao := sqr(c);
    Bo := (gam-1)*(R[3]*u[3]*abs(u[3])*2*f3/D3);
    T0 := Bo*(x[4]-x[3]) + P[3] - Ao*R[3];
    stop := 1;
   end
  else
   begin
    xs[3] := x[3];
    u[3] := MuL*x[3] + BuL;
    P[3] := MPL*x[3] + BPL;
    R[3] := MRL*x[3] + BRL;
   end;
 UNTIL stop = 1;
 //POINT 3 IS FIXED
 //****************
//CALCULATE THE PROPERTIES AT POINT 4 AND TEST FOR CONVERGENCE
 stop := 0;
if (uExhaust[Q] < 0) and (iter = 0) or (u[4] < 0) and (iter > 0) then
 begin
  if iter = 0 then u[4] := uExhaust[Q];
  T4 := Tback - ((gam-1)/2/gam*sqr(u[4])/287);
  P[4] := Pback*power((T4/Tback),(gam/(gam-1)));
  R[4] := P[4]/(287*T4);
  u[4] := (Tplus-P[4])/Qplus;
 end
else
 begin
  P[4] := Pback;
  u[4] := (Tplus-P[4])/Qplus;
  R[4] := (P[4]-T0)/Ao;
 end;
 if iter <> 0 then
  if (ABS(u[4]-uD) < (E1*0.0001)) then
   if (ABS(R[4]-RD) < (E3*0.0001)) then
    if (ABS(P[4]-PD) < (E2*0.001)) then
     stop := 1;
  uD := u[4];
  RD := R[4];
  PD := P[4];
  iter := iter + 1;
  if iter > 1000 then stop := 1;
 UNTIL stop = 1;
 //POINT 4 IS FIXED
 //****************
 uExhaustNew[Q] := u[4];
 PExhaustNew[Q] := P[4];
 RExhaustNew[Q] := R[4];
 cExhaustNew[Q] := sqrt(gam*PExhaustNew[Q]/RExhaustNew[Q]);
End;  //OUTFLOW_EXHAUST_PIPE

//******************************************************************************

Procedure INTERNAL_PIPE(dt,gam,Choice                    : Double;
                        var XInlet,uInlet,PInlet,RInlet,
                            cInlet                       : TInletCalcArray;
                        var XExhaust,uExhaust,PExhaust,
                            RExhaust,cExhaust            : TExhaustCalcArray;
                        var uInletNew,PInletNew,
                            RInletNew,cInletNew          : TInletCalcArray;
                        var uExhaustNew,PExhaustNew,
                            RExhaustNew,cExhaustNew      : TExhaustCalcArray;
                        IManf,EManf                      : TPipe);
  //Interior Point in the Inlet & Exhaust Pipe
Type array_var = Array[1..H] of Double;  //Array for x-values
Var  iter,stop    : Integer;    //Parameters
     dx           : Double;     //x-step
     xs           : array_var;  //x-coördinates for initial reference points
     x            : array_var;  //x-coördinates at temperary points
     u            : array_var;  //Velocities at temperery points
     P            : array_var;  //Pressure at temperery points
     R            : array_var;  //Density at temperery points
     MuL,MPL,MRL  : Double;     //Gradient at left
     MuR,MPR,MRR  : Double;     //Gradient at right
     BuL,BPL,BRL  : Double;     //Constant at left
     BuR,BPR,BRR  : Double;     //Constant at right
     a            : Double;     //Velocity to determine s.o.s
     c            : Double;     //Speed of sound
     pres         : Double;     //Pressure to determine s.o.s
     dens         : Double;     //Density to determine s.o.s
     Lplus,Lminus : Double;     //lambdaplus, lambdaminus
     Qplus,Qminus : Double;     //Q+, Q-
     Tplus,Tminus : Double;     //T+, T-
     Splus,Sminus : Double;     //S+, S-
     Lo,Ao,Bo,T0  : Double;     //lambdao,Ao,Bo,To
     uD,PD,RD     : Double;     //Covergence test for velocity,pressure,density
     D1,D2,D3     : Double;     //Hydroulic Diameter
     f1,f2,f3     : Double;     //Fanning Friction Factor
Begin
 iF choice = 1 THEN //******CALCULATE FOR INLET PIPE**********************
 bEGIN
//DEFINE INITIAL PROPERTIES AND DETERMINE INTERPOLATING POLYNOMIALS
  gam := 1.3994;
  iter := 0;
 //Initial x-values
  xs[1] := xInlet[W-2];
  xs[2] := xInlet[W];
  xs[3] := xInlet[W-1];
  x[4] := xInlet[W-1];
 //Initial velocity-values
  u[1] := uInlet[W-2];
  u[2] := uInlet[W];
  u[3] := uInlet[W-1];
 //Initial pressure-values
  P[1] := PInlet[W-2];
  P[2] := PInlet[W];
  P[3] := PInlet[W-1];
 //Initial density-values
  R[1] := RInlet[W-2];
  R[2] := RInlet[W];
  R[3] := RInlet[W-1];
 //Determine Interpolating Polynomials for Left Two Points
  dx := xInlet[W-2] - xInlet[W-1];
  //velocity
  MuL := (uInlet[W-2]-uInlet[W-1])/dx;
  BuL := uInlet[W-1] - MuL*xInlet[W-1];
  //pressure
  MPL := (PInlet[W-2]-PInlet[W-1])/dx;
  BPL := PInlet[W-1] - MPL*xInlet[W-1];
  //density
  MRL := (RInlet[W-2]-RInlet[W-1])/dx;
  BRL := RInlet[W-1] - MRL*xInlet[W-1];
 //Determine Interpolating Polynomials for Right Two Points
  dx := xInlet[W-1] - xInlet[W];
  //velocity
  MuR := (uInlet[W-1]-uInlet[W])/dx;
  BuR := uInlet[W] - MuR*xInlet[W];
  //pressure
  MPR := (PInlet[W-1]-PInlet[W])/dx;
  BPR := PInlet[W] - MPR*xInlet[W];
  //density
  MRR := (RInlet[W-1]-RInlet[W])/dx;
  BRR := RInlet[W] - MRR*xInlet[W];
//DO ITERATION UNTIL CONVERGENCE IS REACHED
REPEAT
//LOCATE POINT 1 AND DETERMINE COEFFICIENTS ALONG LINE 14(C+)
 REPEAT
  stop := 0;
  if iter = 0 then
   begin
    u[4] := u[1];
    P[4] := P[1];
    R[4] := R[1];
   end;
  a := (u[1]+u[4])/2;
  pres := (P[1]+P[4])/2;
  dens := (R[1]+R[4])/2;
  if (pres < 0) or (dens < 0) then
  showMessage('ERROR : Press/Dens negative in Point 1 at Inlet "Internal_Pipe" !!!');
  c := cThermo(gam,pres,dens);
  Lplus := 1/(a+c);
  x[1] := x[4] - dt/Lplus;
  if x[1] < 0 then x[1] := 0;
  if ABS(x[1]-xs[1]) < 0.001*E4 then
   begin
    D1 := sqrt(4*IManf.Area(x[1])/Pi);
    f1 := FricFact(gam,R[1],u[1],D1,c);
    Qplus := dens*c;
    Splus := - R[1]*u[1]*sqr(c)/IManf.Area(x[1])*IManf.dAdL(x[1])
             + ((gam-1)*u[1] - c)*(R[1]*u[1]*abs(u[1])*2*f1/D1);
    Tplus := P[1] + Qplus*u[1] + Splus*dt;
    stop := 1;
   end
  else
   begin
    xs[1] := x[1];
    if x[1] > xInlet[W-1] then
     begin
      u[1] := MuR*x[1] + BuR;
      P[1] := MPR*x[1] + BPR;
      R[1] := MRR*x[1] + BRR;
     end
    else
     begin
      u[1] := MuL*x[1] + BuL;
      P[1] := MPL*x[1] + BPL;
      R[1] := MRL*x[1] + BRL;
     end;
   end;
 UNTIL stop = 1;
 //POINT 1 IS FIXED
 //****************
//LOCATE POINT 2 AND DETERMINE COEFFICIENTS ALONG LINE 24(C-)
 REPEAT
  stop := 0;
  if iter = 0 then
   begin
    u[4] := u[2];
    P[4] := P[2];
    R[4] := R[2];
   end;
  a := (u[2]+u[4])/2;
  pres := (P[2]+P[4])/2;
  dens := (R[2]+R[4])/2;
  if (pres < 0) or (dens < 0) then
  showMessage('ERROR : Press/Dens negative in Point 2 at Inlet "Internal_Pipe" !!!');
  c := cThermo(gam,pres,dens);
  Lminus := 1/(a-c);
  x[2] := x[4] - dt/Lminus;
  if x[2] > IManf.Length then
   x[2] := IManf.Length;
  if (ABS(x[2]-xs[2]) < 0.001*E4) then
   begin
    D2 := sqrt(4*IManf.Area(x[2])/Pi);
    f2 := FricFact(gam,R[2],u[2],D2,c);
    Qminus := dens*c;
    Sminus := - R[2]*u[2]*sqr(c)/IManf.Area(x[2])*IManf.dAdL(x[2])
              + ((gam-1)*u[2] + c)*(R[2]*u[2]*abs(u[2])*2*f2/D2);
    Tminus := P[2] - Qminus*u[2] + Sminus*dt;
    stop := 1;
   end
  else
   begin
    xs[2] := x[2];
    if x[2] > xInlet[W-1] then
     begin
      u[2] := MuR*x[2] + BuR;
      P[2] := MPR*x[2] + BPR;
      R[2] := MRR*x[2] + BRR;
     end
    else
     begin
      u[2] := MuL*x[2] + BuL;
      P[2] := MPL*x[2] + BPL;
      R[2] := MRL*x[2] + BRL;
     end;
   end;
 UNTIL stop = 1;
 //POINT 2 IS FIXED
 //****************
//LOCATE POINT 3 AND DETERMINE COEFFICIENTS ALONG LINE 34(Co)
 REPEAT
  stop := 0;
  if iter = 0 then
   begin
    u[4] := u[3];
    P[4] := P[3];
    R[4] := R[3];
   end;
  a := (u[3]+u[4])/2;
  pres := (P[3]+P[4])/2;
  dens := (R[3]+R[4])/2;
  if (pres < 0) or (dens < 0) then
  showMessage('ERROR : Press/Dens negative in Point 3 at Inlet "Internal_Pipe" !!!');
  c := cThermo(gam,pres,dens);
  if ABS(a) < 1E-8 then x[3] := x[4]
  else
   begin
    Lo := 1/a;
    x[3] := x[4] - dt/Lo;
   end;
  if x[3] < 0 then x[3] := 0;
  if x[3] > IManf.Length then x[3] := IManf.Length;
  if ABS(x[3]-xs[3]) < 0.001*E4 then
   begin
    D3 := sqrt(4*IManf.Area(x[3])/Pi);
    f3 := FricFact(gam,R[3],u[3],D3,c);
    Ao := sqr(c);
    Bo := (gam-1)*(R[3]*u[3]*abs(u[3])*2*f3/D3);
    T0 := Bo*(x[4]-x[3]) + P[3] - Ao*R[3];
    stop := 1;
   end
  else
   begin
    xs[3] := x[3];
    if x[3] > xInlet[W-1] then
     begin
      u[3] := MuR*x[3] + BuR;
      P[3] := MPR*x[3] + BPR;
      R[3] := MRR*x[3] + BRR;
     end
    else
     begin
      u[3] := MuL*x[3] + BuL;
      P[3] := MPL*x[3] + BPL;
      R[3] := MRL*x[3] + BRL;
     end;
   end;
 UNTIL stop = 1;
 //POINT 3 IS FIXED
 //****************
//CALCULATE THE PROPERTIES AT POINT 4 AND TEST FOR CONVERGENCE
  stop := 0;
  u[4] := (Tplus-Tminus)/(Qplus+Qminus);
  P[4] := Tplus - Qplus*u[4];
  R[4] := (P[4]-T0)/Ao;
  if iter <> 0 then
   if (ABS(u[4]-uD) < (E1*0.0001)) then
    if (ABS(P[4]-PD) < (E2*0.001)) then
     if (ABS(R[4]-RD) < (E3*0.0001)) then
      stop := 1;
  uD := u[4];
  PD := P[4];
  RD := R[4];
  iter := iter + 1;
  if iter > 1000 then stop := 1;
 UNTIL stop = 1;
 //POINT 4 IS FIXED
 //****************
  uInletNew[W-1] := u[4];
  PInletNew[W-1] := P[4];
  RInletNew[W-1] := R[4];
  cInletNew[W-1] := sqrt(gam*PInletNew[W-1]/RInletNew[W-1]);
 eND

eLSE IF choice = 2 then//*******CALCULATE FOR EXHAUST PIPE****************
 bEGIN
//DEFINE INITIAL PROPERTIES AND DETERMINE INTERPOLATING POLYNOMIALS
  gam := 1.3;
  iter := 0;
 //Initial x-values
  xs[1] := xExhaust[W-2];
  xs[2] := xExhaust[W];
  xs[3] := xExhaust[W-1];
  x[4] := xExhaust[W-1];
 //Initial velocity-values
  u[1] := uExhaust[W-2];
  u[2] := uExhaust[W];
  u[3] := uExhaust[W-1];
 //Initial pressure-values
  P[1] := PExhaust[W-2];
  P[2] := PExhaust[W];
  P[3] := PExhaust[W-1];
 //Initial density-values
  R[1] := RExhaust[W-2];
  R[2] := RExhaust[W];
  R[3] := RExhaust[W-1];
 //Determine Interpolating Polynomials for Left Two Points
  dx := xExhaust[W-2] - xExhaust[W-1];
  //velocity
  MuL := (uExhaust[W-2]-uExhaust[W-1])/dx;
  BuL := uExhaust[W-1] - MuL*xExhaust[W-1];
  //pressure
  MPL := (PExhaust[W-2]-PExhaust[W-1])/dx;
  BPL := PExhaust[W-1] - MPL*xExhaust[W-1];
  //density
  MRL := (RExhaust[W-2]-RExhaust[W-1])/dx;
  BRL := RExhaust[W-1] - MRL*xExhaust[W-1];
 //Determine Interpolating Polynomials for Right Two Points
  dx := xExhaust[W-1] - xExhaust[W];
  //velocity
  MuR := (uExhaust[W-1]-uExhaust[W])/dx;
  BuR := uExhaust[W] - MuR*xExhaust[W];
  //pressure
  MPR := (PExhaust[W-1]-PExhaust[W])/dx;
  BPR := PExhaust[W] - MPR*xExhaust[W];
  //density
  MRR := (RExhaust[W-1]-RExhaust[W])/dx;
  BRR := RExhaust[W] - MRR*xExhaust[W];
//DO ITERATION UNTIL CONVERGENCE IS REACHED
REPEAT
//LOCATE POINT 1 AND DETERMINE COEFFICIENTS ALONG LINE 14(C+)
 REPEAT
  stop := 0;
  if iter = 0 then
   begin
    u[4] := u[1];
    P[4] := P[1];
    R[4] := R[1];
   end;
  a := (u[1]+u[4])/2;
  pres := (P[1]+P[4])/2;
  dens := (R[1]+R[4])/2;
  if (pres < 0) or (dens < 0) then
  showMessage('ERROR : Press/Dens negative in Point 1 at Exhaust "Internal_Pipe" !!!');
  c := cThermo(gam,pres,dens);
  Lplus := 1/(a+c);
  x[1] := x[4] - dt/Lplus;
  if x[1] < 0 then x[1] := 0;
  if ABS(x[1]-xs[1]) < 0.001*E4 then
   begin
    D1 := sqrt(4*EManf.Area(x[1])/Pi);
    f1 := FricFact(gam,R[1],u[1],D1,c);
    Qplus := dens*c;
    Splus := - R[1]*u[1]*sqr(c)/EManf.Area(x[1])*EManf.dAdL(x[1])
             + ((gam-1)*u[1] - c)*(R[1]*u[1]*abs(u[1])*2*f1/D1);
    Tplus := P[1] + Qplus*u[1] + Splus*dt;
    stop := 1;
   end
  else
   begin
    xs[1] := x[1];
    if x[1] > xExhaust[W-1] then
     begin
      u[1] := MuR*x[1] + BuR;
      P[1] := MPR*x[1] + BPR;
      R[1] := MRR*x[1] + BRR;
     end
    else
     begin
      u[1] := MuL*x[1] + BuL;
      P[1] := MPL*x[1] + BPL;
      R[1] := MRL*x[1] + BRL;
     end;
   end;
 UNTIL stop = 1;
 //POINT 1 IS FIXED
 //****************
//LOCATE POINT 2 AND DETERMINE COEFFICIENTS ALONG LINE 24(C-)
 REPEAT
  stop := 0;
  if iter = 0 then
   begin
    u[4] := u[2];
    P[4] := P[2];
    R[4] := R[2];
   end;
  a := (u[2]+u[4])/2;
  pres := (P[2] + P[4])/2;
  dens := (R[2]+R[4])/2;
  if (pres < 0) or (dens < 0) then
  showMessage('ERROR : Press/Dens negative in Point 2 at Exhaust "Internal_Pipe" !!!');
  c := cThermo(gam,pres,dens);
  Lminus := 1/(a-c);
  x[2] := x[4] - dt/Lminus;
  if x[2] > EManf.Length then x[2] := EManf.Length;
  if ABS(x[2]-xs[2]) < 0.001*E4 then
   begin
    D2 := sqrt(4*EManf.Area(x[2])/Pi);
    f2 := FricFact(gam,R[2],u[2],D2,c);
    Qminus := dens*c;
    Sminus := - R[2]*u[2]*sqr(c)/EManf.Area(x[2])*EManf.dAdL(x[2])
              + ((gam-1)*u[2] + c)*(R[2]*u[2]*abs(u[2])*2*f2/D2);
    Tminus := P[2] - Qminus*u[2] + Sminus*dt;
    stop := 1;
   end
  else
   begin
    xs[2] := x[2];
    if x[2] > xExhaust[W-1] then
     begin
      u[2] := MuR*x[2] + BuR;
      P[2] := MPR*x[2] + BPR;
      R[2] := MRR*x[2] + BRR;
     end
    else
     begin
      u[2] := MuL*x[2] + BuL;
      P[2] := MPL*x[2] + BPL;
      R[2] := MRL*x[2] + BRL;
     end;
   end;
 UNTIL stop = 1;
 //POINT 2 IS FIXED
 //****************
//LOCATE POINT 3 AND DETERMINE COEFFICIENTS ALONG LINE 34(Co)
 REPEAT
  stop := 0;
  if iter = 0 then
   begin
    u[4] := u[3];
    P[4] := P[3];
    R[4] := R[3];
   end;
  a := (u[3]+u[4])/2;
  pres := (P[3]+P[4])/2;
  dens := (R[3]+R[4])/2;
  if (pres < 0) or (dens < 0) then
  showMessage('ERROR : Press/Dens negative in Point 3 at Exhaust "Internal_Pipe" !!!');
  c := cThermo(gam,pres,dens);
  if ABS(a) < 1E-8 then x[3] := x[4]
  else
   begin
    Lo := 1/a;
    x[3] := x[4] - dt/Lo;
   end;
  if x[3] < 0 then x[3] := 0;
  if x[3] > EManf.Length then x[3] := EManf.Length;
  if ABS(x[3]-xs[3]) < 0.001*E4 then
   begin
    D3 := sqrt(4*EManf.Area(x[3])/Pi);
    f3 := FricFact(gam,R[3],u[3],D3,c);
    Ao := sqr(c);
    Bo := (gam-1)*(R[3]*u[3]*abs(u[3])*2*f3/D3);
    T0 := Bo*(x[4]-x[3]) + P[3] - Ao*R[3];
    stop := 1;
   end
  else
   begin
    xs[3] := x[3];
    if x[3] > xExhaust[W-1] then
     begin
      u[3] := MuR*x[3] + BuR;
      P[3] := MPR*x[3] + BPR;
      R[3] := MRR*x[3] + BRR;
     end
    else
     begin
      u[3] := MuL*x[3] + BuL;
      P[3] := MPL*x[3] + BPL;
      R[3] := MRL*x[3] + BRL;
     end;
   end;
 UNTIL stop = 1;
 //POINT 3 IS FIXED
 //****************
//CALCULATE THE PROPERTIES AT POINT 4 AND TEST FOR CONVERGENCE
  stop := 0;
  u[4] := (Tplus-Tminus)/(Qplus+Qminus);
  P[4] := Tplus - Qplus*u[4];
  R[4] := (P[4]-T0)/Ao;
  if iter <> 0 then
   if (ABS(u[4]-uD) < (E1*0.0001)) then
    if (ABS(P[4]-PD) < (E2*0.001)) then
     if (ABS(R[4]-RD) < (E3*0.0001)) then
      stop := 1;
  uD := u[4];
  PD := P[4];
  RD := R[4];
  iter := iter + 1;
  if iter > 1000 then stop := 1;
 UNTIL stop = 1;
 //POINT 4 IS FIXED
 //****************
  uExhaustNew[W-1] := u[4];
  PExhaustNew[W-1] := P[4];
  RExhaustNew[W-1] := R[4];
  cExhaustNew[W-1] := sqrt(gam*PExhaustNew[W-1]/RExhaustNew[W-1]);
 eND;
End;  //INTERNAL_PIPE

//******************************************************************************

Procedure INLET_VALVE_CLOSED(dt,gam,uSOLID             : Double;
                             var XInlet,uInlet,PInlet,
                                 RInlet,cInlet         : TInletCalcArray;
                             var uInletNew,PInletNew,
                                 RInletNew,cInletNew   : TInletCalcArray;
                             IManf                     : TPipe);
  //Inlet Valve Closed Calculation
Type array_var = Array[1..H] of Double;  //Array for x-values
Var  iter,stop    : Integer;    //Parameters
     dx           : Double;     //x-step
     xs           : array_var;  //x-coördinates for initial reference points
     x            : array_var;  //x-coördinates at temperary points
     u            : array_var;  //Velocities at temperery points
     P            : array_var;  //Pressure at temperery points
     R            : array_var;  //Density at temperery points
     MuL,MPL,MRL  : Double;     //Gradient at left
     BuL,BPL,BRL  : Double;     //Constant at left
     a            : Double;     //Velocity to determine s.o.s
     c            : Double;     //Speed of sound
     pres         : Double;     //Pressure to determine s.o.s
     dens         : Double;     //Density to determine s.o.s
     Lplus        : Double;     //lambdaplus
     Qplus        : Double;     //Q+
     Tplus        : Double;     //T+
     Splus        : Double;     //S+
     Lo,Ao,Bo,T0  : Double;     //lambdao,Ao,Bo,To
     uD,PD,RD     : Double;     //Covergence test for velocity,pressure,density
     Q            : Integer;    //Grid Parameter
     D1,D3        : Double;     //Hydroulic Diameter
     f1,f3        : Double;     //Fanning Friction Factor
Begin
//DEFINE INITIAL PROPERTIES AND DETERMINE INTERPOLATING POLYNOMIALS
  gam := 1.3994;
  iter := 0;
  Q := QI;
 //Initial x-values
  xs[1] := xInlet[Q-1];
  x[3] := xInlet[Q];
 //Initial velocity-values
  u[1] := uInlet[Q-1];
  u[3] := uSOLID;
  u[4] := uSOLID;
 //Initial pressure-values
  P[1] := PInlet[Q-1];
  P[3] := PInlet[Q];
 //Initial density-values
  R[1] := RInlet[Q-1];
  R[3] := RInlet[Q];
 //Determine Interpolating Polynomials for Left Two Points
  dx := xInlet[Q-1] - xInlet[Q];
  //velocity
  MuL := (uInlet[Q-1]-uInlet[Q])/dx;
  BuL := uInlet[Q] - MuL*xInlet[Q];
  //pressure
  MPL := (PInlet[Q-1]-PInlet[Q])/dx;
  BPL := PInlet[Q] - MPL*xInlet[Q];
  //density
  MRL := (RInlet[Q-1]-RInlet[Q])/dx;
  BRL := RInlet[Q] - MRL*xInlet[Q];
 //LOCATE POINT 4
  a := (u[3]+u[4])/2;
  if ABS(a) > 1E-8 then
   begin
    Lo := 1/a;
    x[4] := x[3] + dt/Lo;
   end
  else x[4] := x[3];
//DO ITERATION UNTIL CONVERGENCE IS REACHED
REPEAT
//LOCATE POINT 1 AND DETERMINE COEFFICIENTS ALONG LINE 14(C+)
 REPEAT
  stop := 0;
  if iter = 0 then
   begin
    u[4] := u[1];
    P[4] := P[1];
    R[4] := R[1];
   end;
  a := (u[1]+u[4])/2;
  pres := (P[1]+P[4])/2;
  dens := (R[1]+R[4])/2;
  if (pres < 0) or (dens < 0) then
  showMessage('ERROR : Press/Dens negative in Point 1 at "Inlet_Valve_Closed" !!!');
  c := cThermo(gam,pres,dens);
  Lplus := 1/(a+c);
  x[1] := x[4] - dt/Lplus;
  if x[1] > IManf.Length then x[1] := IManf.Length;
  if ABS(x[1]-xs[1]) < 0.001*E4 then
   begin
    D1 := sqrt(4*IManf.Area(x[1])/Pi);
    f1 := FricFact(gam,R[1],u[1],D1,c);
    Qplus := dens*c;
    Splus := - R[1]*u[1]*sqr(c)/IManf.Area(x[1])*IManf.dAdL(x[1])
             + ((gam-1)*u[1] - c)*(R[1]*u[1]*abs(u[1])*2*f1/D1);
    Tplus := P[1] + Qplus*u[1] + Splus*dt;
    stop := 1;
   end
  else
   begin
    xs[1] := x[1];
    u[1] := MuL*x[1] + BuL;
    P[1] := MPL*x[1] + BPL;
    R[1] := MRL*x[1] + BRL;
   end;
 UNTIL stop = 1;
 //POINT 1 IS FIXED
 //****************
//DETERMINE COEFFICIENTS ALONG LINE 34(Co)
  if iter = 0 then
   begin
    u[4] := u[3];
    P[4] := P[3];
    R[4] := R[3];
   end;
  pres := (P[3]+P[4])/2;
  dens := (R[3]+R[4])/2;
  if (pres < 0) or (dens < 0) then
  showMessage('ERROR : Press/Dens negative in Point 3 at "Inlet_Valve_Closed" !!!');
  c := cThermo(gam,pres,dens);
  if x[3] > IManf.Length then x[3] := IManf.Length;
  D3 := sqrt(4*IManf.Area(x[3])/Pi);
  f3 := FricFact(gam,R[3],u[3],D3,c);
  Ao := sqr(c);
  Bo := (gam-1)*(R[3]*u[3]*abs(u[3])*2*f3/D3);
  T0 := Bo*(x[4]-x[3]) + P[3] - Ao*R[3];
//CALCULATE THE PROPERTIES AT POINT 4 AND TEST FOR CONVERGENCE
  stop := 0;
  u[4] := uSOLID;
  P[4] := Tplus - Qplus*u[4];
  R[4] := (P[4]-T0)/Ao;
  if iter <> 0 then
   if (ABS(P[4]-PD) < (E2*0.001)) then
    if (ABS(R[4]-RD) < (E3*0.0001)) then
     stop := 1;
  PD := P[4];
  RD := R[4];
  iter := iter + 1;
  if iter > 1000 then stop := 1;
 UNTIL stop = 1;
 //POINT 4 IS FIXED
 //****************
 uInletNew[Q] := u[4];
 PInletNew[Q] := P[4];
 RInletNew[Q] := R[4];
 cInletNew[Q] := sqrt(gam*PInletNew[Q]/RInletNew[Q]);
End;  //INLET_VALVE_CLOSED

//******************************************************************************

Procedure EXHAUST_VALVE_CLOSED(dt,gam,uSolid               : Double;
                               var XExhaust,uExhaust,PExhaust,
                                   RExhaust,cExhaust       : TExhaustCalcArray;
                               var uExhaustNew,PExhaustNew,
                                   RExhaustNew,cExhaustNew : TExhaustCalcArray;
                               EManf                       : TPipe);
  //Exhaust Valve Closed Calculation
Type array_var = Array[1..H] of Double;  //Array for x-values
Var  iter,stop    : Integer;    //Parameters
     dx           : Double;     //x-step
     xs           : array_var;  //x-coördinates for initial reference points
     x            : array_var;  //x-coördinates at temperary points
     u            : array_var;  //Velocities at temperery points
     P            : array_var;  //Pressure at temperery points
     R            : array_var;  //Density at temperery points
     MuR,MPR,MRR  : Double;     //Gradient at right
     BuR,BPR,BRR  : Double;     //Constant at right
     a            : Double;     //Velocity to determine s.o.s
     c            : Double;     //Speed of sound
     pres         : Double;     //Pressure to determine s.o.s
     dens         : Double;     //Density to determine s.o.s
     Lminus       : Double;     //lambdaminus
     Qminus       : Double;     //Q-
     Tminus       : Double;     //T-
     Sminus       : Double;     //S-
     Lo,Ao,Bo,T0  : Double;     //lambdao,Ao,Bo,To
     uD,PD,RD     : Double;     //Covergence test for velocity,pressure,density
     D2,D3        : Double;     //Hydroulic Diameter
     f2,f3        : Double;     //Fanning Friction Factor
Begin
//DEFINE INITIAL PROPERTIES AND DETERMINE INTERPOLATING POLYNOMIALS
  gam := 1.3;
  iter := 0;
 //Initial x-values
  xs[2] := xExhaust[2];
  x[3] := xExhaust[1];
 //Initial velocity-values
  u[2] := uExhaust[2];
  u[3] := uSOLID;
  u[4] := uSOLID;
 //Initial pressure-values
  P[2] := PExhaust[2];
  P[3] := PExhaust[1];
 //Initial density-values
  R[2] := RExhaust[2];
  R[3] := RExhaust[1];
 //Determine Interpolating Polynomials for Right Two Points
  dx := x[3] - xExhaust[2];
  //velocity
  MuR := (u[3]-uExhaust[2])/dx;
  BuR := uExhaust[2] - MuR*xExhaust[2];
  //pressure
  MPR := (P[3]-PExhaust[2])/dx;
  BPR := PExhaust[2] - MPR*xExhaust[2];
  //density
  MRR := (R[3]-RExhaust[2])/dx;
  BRR := RExhaust[2] - MRR*xExhaust[2];
 //LOCATE POINT 4
  a := (u[3]+u[4])/2;
  if ABS(a) > 1E-8 then
   begin
    Lo := 1/a;
    x[4] := x[3] + dt/Lo;
   end
  else x[4] := x[3];
//DO ITERATION UNTIL CONVERGENCE IS REACHED
REPEAT
//LOCATE POINT 2 AND DETERMINE COEFFICIENTS ALONG LINE 24(C-)
 REPEAT
  stop := 0;
  if iter = 0 then
   begin
    u[4] := u[2];
    P[4] := P[2];
    R[4] := R[2];
   end;
  a := (u[2]+u[4])/2;
  pres := (P[2]+P[4])/2;
  dens := (R[2]+R[4])/2;
  if (pres < 0) or (dens < 0) then
  showMessage('ERROR : Press/Dens negative in Point 2 at "Exhaust_Valve_Closed" !!!');
  c := cThermo(gam,pres,dens);
  Lminus := 1/(a-c);
  x[2] := x[4] - dt/Lminus;
  if x[2] < 0 then x[2] := 0;
  if ABS(x[2]-xs[2]) < 0.001*E4 then
   begin
    D2 := sqrt(4*EManf.Area(x[2])/Pi);
    f2 := FricFact(gam,R[2],u[2],D2,c);
    Qminus := dens*c;
    Sminus := - R[2]*u[2]*sqr(c)/EManf.Area(x[2])*Emanf.dAdL(x[2])
              + ((gam-1)*u[2] + c)*(R[2]*u[2]*abs(u[2])*2*f2/D2);
    Tminus := P[2] - Qminus*u[2] + Sminus*dt;
    stop := 1;
   end
  else
   begin
    xs[2] := x[2];
    u[2] := MuR*x[2] + BuR;
    P[2] := MPR*x[2] + BPR;
    R[2] := MRR*x[2] + BRR;
   end;
 UNTIL stop = 1;
 //POINT 2 IS FIXED
 //****************
//DETERMINE COEFFICIENTS ALONG LINE 34(Co)
  if iter = 0 then
   begin
    u[4] := u[3];
    P[4] := P[3];
    R[4] := R[3];
   end;
  pres := (P[3]+P[4])/2;
  dens := (R[3]+R[4])/2;
  if (pres < 0) or (dens < 0) then
  showMessage('ERROR : Press/Dens negative in Point 3 at "Exhaust_Valve_Closed" !!!');
  c := cThermo(gam,pres,dens);
  D3 := sqrt(4*EManf.Area(x[3])/Pi);
  f3 := FricFact(gam,R[3],u[3],D3,c);
  Ao := sqr(c);
  Bo := (gam-1)*(R[3]*u[3]*abs(u[3])*2*f3/D3);
  T0 := Bo*(x[4]-x[3]) + P[3] - Ao*R[3];
//CALCULATE THE PROPERTIES AT POINT 4 AND TEST FOR CONVERGENCE
  stop := 0;
  u[4] := uSOLID;
  P[4] := Tminus + Qminus*u[4];
  R[4] := (P[4]-T0)/Ao;
  if iter <> 0 then
   if (ABS(P[4]-PD) < (E2*0.001)) then
    if (ABS(R[4]-RD) < (E3*0.0001)) then
     stop := 1;
  PD := P[4];
  RD := R[4];
  iter := iter + 1;
  if iter > 1000 then stop := 1;
 UNTIL stop = 1;
 //POINT 4 IS FIXED
 //****************
 uExhaustNew[1] := u[4];
 PExhaustNew[1] := P[4];
 RExhaustNew[1] := R[4];
 cExhaustNew[1] := sqrt(gam*PExhaustNew[1]/RExhaustNew[1]);
End;  //EXHAUST_VALVE_CLOSED

//******************************************************************************
//*************  MAIN PROGRAM      *********************************************
//******************************************************************************

Procedure TManifolds.Main_Prog(SaveManifoldData          : Boolean;
                               NoCycles                  : Integer;
                               CA                        : Double;
                               var tStep                 : Integer;
                               Speed,dCrankA             : Double;
                               Pcyl,Tcyl                 : Double;
                               var IPt,EPt               : Double;
                               CylVol,MassCyl,Patm,Tatm  : Double;
                               IValveArea,EValveArea     : Double;
                               var MassIn,MassOut,dPMass : Double;
                               var InletP,ExhaustP,
                                   InletU,ExhaustU       : Double);
Var  i,j                 : Integer;
     gam                 : Double;
     dt,time             : Double;
     LInletP,AInletP     : Double;
     LExhaustp,AExhaustp : Double;
     Mt                  : Double;
     Rcyl,Ratm           : Double;
     Rback,Rplenum       : Double;
     cCyl,cAtm           : Double;
     cBack,cPlenum       : Double;
     uSolid              : Double;
     uInletNew           : TInletCalcArray;
     PInletNew           : TInletCalcArray;
     RInletNew           : TInletCalcArray;
     cInletNew           : TInletCalcArray;
     uExhaustNew         : TExhaustCalcArray;
     PExhaustNew         : TExhaustCalcArray;
     RExhaustNew         : TExhaustCalcArray;
     cExhaustNew         : TExhaustCalcArray;
     InletPress,
     ExhaustPress        : Double;
     Pplenum,Pback       : Double;
     Tplenum,Tback       : Double;
     OutI,OutE           : TextFile;
     OutPc,OutTc         : TextFile;
     OutA,OutM           : TextFile;
     MPinl,MUinl         : TextFile;
     MPexh,MUexh         : TextFile;
     IValvestatus,
     EValveStatus        : Boolean;
     DataWrite           : Boolean;
     Counter             : Double;
Begin
 //************Initial values at time t=0********************
 //**********************************************************
  DataWrite := SaveManifoldData;
  IValvestatus := IValveArea > 0;        //TRUE or FALSE
  EValvestatus := EValveArea > 0;        //TRUE or FALSE
  gam := 1.3994;                            //Specific Heat
  dt := (1/(Speed/60*360))*dCrankA;      //Timestep in ms
  uSolid := 0;                           //Velocity Closed Valve
  LInletP := IManf.Length;               //Length of Inlet Pipe
  AInletP := IManf.Area(LInletP);        //Area of Inlet Pipe at Valve End
  LExhaustP := EManf.Length;             //Length of Exhaust Pipe
  AExhaustP := EManf.Area(0);            //Area of Exhaust Pipe at Valve End

  Pback := ExhBack.Pres(Speed);          //Exhaust Back Pressure
  Tback := ExhBack.Temp(Speed);          //Exhaust Back Temperature
  Rback := Pback/287/Tback;              //Exhaust Back Density
  cBack := sqrt(gam*Pback/Rback);        //Exhaust Back Speed of Sound

  Pplenum := CleanAirPresFn.Result(Speed);  //Inlet Plenum Pressure
  Tplenum := PlenumT;                    //Inlet Plenum Temperature
  Rplenum := Pplenum/287/Tplenum;        //Inlet Plenum Density
  cPlenum := sqrt(gam*Pplenum/Rplenum);  //Inlet Plenum Speed of Sound
  Rcyl := Pcyl/287/Tcyl;                 //Cylinder Density
  cCyl := sqrt(gam*Pcyl/Rcyl);           //Cylinder Speed of Sound
  Ratm := Patm/287/Tatm;                 //Atm. Density
  cAtm := sqrt(gam*Patm/Ratm);           //Atm. Speed of Sound
 //************First Time Step****************************
 //*******************************************************
 if (tStep = 0) then
  BEGIN
   QI := IGrid.GridSize(IManf.Length, Speed);  //Inlet Grid Size
    if QI > NI then
    raise ECFDError.Create('Calculated Inlet Grid Length of ' + IntToStr(QI) +
                           ' but was greater than Maximum of ' + IntToStr (NI));
   QE := EGrid.GridSize(EManf.Length, Speed);  //Exhaust Grid Size
    if QE > NE then
    raise ECFDError.Create('Calculated Exhaust Grid Length of ' + IntToStr(QE) +
                           ' but was greater than Maximum of ' + IntToStr (NE));
    IVR := IVRFunc.Result(Speed);
    if Speed <= 1000 then
     IVF := -3.66666E-05*Speed + 7.250E-01
    else
     IVF := IVFFunc.Result(Speed);
     IVFR := IVFRFunc.Result(Speed);
     EVR := EVRFunc.Result(Speed);
     EVF := EVFFunc.Result(Speed);
     EVFR := EVFRFunc.Result(Speed);

   for i := 1 to QI do TempInlet[i] := Tplenum;
   InletPress := Pplenum;
   for i := 1 to QE do TempExhaust[i] := Tback;
   ExhaustPress := Pback;
 //********** Set Initial Values ********************
   bEgin
    CalcX(IManf,EManf,XInlet,XExhaust);
    CalcVel(uInlet,uExhaust);
    CalcPress(InletPress,ExhaustPress,PInlet,PExhaust);
    CalcDens(PInlet,TempInlet,RInlet,PExhaust,TempExhaust,RExhaust);
    CalcSOS(gam,TempInlet,cInlet,TempExhaust,cExhaust);
    IPt := Pplenum;
    EPt := Pback;
   eNd;
  //Set Throat values equal to zero for calculation of Cylinder Pressure
   Iut := 0;
   Ict := 0;
   IRt := 0;
   ICd := 0;
   Eut := 0;
   Ect := 0;
   ERt := 0;
   ECd := 0;
  END
//***************Next Iteration Step****************************
 ELSE                //Step <> 1
  BEGIN
  //****************** Valve Status : Combustion ********************
   iF (IValveStatus = FALSE) and (EValveStatus = FALSE) then
    bEgin
     //********Inlet Pipe********
      begin
      //**Left InletPipe Boundary**
       INFLOW_INLET_PIPE(dt,gam,Pplenum,Tplenum,XInlet,uInlet,PInlet,RInlet,
                         cInlet,uInletNew,PInletNew,RInletNew,cInletNew,IManf);
      //**Internal InletPipe Points**
       for j := 3 to QI do
        begin
         choice := 1;
         W := j;
         INTERNAL_PIPE(dt,gam,Choice,XInlet,uInlet,PInlet,RInlet,cInlet,
                       XExhaust,uExhaust,PExhaust,RExhaust,cExhaust,uInletNew,
                       PInletNew,RInletNew,cInletNew,uExhaustNew,PExhaustNew,
                       RExhaustNew,cExhaustNew,IManf,EManf);
        end;
      //**Right InletPipe Boundary**
       INLET_VALVE_CLOSED(dt,gam,uSolid,XInlet,uInlet,PInlet,RInlet,cInlet,
                          uInletNew, PInletNew,RInletNew,cInletNew,IManf);
      uInlet := uInletNew;
      Pinlet := PInletNew;
      RInlet := RInletNew;
      cInlet := cInletNew;
      end;
     //********Exhaust Pipe********
      begin
      //**Left ExhaustPipe Boundary**
       EXHAUST_VALVE_CLOSED(dt,gam,uSolid,XExhaust,uExhaust,PExhaust,RExhaust,
                            cExhaust,uExhaustNew,PExhaustNew,RExhaustNew,
                            cExhaustNew,EManf);
      //**Internal ExhaustPipe Points**
       for j := 3 to QE do
        begin
         choice := 2;
         W := j;
         INTERNAL_PIPE(dt,gam,Choice,XInlet,uInlet,PInlet,RInlet,cInlet,
                       XExhaust,uExhaust,PExhaust,RExhaust,cExhaust,uInletNew,
                       PInletNew,RInletNew,cInletNew,uExhaustNew,PExhaustNew,
                       RExhaustNew,cExhaustNew,IManf,EManf);
        end;
      //**Right ExhaustPipe Boundary**
       OUTFLOW_EXHAUST_PIPE(dt,gam,Pback,Tback,XExhaust,uExhaust,PExhaust,
                            RExhaust,cExhaust, uExhaustNew,PExhaustNew,
                            RExhaustNew,cExhaustNew,EManf);
      uExhaust := uExhaustNew;
      PExhaust := PExhaustNew;
      RExhaust := RExhaustNew;
      cExhaust := cExhaustNew;
      end;
    eNd; //**Inlet & Exhaust Valves Closed**
//****************** Valve Status : Exhaust ********************
  iF (IValveStatus = FALSE) and (EValveStatus = TRUE) then
    bEgin
     //********Inlet Pipe********
      begin
      //**Left InletPipe Boundary**
       INFLOW_INLET_PIPE(dt,gam,Pplenum,Tplenum,XInlet,uInlet,PInlet,RInlet,
                         cInlet,uInletNew,PInletNew,RInletNew,cInletNew,IManf);
      //**Internal InletPipe Points**
       for j := 3 to QI do
        begin
         choice := 1;
         W := j;
         INTERNAL_PIPE(dt,gam,Choice,XInlet,uInlet,PInlet,RInlet,cInlet,
                       XExhaust,uExhaust,PExhaust,RExhaust,cExhaust,uInletNew,
                       PInletNew,RInletNew,cInletNew,uExhaustNew,PExhaustNew,
                       RExhaustNew,cExhaustNew,IManf,EManf);
        end;
      //**Right InletPipe Boundary**
       INLET_VALVE_CLOSED(dt,gam,uSolid,XInlet,uInlet,PInlet,RInlet,cInlet,
                          uInletNew, PInletNew,RInletNew,cInletNew,IManf);
      uInlet := uInletNew;
      Pinlet := PInletNew;
      RInlet := RInletNew;
      cInlet := cInletNew;
     end;
    //********Exhaust Pipe********
      begin
      //**Left ExhaustPipe Boundary**
       EXHAUST_VALVE_OPEN(dt,gam,PCyl,TCyl,CA,Mt,Eut,Ect,ERt,EPt,AExhaustP,
                          EValveArea,XExhaust,uExhaust,PExhaust,RExhaust,
                          cExhaust,uExhaustNew,PExhaustNew,RExhaustNew,
                          cExhaustNew,EV,ECd,EManf, EVR, EVF, EVFR);
      //**Internal ExhaustPipe Points**
       for j := 3 to QE do
        begin
         choice := 2;
         W := j;
         INTERNAL_PIPE(dt,gam,Choice,XInlet,uInlet,PInlet,RInlet,cInlet,
                       XExhaust,uExhaust,PExhaust,RExhaust,cExhaust,uInletNew,
                       PInletNew,RInletNew,cInletNew,uExhaustNew,PExhaustNew,
                       RExhaustNew,cExhaustNew,IManf,EManf);
        end;
      //**Right ExhaustPipe Boundary**
       OUTFLOW_EXHAUST_PIPE(dt,gam,Pback,Tback,XExhaust,uExhaust,PExhaust,
                            RExhaust,cExhaust,uExhaustNew,PExhaustNew,
                            RExhaustNew,cExhaustNew,EManf);
      //**Calculation of MassTransfer**
      MassFlow(gam,dt,Iut,Ict,IRt,ICd,Eut,Ect,ERt,ECd,IValveArea,EValveArea,
               cCyl,CylVol,MassIn,MassOut,dPMass);
//     MassFlow(gam,dt,uInletNew[QI],cInletNew[QI],RInletNew[QI],1,uExhaustNew[1],
//              cExhaustNew[1],RExhaustNew[1],1,AInletP,AExhaustP,cCyl,CylVol,
//              MassIn,MassOut,dPMass);
      uExhaust := uExhaustNew;
      PExhaust := PExhaustNew;
      RExhaust := RExhaustNew;
      cExhaust := cExhaustNew;
      end;
    eNd; //**Inlet Valve Closed & Exhaust Valve Open**
//****************** Valve Status : Overlap ********************
iF (IValveStatus = TRUE) and (EValveStatus = TRUE) then
    bEgin
     //********Inlet Pipe********
     //**************************
      begin
      //**Left InletPipe Boundary**
       INFLOW_INLET_PIPE(dt,gam,Pplenum,Tplenum,XInlet,uInlet,PInlet,RInlet,
                         cInlet,uInletNew,PInletNew,RInletNew,cInletNew,IManf);
      //**Internal InletPipe Points**
       for j := 3 to QI do
        begin
         choice := 1;
         W := j;
         INTERNAL_PIPE(dt,gam,Choice,XInlet,uInlet,PInlet,RInlet,cInlet,
                       XExhaust,uExhaust,PExhaust,RExhaust,cExhaust,uInletNew,
                       PInletNew,RInletNew,cInletNew,uExhaustNew,PExhaustNew,
                       RExhaustNew,cExhaustNew,IManf,EManf);
        end;
      //**Right InletPipe Boundary**
       INLET_VALVE_OPEN(dt,gam,PCyl,TCyl,CA,Mt,Iut,Ict,IRt,IPt,AInletP,
                        IValveArea,XInlet,uInlet,PInlet,RInlet,cInlet,uInletNew,
                        PInletNew,RInletNew,cInletNew,IV,ICd,IManf, IVR, IVF, IVFR);
      uInlet := uInletNew;
      Pinlet := PInletNew;
      RInlet := RInletNew;
      cInlet := cInletNew;
      end;
     //********Exhaust Pipe********
     //****************************
      begin
      //**Left ExhaustPipe Boundary**
       EXHAUST_VALVE_OPEN(dt,gam,PCyl,TCyl,CA,Mt,Eut,Ect,ERt,EPt,AExhaustP,
                          EValveArea,XExhaust,uExhaust,PExhaust,RExhaust,
                          cExhaust,uExhaustNew,PExhaustNew,RExhaustNew,
                          cExhaustNew,EV,ECd,EManf, EVR, EVF, EVFR);
      //**Internal ExhaustPipe Points**
       for j := 3 to QE do
        begin
         choice := 2;
         W := j;
         INTERNAL_PIPE(dt,gam,Choice,XInlet,uInlet,PInlet,RInlet,cInlet,
                       XExhaust,uExhaust,PExhaust,RExhaust,cExhaust,uInletNew,
                       PInletNew,RInletNew,cInletNew,uExhaustNew,PExhaustNew,
                       RExhaustNew,cExhaustNew,IManf,EManf);
        end;
      //**Right ExhaustPipe Boundary**
       OUTFLOW_EXHAUST_PIPE(dt,gam,Pback,Tback,XExhaust,uExhaust,PExhaust,
                            RExhaust,cExhaust,uExhaustNew,PExhaustNew,
                            RExhaustNew,cExhaustNew,EManf);
      //**Calculation of MassTransfer**
      MassFlow(gam,dt,Iut,Ict,IRt,ICd,Eut,Ect,ERt,ECd,IValveArea,EValveArea,
               cCyl,CylVol,MassIn,MassOut, dPMass);
//     MassFlow(gam,dt,uInletNew[QI],cInletNew[QI],RInletNew[QI],1,uExhaustNew[1],
//              cExhaustNew[1],RExhaustNew[1],1,AInletP,AExhaustP,cCyl,CylVol,
//              MassIn,MassOut,dPMass);
      uExhaust := uExhaustNew;
      PExhaust := PExhaustNew;
      RExhaust := RExhaustNew;
      cExhaust := cExhaustNew;
      end;
    eNd;  //**Inlet&Exhaust Valves Open**
//****************** Valve Status : Intake ********************
  iF (IValveStatus = TRUE) and (EValveStatus = FALSE) then
    bEgin
     //********Inlet Pipe********
      begin
      //**Left InletPipe Boundary**
       INFLOW_INLET_PIPE(dt,gam,Pplenum,Tplenum,XInlet,uInlet,PInlet,RInlet,
                         cInlet,uInletNew,PInletNew,RInletNew,cInletNew,IManf);
      //**Internal InletPipe Points**
       for j := 3 to QI do
        begin
         choice := 1;
         W := j;
         INTERNAL_PIPE(dt,gam,Choice,XInlet,uInlet,PInlet,RInlet,cInlet,
                       XExhaust,uExhaust,PExhaust,RExhaust,cExhaust,uInletNew,
                       PInletNew,RInletNew,cInletNew,uExhaustNew,PExhaustNew,
                       RExhaustNew,cExhaustNew,IManf,EManf);
        end;
      //**Right InletPipe Boundary**
       INLET_VALVE_OPEN(dt,gam,PCyl,TCyl,CA,Mt,Iut,Ict,IRt,IPt,AInletP,
                        IValveArea,XInlet,uInlet,PInlet,RInlet,cInlet,uInletNew,
                        PInletNew,RInletNew,cInletNew,IV,ICd,IManf, IVR, IVF, IVFR);
      //**Calculation of MassTransfer**
      MassFlow(gam,dt,Iut,Ict,IRt,ICd,Eut,Ect,ERt,ECd,IValveArea,EValveArea,
               cCyl,CylVol,MassIn,MassOut,dPMass);
//     MassFlow(gam,dt,uInletNew[QI],cInletNew[QI],RInletNew[QI],1,uExhaustNew[1],
//              cExhaustNew[1],RExhaustNew[1],1,AInletP,AExhaustP,cCyl,CylVol,
//              MassIn,MassOut,dPMass);
      uInlet := uInletNew;
      Pinlet := PInletNew;
      RInlet := RInletNew;
      cInlet := cInletNew;
      end;
     //********Exhaust Pipe********
      begin
      //**Left ExhaustPipe Boundary**
       EXHAUST_VALVE_CLOSED(dt,gam,uSolid,XExhaust,uExhaust,PExhaust,RExhaust,
                            cExhaust,uExhaustNew,PExhaustNew,RExhaustNew,
                            cExhaustNew,EManf);
      //**Internal ExhaustPipe Points**
       for j := 3 to QE do
        begin
         choice := 2;
         W := j;
         INTERNAL_PIPE(dt,gam,Choice,XInlet,uInlet,PInlet,RInlet,cInlet,
                       XExhaust,uExhaust,PExhaust,RExhaust,cExhaust,uInletNew,
                       PInletNew,RInletNew,cInletNew,uExhaustNew,PExhaustNew,
                       RExhaustNew,cExhaustNew,IManf,EManf);
        end;
      //**Right ExhaustPipe Boundary**
       OUTFLOW_EXHAUST_PIPE(dt,gam,Pback,Tback,XExhaust,uExhaust,PExhaust,
                            RExhaust,cExhaust,uExhaustNew,PExhaustNew,
                            RExhaustNew,cExhaustNew,EManf);
      uExhaust := uExhaustNew;
      PExhaust := PExhaustNew;
      RExhaust := RExhaustNew;
      cExhaust := cExhaustNew;
      end;
    eNd; //**Inlet Valve Open & Exhaust Valve Closed**
  END;
//++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
if CA = IV.C + 360 then inc(tStep);
if (CA >= IV.C + 360) and (CA <= EV.O + 360) then
 begin
  MassIn := 0;
  MassOut := 0;
  dPMass := 0;
 end;
InletP := PInlet[QI];
InletU := uInlet[QI];
InletT := TempInlet[QI];
ExhaustT := TempExhaust[1];
ExhaustP := PExhaust[1];
ExhaustU := uExhaust[1];

if (CA = 359) and (tStep = NoCycles-1) and (DataWrite = TRUE) then
 begin
  AssignFile(OutI,'Inlet.txt');
  Rewrite(OutI);
  AssignFile(OutE,'Exhaust.txt');
  Rewrite(OutE);
  AssignFile(OutPc,'Pcyl.txt');
  Rewrite(OutPc);
  AssignFile(OutTc,'Tcyl.txt');
  Rewrite(OutTc);
  AssignFile(OutM,'MassFlow.txt');
  Rewrite(OutM);
  AssignFile(MPinl,'InlPress.m');
  Rewrite(MPinl);
  AssignFile(MUinl,'InlVel.m');
  Rewrite(MUinl);
  AssignFile(MPexh,'ExhPress.m');
  Rewrite(MPexh);
  AssignFile(MUexh,'ExhVel.m');
  Rewrite(MUexh);
  CloseFile(OutI);
  CloseFile(OutE);
  CloseFile(OutPc);
  CloseFile(OutTc);
  CloseFile(OutM);
  CloseFile(MPinl);
  CloseFile(MUinl);
  CloseFile(MPexh);
  CloseFile(MUexh);
 end;
if (CA >359) and (CA <=720) and (tStep = NoCycles-1) and (DataWrite = TRUE) or
   (CA >0) and (CA <IV.C+360) and (tStep = NoCycles-1) and (DataWrite = TRUE) or
   (CA>=IV.C+360) and (CA<=360) and (tStep=NoCycles) and (DataWrite = TRUE) then
 begin
  AssignFile(OutI,'Inlet.txt');
  Append(OutI);
  AssignFile(OutE,'Exhaust.txt');
  Append(OutE);
  AssignFile(OutPc,'Pcyl.txt');
  Append(OutPc);
  AssignFile(OutTc,'Tcyl.txt');
  Append(OutTc);
  AssignFile(OutM,'MassFlow.txt');
  Append(OutM);
  AssignFile(MPinl,'InlPress.m');
  Append(MPinl);
  AssignFile(MUinl,'InlVel.m');
  Append(MUinl);
  AssignFile(MPexh,'ExhPress.m');
  Append(MPexh);
  AssignFile(MUexh,'ExhVel.m');
  Append(MUexh);
 //Inlet Data
  write(OutI,CA:5:0,'    ',(PInlet[1]/1e5):6:4,'   ',uInlet[1]:6:2,'    ',
                       (PInlet[QI div 2])/1e5:6:4,'   ',uInlet[QI div 2]:6:2,
                       '    ',(PInlet[QI]/1e5):6:4,'   ',uInlet[QI]:6:2);
  writeln(OutI);
 //Exhaust Data
  write(OutE,CA:5:0,'    ',(PExhaust[QE]/1e5):6:4,'   ',uExhaust[QE]:7:2,'    ',
                      (PExhaust[QE div 2])/1e5:6:4,'   ',uExhaust[QE div 2]:7:2,
                       '    ',(PExhaust[1]/1e5):6:4,'   ',uExhaust[1]:7:2);
  writeln(OutE);
 //Cylinder Pressure
  write(OutPc,CA:5:0,'    ',Pcyl/1e5:6:4);
  writeln(OutPc);
 //Cylinder Temperature
  write(OutTc,CA:5:0,'    ',Tcyl:6:2,'    ',CylVol:12:11);
  writeln(OutTc);
 //Mass Flow
  write(OutM,CA:5:0,'    ',MassIn*1e6:6:4,'    ',MassOut*1e6:6:4);
//  write(OutputM,CA:5:0,'    ',IValveArea*1e6:6:4,'    ',EValveArea*1e6:6:4);
  writeln(OutM);
 //Matlab Pressure Data
  for i := 1 to QI do write(MPinl,PInlet[i]/1e5:6:4,' ');
  writeln(MPinl);
 //Matlab Pressure Data
  for i := 1 to QI do write(MUinl,UInlet[i]:6:4,' ');
  writeln(MUinl);
 //Matlab Pressure Data
  for i := 1 to QE do write(MPexh,PExhaust[i]/1e5:6:4,' ');
  writeln(MPexh);
 //Matlab Pressure Data
  for i := 1 to QE do write(MUexh,UExhaust[i]:6:4,' ');
  writeln(MUexh);
 end;
//if (CA = 360) and (tStep = NoCycles) and (DataWrite = TRUE) then
if (CA >359) and (CA <=720) and (tStep = NoCycles-1) and (DataWrite = TRUE) or
   (CA >0) and (CA <IV.C+360) and (tStep = NoCycles-1) and (DataWrite = TRUE) or
   (CA>=IV.C+360) and (CA<=360) and (tStep=NoCycles) and (DataWrite = TRUE) then
 begin
  CloseFile(OutI);
  CloseFile(OutE);
  CloseFile(OutPc);
  CloseFile(OutTc);
  CloseFile(OutM);
  CloseFile(MPinl);
  CloseFile(MUinl);
  CloseFile(MPexh);
  CloseFile(MUexh);
 end;
End;  //OPipe.Main_Prog

//******************************************************************************

Constructor TManifolds.Create;
begin
   inherited;
   IV := TValve.Create;
   EV := TValve.Create;
   IManf := TPipe.Create;
   EManf := TPipe.Create;
   IGrid := TGridsize.Create;
   EGrid := TGridSize.Create;
   ExhBack := TExhaustPandT.Create;

   CleanAirPresFn := TDoubFunc.Create;
   IVRFunc := TDoubFunc.Create;
   IVFFunc := TDoubFunc.Create;
   IVFRFunc := TDoubFunc.Create;

   EVRFunc := TDoubFunc.Create;
   EVFFunc := TDoubFunc.Create;
   EVFRFunc := TDoubFunc.Create;
end;  //TManifolds.Create

//******************************************************************************

Destructor TManifolds.Destroy;
begin
  IV.Free;
  EV.Free;
  IManf.free;
  EManf.Free;
  IGrid.Free;
  EGrid.Free;
  Exhback.free;

  CleanAirPresFn.Free;
  IVRFunc.Free;
  IVFFunc.Free;
  IVFRFunc.Free;

  EVRFunc.Free;
  EVFFunc.Free;
  EVFRFunc.Free;
  inherited Destroy;
end;  //TManifolds.Destroy

//******************************************************************************

END.
