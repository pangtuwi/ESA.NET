//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// Performance Data : Engine Simulation and Analysis (ESA)
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

unit PerfData;

interface

uses
 Dialogs;

const
 MaxnoPoints = 100;

type
 TPerfPoint = Class
  Speed, Torque, Power, VolEff : Double;
 end; //TPerfPoint

 TPerfData = Class
  NoPoints : Integer;
  Point : Array [1..MaxNoPoints] of TPerfPoint;
  Constructor Create;
  procedure AddDataPoint(N, T, P, VEff : Double);
  procedure Clear;
  Destructor Destroy; Override;
 end; //TPerfData

var
 PerformanceData : TPerfData;

implementation

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Constructor TPerfData.Create;
var
 i : integer;
begin
 inherited Create;
 NoPoints := 0;
end; //TPerfData.Create

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Destructor TPerfdata.Destroy;
begin
 Clear;
 inherited destroy;
end; //TPerfdata.Destroy

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TPerfData.Clear;
var
 i : integer;
begin
 if NoPoints = 0 then Exit;
 For i := 1 to NoPoints do
  Point[i].Free;
 NoPoints := 0;
end; //TPerfData.Clear

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TPerfData.AddDataPoint(N, T, P, VEff : Double);
begin
 if NoPoints = MaxNoPoints then
  begin
   ShowMessage ('Max No Of Stored Datapoints reached...' +
                ' This point will not be stored.');
   Exit;
  end; // if NoPoints
 inc(NoPoints);
 Point[NoPoints] := TPerfPoint.Create;
 Point[NoPoints].Speed := N;
 Point[NoPoints].Torque := T;
 Point[NoPoints].Power := P;
 Point[NoPoints].VolEff := VEff;
end; //TPerfData.AddDataPoint

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
end.
