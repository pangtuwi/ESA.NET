{****************************************************************************}
{This Unit calculates gas properties -                                       }
{  Cp, h, M, R, u, du/dT, du/dp, du/dF etc.                                  }
{ Author - Arthur.                                                           }
{ Converted for Delphi II Engine simulation P. N. T. Williams 1997           }
{Requires the units Eqbm, and MathDPM.                                       }
{Valid for Temperatures 300 < T < 5000 K.                                    }
{Calling procedure ReturnProps returns the variables R,h,u,dudT,dudp,dudF.   }
{Modifications                                                               }
{30/04/98                                                                    }
{InitFAirResConcs added to prevent FAirResConcs form adding to itself        }
{every time it is called - modified Procedures FuelAirResConcs and           }
{ResidualConcs                                                               }
{****************************************************************************}

{The author of this program accepts no liability to damage of computers,
 operating systems, programs or personal data, as a result of, or arising
 out of, whether directly or indirectly, the use, either correctly or
 incorrectly, of this code.  The author also indemnifies himself from any
 responsibility for mental instability, disturbance or break down, either
 temporary or permanent, caused by use of this code as above.}

unit GasProps;

interface

uses classes, MathDPM, Eqbm, Dialogs, sysutils;

{These are the look up tables for the coefficients of the Least Squares fit for
the Thermodynamic Functions of specific heat, enthalpy and entropy as functions
of Temperature.
  Row #        Species
  1             H
  2             O
  3             N
  4             H2
  5             OH
  6             CO
  7             NO
  8             O2
  9             H2O
  10            CO2
  11            N2
  12            AR        }

  const

{300 to 1000 K}
ThermoLT : array[1..12,1..7] of Double =
(( 0.25000000e+1,  0            ,  0            ,  0            ,  0             ,  0.25471627e+5, -0.46011762    ),
 ( 0.29464287e+1, -0.16381665e-2,  0.24210316e-5, -0.16028432e-8,  0.38906964e-12,  0.29147644e+5,  0.29639949e+1 ),
 ( 0.25030714e+1, -0.21800181e-4,  0.54205287e-7, -0.56475602e-10,  0.20999044e-13,  0.56098904e+5,  0.41675764e+1 ),
 ( 0.30574451e+1,  0.26765200e-2, -0.58099162e-5,  0.55210391e-8, -0.18122739e-11, -0.98890474e+3, -0.22997056e+1 ),
 ( 0.38375943e+1, -0.10778858e-2,  0.96830378e-6,  0.18713972e-9, -0.22571094e-12,  0.36412823e+4,  0.49370009e+0 ),
 ( 0.37100928e+1, -0.16190964e-2,  0.36923594e-5, -0.20319674e-8,  0.23953344e-12, -0.14356310e+5,  0.29555351e+1 ),
 ( 0.40459521e+1, -0.34181783e-2,  0.79819190e-5, -0.61139316e-8,  0.15919076e-12,  0.97453934e+4,  0.29974988e+1 ),
 ( 0.36255985e+1, -0.18782184e-2,  0.70554544e-5, -0.67635137e-8,  0.21555993e-11, -0.10475226e+4,  0.43052778e+1 ),
 ( 0.40701275e+1, -0.11084499e-2,  0.41521180e-5, -0.29637404e-8,  0.80702103e-12, -0.30279722e+5, -0.32270046e+0 ),
 ( 0.24007797e+1,  0.87350957e-2, -0.66070878e-5,  0.20021861e-8,  0.63274039e-15, -0.48377527e+5,  0.96951457e+1 ),
 ( 0.36748261e+1, -0.12081500e-2,  0.23240102e-5, -0.63217559e-9, -0.22577253e-12, -0.10611588e+4,  0.23580424e+1 ),
 ( 0.25000000e+1,  0            ,  0            ,  0            ,  0             , -0.74537498e+3,  0.43660006e+1 ));

 {1000 to 5000 K}
ThermoHT : array[1..12,1..7] of Double =
(( 0.25000000e+1,  0            ,  0            ,  0             ,  0             ,  0.25471627e+5, -0.46011763    ),
 ( 0.25420596e+1, -0.27550619e-4, -0.31028033e-8,  0.45510674e-11, -0.43680515e-15,  0.29230803e+5,  0.49203080e+1 ),
 ( 0.24502682e+1,  0.10661458e-3, -0.74653373e-7,  0.18796524e-10, -0.10259839e-14,  0.56116040e+5,  0.44487581e+1 ),
 ( 0.31001901e+1,  0.51119464e-3,  0.52644210e-7, -0.34909973e-10,  0.36945345e-14, -0.87738042e+3, -0.19629421e+1 ),
 ( 0.29106427e+1,  0.95931650e-3, -0.19441702e-6,  0.13756646e-10,  0.14224542e-15,  0.39353815e+4,  0.54423445e+1 ),
 ( 0.29840696e+1,  0.14891390e-2, -0.57899684e-6,  0.10364577e-09, -0.69353550e-14, -0.14245228e+5,  0.63479156e+1 ),
 ( 0.31890000e+1,  0.13382281e-2, -0.52899318e-6,  0.95919332e-10, -0.64847932e-14,  0.98283290e+4,  0.67458126e+1 ),
 ( 0.36219535e+1,  0.73618264e-3, -0.19652228e-6,  0.36201558e-10, -0.28945627e-14, -0.12019825e+4,  0.36150960e+1 ),
 ( 0.27167633e+1,  0.29451374e-2, -0.80224374e-6,  0.10226682e-09, -0.48472145e-14, -0.29905826e+5,  0.66305671e+1 ),
 ( 0.44608041e+1,  0.30981719e-2, -0.12392571e-5,  0.22741325e-09, -0.15525954e-13, -0.48961442e+5, -0.98635982e+0 ),
 ( 0.28963194e+1,  0.15154866e-2, -0.57235277e-6,  0.99807393e-10, -0.65223555e-14, -0.90586184e+3,  0.61615148e+1 ),
 ( 0.25000000e+1,  0            ,  0            ,  0             ,  0             , -0.74537502e+3,  0.43660006e+1  ));

FuelThermo : array[0..6,1..7] of Double =
(( 0            ,  0            ,  0            ,  0            ,  0             ,  0            ,  0             ),
 ( 1.412633     ,  2.087101e-2  , -8.142134e-6  ,  0            ,  0             , -1.026351e+4  ,  1.917126e+1   ),
{( 1.971324     ,  7.871586e-3  , -1.048592e-6  ,  0            ,  0             , -9.930422e+3  ,  8.873728      ),}
 ( 0.15027072e1 ,  0.10416798e-1 , -0.39181522e-5 , 0.67777899e-9 , -0.44283706e-13,  -0.99787078e4, 0.10707143e2 ),
 ( 1.779819     ,  1.262503e-2  , -3.624890e-6  ,  0            ,  0             , -2.525420e+4  ,  1.50884e+1    ),
 ( -2.545087    ,  4.79554e-2   , -2.030765e-5  ,  0            ,  0             ,  8.782234e+3  ,  3.348825e+1   ),
 ( 4.0652       ,  6.0977e-2    , -1.8801e-5    ,  0            ,  0             , -3.5880e+4    ,  1.545e+1      ),
 ( 7.9710       ,  1.1954e-1    , -3.6858e-5    ,  0            ,  0             , -1.9385e+4    , -1.7879        ));

{CHON ratio of fuels}
{Ferg p114}
{     0    user specified
      1    CH3NO2
      2    CH4
      3    CH3OH
      4    C6H6
      5    C7H17 gasoline
      6    C14.4H24.9 diesel}

FuelComp : array[0..6,1..4] of Double =
(( 0 , 0 , 0 ,0),
 ( 1 , 3 , 2 , 1),
 ( 1 , 4 , 0 , 0),
 ( 1 , 4 , 1 , 0),
 ( 6 , 6 , 0 , 0),
 ( 7 , 17 , 0 , 0),
 ( 14.4 , 24.9 , 0 , 0));

 FuelMolWt : array[0..6] of Double =
 ( 0 , 61.0407 , 16.0432 , 32.0426 , 78.1152 , 101.2144 , 198.06);

MolWt : array[1..12] of Double =
(1.0080, 15.9994, 14.0067, 2.0160, 17.0074, 28.0106, 30.0061, 31.9988, 18.0154, 44.0100, 28.0134, 39.948);

Runiversal = 8314.41; {J/[kmol.K]}

RefEnth : array[1..12] of Double =
(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

AirComposition : EqSpecArray =
(0, 0, 0, 0, 0, 0, 0, 0.2096, 0, 0, 0.7811, 0.0093);

ZeroArray : EqSpecArray =
(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

type
  TProp = class
     Eqbm1: TEqbm;
     InitFAirResConcs,
     FAirResConcs: EqSpecArray;     {Within Residual concs procedure = concs of residual,   }
                                 {elsewhere = concs of Air Residual Gas mixture.         }
     FuelMolFrac: Double;
     constructor Create(Burned: shortint);
     destructor Destroy; override;
     procedure SetUp(Type_Fuel: shortint; n1,m1,l1,k1,ER1,f1: Double);
     procedure ChangeParam(n1,m1,l1,k1,ER1,f1: Double);
     procedure ReturnProps(Pres,GasTemp:Double;
                           var R,h,u,Cp,dudT,dudp,dudF:Double;
                           var err :integer);
     function Get_R(Pres,GasTemp:Double; var err :integer):Double;
     function Get_h(Pres,GasTemp:Double; var err :integer):Double;
     function Get_cp(Pres,GasTemp:Double; var err :integer):Double;
     function Get_cv(Pres,GasTemp:Double; var err :integer):Double;
     function Get_gamma(Pres,GasTemp:Double; var err :integer):Double;
     function Get_u(Pres,GasTemp:Double; var err :integer):Double;
     function Get_dudT(Pres,GasTemp:Double; var err :integer):Double;
     function Get_dudp(Pres,GasTemp:Double; var err :integer):Double;
     function Get_dudF(Pres,GasTemp:Double; var err :integer):Double;
     function Get_FuelEnthalpy(Type_Fuel:integer; FuelTemp:Double; var err :integer):Double;
     function Get_dlnvdlnT(Pres,GasTemp:Double; var err :integer):Double;
     function Get_dlnvdlnP(Pres,GasTemp:Double; var err :integer):Double;
   private				{Put routines and variables in here that only the object itself needs to access}
     FuelType,BurnedGasCntrl: shortint;
     n,m,l,k,ER,f: Double;
{     FAirResConcs: EqSpecArray;     {Within Residual concs procedure = concs of residual,   }
                                   {elsewhere = concs of Air Residual Gas mixture.         }
 {    FuelMolFrac: Double;}
     errcode,errcount,err1,err2count,err3count: integer;
     debug: boolean;
     function SpecHeat(Specie: integer; GasTemp: Double):Double;
     function FuelSpecHeat(Fuel_Type: shortint; GasTemp: Double):Double;
     function Enthalpy(Specie: integer; GasTemp: Double):Double;
     function FuelEnthalpy(Type_Fuel: shortint; GasTemp: Double):Double;
     function MixMolWt(x: EqSpecArray; FuelMol: Double; Type_Fuel: shortint):Double;
     function MixGasConst(MWt: Double):Double;
     function MixSpecHeat(x: EqSpecArray; FuelMol,GasTemp,MWt: Double; Type_Fuel: shortint):Double;
     function MixEnth(x: EqSpecArray; FuelMol,GasTemp,MWt: Double; Type_Fuel: shortint):Double;
     function MixIntE(h,R,GasTemp:Double):Double;
     function MixdMdT(dxdT: EqSpecArray):Double;
     function MixdMdp(dxdp: EqSpecArray):Double;
     function MixdMdF(dxdF: EqSpecArray):Double;
     function MixdRdT(R,MWt,dMdT: Double):Double;
     function MixdRdp(R,MWt,dMdp: Double):Double;
     function MixdRdF(R,MWt,dMdF: Double):Double;
     function MixdhdT(dxdT: EqSpecArray; cp,MWt,GasTemp,dMdT,h: Double):Double;
     function Mixdhdp(dxdp: EqSpecArray; MWt,GasTemp,dMdp,h: Double):Double;
     function MixdhdF(dxdF: EqSpecArray; MWt,GasTemp,dMdF,h: Double):Double;
     function MixdudT(dhdT,R,dRdT,GasTemp: Double):Double;
     function Mixdudp(dhdp,dRdp,GasTemp: Double):Double;
     function MixdudF(dhdF,dRdF,GasTemp: Double):Double;
     procedure ResidualConcs(ERloc,nloc,mloc,lloc,kloc,ExTemp:Double);
     procedure FuelAirResConcs(ERloc,nloc,mloc,lloc,kloc,floc:Double; Type_Fuel: shortint);
     function CalcK(GasTemp: Double):Double;
     procedure ResetError;
     function ErrorRet:integer;
     procedure Error(e,infoi :integer; infor :Double);
  end; //TProp

  EGasPropsError = class(Exception)
  end; //EGasProps

implementation

{--------------------------------------------------}
{ TEqbm's method implementations:     }
{--------------------------------------------------}
{At present Spark Ignition Only!!! ie ER specified here constant throughout.}
{Single fuel routines use the ER and CHON specified here.}
{Specify whether burned or unburned and fuel type.}
{Fuel type 1 - 6 uses above fuel type arrays.}
{Fuel type 0 specifies other and uses specified n,m,l,k and assumes gasoline
 or diesel according to C:H ratio for properties.}
constructor TProp.Create(Burned:shortint);  {Burned gas - Burned = 1}
begin
  debug:=true;
  ResetError;
  BurnedGasCntrl:=Burned;
  if (BurnedGasCntrl <> 0 ) and (BurnedGasCntrl <> 1 ) then
  begin
    error(1,BurnedGasCntrl,0);
    BurnedGasCntrl:=1;   {Assume burned if incorrectly called.}
  end;
  if BurnedGasCntrl = 1 then Eqbm1 := TEqbm.Create;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

destructor TProp.Destroy;
begin
  if BurnedGasCntrl = 1 then Eqbm1.Free;
  inherited Destroy;
end;

{--------------------------------------------------}
{TEqbm's Public Procedures                     }
{--------------------------------------------------}

procedure TProp.SetUp(Type_Fuel: shortint; n1,m1,l1,k1,ER1,f1: Double);
// ER1 : Fuel Air Eq Ratio
//     Lean < 1 < Rich
// f1 : Exhaust Gas Recirculation
var dum,i : shortint;
    ExhTemp : Double;
begin
  ER:=ER1;
  f:=f1;
  FuelType:=Type_Fuel;
  if FuelType = 0 then
  begin
    n:=n1;
    m:=m1;
    l:=l1;
    k:=k1;
    if (m/n > 2.1) then dum:=5 else dum:=6;
    for i:=  1 to 7 do
    begin
      FuelThermo[0,i]:=FuelThermo[dum,i];
    end;
    FuelMolWt[0]:=(n*12.0112) + (m*1.008) + (l*15.9994) + (k*14.0067);
  end else
  begin
    n:=FuelComp[FuelType,1];
    m:=FuelComp[FuelType,2];
    l:=FuelComp[FuelType,3];
    k:=FuelComp[FuelType,4];
  end;
  if BurnedGasCntrl = 0 then
  begin
    ExhTemp:=1000;   {Assume exh. T of 1000K and frozen reactions once diluted with air}
    ResidualConcs(ER,n,m,l,k,ExhTemp);
  end;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

{A value of 99 for any parameter leaves it unchanged.}
procedure TProp.ChangeParam(n1,m1,l1,k1,ER1, f1: Double);
begin
   if n1 <> 99 then n:=n1;
   if m1 <> 99 then m:=m1;
   if l1 <> 99 then l:=l1;
   if k1 <> 99 then k:=k1;
   if ER1 <> 99 then ER:=ER1;
   if f1 <> 99 then f := f1;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

{Single fuel Spark Ignition - no changing ER or CHON.}
procedure TProp.ReturnProps(Pres,GasTemp:Double;
                            var R,h,u,Cp,dudT,dudp,dudF :Double;
                            var err :integer);
var MolWt,Enth,cp_1,dMdT,dhdT,dRdT,dMdp,dhdp,dRdp,dMdF,dhdF,dRdF: Double;
i, error1: integer;   tot, u1, u2: Double;
begin
  ResetError;
  if BurnedGasCntrl = 0 then
  begin
    FuelAirResConcs(ER,n,m,l,k,f,FuelType);
    MolWt:=MixMolWt(FAirResConcs,FuelMolFrac,FuelType);
    R:=MixGasConst(MolWt);
    Enth:=MixEnth(FAirResConcs,FuelMolFrac,GasTemp,MolWt,FuelType);
    h:=Enth;
    u:=MixIntE(Enth,R,GasTemp);
   // v:= (h-u)/Pres;
    cp_1:=MixSpecHeat(FAirResConcs,FuelMolFrac,GasTemp,MolWt,FuelType);
    dhdT:=MixdhdT(ZeroArray,cp_1,MolWt,GasTemp,0,Enth);   {dMdT = 0 frozen reactions}
    dudT:=MixdudT(dhdT,R,0,GasTemp);      {0 dRdT = 0 frozen reaction}
    dudp:=0;
    dudF:=0;  {dudF not 0!!!!!!}
    Cp := dhdt;
   // dlvlt := 1.0;
   // dlvlp := -1.0;
  end
 else if BurnedGasCntrl = 1 then
  begin
    Eqbm1.Go2(ER,n,m,l,k,Pres,GasTemp);
    cP:= -2;
    if Eqbm1.x[8] <> 99 then   {No reasonable O2 found to solve Eqbm}
    begin
{ tot:=0;
 for i:= 1 to 12 do begin writeln(Eqbm1^.dxdt[i]);
 tot:=tot+Eqbm1^.dxdt[i]; end;   writeln('tot   ',tot);}
      MolWt:=MixMolWt(Eqbm1.x,0,0);
      R:=MixGasConst(MolWt);
      Enth:=MixEnth(Eqbm1.x,0,GasTemp,MolWt,0);
      h:=Enth;
      u:=MixIntE(Enth,R,GasTemp);
     // v:= (h-u)/Pres;
      cp_1:=MixSpecHeat(Eqbm1.x,0,GasTemp,MolWt,0);
      dMdT:=MixdMdT(Eqbm1.dxdT);
      dhdT:=MixdhdT(Eqbm1.dxdT,cp_1,MolWt,GasTemp,dMdT,Enth);
      dRdT:=MixdRdT(R,MolWt,dMdT);
      dudT:=MixdudT(dhdT,R,dRdT,GasTemp);
      dMdp:=MixdMdp(Eqbm1.dxdp);
      dhdp:=Mixdhdp(Eqbm1.dxdp,MolWt,GasTemp,dMdp,Enth);
      dRdp:=MixdRdp(R,M,dMdp);
      dudp:=Mixdudp(dhdp,dRdp,GasTemp);
//#
     u1:=Get_u(pres-(0.00025*pres),gastemp,error1);
     u2:=get_u(pres+(0.00025*pres),gastemp,error1);
     dudp:=(u2-u1)/(0.0005*pres);

      dMdF:=MixdMdF(Eqbm1.dxdF);
      dhdF:=MixdhdF(Eqbm1.dxdF,MolWt,GasTemp,dMdF,Enth);
      dRdF:=MixdRdF(R,MolWt,dMdF);
      dudF:=MixdudF(dhdF,dRdF,GasTemp);
     // dlvlt := 1.0 - GasTemp*DMdt/MolWt;
    //  dlvlp :=  -1.0 - Pres/1e5*dMdP/MolWt;
      Cp := dhdt;
    end else
    begin
      error(3,0,0);
      R:=1;
      h:=1;
      u:=1;
    //  v := 1;
      dudT:=1;
      dudp:=1;
      dudF:=1;
    end;
  end;
  err:=ErrorRet;
 end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

{Single fuel Spark Ignition - no changing ER or CHON.}
function TProp.Get_R(Pres,GasTemp:Double; var err :integer):Double;
var MolWt: Double;
begin
  ResetError;
  if BurnedGasCntrl = 0 then
  begin
    FuelAirResConcs(ER,n,m,l,k,f,FuelType);
    MolWt:=MixMolWt(FAirResConcs,FuelMolFrac,FuelType);
    Get_R:=MixGasConst(MolWt);
  end else
  if BurnedGasCntrl = 1 then
  begin
    Eqbm1.Go2(ER,n,m,l,k,Pres,GasTemp);
    if Eqbm1.x[8] <> 99 then  {No reasonable O2 found to solve Eqbm}
    begin
      MolWt:=MixMolWt(Eqbm1.x,0,0);
      Get_R:=MixGasConst(MolWt);
    end else
    begin
      error(3,0,0);
      Get_R:=1;
    end;
  end;
  err:=ErrorRet;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TProp.Get_h(Pres,GasTemp:Double; var err :integer):Double;
var MolWt: Double;
begin
  ResetError;
  if BurnedGasCntrl = 0 then
  begin
    FuelAirResConcs(ER,n,m,l,k,f,FuelType);
    MolWt:=MixMolWt(FAirResConcs,FuelMolFrac,FuelType);
    Get_h:=MixEnth(FAirResConcs,FuelMolFrac,GasTemp,MolWt,FuelType);
  end else
  if BurnedGasCntrl = 1 then
  begin
    Eqbm1.Go2(ER,n,m,l,k,Pres,GasTemp);
    if Eqbm1.x[8] <> 99 then  {No reasonable O2 found to solve Eqbm}
    begin
      MolWt:=MixMolWt(Eqbm1.x,0,0);
      Get_h:=MixEnth(Eqbm1.x,0,GasTemp,MolWt,0);
    end else
    begin
      error(3,0,0);
      Get_h:=1;
    end;
  end;
  err:=ErrorRet;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TProp.Get_cp(Pres,GasTemp:Double; var err :integer):Double;
var MolWt,Enth,cp_1,dMdT: Double;
begin
  ResetError;
  if BurnedGasCntrl = 0 then
  begin
    FuelAirResConcs(ER,n,m,l,k,f,FuelType);
    MolWt:=MixMolWt(FAirResConcs,FuelMolFrac,FuelType);
    Enth:=MixEnth(FAirResConcs,FuelMolFrac,GasTemp,MolWt,FuelType);
    cp_1:=MixSpecHeat(FAirResConcs,FuelMolFrac,GasTemp,MolWt,FuelType);
    Get_cp:=MixdhdT(ZeroArray,cp_1,MolWt,GasTemp,0,Enth);
  end else
  if BurnedGasCntrl = 1 then
  begin
    Eqbm1.Go2(ER,n,m,l,k,Pres,GasTemp);
    if Eqbm1.x[8] <> 99 then  {No reasonable O2 found to solve Eqbm}
    begin
      MolWt:=MixMolWt(Eqbm1.x,0,0);
      Enth:=MixEnth(Eqbm1.x,0,GasTemp,MolWt,0);
      cp_1:=MixSpecHeat(Eqbm1.x,0,GasTemp,MolWt,0);
      dMdT:=MixdMdT(Eqbm1.dxdT);
      Get_cp:=MixdhdT(Eqbm1.dxdT,cp_1,MolWt,GasTemp,dMdT,Enth);
    end else
    begin
      error(3,0,0);
      Get_cp:=1;
    end;
  end;
  err:=ErrorRet;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TProp.Get_cv(Pres,GasTemp:Double; var err :integer):Double;
var MolWt,R,cp,cp_1,Enth,dMdT: Double;
begin
  ResetError;
  if BurnedGasCntrl = 0 then
  begin
    FuelAirResConcs(ER,n,m,l,k,f,FuelType);
    MolWt:=MixMolWt(FAirResConcs,FuelMolFrac,FuelType);
    R:=MixGasConst(MolWt);
    Enth:=MixEnth(FAirResConcs,FuelMolFrac,GasTemp,MolWt,FuelType);
    cp_1:=MixSpecHeat(FAirResConcs,FuelMolFrac,GasTemp,MolWt,FuelType);
    cp:=MixdhdT(ZeroArray,cp_1,MolWt,GasTemp,0,Enth);
{   writeln(cp,' ',CP_1);}
    Get_cv:=cp-R;
  end else
  if BurnedGasCntrl = 1 then
  begin
    Eqbm1.Go2(ER,n,m,l,k,Pres,GasTemp);
    if Eqbm1.x[8] <> 99 then   {No reasonable O2 found to solve Eqbm}
    begin
      MolWt:=MixMolWt(Eqbm1.x,0,0);
      R:=MixGasConst(MolWt);
      Enth:=MixEnth(Eqbm1.x,0,GasTemp,MolWt,0);
      cp_1:=MixSpecHeat(Eqbm1.x,0,GasTemp,MolWt,0);
      dMdT:=MixdMdT(Eqbm1.dxdT);
      cp:=MixdhdT(Eqbm1.dxdT,cp_1,MolWt,GasTemp,dMdT,Enth);
      Get_cv:=cp-R;
    end else
    begin
      error(3,0,0);
      Get_cv:=1;
    end;
  end;
  err:=ErrorRet;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TProp.Get_gamma(Pres,GasTemp:Double; var err :integer):Double;
var MolWt,R,cp,cp_1,Enth,dMdT: Double;
begin
  ResetError;
  if BurnedGasCntrl = 0 then
  begin
    FuelAirResConcs(ER,n,m,l,k,f,FuelType);
    MolWt:=MixMolWt(FAirResConcs,FuelMolFrac,FuelType);
    R:=MixGasConst(MolWt);
    Enth:=MixEnth(FAirResConcs,FuelMolFrac,GasTemp,MolWt,FuelType);
    cp_1:=MixSpecHeat(FAirResConcs,FuelMolFrac,GasTemp,MolWt,FuelType);
    cp:=MixdhdT(ZeroArray,cp_1,MolWt,GasTemp,0,Enth);
{  writeln(cp/(cp-R));
  writeln(cp);}
    Get_gamma:=cp/(cp-R);
  end else
  if BurnedGasCntrl = 1 then
  begin
    Eqbm1.Go2(ER,n,m,l,k,Pres,GasTemp);
    if Eqbm1.x[8] <> 99 then   {No reasonable O2 found to solve Eqbm}
    begin
      MolWt:=MixMolWt(Eqbm1.x,0,0);
      R:=MixGasConst(MolWt);
      Enth:=MixEnth(Eqbm1.x,0,GasTemp,MolWt,0);
      cp_1:=MixSpecHeat(Eqbm1.x,0,GasTemp,MolWt,0);
      dMdT:=MixdMdT(Eqbm1.dxdT);
      cp:=MixdhdT(zeroarray{Eqbm1.dxdT},cp_1,MolWt,GasTemp,0{dMdT},Enth);
 {   writeln('dmdt ',dmdt,' ','gamma ',cp/(cp-R));}
      Get_gamma:=cp/(cp-R);
    end else
    begin
      error(3,0,0);
      Get_gamma:=1;
    end;
  end;
  err:=ErrorRet;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TProp.Get_u(Pres,GasTemp:Double; var err :integer):Double;
var MolWt,R,Enth: Double;
begin
  ResetError;
  if BurnedGasCntrl = 0 then
  begin
    FuelAirResConcs(ER,n,m,l,k,f,FuelType);
    MolWt:=MixMolWt(FAirResConcs,FuelMolFrac,FuelType);
    R:=MixGasConst(MolWt);
    Enth:=MixEnth(FAirResConcs,FuelMolFrac,GasTemp,MolWt,FuelType);
    Get_u:=MixIntE(Enth,R,GasTemp);
  end else
  if BurnedGasCntrl = 1 then
  begin
    Eqbm1.Go2(ER,n,m,l,k,Pres,GasTemp);
    if Eqbm1.x[8] <> 99 then   {No reasonable O2 found to solve Eqbm}
    begin
      MolWt:=MixMolWt(Eqbm1.x,0,0);
      R:=MixGasConst(MolWt);
      Enth:=MixEnth(Eqbm1.x,0,GasTemp,MolWt,0);
      Get_u:=MixIntE(Enth,R,GasTemp);
    end else
    begin
      error(3,0,0);
      Get_u:=1;
    end;
  end;
  err:=ErrorRet;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TProp.Get_dudT(Pres,GasTemp:Double; var err :integer):Double;
var MolWt,R,Enth,cp_1,dMdT,dhdT,dRdT: Double;
begin
  ResetError;
  if BurnedGasCntrl = 0 then
  begin
    FuelAirResConcs(ER,n,m,l,k,f,FuelType);
    MolWt:=MixMolWt(FAirResConcs,FuelMolFrac,FuelType);
    R:=MixGasConst(MolWt);
    Enth:=MixEnth(FAirResConcs,FuelMolFrac,GasTemp,MolWt,FuelType);
    cp_1:=MixSpecHeat(FAirResConcs,FuelMolFrac,GasTemp,MolWt,FuelType);
    dhdT:=MixdhdT(ZeroArray,cp_1,MolWt,GasTemp,0,Enth);
    Get_dudT:=MixdudT(dhdT,R,0,GasTemp);
  end else
  if BurnedGasCntrl = 1 then
  begin
    Eqbm1.Go2(ER,n,m,l,k,Pres,GasTemp);
    if Eqbm1.x[8] <> 99 then     {No reasonable O2 found to solve Eqbm}
    begin
      MolWt:=MixMolWt(Eqbm1.x,0,0);
      R:=MixGasConst(MolWt);
      Enth:=MixEnth(Eqbm1.x,0,GasTemp,MolWt,0);
      cp_1:=MixSpecHeat(Eqbm1.x,0,GasTemp,MolWt,0);
      dMdT:=MixdMdT(Eqbm1.dxdT);
      dhdT:=MixdhdT(Eqbm1.dxdT,cp_1,MolWt,GasTemp,dMdT,Enth);
      dRdT:=MixdRdT(R,MolWt,dMdT);
      Get_dudT:=MixdudT(dhdT,R,dRdT,GasTemp);
    end else
    begin
      error(3,0,0);
      Get_dudT:=1;
    end;
  end;
  err:=ErrorRet;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TProp.Get_dudp(Pres,GasTemp:Double; var err :integer):Double;
var MolWt,R,Enth,dMdp,dhdp,dRdp: Double;  u1,u2:Double; error1:integer;
begin
  ResetError;
  if BurnedGasCntrl = 0 then
  begin
    Get_dudp:=0;
  end else
  if BurnedGasCntrl = 1 then
  begin
    Eqbm1.Go2(ER,n,m,l,k,Pres,GasTemp);
    if Eqbm1.x[8] <> 99 then   {No reasonable O2 found to solve Eqbm}
    begin
{      MolWt:=MixMolWt(Eqbm1.x,0,0);
      R:=MixGasConst(MolWt);
      Enth:=MixEnth(Eqbm1.x,0,GasTemp,MolWt,0);
      dMdp:=MixdMdp(Eqbm1.dxdp);
      dhdp:=Mixdhdp(Eqbm1.dxdp,MolWt,GasTemp,dMdp,Enth);
      dRdp:=MixdRdp(R,M,dMdp);
      Get_dudp:=Mixdudp(dhdp,dRdp,GasTemp);
{      writeln(MolWt,' ',R,' ',Enth,' ',dmdp,' ',dhdp,' ',drdp);}
     u1:=Get_u(pres-(0.0025*pres),gastemp,error1);
     u2:=get_u(pres+(0.0025*pres),gastemp,error1);
     Get_dudp:=(u2-u1)/(0.005*pres);

    end else
    begin
      error(3,0,0);
      Get_dudp:=1;
    end;
  end;
  err:=ErrorRet;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TProp.Get_dudF(Pres,GasTemp:Double; var err :integer):Double;
var MolWt,R,Enth,cp,dMdF,dhdF,dRdF: Double;
begin
  ResetError;
  if BurnedGasCntrl = 0 then
  begin
    Get_dudF:=0;   {dudF not 0!!!!}
  end else
  if BurnedGasCntrl = 1 then
  begin
    Eqbm1.Go2(ER,n,m,l,k,Pres,GasTemp);
    if Eqbm1.x[8] <> 99 then    {No reasonable O2 found to solve Eqbm}
    begin
      MolWt:=MixMolWt(Eqbm1.x,0,0);
      R:=MixGasConst(MolWt);
      Enth:=MixEnth(Eqbm1.x,0,GasTemp,MolWt,0);
      dMdF:=MixdMdF(Eqbm1.dxdF);
      dhdF:=MixdhdF(Eqbm1.dxdF,MolWt,GasTemp,dMdF,Enth);
      dRdF:=MixdRdF(R,MolWt,dMdF);
      Get_dudF:=MixdudF(dhdF,dRdF,GasTemp);
    end else
    begin
      error(3,0,0);
      Get_dudF:=0;
    end;
  end;
  err:=ErrorRet;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TProp.Get_FuelEnthalpy(Type_Fuel:integer; FuelTemp:Double; var err :integer):Double;
begin
  ResetError;
  Get_FuelEnthalpy:=FuelEnthalpy(Type_Fuel,FuelTemp);
  err:=ErrorRet;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TProp.Get_dlnvdlnT(Pres,GasTemp:Double; var err :integer):Double;
var dMdT, MolWt: Double;
begin
  ResetError;
  if BurnedGasCntrl = 0 then
  begin
//    FuelAirResConcs(ER,n,m,l,k,f,FuelType);
//    MolWt:=MixMolWt(FAirResConcs,FuelMolFrac,FuelType);
    Get_dlnvdlnT := 1;  //Because dMdt = 0
  end else
  if BurnedGasCntrl = 1 then
  begin
    Eqbm1.Go2(ER,n,m,l,k,Pres,GasTemp);
    if Eqbm1.x[8] <> 99 then  {No reasonable O2 found to solve Eqbm}
    begin
      MolWt:=MixMolWt(Eqbm1.x,0,0);
      dMdT:=MixdMdT(Eqbm1.dxdT);
      Get_dlnvdlnT:=1-GasTemp*dMdT/MolWt;
    end else
    begin
      error(3,0,0);
      Get_dlnvdlnT:=1;
    end;
  end;
  err:=ErrorRet;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TProp.Get_dlnvdlnP(Pres,GasTemp:Double; var err :integer):Double;
var dMdP, MolWt: Double;
begin
  ResetError;
  if BurnedGasCntrl = 0 then
  begin
//    FuelAirResConcs(ER,n,m,l,k,f,FuelType);
//    MolWt:=MixMolWt(FAirResConcs,FuelMolFrac,FuelType);
    Get_dlnvdlnP := -1;  //Because dMdP = 0
  end else
  if BurnedGasCntrl = 1 then
  begin
    Eqbm1.Go2(ER,n,m,l,k,Pres,GasTemp);
    if Eqbm1.x[8] <> 99 then  {No reasonable O2 found to solve Eqbm}
    begin
      MolWt:=MixMolWt(Eqbm1.x,0,0);
      dMdP:=MixdMdP(Eqbm1.dxdP);
      Get_dlnvdlnP:=-1-Pres*dMdP/MolWt;
    end else
    begin
      error(3,0,0);
      Get_dlnvdlnP:=1;
    end;
  end;
  err:=ErrorRet;
end;


{--------------------------------------------------}
{TEqbm's Private Procedures                     }
{--------------------------------------------------}

{Returns Specific Heat at const. pressure for a given specie - cpi. J/(kMol.K)}
function TProp.SpecHeat(Specie: integer; GasTemp: Double):Double;
var  S: integer;  T,SH: Double;
begin
  if (GasTemp < 260{300}) or (GasTemp > 5000) then    {260 to avoid error messages for now}
  begin
   error(2,0,GasTemp);
   if GasTemp > 5000 then GasTemp:=5000 else GasTemp:=300;
  end;
  S:=Specie;
  T:=GasTemp;
  if GasTemp > 1000
    then  SH:=ThermoHT[S,1] + ThermoHT[S,2]*T + ThermoHT[S,3]*sqr(T) +
                  ThermoHT[S,4]*T*T*T + ThermoHT[S,5]*T*T*T*T
    else  SH:=ThermoLT[S,1] + ThermoLT[S,2]*T + ThermoLT[S,3]*sqr(T) +
                  ThermoLT[S,4]*T*T*T + ThermoLT[S,5]*T*T*T*T;
  SpecHeat:=SH*Runiversal;
  {writeln(sh); }
{  SpecHeat:=SH*MolWt[s]/4.184;}    {cal/mol K}
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

{Returns Specific Heat at const. pressure for a given fuel type - cpi. J/(kMol.K)}
function TProp.FuelSpecHeat(Fuel_Type: shortint; GasTemp: Double):Double;
var S: shortint;
    SH,T: Double;
begin
   S:=Fuel_Type;
   T:=GasTemp;
   SH:=FuelThermo[S,1] + FuelThermo[S,2]*T + FuelThermo[S,3]*sqr(T);
{   FuelSpecHeat}SH:=SH*Runiversal;
   FuelSpecheat:=SH;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

{Returns the enthalpy of a given specie - hi. J/Kmol}
function TProp.Enthalpy(Specie: integer; GasTemp: Double):Double;
var  S: integer;  T,He: Double;
begin
  if (GasTemp < 260{300}) or (GasTemp > 5000) then    {260 to avoid error messages for now}
  begin
   error(2,0,GasTemp);
   if GasTemp > 5000 then GasTemp:=5000 else GasTemp:=300;
  end;
  S:=Specie;
  T:=GasTemp;
  if GasTemp > 1000
    then  He:=ThermoHT[S,1] + ThermoHT[S,2]*T/2 + ThermoHT[S,3]*sqr(T)/3 +
                  ThermoHT[S,4]*T*T*T/4 + ThermoHT[S,5]*T*T*T*T/5 + ThermoHT[S,6]/T
    else  He:=ThermoLT[S,1] + ThermoLT[S,2]*T/2 + ThermoLT[S,3]*sqr(T)/3 +
                  ThermoLT[S,4]*T*T*T/4 + ThermoLT[S,5]*T*T*T*T/5 + ThermoLT[S,6]/T;
  Enthalpy:=He*Runiversal*T;   {*MolWt[s]/4.184; cal /mol K}
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

{Returns the enthalpy of a given fuel type - hi. J/Kmol}
function TProp.FuelEnthalpy(Type_Fuel: shortint; GasTemp: Double):Double;
var S: shortint;
    He,T: Double;
begin
  S:=Type_Fuel;
  T:=GasTemp;
  He:=FuelThermo[S,1] + FuelThermo[S,2]*T/2 + FuelThermo[S,3]*sqr(T)/3 + FuelThermo[S,6]/T;
  He:=he + FuelThermo[S,4]*T*T*T/4 + FuelThermo[S,5]*T*T*T*T/5;
{  writeln('FuelEnth  ',He,' ',T,' ',burnedgascntrl);}
  FuelEnthalpy:=He*Runiversal*T;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

{Returns the Molecular weight of a given gas mixture - M.}
function TProp.MixMolWt(x: EqSpecArray; FuelMol: Double; Type_Fuel: shortint):Double;
var MixMolWt_1: Double;
    i: shortint;
begin
  MixMolWt_1:=0;
  for i:= 1 to 12 do  MixMolWt_1:= MixMolWt_1 + x[i]*MolWt[i];
  if BurnedGasCntrl = 0 then MixMolWt_1:= MixMolWt_1 + FuelMol*FuelMolWt[Type_Fuel];
  MixMolWt:=MixMolWt_1;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

{Returns the Gas Constant of a gas mixture - R. J/(kg.K)}
function TProp.MixGasConst(MWt: Double):Double;
begin
  MixGasConst:=Runiversal/MWt;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

{Returns the Specific Heat at const. pressure of a given gas mixture - cp. J/kg.K}
{Note: For non reacting gas mixture - one component of the dh/dT or cp calculatoion for reacting.}
function TProp.MixSpecHeat(x: EqSpecArray; FuelMol,GasTemp,MWt: Double; Type_fuel: shortint):Double;
var SH,T: Double;
    i: shortint;
begin
   T:=GasTemp;
   SH:=0;
   for i:= 1 to 12 do  SH:=SH + x[i]*SpecHeat(i,T);
   if BurnedGasCntrl = 0 then SH:= SH + FuelMol*FuelSpecHeat(Type_Fuel,T);
   MixSpecHeat:=SH/MWt;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

{Returns the Enthalpy of a given gas mixture - h. J/kg}
function TProp.MixEnth(x: EqSpecArray; FuelMol,GasTemp,MWt:Double; Type_Fuel: shortint):Double;
var He,T: Double;
    i: shortint;
    sum: Double;
begin
  T:=GasTemp;
  He:=0;
  for i:= 1 to 12 do  He:=He + x[i]*Enthalpy(i,T);
  if BurnedGasCntrl = 0 then He:= He + FuelMol*FuelEnthalpy(Type_Fuel,T);
  MixEnth:=He/MWt;
//  for i:= 1 to 12 do sum:=sum +x[i];
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

{Returns internal energy in J/Kg}
function TProp.MixIntE(h,R,GasTemp:Double):Double;
begin
{  writeln(h,'  ',R,'  ',GasTemp);}
  MixIntE:=h-R*GasTEmp;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TProp.MixdMdT(dxdT: EqSpecArray):Double;
var dMdT: Double;
    i: shortint;
begin
  dMdT:=0;
  for i:= 1 to 12 do dMdT:=dMdT + dxdT[i]*MolWt[i];
  MixdMdT:=dMdT;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TProp.MixdMdp(dxdp: EqSpecArray):Double;
var  dMdp: Double;
     i: shortint; dum:Double;
begin
  dMdp:=0;  dum:=0;
  for i:= 1 to 12 do dMdp:=dMdp + dxdp[i]*MolWt[i];

  {for i:= 1 to 12 do begin
           writeln('dxdp[',i,']= ',dxdp[i],' molwt[',i,']= ',molwt[i]);
            dum:=dum + dxdp[i]; end;  writeln(dum,'   ',dmdp); readln;}
  MixdMdp:=dMdp;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TProp.MixdMdF(dxdF: EqSpecArray):Double;
var  dMdF: Double;
     i: shortint;
begin
  dMdF:=0;
  for i:= 1 to 12 do dMdF:=dMdF + dxdF[i]*MolWt[i];
  MixdMdF:=dMdF;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TProp.MixdRdT(R,MWt,dMdT: Double):Double;
begin
  MixdRdT:=-R*dMdT/MWt;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TProp.MixdRdp(R,MWt,dMdp: Double):Double;
begin
  MixdRdp:=-R*dMdp/MWt;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TProp.MixdRdF(R,MWt,dMdF: Double):Double;
begin
  MixdRdF:=-R*dMdF/MWt;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TProp.MixdhdT(dxdT: EqSpecArray; cp,MWt,GasTemp,dMdT,h: Double):Double;
var  T,dum: Double;
     i: shortint;
begin
  T:=GasTemp;
  dum:=0;
  for i:= 1 to 12 do dum:=dum + dxdT[i]*Enthalpy(i,T);
  MixdhdT:=cp + (dum - dMdT*h)/MWt;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TProp.Mixdhdp(dxdp: EqSpecArray; MWt,GasTemp,dMdp,h: Double):Double;
var T,dum1: Double;
    i: shortint;
begin
  T:=GasTemp;
  dum1:=0;
  for i:= 1 to 12 do dum1:=dum1 + dxdp[i]*Enthalpy(i,T);
  Mixdhdp:=(dum1 - dMdp*h)/MWt;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TProp.MixdhdF(dxdF: EqSpecArray; MWt,GasTemp,dMdF,h: Double):Double;
var T,dum1: Double;
    i: shortint;
begin
  T:=GasTemp;
  dum1:=0;
  for i:= 1 to 12 do dum1:=dum1 + dxdF[i]*Enthalpy(i,T);
  MixdhdF:=(dum1 - dmdF*h)/MWt;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TProp.MixdudT(dhdT,R,dRdT,GasTemp: Double):Double;
begin
  MixdudT:=dhdT - R - dRdT*GasTemp;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TProp.Mixdudp(dhdp,dRdp,GasTemp: Double):Double;
begin
  Mixdudp:=dhdp - dRdp*GasTemp;
{  writeln(dhdp,' ',drdp,' ',gastemp,' ',drdp*gastemp);
  writeln('dudp= ',dhdp-drdp*gastemp); readln;}
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TProp.MixdudF(dhdF,dRdF,GasTemp: Double):Double;
begin
  MixdudF:=dhdF - dRdF*GasTemp;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

{According to Ferg solution to Ex 3.2 pg 110.}
function TProp.CalcK(GasTemp: Double):Double;
var t,lnK: Double;
begin
  t:=GasTEmp/1000;
  lnk:=2.743 - 1.761/t - 1.611/sqr(t) + 0.2803/(t*t*t);
  CalcK:=exp(lnK);
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

{According to Ferguson pg 108 with numbering system according to Olikara & Borman.}
{Sets global FAirResConcs = to mol fractions of components of residual gas.}
{Note: ER is ER after Combustion for calculation of Residual and GasTemp is Exhaust Temperature.}
procedure TProp.ResidualConcs(ERloc,nloc,mloc,lloc,kloc,ExTemp:Double);
var Keq,eps,a,b,c,TotMols: Double;    {NB! Ferg use y for moles : O&B use x for Mole fractions.}
    i: shortint;
begin
  Keq:=CalcK(ExTemp);
  eps:=0.2096/(nloc+0.25*mloc-0.5*lloc);
  if ERloc > 1 then
  begin
     a:=1-Keq;
     b:=0.4192 - ERloc*eps*(2*nloc-lloc)
          + Keq*(0.4192*(ERloc-1) + nloc*ERloc*eps);
     c:=-0.4192*nloc*ERloc*eps*(ERloc-1)*Keq;
     InitFAirResConcs[6]:=(-b+sqrt(sqr(b) - 4*a*c))/(2*a);
     InitFAirResConcs[1]:=0;
     InitFAirResConcs[2]:=0;
     InitFAirResConcs[3]:=0;
     InitFAirResConcs[4]:=0.4192*(ERloc-1) - InitFAirResConcs[6];
     InitFAirResConcs[5]:=0;
     InitFAirResConcs[7]:=0;
     InitFAirResConcs[8]:=0;
     InitFAirResConcs[9]:=0.4192 - ERloc*eps*(2*nloc-lloc)
                                 + InitFAirResConcs[6];
     InitFAirResConcs[10]:=nloc*ERloc*eps - InitFAirResConcs[6];
     InitFAirResConcs[11]:=0.7811 + kloc*ERloc*eps/2;
     InitFAirResConcs[12]:=0.0093;
     TotMols:=InitFAirResConcs[4]+
              InitFAirResConcs[6]+
              InitFAirResConcs[9]+
              InitFAirResConcs[10]+
              InitFAirResConcs[11]+
              InitFAirResConcs[12];
  end else
  begin
    InitFAirResConcs[1]:=0;
    InitFAirResConcs[2]:=0;
    InitFAirResConcs[3]:=0;
    InitFAirResConcs[4]:=0;
    InitFAirResConcs[5]:=0;
    InitFAirResConcs[6]:=0;
    InitFAirResConcs[7]:=0;
    InitFAirResConcs[8]:=0.2096*(1-ERloc);
    InitFAirResConcs[9]:=mloc*ERloc*eps/2;
    InitFAirResConcs[10]:=nloc*ERloc*eps;
    InitFAirResConcs[11]:=0.7811 + kloc*ERloc*eps/2;
    InitFAirResConcs[12]:=0.0093;
    TotMols:=InitFAirResConcs[8]
             +InitFAirResConcs[9]
             +InitFAirResConcs[10]
             +InitFAirResConcs[11]
             +InitFAirResConcs[12];
   end;
   for i:= 1 to 12 do
    InitFAirResConcs[i]:=InitFAirResConcs[i]/TotMols; {Turns into Mole Fractions.}
{   for i:= 1 to 12 do writeln(FAirResConcs[i]);}
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

{According to Ferg p111 with numbering according to Olikara & Borman.}
{Sets FuelAirResConcs to the concentration of Fuel, Air and Residual mixture.}
{Note: ER is instantaneous ER for calculation of Fuel Mol Fraction.}
procedure TProp.FuelAirResConcs(ERloc,nloc,mloc,lloc,kloc,floc:Double; Type_Fuel: shortint);
var eps,Mreac,Mprod,yr: Double;
    FuelAirMixConc: EqSpecArray;
    i: shortint;
begin
   if floc < 1e-5  then floc:=1e-5;    {Zero f causes crashes.}
   eps:=0.2096/(nloc+0.25*mloc-0.5*lloc);
   for i:= 1 to 12 do FuelAirMixConc[i]:=AirComposition[i]/(1+eps*ERloc);
   FuelMolFrac:=eps*ERloc/(1+eps*ERloc);
   Mreac:=MixMolWt(FuelAirMixConc,FuelMolFrac,Type_Fuel);
   Mprod:=MixMolWt(FAirResConcs,0,Type_Fuel);
   yr:=1/(1 + (Mprod/Mreac)*((1/floc) - 1));
   for i:= 1 to 12 do
   begin
    FAirResConcs[i]:=(1-yr)*FuelAirMixConc[i] + yr*InitFAirResConcs[i];
   end;
   FuelMolFrac:=(1-yr)*FuelMolFrac;
{   writeln(FuelMolFrac);}
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TProp.ResetError;
begin
  errcode:=0;
  errcount:=0;
  err2count:=0;
  err3count:=0;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

{Returns the error code.}
{Returns 3 digit error code ### (var : errcode)
 first digit: 1 for error in this unit (gasprops)
              2 for error in eqbm
              8 for more than 1 error in Eqbm object
              9 for more than 1 error.
 last 2 digits: error code of the last error encountered.}
function TProp.ErrorRet:integer;
var err: integer;
begin
{  writeln('count ',errcount);}
  if err1 = 1 then
  begin
    errcount:=errcount+1;
    if errcode = 0 then errcode:=1;
  end;
  if (BurnedGasCntrl = 1) then
  begin
    if (errcount = 0) then  errcode:=Eqbm1.errcode else
     if (errcount = 1) and  (Eqbm1.errcode <> 0) then errcount:=errcount+1;
    if (errcode = 202) or (errcode = 802) then halt;
  end;
   if (errcount > 1) then err:=errcode+900 else
    if (errcount = 1) then err:=errcode+100 else
     err:=errcode;
{  writeln('count ',errcount,'eqbm  ',Eqbm1.errcode);
  writeln('errcode   ',err);}
  ErrorRet:=err;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//


procedure TProp.Error(e,infoi :integer; infor :Double);
begin
  if e = 1 then
  begin
    err1:=1;
    Raise EgasPropsError.Create('Error in GasProps : Properties Incorrectly called :' +
                'Unburned - 0, Burned - 1. Called :'+ inttoStr(infoi));
  end;
  if e = 2 then
  begin
    if err2count < 1 then
    begin
      Raise EgasPropsError.Create('Error in GasProps: Temperature Out of Range ' +
                   'for Gas Properties Curve Fit. Requested Temp: ' +
                   FloatToStrf(infor, fffixed,10,1) +
                   ' Run will be terminated');
      errcount:=errcount+1;
    end;
    err2count:=err2count+1;
 end;
  if e = 3 then
  begin
    Raise EgasPropsError.Create('Error in GasProps : Could not find reasonable equilibrium' +
                'concentration so garbage returned.');
    if err3count < 1 then errcount:=errcount+1;
    err3count:=err3count+1;
  end;
  if(e<>0) then
   begin
    Raise EgasPropsError.Create ('Unknown Error in Two Zone Model : Consult your program vendor');
   end;

  errcode:=e;
end;

{--------------------------------------------------}
{Unit Startup Code   (Usually empty)   }
{--------------------------------------------------}

begin
end.
