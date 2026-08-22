//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// 5th Order Runga Kutta Fehlberg : Engine Simulation and Analysis (ESA)
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

unit RKF5;

interface

uses classes;

const MaxN = 4; //can be anything, I think.......

type
  yarray =array[1..MaxN] of Double;
  dxdyFunction = function (x : Double; y : yarray) : Double;


  TRKF = class
    NEqns : Integer;
    Integrator : Integer;
    x, dx : Double;
    y : yarray;
    fn : array [1..MaxN] of dxdyFunction;
    Procedure Integrate;
    Procedure IntegrateRKF;
    Procedure IntegrateEuler;
    Constructor Create;
    Destructor Destroy; Override;
  end; //TRKF

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

implementation

Procedure TRKF.Integrate;
begin
 If Integrator = 0 then IntegrateRKF else IntegrateEuler;
end; //TRKF.Integrate

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TRKF.IntegrateRKF;
var
 k : array [1..6] of yarray;
 ytemp : yarray;
 i, j : Integer;
begin
 for i := 1 to NEqns do
  k[1,i] := dx*fn[i](x, y);

 for i := 1 to NEqns do
   begin
     for j := 1 to NEqns do ytemp[j] := y[j] + k[1,j]/4;
     k[2,i] := dx*fn[i](x+dx/4, ytemp);
   end;

 for i := 1 to NEqns do
   begin
     for j := 1 to NEqns do ytemp[j] := y[j] + 3/32*k[1,j] + 9/32*k[2,j];
     k[3,i] := dx*fn[i](x+3/8*dx, ytemp);
   end;

 for i := 1 to NEqns do
   begin
     for j := 1 to NEqns do ytemp[j] := y[j] + 1932/2197*k[1,j]
                                             - 7200/2197*k[2,j]
                                             + 7296/2197*k[3,j];
     k[4,i] := dx*fn[i](x+12/13*dx, ytemp);
   end;

 for i := 1 to NEqns do
   begin
     for j := 1 to NEqns do ytemp[j] := y[j] + 439/216*k[1,j]
                                             - 8*k[2,j]
                                             + 3680/513*k[3,j]
                                             - 854/4104*k[4,j];
     k[5,i] := dx*fn[i](x+dx, ytemp);
   end;

 for i := 1 to NEqns do
   begin
     for j := 1 to NEqns do ytemp[j] := y[j] - 8/27*k[1,j] + 2*k[2,j]
                                       -3544/2565*k[3,j] + 1859/4104*k[4,j]
                                      -11/40*k[5,j];
     k[6,i] := dx*fn[i](x+dx/2, ytemp);
   end;

 for i := 1 to NEqns do
    y[i] := y[i] + (16/135*k[1,i] + 6656/12825*k[3,i] + 28561/56430*k[4,i]
                   - 9/50*k[5,i] + 2/55*k[6,i]);
end; //TRKF.IntegrateRKF

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TRKF.IntegrateEuler; //this here for A quick Alternative
var
 k : array [1..6] of yarray;
 ytemp : yarray;
 i, j : Integer;
begin
 for i := 1 to NEqns do
  ytemp[i] := y[i] + dx*fn[i](x,y);
 y := ytemp;
end; //TRKF.IntegrateEuler

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Constructor TRKF.Create;
var
 i : Integer;
begin
 inherited Create;
 for i := 1 to MaxN do y[i] := 0;
 x := 0;
 dx := 0;
end; //TRKF.Create

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Destructor TRKF.Destroy;
begin
  Inherited Destroy;
end; //TRKF.Destroy

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
end.
