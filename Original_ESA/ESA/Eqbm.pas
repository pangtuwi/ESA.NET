{****************************************************************************************}
{ This unit calculates the Species concentrations resulting from Equilibrium Combustion. }
{ Derivatives of Species Concentratin wrt Temperature, pressure and Equivalence Ratio    }
{   are also calculated.                                                                 }
{ Species Calculated - H, O, N, H2, OH, CO, NO, O2, H2O, CO2, N2, AR.                    }
{ Author - Arthur.                                                                       }
{ Requires MathDPM.                                                                      }
{ Valid for Temperatures 600 K < GasTemp < 4000 K                                        }
{ Calling procedure go calculates the species concentrations and derivatives -           }
{     access results via arrays x, dxdT, dxdp, dxdF.                                     }
{ ver: 1   21 Dec 96  status: returns sensible results - checked.                        }
{****************************************************************************************}

unit Eqbm;    {Arthur's equilibrium model}

interface

uses Classes, MathDPM, dialogs, sysutils;

(*{$R Eqbm.RES} not written yet*)

{This is the look up table for the coefficients for the equation to calculate
  the log of the Equilibrium Constants.  The zero arrays are dummies to set
  the numbering as per Olikara and Borman numbering system.
  Row #         Reaction
  1             0.5 H2 <> H
  2             0.5 O2 <> O
  3             0.5 N2 <> N
  4             dummy
  5             0.5 O2 + 0.5 H2 <> OH
  6             dummy
  7             0.5 N2 + 0.5 O2 <> NO
  8             dummy
  9             H2 + 0.5 O2 <> H2O
  10            CO + 0.5 O2 <> CO2}
const
KCoef : array[1..10,1..5] of Extended =
(( 0.432168   ,  -0.112464e2,   0.267269e1,  -0.745744e-1,   0.242484e-2),
 ( 0.310805   ,  -0.129540e2,   0.321779e1,  -0.738336e-1,   0.344645e-2),
 ( 0.389716   ,  -0.245828e2,   0.314505e1,  -0.963730e-1,   0.585643e-2), (0,0,0,0,0),
 (-0.141784   ,  -0.213308e1,   0.853461  ,   0.355015e-1,  -0.310227e-2), (0,0,0,0,0),
 ( 0.150879e-1,  -0.470959e1,   0.646096  ,   0.272805e-2,  -0.154444e-2), (0,0,0,0,0),
 (-0.752364   ,   0.124210e2,  -0.260286e1,   0.259556   ,  -0.162687e-1),
 (-0.415302e-2,   0.148627e2,  -0.475746e1,   0.124699   ,  -0.900227e-2));

{Temporarily here until can functions are in propties.}
{MolWt : array[1..12] of Extended =
(1.0080, 15.9994, 14.0067, 2.0160, 17.0074, 28.0106, 30.0061, 31.9988, 18.0154, 44.0100, 28.0134, 39.948);}

type
  EqSpecArray = array [1..12] of Extended;

type
  //PEqbm = ^TEqbm;
  TEqbm = class
     x,dxdT,dxdp,dxdF: EqSpecArray;
     errcode:integer;
     Frozen : Boolean;
     constructor Create; {(AName: PChar);}
     destructor Destroy; override;
     procedure go2(ER,n,m,l,k,Pres,GasTemp : Extended);
   private
     {The numbering system of species according to Olikara and Borman as follows
     1-H, 2-O, 3-N, 4-H2, 5-OH, 6-CO, 7-NO, 8-O2, 9-H2O, 10-CO2, 11-N2, 12-AR}
     {x13 - MOLES OF FUEL FOR 1 MOLE OF PRODUCTS. }
     x13,C1,C2,C3,C5,C7,C9,C10:  Extended;
     ERpr,Tpr,Ppr,npr,mpr,lpr,kpr : Extended;   {Values at previous step, to decide whether to make an initial estimate.}
     errcount,err2count,err3count,err5count,err6count: shortint;
     debug: boolean;
     procedure InitialEstimate(ER,n,m,l,k,r,rd,rdd:Extended);
     procedure EquilibriumCalc(d1,d2,d3,d4,n,rdd:Extended);
     procedure Partial_dxd(Pres,GasTemp,d1,d2,d3,d4,ro,ER,n:Extended);
     procedure SetupMatrix(d1,d2,d3,d4:Extended; var A:Tnxn_Array);
     function KEquilib(ReactionNum:integer; GasTemp:Extended):Extended;
     function dKEq_dT(ReactionNum:integer; GasTemp:Extended):Extended;
     procedure ResetError;
     procedure error(e,infoi : integer; infor : Extended);
  end;

  EEqbmError = class(Exception);

implementation

{--------------------------------------------------}
{ TEqbm's method implementations:     }
{--------------------------------------------------}
constructor TEqbm.Create; {(AName: PChar);}
begin
  inherited Create;
  Tpr:=1e6;
  Ppr:=1e3;     {Ensure that InitialEstimate is done first time at least.}
  debug:=true;
  Frozen := FALSE;
end;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

destructor TEqbm.Destroy;
begin
  inherited Destroy;
end;

{--------------------------------------------------}
{TEqbm's Public Procedures                     }
{--------------------------------------------------}

{This procedure calculates the parameters required in the following
procedures.}
procedure TEqbm.go2(ER,n,m,l,k,Pres,GasTemp : Extended);
var p,r,rd,rdd,ro,d1,d2,d3,d4: Extended;
begin
//  if GasTemp < 1000 then Frozen := TRUE else FROZEN := FALSE; //Assume Eqs "Freeze" below 1000K
  if Frozen then EXIT; //can do this? PNTW tring to do Overlap
  if GasTemp < 1000 then GasTemp := 1000;
  ResetError;
  if ER < 1e-10 then ER:= 1e-10;    {0 ER causes error 200 crash!}
  p:=Pres/101325;
  ro:=(n+m/4-l/2)/ER;
  rdd:=0.0444*ro;
  rd:=k/2+3.7274*ro;
  r:=l/2+ro;
  d1:=m/n;
  d2:=2*r/n;
  d3:=2*rd/n;
  d4:=rdd/n;
  C1:=KEquilib(1,GasTemp)/sqrt(p);
  C2:=KEquilib(2,GasTemp)/sqrt(p);
  C3:=KEquilib(3,GasTemp)/sqrt(p);
  C5:=KEquilib(5,GasTemp);
  C7:=KEquilib(7,GasTemp);
  C9:=KEquilib(9,GasTemp)*sqrt(p);
  C10:=KEquilib(10,GasTemp)*sqrt(p);
  InitialEstimate(ER,n,m,l,k,r,rd,rdd);
  if x[8] <> 99 then
  begin
    EquilibriumCalc(d1,d2,d3,d4,n,rdd);
    Partial_dxd(p,GasTemp,d1,d2,d3,d4,ro,ER,n);
    ERpr:=ER;
    Tpr:=GasTEmp;
    Ppr:=Pres;
    npr:=n;
    mpr:=m;
    lpr:=l;
    kpr:=k;
  end else
  begin
  end;
end;

{--------------------------------------------------}
{TEqbm's Private Procedures                     }
{--------------------------------------------------}


{ This provides an initial estimate of the mole fractions of the more
  important species to start the Newton Raphson iteration for all species.
  Equations 16 & 17 Olikara & Borman.
  NB P in Pa, convert to atm in procedure}

procedure TEqbm.InitialEstimate(ER,n,m,l,k,r,rd,rdd :Extended);
var x8new,tol,fn,dfn,srx8    :Extended;
    IterCount  : integer;
    outloop : boolean;
begin
  if r < 0.5*n then error(1,0,0.5*n/r);   {Solid Carbon Formed!!!}
  if ER > 1 then x13:=1/(n+m/2+rd+rdd) else x13:=1/(m/4+r+rd+rdd);
  x[8]:=1;  {first guess}
  srx8:=sqrt(x[8]);
  fn:=(2*c10*n*srx8+n)/(1+c10*srx8)+0.5*C9*m*srx8/(1+C9*srx8)+2*x[8]/x13-2*r;
  outloop:=false;
  if fn > 0 then
  repeat
    x[8]:=0.1*x[8];
    if x[8] < 1e-33 then
     begin
       error(2,0,x[8]);   {No initial estimate found within reasonable limits.}
       outloop:=true;     {Cannot solve for fn <= 0}
//##       writeln(ErrorLogFile,x[8]);
     end;
    srx8:=sqrt(x[8]);
 {   writeln('iterating');}
    fn:=(2*C10*n*srx8+n)/(1+C10*srx8)+0.5*C9*m*srx8/(1+C9*srx8)+2*x[8]/x13-2*r;
  until (fn <= 0) or (outloop);
{  writeln('        out');}
  if outloop = false then
  begin
    IterCount:=0;
    repeat
      IterCount:=IterCount+1;
      srx8:=sqrt(x[8]);
      fn:=(2*C10*n*srx8+n)/(1+C10*srx8)+0.5*C9*m*srx8/(1+C9*srx8)+2*x[8]/x13-2*r;
      dfn:=C10*n/(2*sqr(1+C10*srx8)*srx8)+C9*m/(4*sqr(1+C9*srx8)*srx8)+2/x13;
      x8new:=x[8]-fn/dfn;
      tol:=abs(x8new-x[8]);
{      writeln(x[8],' ',x8new,'  ',fn);}
      x[8]:=x8new;
    until (tol < 0.0004) or (abs(fn) < 0.0005) or (IterCount >= 20);
    x[6]:=n*x13/(1+C10*sqrt(x[8]));
    x[4]:=0.5*m*x13/(1+C9*sqrt(x[8]));
    x[11]:=rd*x13;
   {writeln(r:12,' ',rd:12,' ',rdd:12,' ',ro:12);
    writeln('initial estimate gives : ');
   writeln(x[4]:12,'  ',x[6]:12,'  ',x[8]:12,'  ',x[11]:12,'  ',x13:12);}
 end else x[8]:=99;  {No reasonable 02 found, cannot solve fn<=0}
end;

{This calculates the Equilibrium Concentrations of the species.}
procedure TEqbm.EquilibriumCalc(d1,d2,d3,d4,n,rdd:Extended);
var {T14,T28,T311,T54,T58,T78,T711,T94,T98,T106,T108: Extended;}
    A: Tnxn_Array;   B: Tnx1_Array;
    tol: Extended;
    resolution,IterCount: integer;
begin
  IterCount:=0;
  repeat
    IterCount:=IterCount+1;
    SetupMatrix(d1,d2,d3,d4,A);
    x[1]:=C1*sqrt(x[4]);
    x[2]:=C2*sqrt(x[8]);
    x[3]:=C3*sqrt(x[11]);
    x[5]:=C5*sqrt(x[4])*sqrt(x[8]);
    x[7]:=C7*sqrt(x[8])*sqrt(x[11]);
    x[9]:=C9*x[4]*sqrt(x[8]);
    x[10]:=C10*x[6]*sqrt(x[8]);
{  writeln('x1 : ',x[1]);
  writeln('x2 : ',x[2]);
  writeln('x3 : ',x[3]);
  writeln('x4 : ',x[4]);
  writeln('x5 : ',x[5]);
  writeln('x6 : ',x[6]);
  writeln('x7 : ',x[7]);
  writeln('x8 : ',x[8]);
  writeln('x9 : ',x[9]);
  writeln('x10 : ',x[10]);
  writeln('ro    r   rd   rdd');
  writeln(ro:12,'  ',r:12,'  ',rd:12,'  ',rdd:12);
  writeln('d1    d2     d3     d4');
  writeln(d1:12,'  ',d2:12,'  ',d3:12,'   ',d4:12);
  writeln('matrix');
  writeln(A[0,0]:12,'  ',A[0,1]:12,'  ',A[0,2]:12,'   ',A[0,3]:12);
  writeln(A[1,0]:12,'  ',A[1,1]:12,'  ',A[1,2]:12,'   ',A[1,3]:12);
  writeln(A[2,0]:12,'  ',A[2,1]:12,'  ',A[2,2]:12,'   ',A[2,3]:12);
  writeln(A[3,0]:12,'  ',A[3,1]:12,'  ',A[3,2]:12,'   ',A[3,3]:12);}
    B[0]:=-( x[1] + 2*x[4] + x[5] + 2*x[9] ) + d1*( x[6] + x[10] );
    B[1]:=-( x[2] + x[5] + x[6] + x[7] + 2*x[8] + x[9] + 2*x[10] )+ d2*( x[6] + x[10] );
    B[2]:=-( x[3] + x[7] + 2*x[11] ) + d3*(x[6] + x[10] );
    B[3]:=1 - d4*( x[6] + x[10] ) - ( x[1] + x[2] + x[3] + x[4] + x[5] + x[6] + x[7] + x[8] + x[9] + x[10] + x[11]);
{   writeln('Bvect ',B[0]:12,'  ',B[1]:12,'  ',B[2]:12,'  ',B[3]:12);}
    resolution:=GaussReduce(A,B);
{   writeln('yvect ',B[0]:12,'  ',B[1]:12,'  ',B[2]:12,'  ',B[3]:12);}
    x[4]:=x[4]+B[0];
    x[6]:=x[6]+B[1];
    x[8]:=x[8]+B[2];
    x[11]:=x[11]+B[3];
    if (x[4] < 0) or (x[6] < 0) or (x[8] < 0) or (x[11] < 0) then
    begin
 {     error(5,1,0);  {Negative mole fractions.}{removed as no big deal, checked after iterations.}
      if (x[4] <= 0) then x[4]:=1e-25;     {Crash avoidance}
      if (x[6] <= 0) then x[6]:=1e-25;
      if (x[8] <= 0) then x[8]:=1e-25;
      if (x[11] <= 0) then x[11]:=1e-25;
    end;
    tol:=abs(B[0]/x[4]);
    if abs(B[1]/x[6]) > tol then tol:=abs(B[1]/x[6]);
    if abs(B[2]/x[8]) > tol then tol:=abs(B[2]/x[8]);
    if abs(B[3]/x[11]) > tol then tol:=abs(B[3]/x[11]);
{    writeln('res ',resolution,' tolerance ',tol,' itercount ',itercount);}
{    writeln('concs',x[4]:12,'  ',x[6]:12,'  ',x[8]:12,'  ',x[11]:12);}
  until ((tol < 0.0001) and (resolution > 5)) or (IterCount > 25);
  if resolution < 5 then error(3,resolution,1);   {Gauss Reduction - insufficient resolution.}
  if Itercount > 24 then error(4,Itercount,0);  {Newton Raphson not converging}
  if (x[4] < 0) or (x[6] < 0) or (x[8] < 0) or (x[11] < 0) then error(5,2,0);  {negative mol fractions after iterations}
  x[1]:=C1*sqrt(x[4]);
  x[2]:=C2*sqrt(x[8]);
  x[3]:=C3*sqrt(x[11]);
  x[5]:=C5*sqrt(x[4])*sqrt(x[8]);
  x[7]:=C7*sqrt(x[8])*sqrt(x[11]);
  x[9]:=C9*x[4]*sqrt(x[8]);
  x[10]:=C10*x[6]*sqrt(x[8]);
  x13:=(x[6]+x[10])/n;
  x[12]:=rdd*x13;
end;

procedure TEqbm.Partial_dxd(Pres,GasTemp,d1,d2,d3,d4,ro,ER,n:Extended);
var
{T14,T28,T311,T54,T58,T78,T711,T94,T98,T106,T108: Extended;}
dC1dT,dC2dT,dC3dT,dC5dT,dC7dT,dC9dT,dC10dT: Extended;
dC1dp,dC2dp,dC3dp,dC9dp,dC10dp: Extended;
d5,p: Extended;
A: Tnxn_Array;   B_T: Tnx1_Array;   B_p: Tnx1_Array;  B_F: Tnx1_Array;
resolution: integer;

begin
  p:=Pres;
  SetupMatrix(d1,d2,d3,d4,A);
  dC1dT:=dKEq_dT(1,GasTemp)/sqrt(p);
  dC2dT:=dKEq_dT(2,GasTemp)/sqrt(p);
  dC3dT:=dKEq_dT(3,GasTemp)/sqrt(p);
  dC5dT:=dKEq_dT(5,GasTemp);
  dC7dT:=dKEq_dT(7,GasTemp);
  dC9dT:=dKEq_dT(9,GasTemp)*sqrt(p);
  dC10dT:=dKEq_dT(10,GasTemp)*sqrt(p);
{  writeln('C1:= ',C1);
  writeln('C2:= ',C2);
  writeln('C3:= ',C3);
  writeln('C5:= ',C5);
  writeln('C7:= ',C7);
  writeln('C9:= ',C9);
  writeln('C10:= ',C10);}
  B_T[0]:=-(dC1dT*x[1]/C1 + dC5dT*x[5]/C5 + 2*dC9dT*x[9]/C9 - d1*dC10dT*x[10]/C10);
  B_T[1]:=-(dC2dT*x[2]/C2 + dC5dT*x[5]/C5 + dC7dT*x[7]/C7 + dC9dT*x[9]/C9 + (2-d2)*dC10dT*x[10]/C10);
  B_T[2]:=-(dC3dT*x[3]/C3 + dC7dT*x[7]/C7 - d3*dC10dT*x[10]/C10);
  B_T[3]:=-(dC1dT*x[1]/C1 + dC2dT*x[2]/C2 + dC3dT*x[3]/C3 + dC5dT*x[5]/C5 + dC7dT*x[7]/C7
           + dC9dT*x[9]/C9 + (1+d4)*dC10dT*x[10]/C10);
  resolution:=GaussReduce(A,B_T);
{  writeln(resolution);}
  if resolution < 5 then error(3,resolution,2);  {Gauss Reduce sometimes returns 4 here?}
  dC1dp:=-0.5*C1/p;
  dC2dp:=-0.5*C2/p;
  dC3dp:=-0.5*C3/p;
  dC9dp:=0.5*C9/p;
  dC10dp:=0.5*C10/p;
  B_p[0]:=-(dC1dp*x[1]/C1 + 2*dC9dp*x[9]/C9 - d1*dC10dp*x[10]/C10);
  B_p[1]:=-(dC2dp*x[2]/C2 + dC9dp*x[9]/C9 + (2-d2)*dC10dp*x[10]/C10);
  B_p[2]:=-(dC3dp*x[3]/C3 - d3*dC10dp*x[10]/C10);
  B_p[3]:=-(dC1dp*x[1]/C1 + dC2dp*x[2]/C2 + dC3dp*x[3]/C3 + dC9dp*x[9]/C9
              + (1+d4)*dC10dp*x[10]/C10);
  resolution:=GaussReduce(A,B_p);
  if resolution < 5 then error(3,resolution,3);
  d5:=-ro*x13/ER;
  B_F[0]:=0;
  B_F[1]:=(x[6]+x[10])*2*d5/n;
  B_F[2]:=(x[6]+x[10])*7.4548*d5/n;
  B_F[3]:=-(x[6]+x[10])*0.0444*d5/n;
  resolution:=GaussReduce(A,B_F);
  if resolution < 5 then error(3,resolution,4);
  dxdT[1]:=C1*0.5*B_T[0]/sqrt(x[4]) + dC1dT*sqrt(x[4]);
  dxdT[2]:=C2*0.5*B_T[2]/sqrt(x[8]) + dC2dT*sqrt(x[8]);
  dxdT[3]:=C3*0.5*B_T[3]/sqrt(x[11]) + dC3dT*sqrt(x[11]);
  dxdT[4]:=B_T[0];
  dxdT[5]:=C5*0.5*(sqrt(x[8])*B_T[0]/sqrt(x[4]) + sqrt(x[4])*B_T[2]/sqrt(x[8]))
          + dC5dT*sqrt(x[4])*sqrt(x[8]);
  dxdT[6]:=B_T[1];
  dxdT[7]:=C7*0.5*(sqrt(x[11])*B_T[2]/sqrt(x[8]) + sqrt(x[8])*B_T[3]/sqrt(x[11]))
          + dC7dT*sqrt(x[8])*sqrt(x[11]);
  dxdT[8]:=B_T[2];
  dxdT[9]:=C9*(sqrt(x[8])*B_T[0] + 0.5*x[4]*B_T[2]/sqrt(x[8])) + dC9dT*x[4]*sqrt(x[8]);
  dxdT[10]:=C10*(sqrt(x[8])*B_T[1] + 0.5*x[6]*B_T[2]/sqrt(x[8])) + dC10dT*x[6]*sqrt(x[8]);
  dxdT[11]:=B_T[3];
  dxdT[12]:=d4*(B_T[1]+dxdT[10]);
  dxdp[1]:=C1*0.5*B_p[0]/sqrt(x[4]) + dC1dp*sqrt(x[4]);
  dxdp[2]:=C2*0.5*B_p[2]/sqrt(x[8]) + dC2dp*sqrt(x[8]);
  dxdp[3]:=C3*0.5*B_p[3]/sqrt(x[11]) + dC3dp*sqrt(x[11]);
  dxdp[4]:=B_p[0];
  dxdp[5]:=C5*0.5*(sqrt(x[8])*B_p[0]/sqrt(x[4]) + sqrt(x[4])*B_p[2]/sqrt(x[8]));
  dxdp[6]:=B_p[1];
  dxdp[7]:=C7*0.5*(sqrt(x[11])*B_p[2]/sqrt(x[8]) + sqrt(x[8])*B_p[3]/sqrt(x[11]));
  dxdp[8]:=B_p[2];
  dxdp[9]:=C9*(sqrt(x[8])*B_p[0] + 0.5*x[4]*B_p[2]/sqrt(x[8])) + dC9dp*x[4]*sqrt(x[8]);
  dxdp[10]:=C10*(sqrt(x[8])*B_p[1] + 0.5*x[6]*B_p[2]/sqrt(x[8])) + dC10dp*x[6]*sqrt(x[8]);
  dxdp[11]:=B_p[3];
  dxdp[12]:=d4*(B_p[1]+dxdp[10]);
  dxdF[1]:=C1*0.5*B_F[0]/sqrt(x[4]);
  dxdF[2]:=C2*0.5*B_F[2]/sqrt(x[8]);
  dxdF[3]:=C3*0.5*B_F[3]/sqrt(x[11]);
  dxdF[4]:=B_F[0];
  dxdF[5]:=C5*0.5*(sqrt(x[8])*B_F[0]/sqrt(x[4]) + sqrt(x[4])*B_F[2]/sqrt(x[8]));
  dxdF[6]:=B_F[1];
  dxdF[7]:=C7*0.5*(sqrt(x[11])*B_F[2]/sqrt(x[8]) + sqrt(x[8])*B_F[3]/sqrt(x[11]));
  dxdF[8]:=B_F[2];
  dxdF[9]:=C9*(sqrt(x[8])*B_F[0] + 0.5*x[4]*B_F[2]/sqrt(x[8]));
  dxdF[10]:=C10*(sqrt(x[8])*B_F[1] + 0.5*x[6]*B_F[2]/sqrt(x[8]));
  dxdF[11]:=B_F[3];
  dxdF[12]:=d4*(dxdF[6]+dxdF[10]) + 0.0444*d5;
end;


procedure TEqbm.SetupMatrix(d1,d2,d3,d4:Extended; var A:Tnxn_Array);
var T14,T28,T311,T54,T58,T78,T711,T94,T98,T106,T108: Extended;
begin
  T14:=0.5*C1/sqrt(x[4]);
  T28:=0.5*C2/sqrt(x[8]);
  T311:=0.5*C3/sqrt(x[11]);
  T54:=0.5*C5*sqrt(x[8])/sqrt(x[4]);
  T58:=0.5*C5*sqrt(x[4])/sqrt(x[8]);
  T78:=0.5*C7*sqrt(x[11])/sqrt(x[8]);
  T711:=0.5*C7*sqrt(x[8])/sqrt(x[11]);
  T94:=C9*sqrt(x[8]);
  T98:=0.5*C9*x[4]/sqrt(x[8]);
  T106:=C10*sqrt(x[8]);
  T108:=0.5*C10*x[6]/sqrt(x[8]);
  A[0,0]:=T14+2+T54+2*T94;
  A[0,1]:=-d1*(1+T106);
  A[0,2]:=T58+2*T98-d1*T108;
  A[0,3]:=0;
  A[1,0]:=T54+T94;
  A[1,1]:=1+2*T106-d2*(1+T106);
  A[1,2]:=T28+T58+T78+2+T98+2*T108-d2*T108;
  A[1,3]:=T711;
  A[2,0]:=0;
  A[2,1]:=-d3*(1+T106);
  A[2,2]:=T78-d3*T108;
  A[2,3]:=T311+T711+2;
  A[3,0]:=T14+1+T54+T94;
  A[3,1]:=1+T106+d4*(1+T106);
  A[3,2]:=T28+T58+T78+1+T98+T108+d4*T108;
  A[3,3]:=T311+T711+1;
end;

{This function calculates the Equilibrium Constants - Ki.
  Requires the Reaction Number and Gas Temperature in Kelvin.}
function  TEqbm.KEquilib(ReactionNum:integer;GasTemp:Extended):Extended;
var Rnum : integer; T,LogK,K : Extended;     {Rnum=reaction number}
begin
  if (GasTemp < 600) or (GasTemp > 4000) then
  begin
    error(6,0,GasTemp);
    if GasTemp > 4000 then GasTemp:=4000 else GasTemp:=600;
  end;
  Rnum:=ReactionNum;
  T:=GasTemp/1000;
  LogK:=KCoef[Rnum,1]*ln(T)+KCoef[Rnum,2]/T+KCoef[Rnum,3]+KCoef[Rnum,4]*T+KCoef[Rnum,5]*sqr(T);
  K:=DoublePower(10,LogK);
  if k < 1e-10 then KEquilib:= 1e-10 else KEquilib:=k;   {Small numbers return as zero, causing error 207.}
{  writeln('K ',K);          }
end;


{This function calculates the derivative of the Equilibrium Constants wrt Temperature
  - dKi/dT }
function TEqbm.dKEq_dT(ReactionNum:integer; GasTemp:Extended):Extended;
var Rnum : integer; T,Ki : Extended;
begin
  Rnum:=ReactionNum;
  T:=GasTemp/1000;
  Ki:=KEquilib(Rnum,GasTemp);
  dKEq_dT:=0.001*ln(10)*Ki*(KCoef[Rnum,1]/T-KCoef[Rnum,2]/sqr(T)+KCoef[Rnum,4]+2*KCoef[Rnum,5]*T);
end;

procedure TEqbm.ResetError;
begin
   errcode:=0;
   errcount:=0;
   err2count:=0;
   err3count:=0;
   err5count:=0;
   err6count:=0;
end;

{Don't know what to do if error, D can fill in here.
 NB! I don't expect errors but just in case.}

procedure TEqbm.Error(e,infoi : integer; infor : Extended);
begin
    if e = 1 then
    begin
      Raise EEqbmError.Create ('Error in Chemical Equilibrium :  ' +
                   ' Solid Carbon Will Form: 0.5n/r=' +
                   FloatToStrF(infor, fffixed, 10, 5));
      errcount:=errcount+1;
    end;
    if e = 2 then
    begin
      if (err2count < 1) then
      begin
        Raise EEqbmError.Create ('Error in Chemical Equilibrium :  ' +
           'Initial Estimate Predicts Extremely Low O2 Concentrations of ' +
            FloatToStrF(infor, fffixed, 10, 5));
        errcount:=errcount+1;
      end;
      err2count:=err2count+1;
    end;
    if e= 3 then
    begin
      if (err3count < 1) then
      begin
        Raise EEqbmError.Create ('Error in Chemical Equilibrium :  ' +
              'Matrixsolver Returned Insufficient Resolution ');

             errcount:=errcount+1;
      end;
      err3count:=err3count+1;
    end;
    if e = 4 then
    begin
      Raise EEqbmError.Create ('Error in Chemical Equilibrium :  ' +
               'Newton Raphson Iteration did not Converge.');
      errcount:=errcount+1;
    end;
    if e= 5 then
    begin
      if err5count < 1 then
      begin
        Raise EEqbmError.Create ('Negative Mole Fractions Found During Iteration.');    
        errcount:=errcount+1;
      end;
      err5count:=err5count+1;
    end;
    if e= 6 then
    begin
      if err6count < 1 then
      begin
       Raise EEqbmError.Create( 'Temperature Out of Range for Equilibrium ' +
                                ' Constant Curve Fit. Requested Temp: ' +
                                 FloatToStrF(infor, fffixed, 10, 2) + 'K');
        errcount:=errcount+1;
      end;
      err6count:=err6count+1;
    end;
    if (errcount > 1) then errcode:=800+e else errcode:=200+e;
end;

{--------------------------------------------------}
{Unit Startup Code   (Usually empty)   }
{--------------------------------------------------}
begin
end.
