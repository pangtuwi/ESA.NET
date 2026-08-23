//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// Manifold Grid Size : Engine Simulation and Analysis (ESA)
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

unit Gridsizes;

interface

uses
 SysUtils, Dialogs, Controls, AdCalc, Classes, StdCtrls;

type
 TGridSize = Class (TObject)         
  functionString : string;
  MyEdit : TEdit;
  GridFunc : TAdCalc;
  function Gridsize(L,N : Double): Integer; //L = Length, N = Engine rpm
  constructor Create;
  Destructor Destroy; Override;
 end; //TGridSize

implementation

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Constructor TGridSize.Create;
begin
 inherited Create;
 GridFunc := TAdCalc.Create(MyEdit);  //Needed
end; //TGridSize.Create

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function TGridSize.GridSize (L, N : Double) : integer;
var
 FRes : Extended;
 FuncStrings : String;
begin
//  FuncStrings := TStringList.Create;
 //FuncStrings.Clear;
  FuncStrings :=  functionString;
  GridFunc.RegVariable('L', EtExtended,'Length');
  GridFunc.SetExtendedVarValue('L',L);
  GridFunc.RegVariable('N', EtExtended, 'EngineSpeed');
  GridFunc.SetExtendedVarValue('N',N);
  GridFunc.GetExtendedResult(FuncStrings,FRes,1);
  Result := Round(FRes);
//  FuncStrings.Free;
end; //TGridSize.GridSize

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Destructor TGridSize.Destroy;
begin
  GridFunc.Free;
  inherited Destroy;
end; //TManifolds.Destroy

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
end.
