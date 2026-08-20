 (***********************************************)
 (*                                             *)
 (*  Advanced Calculator v4.1 for Delphi        *)
 (*                                             *)
 (*  Copiright 1999-2000 by George Barbakadze   *)
 (*  All rights reserved                        *)
 (*                                             *)
 (***********************************************)


{The mechanism of evaluating is based on Five main processes:

  1. Arithmetical row     ( 5+10*3-20/4+... )
       function TParser.GetNumRowValue;

  2. Logical row          ( true and false or false and ... )
       function TParser.GetBoolRowValue;

  2. String row          ( "My "+"string"+... )
       function TParser.GetStringRowValue;

  4. Function             ( FunctionName(100,false,"Name - ",...) )
       function TParser.GetFunctionParams;

  5. Comparisons          ( 200>100 )
       function TParser.GetCompareResult;

The Structure of mutual request is:

                                      |------------------------|
                              |------>|      Direct value      |
                              |       | (constant or variable) |
                              |       |------------------------|
                              |
             |--------|   |-------|   |------------------------|
Expression-->|  Row   |-->| Value |-->| Function or comparison |
             |--------|   |-------|   |------------------------|
               |   /\                    |         |
               |    |                    |         |
               |    |--------------------|---------|
               |                         |
              \/                        \/
             |-----------------------------|
             |           Symbol            |
             |-----------------------------|
}

{$D+,L+,Y+}

{.$DEFINE DEMO}

unit AdCalc;

interface

uses
  Windows, SysUtils, Classes, Graphics, Controls, Forms, Dialogs,
  Math;

const
  MaxIdentLen=64; {Maximum length of identifier}
  CurrentSavingVersion=1; {Version of saving's format of library}

Type
  TExpection=(exIndef,    {expections}
              exNone,
              exComma,
              exLeftParenthesis,
              exRightParenthesis,
              exArithSign,
              exLogSign,
              exCompareSign,
              exEnd,
              exComment,
              exStrSign);

  TSymbol=(sNone,  {Tokens}
           sComma,
           sLeftParenthesis,
           sRightParenthesis,
           sAnd,
           sOr,
           sGreaterOrEqual,
           sLessOrEqual,
           sGreaterOrLess,
           sEqual,
           sGreater,
           sLess,
           sAddition,
           sSubtract,
           sMultiplic,
           sDivision,
           sPower,
           sStringconst,
           sBooleanFunct,
           sExtendedFunct,
           sStringFunct,
           sParam,
           sSemicolon,
           sApostrophe,
           sQMark);

  TSymbolProperties=record
    SymbolStr:string;
    Expection:TExpection;
  end;
  TSymbolTable=array [TSymbol] of TSymbolProperties;

const
  SymbolTable:TSymbolTable=(
    (SymbolStr:'';        Expection:exNone),
    (SymbolStr:',';       Expection:exComma),
    (SymbolStr:'(';       Expection:exLeftParenthesis),
    (SymbolStr:')';       Expection:exRightParenthesis),
    (SymbolStr:'and';     Expection:exLogSign),
    (SymbolStr:'or';      Expection:exLogSign),
    (SymbolStr:'>=';      Expection:exCompareSign),
    (SymbolStr:'<=';      Expection:exCompareSign),
    (SymbolStr:'<>';      Expection:exCompareSign),
    (SymbolStr:'=';       Expection:exCompareSign),
    (SymbolStr:'>';       Expection:exCompareSign),
    (SymbolStr:'<';       Expection:exCompareSign),
    (SymbolStr:'+';       Expection:exArithSign),
    (SymbolStr:'-';       Expection:exArithSign),
    (SymbolStr:'*';       Expection:exArithSign),
    (SymbolStr:'/';       Expection:exArithSign),
    (SymbolStr:'^';       Expection:exArithSign),
    (SymbolStr:'';        Expection:exInDef),
    (SymbolStr:'';        Expection:exInDef),
    (SymbolStr:'';        Expection:exInDef),
    (SymbolStr:'';        Expection:exInDef),
    (SymbolStr:'param';   Expection:exIndef),
    (SymbolStr:';';       Expection:exEnd),
    (SymbolStr:'''';      Expection:exComment),
    (SymbolStr:'"';       Expection:exStrSign));

type
  {Arith. row that is caused at first level}
  TRow=(FirstLevelNumRow,
  {Arith. row that is caused As a last param. of funct.}
        LastParameterNumRow,
  {Arith. row that is caused As a non last param. of funct.}
        NonLastParameterNumRow,
  {Arith. row that is caused As a first value of comparison}
        ComparePrimaryNumRow,
  {Arith. row that is caused As a second value of comparison}
        CompareSecondNumRow,
  {Arithm. row that is caused for assignment value to variable}
        BlockNumRow,
  {Logical row that is caused at first level}
        FirstLevelBoolRow,
  {Logical row that is caused As a last param. of funct.}
        LastParameterBoolRow,
  {Logical row that is caused As a non last param. of funct.}
        NonLastParameterBoolRow,
  {Logical row that is caused for assignment value to variable}
        BlockBoolRow,
  {String row that is caused at first level}
        FirstLevelStringRow,
  {String row that is caused As a last param. of funct.}
        LastParameterStringRow,
  {String row that is caused As a non last param. of funct.}
        NonLastParameterStringRow,
  {String row that is caused As a first value of comparison}
        ComparePrimaryStringRow,
  {String row that is caused As a second value of comparison}
        CompareSecondStringRow,
  {String row that is caused for assignment value to variable}
        BlockStringRow);

  TRowLastExpection=array[TRow] of TExpection;
                              {Types of expectations of closing a row}

  TRowExpection=array[TRow] of TExpection;
                     {Types of expectations of binding values in row}

const
  RowLastExpection:
    TRowLastExpection =(exNone,
                        exRightParenthesis,
                        exComma,
                        exCompareSign,
                        exIndef,
                        exEnd,
                        exNone,
                        exRightParenthesis,
                        exComma,
                        exEnd,
                        exNone,
                        exRightParenthesis,
                        exComma,
                        exCompareSign,
                        exIndef,
                        exEnd);

  RowExpection:TRowExpection=(exArithSign,
                              exArithSign,
                              exArithSign,
                              exArithSign,
                              exArithSign,
                              exArithSign,
                              exLogSign,
                              exLogSign,
                              exLogSign,
                              exLogSign,
                              exInDef,
                              exInDef,
                              exInDef,
                              exInDef,
                              exInDef,
                              exInDef);

type
  {Structure of table of dynamic accommodation of symbols in memory}
  PSymbolGroup=^TSymbolGroup;
  TSymbolElement=record
    Active:boolean;
    Symbol:TSymbol;
    NextGroup:PSymbolGroup;
  end;
  TSymbolGroup=array [char] of TSymbolElement;

  TExprType=(expBoolean,expExtended,expString);
  TPriorityVar=(pvCode,pvAdCalc);

  {Structure of table of dynamic accommodation of declared variables}
  PVariable=^TVariable;
  TVariable=record
    VarName,Descript,StrValue:string;
    VarType:TExprType;
    VarValue,
    ConnectedVar:pointer;
    NextVar:PVariable;
  end;

  {Structure of table of dynamic accommodation of declared functions}
  PFunctParam=^TFunctParam;
  TFunctParam=record
    ParamType:TExprType;
    ParamValue:pointer;
    StrValue:string;
    NextParam:PFunctParam;
  end;
  PFunct=^TFunct;

  TErrorEvent=procedure(Sender:TObject; ErrorLine,
    ErrorPosition,ErrorPlace,ErrorCode:integer;
    ErrorStr:string;Handle:integer) of Object;
  TFunctErrorEvent=procedure(Sender:TObject;FunctName:string;
    ErrorLine,ErrorPosition,ErrorPlace,ErrorCode:integer;
    ErrorStr:string) of Object;
  TGetExtendedVarEvent=procedure(Sender:TObject;
    VarName:string; var Found:boolean; var Value:extended) of Object;
  TGetBooleanVarEvent=procedure(Sender:TObject;
    VarName:string; var Found:boolean; var Value:boolean) of Object;
  TGetStringVarEvent=procedure(Sender:TObject;
    VarName:string; var Found:boolean; var Value:string) of Object;
  TGetExtendedFunctEvent=procedure(Sender:TObject;
    FunctName:string; var Found:boolean; var Value:extended) of Object;
  TGetBooleanFunctEvent=procedure(Sender:TObject;
    FunctName:string; var Found:boolean; var Value:boolean) of Object;
  TGetStringFunctEvent=procedure(Sender:TObject;
    FunctName:string; var Found:boolean; var Value:string) of Object;
  TVarListEvent=procedure(Sender:TObject;
    VarName:string;VarType:TExprType;
    Value:pointer;Description:string) of Object;
  TFunctListEvent=procedure(Sender:TObject;
    FunctName:string;FunctType:TExprType;Formula:TStrings;
    Params:array of TExprType;Description:string) of Object;

  PParserList=^TParserList;

  TAdCalc = class(TComponent)
  private
    { Private declarations }
    FOnError            : TErrorEvent;
    FOnFunctError       : TFunctErrorEvent;
    FOnGetExtendedVar   : TGetExtendedVarEvent;
    FOnGetBooleanVar    : TGetBooleanVarEvent;
    FOnGetStringVar     : TGetStringVarEvent;
    FOnGetExtendedFunct : TGetExtendedFunctEvent;
    FOnGetBooleanFunct  : TGetBooleanFunctEvent;
    FOnGetStringFunct   : TGetStringFunctEvent;
    FOnVarList          : TVarListEvent;
    FOnFunctList        : TFunctListEvent;
  protected
    { Protected declarations }
    FirstGroup:PSymbolGroup;
    FirstVariable:PVariable;
    FirstFunct:PFunct;
    ParamsBridge:PFunctParam;
    WasError:boolean;
    FirstParser,FreeList:PParserList;
    procedure ResetSymbolGroup(SymbolGroup:PSymbolGroup);
    procedure RegSymbol(Str:string;Sym:TSymbol);
    procedure RegSymbolTable;
    procedure RemoveSymbol(SymStr:string);
    procedure ClearSymbolGroup(var Group:PSymbolGroup);
    procedure ClearSymbolTable;
    function GetVar(VName:string):PVariable;
    procedure DisposeVar(v:PVariable);
    procedure RegFunct(Def:integer;FName:string;T:TExprType;
      Par:array of TExprType;FL:TStrings;Description:string);
    procedure ClearAllFunctions;
    procedure DisposeFunct(Funct:PFunct);
    procedure AutoDefExtendedFunct(FName:string;Params:array of TExprType);
    procedure AutoDefBooleanFunct(FName:string;Params:array of TExprType);
    procedure AutoDefStringFunct(FName:string;Params:array of TExprType);
    function GetFunct(FName:string):PFunct;
    procedure RegAutoFunctions;
    function CheckName(IdName:string):boolean;
    function GetParser(Handle:integer):PParserList;
  public
    { Public declarations }
    FunctParams:array of TExprType;
      {FunctParams: Used only after calling of function GetFunctProperty}
    constructor Create(AOwner:TComponent); Override;
    Destructor Destroy; Override;
    procedure RegExtendedFunct(FName:string;
      Params:array of TExprType;Formula:TStrings;Description:string);
    procedure RegBooleanFunct(FName:string;
      Params:array of TExprType;Formula:TStrings;Description:string);
    procedure RegStringFunct(FName:string;
      Params:array of TExprType;Formula:TStrings;Description:string);
    function GetExtendedResult(Formula:TStrings;
      var Value:extended;Handle:integer):boolean;
    function GetBooleanResult (Formula:TStrings;
      var Value:boolean;Handle:integer):boolean;
    function GetStringResult(Formula:TStrings;
      var Value:string;Handle:integer):boolean;
    function GetBlockResult(Formula:TStrings;Handle:integer):boolean;
    function ExecuteExtended(var Value:extended;Handle:integer):boolean;
    function ExecuteBoolean(var Value:boolean;Handle:integer):boolean;
    function ExecuteString(var Value:string;Handle:integer):boolean;
    function ExecuteBlock(Handle:integer):boolean;
    procedure DefExtendedFunct(FName:string;Params:array of TExprType);
    procedure DefBooleanFunct(FName:string;Params:array of TExprType);
    procedure DefStringFunct(FName:string;Params:array of TExprType);
    procedure ClearVariables;
    procedure ClearFunctions;
    function GetFunctProperties(FName:string;var FType:TExprType;
      var Formula:TStrings;var Description:string):boolean;
    function SetFunctDescription(FName:string;Description:string):boolean;
    function GetFunctDescription(FName:string;var Description:string):boolean;
    procedure RemoveFunct(FName:string);
    function GetParam(n:integer):pointer;
    procedure RegVariable(VName:string;VType:TExprType;Description:string);
    function ConnectExtendedVar(VName:string;var Variable:extended;
                                PriorityVar:TPriorityVar):boolean;
    function ConnectBooleanVar(VName:string;var Variable:boolean;
                               PriorityVar:TPriorityVar):boolean;
    function ConnectStringVar(VName:string;var Variable:string;
                              PriorityVar:TPriorityVar):boolean;
    procedure DisconnectVar(VName:string;PriorityVar:TPriorityVar);
    function SetExtendedVarValue(VName:string;Value:Extended):boolean;
    function SetBooleanVarValue(VName:string;Value:boolean):boolean;
    function SetStringVarValue(VName,Value:string):boolean;
    function GetExtendedVarValue(VName:string;var Value:Extended):boolean;
    function GetBooleanVarValue(VName:string;var Value:boolean):boolean;
    function GetStringVarValue(VName:string;var Value:string):boolean;
    function GetVarProperties(VName:string;var VType:TExprType;
      var Value:pointer;var Description:string):boolean;
    function SetVarDescription(VName:string;Description:string):boolean;
    function GetVarDescription(VName:string;var Description:string):boolean;
    function RemoveVar(VName:string):Boolean;
    procedure GetVarList;
    procedure GetFunctList;
    procedure SaveLibrary(FileName:string;Version:integer);
    procedure LoadLibrary(FileName:string);
    procedure CompileText(Lines:TStrings;Handle:integer);
    procedure RemoveText(Handle:integer);
    procedure ClearExpressions;
  published
    { Published declarations }
    property OnError:TErrorEvent
      read FOnError write FOnError;
    property OnFunctError:TFunctErrorEvent
      read FOnFunctError write FOnFunctError;
    property OnGetExtendedVar:TGetExtendedVarEvent
      read FOnGetExtendedVar write FOnGetExtendedVar;
    property OnGetBooleanVar:TGetBooleanVarEvent
      read FOnGetBooleanVar write FOnGetBooleanVar;
    property OnGetStringVar:TGetStringVarEvent
      read FOnGetStringVar write FOnGetStringVar;
    property OnGetExtendedFunct:TGetExtendedFunctEvent
      read FOnGetExtendedFunct write FOnGetExtendedFunct;
    property OnGetBooleanFunct:TGetBooleanFunctEvent
      read FOnGetBooleanFunct write FOnGetBooleanFunct;
    property OnGetStringFunct:TGetStringFunctEvent
      read FOnGetStringFunct write FOnGetStringFunct;
    property OnVarList:TVarListEvent
      read FOnVarList write FOnVarList;
    property OnFunctList:TFunctListEvent
      read FOnFunctList write FOnFunctList;
  end;

  TPosition=record
    pFLine,pNextStr,pErrorStr:string;
    pCurLine,pCurChar,pGlobChar,pCurCS:integer;
    pNextSym:TSymbol;
    pNextEr,pErrorCode:integer;
    pFixedError:boolean;
  end;

  TCompiledSymbols=array of TPosition;
  TCompile=(cYes,cNo);

  TParser = class
  protected
    Handle:integer;
    AdCalc:TAdCalc;
    PrevPos,ParamPos:TPosition;
    FLines:TStrings;
    FLine,NextStr,ErrorStr:string;
    CurLine,CurChar,GlobChar,CSCount,CurCS:integer;
    NextSym:TSymbol;
    NextEr,ErrorCode:integer;
    CompiledSymbols:TCompiledSymbols;
    FixedError:boolean;
    procedure GetStringConst;
    procedure NextSymbol;
    procedure InitNewFormula;
    procedure SavePosition(var Pos:TPosition);
    procedure RestorePosition(Pos:TPosition);
    procedure ExecError(Code:integer);
    procedure ClearCompiledSymbols;
    procedure CompileLines;
    function GetNextLine:string;
    function GetSymbol(var VarStr:string; var ErCode:integer):Tsymbol;
    function GetExtendedFunctValue(FName:string;var Value:extended):boolean;
    function GetBooleanFunctValue (FName:string;var Value:boolean):boolean;
    function GetStringFunctValue (FName:string;var Value:string):boolean;
    function CheckParam(n:integer;Value:pointer;Expr:TExprType):boolean;
    function GetFunctionResult(Sym:TSymbol;FName:string;Value:Pointer):boolean;
    function GetNumValue(s:string;var EV:extended):boolean;
    function GetBoolValue(s:string;var BV:boolean):boolean;
    function GetStringValue(s:string;var SV:string):boolean;
    function GetLevel(Sym:TSymbol):integer;
    function GetBindingExpection(Exp:TExpection;var Sym:TSymbol):boolean;
    function GetBindingSymbol(Sym:TSymbol):boolean;
    function GetRowSymbol(var Sym:TSymbol;Row:TRow;var Last:boolean):boolean;
    function GetFunctName(var FName:string;Sym:TSymbol):boolean;
    function GetExtendedExpection(var n:extended):boolean;
    function GetBooleanExpection(var b:boolean):boolean;
    function GetStringExpection(var s:string):boolean;
    function GetRowErrorCode(Row:TRow):integer;
    function GetNumRowValue(Row:TRow;var Value:Extended;
                            Level:word;Moment:word):boolean;
    function GetBoolRowValue(Row:TRow;var Value:boolean):boolean;
    function GetStringRowValue(Row:TRow;var Value:string):boolean;
    function GetFunctionParams(PC:integer;FP:PFunctParam):boolean;
    function GetCompareResult(var Value:boolean):boolean;
    function ExecBlock:boolean;
    function CalcExtended(var Value:extended):boolean;
    function Calcboolean (var Value:boolean) :boolean;
    function CalcString(var Value:string):boolean;
    function CalcExtendedFunct(FName:string;var Value:extended):boolean;
    function CalcbooleanFunct (FName:string;var Value:boolean) :boolean;
    function CalcStringFunct(FName:string;var Value:string):boolean;
    function CalcBlock:boolean;
  public
    constructor Create(Calc:TAdCalc;FL:TStrings;Compile:TCompile;H:integer);
    destructor Destroy; override;
  end;

  TParserList=record
    Parser:TParser;
    Next:PParserList;
  end;

  TFunct=record
    FunctName:string;
    Descript:string;
    FunctType:TExprType;
    FunctLines:TStrings;
    ParCount:integer;
    Params:PFunctParam;
    Definer:integer;
      {Definer: 1-default,2-determined at event,3-determined at FunctLines}
    Parser:TParser;
    NextFunct:PFunct;
  end;

implementation

uses AdCMsg;

{$IFDEF DEMO}
function DelphiRunning : boolean;
begin
 if((FindWindow('TApplication','Delphi') = 0) and
   (FindWindow('TApplication','Delphi 2.0') = 0) and
   (FindWindow('TApplication','Delphi 3') = 0) and
   (FindWindow('TApplication','Delphi 4') = 0) and
   (FindWindow('TApplication','Delphi 5') = 0)) or
   (FindWindow('TPropertyInspector',nil) = 0) or
   (FindWindow('TAppBuilder',nil) = 0) then result:= false
 else result:= true;
end;
{$ENDIF}

function AutoDivider(c:char):boolean;
begin
  Result:=(ord(c)<14)or(ord(c)=32);
end;

function IsDividerSymbol(Sym:TSymbol):boolean;
begin
  Result:=false;
  case SymBolTable[Sym].Expection of
    exComma:Result:=true;
    exLeftParenthesis:Result:=true;
    exRightParenthesis:Result:=true;
    exArithSign:Result:=true;
    exCompareSign:Result:=true;
    exEnd:Result:=true;
    exComment:Result:=true;
    exStrSign:Result:=true;
  end;
end;

function IsDivider(c:char):boolean;
var
  Sym:TSymbol;
  s:string;
begin
  if AutoDivider(c) then IsDivider:=true
  else begin
    Result:=false;
    for Sym:=low(TSymbol) to high(TSymbol) do begin
      s:=SymBolTable[Sym].SymbolStr;
      if s<>'' then if (s[1]=c) and IsDividerSymbol(Sym) then begin
        Result:=true;
        Exit;
      end;
    end;
  end;
end;

function IsNumeral(s:string):boolean;
var
  i:integer;
  n:extended;
begin
  Val(s,n,i);
  if n=0 then begin end;
  Result:=i=0;
end;

procedure Pac(var s:string);
var
  s1:string;
  i:integer;
begin
  s1:=s;
  s:='';
  for i:=1 to Length(s1) do
    if not AutoDivider(s1[i]) then s:=s+s1[i];
  s:=AnsiLowerCase(s);
end;

function GetTempFileName:string;
var
  i:integer;
begin
  i:=0;
  Result:='adc0.tmp';
  while FileExists(Result) do begin
    inc(i);
    Result:='adc'+IntToStr(i)+'.tmp';
  end;
end;

constructor TAdCalc.Create(AOwner:TComponent);
begin
{$IFDEF DEMO}
  if not DelphiRunning then begin
    ShowMessage(
    'The Demo version of "Advanced Calculator" works while IDE is running');
    Halt(1);
  end;
{$ENDIF}
  inherited Create(AOwner);
  FirstParser:=nil;
  New(FreeList);
  with FreeList^ do begin
    Parser:=nil;
    Next:=nil;
  end;
  FirstGroup:=nil;
  RegSymbolTable;
  FirstFunct:=nil;
  FirstVariable:=nil;
  RegAutoFunctions;
end;

Destructor TAdCalc.Destroy;
begin
  ClearVariables;
  ClearAllFunctions;
  ClearSymbolTable;
  ClearExpressions;
  inherited destroy;
end;

procedure TAdCalc.ResetSymbolGroup(SymbolGroup:PSymbolGroup);
var
  c:char;
begin
  for c:=Low(char) to High(char) do with SymbolGroup^[c] do begin
    Active:=false;
    Symbol:=sNone;
    NextGroup:=nil;
  end;
end;

procedure TAdCalc.RegSymbol(Str:string;Sym:TSymbol);
var
  i:integer;
  c:char;
  CurGroup:PSymbolGroup;
begin
  Str:=AnsiLowerCase(Str);
  CurGroup:=FirstGroup;
  if (CurGroup=nil)and(Length(Str)>0) then begin
    New(CurGroup);
    FirstGroup:=CurGroup;
    ResetSymbolGroup(CurGroup);
  end;
  for i:=1 to Length(Str) do begin
    c:=Str[i];
    CurGroup^[c].Active:=true;
    if i=Length(Str) then CurGroup^[c].Symbol:=Sym
    else begin
      if CurGroup^[c].NextGroup=nil then begin
        New(CurGroup^[c].NextGroup);
        ResetSymbolGroup(CurGroup^[c].NextGroup);
      end;
      CurGroup:=CurGroup^[c].NextGroup;
    end;
  end;
end;

procedure TAdCalc.RegSymbolTable;
{Registration of table of symbols}
var
  Sym:TSymbol;
begin
  for Sym:=Low(TSymbol) to High(TSymbol) do
    if SymbolTable[Sym].SymbolStr<>'' then
      RegSymbol(SymbolTable[Sym].SymbolStr,Sym);
end;

procedure TAdCalc.RemoveSymbol(SymStr:string);
var
  CurGroup,GroupSaver:PSymbolGroup;
  i:integer;
  CharFound,AllPassive:boolean;
  CharSaver,c:char;
begin
  if SymStr<>'' then begin
    CharSaver:=#0;
    GroupSaver:=nil;
    i:=1;
    CharFound:=true;
    CurGroup:=FirstGroup;
    while CharFound do begin
      CharFound:=CurGroup^[SymStr[i]].Active;
      if i>=Length(SymStr) then begin
        if CharFound then begin
          CurGroup^[SymStr[i]].Symbol:=sNone;
          CharFound:=false;
          AllPassive:=true;
          for c:=Low(char) to High(Char) do
            if (CurGroup^[c].Symbol<>sNone)or
               (CurGroup^[c].NextGroup<>nil) then AllPassive:=false;
          if AllPassive then if CurGroup=FirstGroup then begin
            DisPose(FirstGroup);
            FirstGroup:=nil;
          end
          else begin
            Dispose(GroupSaver^[CharSaver].NextGroup);
            GroupSaver^[CharSaver].NextGroup:=nil;
          end;
        end;
      end
      else begin
        GroupSaver:=CurGroup;
        CharSaver:=SymStr[i];
        CurGroup:=CurGroup^[SymStr[i]].NextGroup;
        if CurGroup=nil then CharFound:=false;
        inc(i);
      end;
    end;
  end;
end;

procedure TAdCalc.ClearSymbolGroup(var Group:PSymbolGroup);
var
  c:char;
begin
  if Group<>nil then begin
    for c:=Low(Char) to High(Char) do if Group^[c].NextGroup<>nil then
      ClearSymbolGroup(Group^[c].NextGroup);
    Dispose(Group);
    Group:=nil;
  end;
end;

procedure TAdCalc.ClearSymbolTable;
begin
  ClearSymbolGroup(FirstGroup);
end;

procedure TAdCalc.RegVariable(VName:string;VType:TExprType;
                              Description:string);
{Registration of variable}
var
  CurVar:PVariable;
  Ext:boolean;
begin
  if CheckName(VName) then begin
    Pac(VName);
    Ext:=false;
    CurVar:=FirstVariable;
    if CurVar=nil then begin
      New(CurVar);
      FirstVariable:=CurVar;
    end
    else begin
      repeat
        with CurVar^ do begin
          if VName=VarName then Ext:=true
          else if NextVar<>nil then begin
            CurVar:=NextVar;
            if VName=CurVar^.VarName then Ext:=true
          end;
        end;
      until (CurVar^.NextVar=nil) or Ext;
      if not Ext then begin
        New(CurVar^.NextVar);
        CurVar:=CurVar^.NextVar;
      end;
    end;
    with CurVar^ do begin
      if Ext then begin
        if VarValue<>nil then case VarType of
          expExtended:FreeMem(VarValue,SizeOf(extended));
          expBoolean :FreeMem(VarValue,SizeOf(boolean));
        end;
      end
      else NextVar:=nil;
      VarName:=VName;
      Descript:=Description;
      VarType:=VType;
      ConnectedVar:=nil;
      case VarType of
        expExtended:begin
          GetMem(VarValue,SizeOf(extended));
          extended(VarValue^):=0;
        end;
        expBoolean :begin
          GetMem(VarValue,SizeOf(boolean));
          boolean(VarValue^):=false;
        end;
        expString:StrValue:='';
      end;
    end;
  end;
end;

function TAdCalc.GetVar(VName:string):PVariable;
var
  CurVar:PVariable;
begin
  Result:=nil;
  Pac(VName);
  CurVar:=FirstVariable;
  while CurVar<>nil do with CurVar^ do begin
    if VName=VarName then begin
      Result:=CurVar;
      Exit;
    end;
    CurVar:=CurVar^.NextVar;
  end;
end;

function TAdCalc.ConnectExtendedVar(VName:string;
  var Variable:extended;PriorityVar:TPriorityVar):boolean;
var
  CurVar:PVariable;
begin
  Result:=false;
  CurVar:=GetVar(VName);
  if CurVar<>nil then begin
    CurVar^.ConnectedVar:=@Variable;
    if PriorityVar=pvAdCalc then Variable:=extended(CurVar^.VarValue^);
    Result:=true;
  end;
end;

function TAdCalc.ConnectBooleanVar(VName:string;
  var Variable:boolean;PriorityVar:TPriorityVar):boolean;
var
  CurVar:PVariable;
begin
  Result:=false;
  CurVar:=GetVar(VName);
  if CurVar<>nil then begin
    CurVar^.ConnectedVar:=@Variable;
    if PriorityVar=pvAdCalc then Variable:=boolean(CurVar^.VarValue^);
    Result:=true;
  end;
end;

function TAdCalc.ConnectStringVar(VName:string;
  var Variable:string;PriorityVar:TPriorityVar):boolean;
var
  CurVar:PVariable;
begin
  Result:=false;
  CurVar:=GetVar(VName);
  if CurVar<>nil then begin
    CurVar^.ConnectedVar:=@Variable;
    if PriorityVar=pvAdCalc then Variable:=CurVar^.StrValue;
    Result:=true;
  end;
end;

procedure TAdCalc.DisconnectVar(VName:string;PriorityVar:TPriorityVar);
var
  CurVar:PVariable;
begin
  CurVar:=GetVar(VName);
  if CurVar<>nil then with CurVar^ do if ConnectedVar<>nil then begin
    if PriorityVar=pvCode then case VarType of
      ExpExtended:extended(VarValue^):=extended(ConnectedVar^);
      ExpBoolean:boolean(VarValue^):=boolean(ConnectedVar^);
      ExpString:StrValue:=string(ConnectedVar^);
    end;                                   
    CurVar^.ConnectedVar:=nil;
  end;
end;

procedure TAdCalc.ClearVariables;
var
  CurVar,Bridge:PVariable;
begin
  CurVar:=FirstVariable;
  while CurVar<>nil do begin
    Bridge:=CurVar;
    CurVar:=CurVar^.NextVar;
    DisposeVar(Bridge);
  end;
  FirstVariable:=nil;
end;

procedure TAdCalc.DisposeVar(v:PVariable);
begin
  if v<>nil then with v^ do if VarValue<>nil then case VarType of
    expExtended:FreeMem(VarValue,SizeOf(extended));
    expBoolean :FreeMem(VarValue,SizeOf(boolean));
  end;
  Dispose(v);
end;

function TAdCalc.SetExtendedVarValue(VName:string;Value:Extended):boolean;
var
  CurVar:PVariable;
begin
  Result:=false;
  CurVar:=GetVar(VName);
  if CurVar<>nil then with CurVar^ do if VarType=expExtended then begin
    if VarValue=nil then GetMem(VarValue,SizeOf(extended));
    if ConnectedVar=nil then extended(VarValue^):=Value
    else extended(ConnectedVar^):=Value;
    Result:=true;
  end;
end;

function TAdCalc.SetBooleanVarValue(VName:string;Value:boolean):boolean;
var
  CurVar:PVariable;
begin
  Result:=false;
  CurVar:=GetVar(VName);
  if CurVar<>nil then with CurVar^ do if VarType=expBoolean then begin
    if VarValue=nil then GetMem(VarValue,SizeOf(boolean));
    if ConnectedVar=nil then boolean(VarValue^):=Value
    else boolean(ConnectedVar^):=Value;
    Result:=true;
  end;
end;

function TAdCalc.SetStringVarValue(VName,Value:string):boolean;
var
  CurVar:PVariable;
begin
  Result:=false;
  CurVar:=GetVar(VName);
  if CurVar<>nil then with CurVar^ do if VarType=expString then begin
    if ConnectedVar=nil then StrValue:=Value
    else string(ConnectedVar^):=Value;
    Result:=true;
  end;
end;

function TAdCalc.SetVarDescription(VName:string;Description:string):boolean;
var
  CurVar:PVariable;
begin
  Result:=false;
  CurVar:=GetVar(VName);
  if CurVar<>nil then with CurVar^ do begin
    Descript:=Description;
    Result:=true;
  end;
end;

function TAdCalc.GetExtendedVarValue(VName:string;var Value:Extended):boolean;
var
  CurVar:PVariable;
begin
  Result:=false;
  Value:=0;
  CurVar:=GetVar(VName);
  if CurVar<>nil then with CurVar^ do if VarType=expExtended then begin
    if ConnectedVar=nil then Value:=extended(VarValue^)
    else Value:=extended(ConnectedVar^);
    Result:=true;
  end;
end;

function TAdCalc.GetBooleanVarValue(VName:string;var Value:boolean):boolean;
var
  CurVar:PVariable;
begin
  Result:=false;
  Value:=false;
  CurVar:=GetVar(VName);
  if CurVar<>nil then with CurVar^ do if VarType=expBoolean then begin
    if ConnectedVar=nil then Value:=boolean(VarValue^)
    else Value:=boolean(ConnectedVar^);
    Result:=true;
  end;
end;

function TAdCalc.GetStringVarValue(VName:string;var Value:string):boolean;
var
  CurVar:PVariable;
begin
  Result:=false;
  Value:='';
  CurVar:=GetVar(VName);
  if CurVar<>nil then with CurVar^ do if VarType=expString then begin
    if ConnectedVar=nil then Value:=StrValue
    else Value:=string(ConnectedVar^);
    Result:=true;
  end;
end;

function TAdCalc.GetVarDescription(VName:string;var Description:string):boolean;
var
  CurVar:PVariable;
begin
  Result:=false;
  Description:='';
  CurVar:=GetVar(VName);
  if CurVar<>nil then with CurVar^ do begin
    Description:=Descript;
    Result:=true;
  end;
end;

function TAdCalc.GetVarProperties(VName:string;var VType:TExprType;
  var Value:pointer;var Description:string):boolean;
var
  CurVar:PVariable;
begin
  Result:=false;
  Value:=nil;
  CurVar:=GetVar(VName);
  if CurVar<>nil then with CurVar^ do begin
    Description:=Descript;
    VType:=VarType;
    if ConnectedVar=nil then begin
      if VarType=expString then Value:=@StrValue
      else Value:=VarValue;
    end
    else Value:=ConnectedVar;
    Result:=true;
  end;
end;

function TAdCalc.RemoveVar(VName:string):Boolean;
var
  CurVar,Bridge:PVariable;
begin
  Pac(VName);
  Result:=false;
  CurVar:=FirstVariable;
  if CurVar^.VarName=VName then begin
    FirstVariable:=CurVar^.NextVar;
    DisposeVar(CurVar);
    Result:=true;
  end
  else while CurVar^.NextVar<>nil do begin
    if VName=CurVar^.NextVar^.VarName then begin
      Bridge:=CurVar^.NextVar;
      CurVar^.NextVar:=Bridge^.NextVar;
      DisposeVar(Bridge);
      Result:=true;
      Exit;
    end;
    CurVar:=CurVar^.NextVar;
  end;
end;

procedure TAdCalc.GetVarList;
var
  CurVar:PVariable;
  Value:pointer;
begin
  CurVar:=FirstVariable;
  while CurVar<>nil do with CurVar^ do begin
    if ConnectedVar=nil then begin
      if VarType=expString then Value:=@StrValue
      else Value:=VarValue;
    end
    else Value:=ConnectedVar;
    if Assigned(FOnVarList) then FOnVarList(Self,
      VarName,VarType,Value,Descript);
    CurVar:=CurVar^.NextVar;
  end;
end;

procedure TAdCalc.RegFunct(Def:integer;FName:string;T:TExprType;
  Par:array of TExprType;FL:TStrings;Description:string);
{Registration of function}
var
  i:integer;
  CurFunct:PFunct;
  CurParam,MidParam:PFunctParam;
  Exists:boolean;
begin
  Pac(FName);
  Exists:=false;
  if FirstFunct=nil then begin
    New(FirstFunct);
    CurFunct:=FirstFunct;
    CurFunct^.FunctLines:=TStringList.Create;
  end
  else begin
    CurFunct:=FirstFunct;
    repeat
      if FName=CurFunct^.FunctName then Exists:=true
      else if CurFunct^.NextFunct<>nil then begin
        CurFunct:=CurFunct^.NextFunct;
        if FName=CurFunct^.FunctName then Exists:=true
      end;
    until (CurFunct^.NextFunct=nil) or Exists;
    if not Exists then begin
      New(CurFunct^.NextFunct);
      CurFunct:=CurFunct^.NextFunct;
      CurFunct^.FunctLines:=TStringList.Create;
    end;
  end;
  with CurFunct^ do begin
    FunctName:=FName;
    Descript:=Description;
    FunctType:=T;
    FunctLines.Clear;
    if FL<>nil then for i:=0 to FL.Count-1 do FunctLines.Add(FL[i]);
    ParCount:=High(Par)+1;
    Definer:=Def;
    if Exists then begin
      CurParam:=Params;
      while CurParam<>nil do begin
        MidParam:=CurParam;
        CurParam:=CurParam^.NextParam;
        Dispose(MidParam);
      end;
      if Parser<>nil then Parser.Destroy;
    end
    else NextFunct:=nil;
    if ParCount>0 then begin
      New(Params);
      CurParam:=Params;
      for i:=0 to ParCount-1 do begin
        if i>0 then begin
          New(CurParam^.NextParam);
          CurParam:=CurParam^.NextParam;
        end;
        with CurParam^ do begin
          ParamType:=Par[i];
          ParamValue:=nil;
          StrValue:='';
          NextParam:=nil;
        end;
      end;
    end
    else Params:=nil;
    if Def=2 then Parser:=TParser.Create(Self,FunctLines,cYes,0)
    else Parser:=nil;
  end;
end;

procedure TAdCalc.ClearFunctions;
var
  CurFunct,Bridge:PFunct;
begin
  CurFunct:=FirstFunct;
  if CurFunct<>nil then begin
    while CurFunct^.NextFunct<>nil do begin
      if CurFunct^.NextFunct^.Definer=2 then begin
        Bridge:=CurFunct^.NextFunct;
        CurFunct^.NextFunct:=CurFunct^.NextFunct^.NextFunct;
        RemoveSymbol(Bridge^.FunctName);
        DisposeFunct(Bridge);
      end
      else CurFunct:=CurFunct^.NextFunct;
    end;
    if FirstFunct^.Definer=2 then begin
      Bridge:=FirstFunct;
      FirstFunct:=FirstFunct^.NextFunct;
      RemoveSymbol(Bridge^.FunctName);
      DisposeFunct(Bridge);
    end;
  end;
end;

procedure TAdCalc.ClearAllFunctions;
var
  CurFunct,Bridge:PFunct;
begin
  CurFunct:=FirstFunct;
  while CurFunct<>nil do begin
    Bridge:=CurFunct;
    CurFunct:=CurFunct^.NextFunct;
    RemoveSymbol(Bridge^.FunctName);
    DisposeFunct(Bridge);
  end;
  FirstFunct:=nil;
end;

procedure TAdCalc.DisposeFunct(Funct:PFunct);
var
  Par,Bridge:PFunctParam;
begin
  with Funct^ do begin
    FunctLines.Destroy;
    Par:=Params;
    while Par<>nil do begin
      Bridge:=Par;
      Par:=Par^.NextParam;
      Dispose(Bridge);
    end;
    if Parser<>nil then Parser.Destroy;
  end;
  Dispose(Funct);
end;

procedure TAdCalc.RemoveFunct(FName:string);
var
  CurFunct,Bridge:PFunct;
begin
  Pac(FName);
  RemoveSymbol(FName);
  CurFunct:=FirstFunct;
  if CurFunct^.FunctName=FName then begin
    Bridge:=FirstFunct;
    FirstFunct:=FirstFunct^.NextFunct;
    DisPoseFunct(Bridge);
  end
  else while CurFunct^.NextFunct<>nil do begin
    if CurFunct^.NextFunct^.FunctName=FName then begin
      Bridge:=CurFunct^.NextFunct;
      CurFunct^.NextFunct:=Bridge^.NextFunct;
      DisPoseFunct(Bridge);
      Exit;
    end
    else CurFunct:=CurFunct^.NextFunct;
  end;
end;

function TAdCalc.CheckName(IdName:string):boolean;
var
  i:integer;
  FN:boolean;
begin
  Result:=false;
  Pac(IdName);
  if IdName='' then ShowIdNameMissingMessage
  else if (LowerCase(IdName)='param') or
          (LowerCase(IdName)='and') or
          (LowerCase(IdName)='or') or
          (LowerCase(IdName)='true') or
          (LowerCase(IdName)='false') then ShowResWordMessage(IdName)
  else begin
    FN:=IdName[1] in ['0'..'9'];
    for i:=1 to length(IdName) do
      if IsDivider(IdName[i]) or FN then begin
        ShowIncorrectIdNameMessage;
        Exit;
      end;
    Result:=true;
  end;
end;

procedure TAdCalc.RegAutoFunctions;
{Registration of default functions}
begin
  AutoDefExtendedFunct('if',[ExpBoolean,ExpExtended,ExpExtended]);
  AutoDefBooleanFunct ('not',[ExpBoolean]);
  AutoDefExtendedFunct('frac',[ExpExtended]);
  AutoDefExtendedFunct('int',[ExpExtended]);
  AutoDefExtendedFunct('round',[ExpExtended]);
  AutoDefExtendedFunct('exp',[ExpExtended]);
  AutoDefExtendedFunct('log',[ExpExtended,ExpExtended]);
  AutoDefExtendedFunct('ln',[ExpExtended]);
  AutoDefExtendedFunct('sin',[ExpExtended]);
  AutoDefExtendedFunct('cos',[ExpExtended]);
  AutoDefExtendedFunct('arcsin',[ExpExtended]);
  AutoDefExtendedFunct('arccos',[ExpExtended]);
  AutoDefExtendedFunct('sinh',[ExpExtended]);
  AutoDefExtendedFunct('cosh',[ExpExtended]);
  AutoDefExtendedFunct('arcsinh',[ExpExtended]);
  AutoDefExtendedFunct('arccosh',[ExpExtended]);
  AutoDefExtendedFunct('tan',[ExpExtended]);
  AutoDefExtendedFunct('cotan',[ExpExtended]);
  AutoDefExtendedFunct('arctan',[ExpExtended]);
  AutoDefExtendedFunct('tanh',[ExpExtended]);
  AutoDefExtendedFunct('arctanh',[ExpExtended]);
  AutoDefExtendedFunct('sqr',[ExpExtended]);
  AutoDefExtendedFunct('sqrt',[ExpExtended]);
  AutoDefExtendedFunct('abs',[ExpExtended]);
  AutoDefExtendedFunct('length',[ExpString]);
  AutoDefExtendedFunct('pos',[ExpString,ExpString]);
  AutoDefExtendedFunct('strtofloat',[ExpString]);
  AutoDefStringFunct('ansilowercase',[ExpString]);
  AutoDefStringFunct('ansiuppercase',[ExpString]);
  AutoDefStringFunct('lowercase',[ExpString]);
  AutoDefStringFunct('uppercase',[ExpString]);
  AutoDefStringFunct('copy',[ExpString,ExpExtended,ExpExtended]);
  AutoDefStringFunct('floattostr',[ExpExtended]);
end;

procedure TAdCalc.RegExtendedFunct(FName:string;
  Params:array of TExprType;Formula:TStrings;Description:string);
begin
  if CheckName(FName) then begin
    RegSymbol(FName,sExtendedFunct);
    RegFunct(2,FName,ExpExtended,Params,Formula,Description);
  end;
end;

procedure TAdCalc.RegBooleanFunct(FName:string;
  Params:array of TExprType;Formula:TStrings;Description:string);
begin
  if CheckName(FName) then begin
    RegSymbol(FName,sBooleanFunct);
    RegFunct(2,FName,ExpBoolean,Params,Formula,Description);
  end;
end;

procedure TAdCalc.RegStringFunct(FName:string;
  Params:array of TExprType;Formula:TStrings;Description:string);
begin
  if CheckName(FName) then begin
    RegSymbol(FName,sStringFunct);
    RegFunct(2,FName,ExpString,Params,Formula,Description);
  end;
end;

procedure TAdCalc.DefExtendedFunct(FName:string;
  Params:array of TExprType);
begin
  if CheckName(FName) then begin
    RegSymbol(FName,sExtendedFunct);
    RegFunct(1,FName,ExpExtended,Params,nil,'');
  end;
end;

procedure TAdCalc.DefBooleanFunct(FName:string;
  Params:array of TExprType);
begin
  if CheckName(FName) then begin
    RegSymbol(FName,sBooleanFunct);
    RegFunct(1,FName,ExpBoolean,Params,nil,'');
  end;
end;

procedure TAdCalc.DefStringFunct(FName:string;
  Params:array of TExprType);
begin
  if CheckName(FName) then begin
    RegSymbol(FName,sStringFunct);
    RegFunct(1,FName,ExpString,Params,nil,'');
  end;
end;

procedure TAdCalc.AutoDefExtendedFunct(FName:string; Params:array of TExprType);
begin
  if CheckName(FName) then begin
    RegSymbol(FName,sExtendedFunct);
    RegFunct(0,FName,ExpExtended,Params,nil,'');
  end;
end;

procedure TAdCalc.AutoDefBooleanFunct(FName:string;
  Params:array of TExprType);
begin
  if CheckName(FName) then begin
    RegSymbol(FName,sBooleanFunct);
    RegFunct(0,FName,ExpBoolean,Params,nil,'');
  end;
end;

procedure TAdCalc.AutoDefStringFunct(FName:string;
  Params:array of TExprType);
begin
  if CheckName(FName) then begin
    RegSymbol(FName,sStringFunct);
    RegFunct(0,FName,ExpString,Params,nil,'');
  end;
end;

function TAdCalc.GetExtendedResult(Formula:TStrings;
  var Value:extended;Handle:integer):boolean;
var
  Parser:TParser;
begin
  Parser:=TParser.Create(Self,Formula,cNo,Handle);
  try
    WasError:=false;
    Value:=0;
    Result:=Parser.CalcExtended(Value);
  finally
    Parser.Destroy;
  end;
end;

function TAdCalc.GetBooleanResult(Formula:TStrings;
  var Value:boolean;Handle:integer):boolean;
var
  Parser:TParser;
begin
  Parser:=TParser.Create(Self,Formula,cNo,Handle);
  try
    WasError:=false;
    Value:=false;
    Result:=Parser.CalcBoolean(Value);
  finally
    Parser.Destroy;
  end;
end;

function TAdCalc.GetStringResult(Formula:TStrings;
  var Value:string;Handle:integer):boolean;
var
  Parser:TParser;
begin
  Parser:=TParser.Create(Self,Formula,cNo,Handle);
  try
    WasError:=false;
    Value:='';
    Result:=Parser.CalcString(Value);
  finally
    Parser.Destroy;
  end;
end;

function TAdCalc.GetBlockResult(Formula:TStrings;Handle:integer):boolean;
var
  Parser:TParser;
begin
  Parser:=TParser.Create(Self,Formula,cNo,Handle);
  try
    WasError:=false;
    Result:=Parser.CalcBlock;
  finally
    Parser.Destroy;
  end;
end;

function TAdCalc.ExecuteExtended(var Value:extended;Handle:integer):boolean;
var
  Parser:TParser;
begin
  Parser:=GetParser(Handle)^.Parser;
  if Parser<>nil then begin
    WasError:=false;
    Result:=Parser.CalcExtended(Value);
  end
  else Result:=false;
end;

function TAdCalc.ExecuteBoolean(var Value:boolean;Handle:integer):boolean;
var
  Parser:TParser;
begin
  Parser:=GetParser(Handle)^.Parser;
  if Parser<>nil then begin
    WasError:=false;
    Result:=Parser.CalcBoolean(Value);
  end
  else Result:=false;
end;

function TAdCalc.ExecuteString(var Value:string;Handle:integer):boolean;
var
  Parser:TParser;
begin
  Parser:=GetParser(Handle)^.Parser;
  if Parser<>nil then begin
    WasError:=false;
    Result:=Parser.CalcString(Value);
  end
  else Result:=false;
end;

function TAdCalc.ExecuteBlock(Handle:integer):boolean;
var
  Parser:TParser;
begin
  Parser:=GetParser(Handle)^.Parser;
  if Parser<>nil then begin
    WasError:=false;
    Result:=Parser.CalcBlock;
  end
  else Result:=false;
end;

function TAdCalc.GetFunct(FName:string):PFunct;
var
  CurFunct:PFunct;
begin
  Pac(FName);
  Result:=nil;
  CurFunct:=FirstFunct;
  while (Result=nil) and (CurFunct<>nil) do with CurFunct^ do
    if FunctName=FName then Result:=CurFunct
    else CurFunct:=CurFunct^.NextFunct;
end;

function TAdCalc.GetFunctProperties(FName:string;var FType:TExprType;
  var Formula:TStrings;var Description:string):boolean;
var
  CurFunct:PFunct;
  CurParam:PFunctParam;
  i:integer;
begin
  Pac(FName);
  Result:=false;
  CurFunct:=FirstFunct;
  while not Result and (CurFunct<>nil) do with CurFunct^ do begin
    if Definer=2 then begin
      if FunctName=FName then begin
        Result:=true;
        Description:=Descript;
        FType:=FunctType;
        Formula.Clear;
        for i:=0 to FunctLines.Count-1 do Formula.Add(FunctLines[i]);
        SetLength(FunctParams,ParCount);
        CurParam:=Params;
        i:=0;
        while CurParam<>nil do with CurParam^ do begin
          FunctParams[i]:=ParamType;
          inc(i);
          CurParam:=Curparam^.NextParam;
        end;
      end;
    end;
    CurFunct:=CurFunct^.NextFunct;
  end;
end;

function TAdCalc.SetFunctDescription(FName:string;Description:string):boolean;
var
  CurFunct:PFunct;
begin
  Result:=false;
  CurFunct:=GetFunct(FName);
  if CurFunct<>nil then with CurFunct^ do begin
    Descript:=Description;
    Result:=true;
  end;
end;

function TAdCalc.GetFunctDescription(FName:string;var Description:string):boolean;
var
  CurFunct:PFunct;
begin
  Result:=false;
  Description:='';
  CurFunct:=GetFunct(FName);
  if CurFunct<>nil then with CurFunct^ do begin
    Description:=Descript;
    Result:=true;
  end;
end;

procedure TAdCalc.GetFunctList;
var
  CurFunct:PFunct;
  CurParam:PFunctParam;
  Par:array of TExprType;
  i:integer;
begin
  CurFunct:=FirstFunct;
  while CurFunct<>nil do with CurFunct^ do begin
    if Definer=2 then begin
      CurParam:=Params;
      i:=0;
      SetLength(Par,ParCount);
      while CurParam<>nil do with CurParam^ do begin
        Par[i]:=ParamType;
        inc(i);
        CurParam:=Curparam^.NextParam;
      end;
      if Assigned(FOnFunctList) then FOnFunctList(Self,FunctName,
        FunctType,FunctLines,Par,Descript);
    end;
    CurFunct:=CurFunct^.NextFunct;
  end;
end;

function TAdCalc.GetParam(n:integer):pointer;
var
  CurParam:PFunctParam;
  i:integer;
begin
  Result:=nil;
  i:=1;
  CurParam:=ParamsBridge;
  while (CurParam<>nil)and(i<=n) do begin
    if i=n then with CurParam^ do begin
      case ParamType of
        expString:Result:=@StrValue;
        else Result:=ParamValue;
      end;
    end
    else CurParam:=CurParam^.NextParam;
    inc(i);
  end;
end;

function TParser.CheckParam(n:integer;Value:pointer;
                            Expr:TExprType):boolean;
var
  CurParam:PFunctParam;
  i:integer;
begin
  Result:=false;
  i:=1;
  CurParam:=AdCalc.ParamsBridge;
  while (CurParam<>nil)and(i<=n) do begin
    if i=n then begin
      Result:=Expr=CurParam^.ParamType;
      if Result then case Expr of
        expExtended : extended(Value^):=extended(CurParam.ParamValue^);
        expBoolean  : boolean (Value^):=boolean (CurParam.ParamValue^);
        expString   : string  (Value^):=CurParam.StrValue;
      end
      else begin
        RestorePosition(PrevPos);
        Exit;
      end;
    end
    else CurParam:=CurParam^.NextParam;
    inc(i);
  end;
  if not Result then begin
    RestorePosition(ParamPos);
    ExecError(14);
  end;
end;

procedure TAdCalc.SaveLibrary(FileName:string;Version:integer);
var
  TempFile:string;
  f,f1:file;
  CurVar:PVariable;
  CurFunct:PFunct;
  CurParam:PFunctParam;
  i,i1,LCount,Len,FLibCount,NameSize:integer;
begin
{$i-}
  TempFile:=GetTempFileName;
  System.Assign(f, TempFile);
  System.Assign(f1,FileName);
  if IOResult=0 then begin end;
  Rewrite(f,1);
  if IOResult=0 then begin
    try
      if Version>0 then begin
        i:=-1;
        BlockWrite(f,i,SizeOf(i));
        BlockWrite(f,Version,SizeOf(Version));
      end;
      FLibCount:=0;
      BlockWrite(f,FLibCount,SizeOf(FLibCount));
      CurFunct:=FirstFunct;
      while CurFunct<>nil do with CurFunct^ do begin
        if Definer=2 then begin
          inc(FLibCount);
          NameSize:=Length(FunctName);
          BlockWrite(f,NameSize,SizeOf(NameSize));
          for i:=1 to NameSize do BlockWrite(f,FunctName[i],1);
          if Version>0 then begin
            NameSize:=Length(Descript);
            BlockWrite(f,NameSize,SizeOf(NameSize));
            for i:=1 to NameSize do BlockWrite(f,Descript[i],1);
          end;
          BlockWrite(f,FunctType,SizeOf(FunctType));
          LCount:=FunctLines.Count;
          BlockWrite(f,LCount,SizeOf(LCount));
          for i:=0 to LCount-1 do begin
            Len:=Length(FunctLines[i]);
            BlockWrite(f,Len,SizeOf(Len));
            for i1:=1 to Len do BlockWrite(f,FunctLines[i][i1],1);
          end;
          BlockWrite(f,ParCount,SizeOf(ParCount));
          CurParam:=Params;
          while CurParam<>nil do with CurParam^ do begin
            BlockWrite(f,ParamType,SizeOf(ParamType));
            CurParam:=Curparam^.NextParam;
          end;
        end;
        CurFunct:=CurFunct^.NextFunct;
      end;
      case Version of
        0:Seek(f,0);
        1:Seek(f,2*SizeOf(integer));
      end;
      BlockWrite(f,FLibCount,SizeOf(FLibCount));
      Seek(f,FileSize(f));
      CurVar:=FirstVariable;
      while CurVar<>nil do with CurVar^ do begin
        NameSize:=Length(VarName);
        BlockWrite(f,NameSize,SizeOf(NameSize));
        for i:=1 to NameSize do BlockWrite(f,VarName[i],1);
        if Version>0 then begin
          NameSize:=Length(Descript);
          BlockWrite(f,NameSize,SizeOf(NameSize));
          for i:=1 to NameSize do BlockWrite(f,Descript[i],1);
        end;
        BlockWrite(f,VarType,SizeOf(VarType));
        case VarType of
          expExtended:begin
            if VarValue=nil then begin
              GetMem(VarValue,Sizeof(extended));
              extended(VarValue^):=0;
            end;
            BlockWrite(f,VarValue^,SizeOf(extended));
          end;
          expBoolean :begin
            if VarValue=nil then begin
              GetMem(VarValue,Sizeof(boolean));
              boolean(VarValue^):=false;
            end;
            BlockWrite(f,VarValue^,SizeOf(boolean));
          end;
          expString :begin
            Len:=Length(StrValue);
            BlockWrite(f,Len,SizeOf(Len));
            for i:=1 to Len do BlockWrite(f,StrValue[i],1);
          end;
        end;
        CurVar:=CurVar^.NextVar;
      end;
      System.Close(f);
      Erase(f1);
      if IOResult=0 then begin end;
      Rename(f,FileName);
    Except
      System.Close(f);
      Erase(f);
      if IOResult=0 then begin end;
    end;
  end
  else ShowCanNotCreateFileMessage(FileName);
end;

procedure TAdCalc.LoadLibrary(FileName:string);
var
  f:file;
  i,i1,i2,LCount,Len,FLibCount:integer;
  s:string;
  NameSize:integer;
  FunctName,VarName,Description:string;
  FunctType,VarType:TExprType;
  FunctLines:TStrings;
  ParCount:integer;
  ParamTypes:array of TExprType;
  EV:extended;
  BV:boolean;
  SV:string;
  Version:integer;
begin
{$i-}
  System.Assign(f,FileName);
  if IOResult=0 then begin end;
  Reset(f,1);
  if IOResult=0 then begin
    ClearVariables;
    ClearFunctions;
    FunctLines:=TStringList.Create;
    try
      Version:=0;
      BlockRead(f,FLibCount,SizeOf(FLibCount));
      if FLibCount<0 then begin
        BlockRead(f,Version,SizeOf(Version));
        if (Version<=0)or(Version>CurrentSavingVersion) then begin
          ShowErrorInFileMessage;
          Exit;
        end;
        BlockRead(f,FLibCount,SizeOf(FLibCount));
      end;
      for i:=0 to FLibCount-1 do begin
        BlockRead(f,NameSize,SizeOf(NameSize));
        SetLength(FunctName,NameSize);
        for i1:=1 to NameSize do BlockRead(f,FunctName[i1],1);
        if Version>0 then begin
          BlockRead(f,NameSize,SizeOf(NameSize));
          SetLength(Description,NameSize);
          for i1:=1 to NameSize do BlockRead(f,Description[i1],1);
        end
        else Description:='';
        BlockRead(f,FunctType,SizeOf(FunctType));
        BlockRead(f,LCount,SizeOf(LCount));
        FunctLines.Clear;
        for i1:=0 to LCount-1 do begin
          BlockRead(f,Len,SizeOf(Len));
          s:='';
          SetLength(s,Len);
          for i2:=1 to Len do BlockRead(f,s[i2],1);
          FunctLines.Add(s);
        end;
        BlockRead(f,ParCount,SizeOf(ParCount));
        SetLength(ParamTypes,ParCount);
        for i1:=0 to ParCount-1 do
          BlockRead(f,ParamTypes[i1],SizeOf(TExprType));
        case FunctType of
          ExpExtended:RegExtendedFunct(FunctName,ParamTypes,
                                       FunctLines,Description);
          ExpBoolean :RegBooleanFunct (FunctName,ParamTypes,
                                       FunctLines,Description);
          ExpString  :RegStringFunct  (FunctName,ParamTypes,
                                       FunctLines,Description);
        end;
      end;
      while not Eof(f) do begin
        BlockRead(f,NameSize,SizeOf(NameSize));
        SetLength(VarName,NameSize);
        for i:=1 to NameSize do BlockRead(f,VarName[i],1);
        if Version>0 then begin
          BlockRead(f,NameSize,SizeOf(NameSize));
          SetLength(Description,NameSize);
          for i1:=1 to NameSize do BlockRead(f,Description[i1],1);
        end
        else Description:='';
        BlockRead(f,VarType,SizeOf(VarType));
        RegVariable(VarName,VarType,Description);
        case VarType of
          expExtended:begin
            BlockRead(f,EV,SizeOf(EV));
            SetExtendedVarValue(VarName,EV);
          end;
          expBoolean :begin
            BlockRead(f,BV,SizeOf(BV));
            SetBooleanVarValue(VarName,BV);
          end;
          expString :begin
            BlockRead(f,Len,SizeOf(Len));
            SetLength(SV,Len);
            for i:=1 to Len do BlockRead(f,SV[i],1);
            SetStringVarValue(VarName,SV);
          end;
        end;
      end;
    finally
      System.Close(f);
      FunctLines.Destroy;
    end;
  end
  else ShowCanNotOpenFileMessage(FileName);
end;

procedure TAdCalc.CompileText(Lines:TStrings;Handle:integer);
var
  Parser: TParser;
  CurList:PParserList;
begin
  Parser:=TParser.Create(Self,Lines,cYes,Handle);
  if FirstParser=nil then begin
    New(FirstParser);
    FirstParser^.Parser:=Parser;
    FirstParser^.Next:=nil;
  end
  else begin
    CurList:=FirstParser;
    repeat
      if CurList^.Parser.Handle=Parser.Handle then begin
        CurList^.Parser.Destroy;
        CurList^.Parser:=Parser;
        Exit;
      end;
      if CurList^.Next=nil then begin
        New(CurList^.Next);
        CurList^.Next^.Parser:=Parser;
        CurList^.Next^.Next:=nil;
        Exit;
      end
      else CurList:=CurList^.Next;
    until false;
  end;
end;

function TAdCalc.GetParser(Handle:integer):PParserList;
var
  CurList:PParserList;
begin
  CurList:=FirstParser;
  while CurList<>nil do begin
    if Handle=CurList^.Parser.Handle then begin
      Result:=CurList;
      Exit;
    end;
    CurList:=CurList^.Next;
  end;
  Result:=FreeList;
end;

procedure TAdCalc.RemoveText(Handle:integer);
var
  CurList,Bridge:PParserList;
begin
  CurList:=FirstParser;
  Bridge:=CurList;
  while CurList<>nil do begin
    if Handle=CurList^.Parser.Handle then begin
      CurList^.Parser.Destroy;
      if CurList=FirstParser then FirstParser:=CurList^.Next
      else Bridge^.Next:=CurList^.Next;
      Dispose(CurList);
      Exit;
    end;
    Bridge:=CurList;
    CurList:=CurList^.Next;
  end;
end;

procedure TAdCalc.ClearExpressions;
var
  CurList,Bridge:PParserList;
begin
  CurList:=FirstParser;
  FirstParser:=nil;
  while CurList<>nil do begin
    CurList^.Parser.Destroy;
    Bridge:=CurList^.Next;
    Dispose(CurList);
    CurList:=Bridge;
  end;
end;

constructor TPArser.Create(Calc:TAdCalc;FL:TStrings;
  Compile:TCompile;H:integer);
begin
  inherited Create;
  FLines:=FL;
  Handle:=H;
  CSCount:=0;
  CurCS:=0;
  AdCalc:=Calc;
  if Compile=cYes then CompileLines;
end;

destructor TPArser.Destroy;
begin
  inherited Destroy;
end;

procedure TPArser.InitNewFormula;
begin
  NextStr    :='';
  ErrorStr   :='';
  CurLine    :=0;
  CurChar    :=1;
  CurCS      :=0;
  GlobChar   :=0;
  NextSym    :=sNone;
  NextEr     :=0;
  ErrorCode  :=0;
  FixedError :=false;
  FLine:='';
  NextSymbol;
end;

procedure TPArser.SavePosition(var Pos:TPosition);
begin
  with Pos do begin
    pFLine      := FLine;
    pNextStr    := NextStr;
    pErrorStr   := ErrorStr;
    pCurLine    := CurLine;
    pCurChar    := CurChar;
    pGlobChar   := GlobChar;
    pCurCS      := CurCS;
    pNextSym    := NextSym;
    pNextEr     := NextEr;
    pErrorCode  := ErrorCode;
    pFixedError := FixedError;
  end;
end;

procedure TPArser.RestorePosition(Pos:TPosition);
begin
  with Pos do begin
    FLine      := pFLine;
    NextStr    := pNextStr;
    ErrorStr   := pErrorStr;
    CurLine    := pCurLine;
    CurChar    := pCurChar;
    GlobChar   := pGlobChar;
    CurCS      := pCurCS;
    NextSym    := pNextSym;
    NextEr     := pNextEr;
    ErrorCode  := pErrorCode;
    FixedError := pFixedError;
  end;
end;

function TPArser.GetNextLine:string;
var
  i:integer;
  s:string;
begin
  Result:='';
  if CurLine<FLines.Count then begin
    repeat
      if CurLine>0 then inc(GlobChar,Length(FLines[CurLine-1])+2);
      inc(CurLine);
      s:=FLines[CurLine-1];
      i:=1; {ignoring free lines}
      if s<>'' then while AutoDivider(s[i])and(i<=Length(s)) do inc(i);
    until (CurLine>=FLines.Count)or(Length(s)>=i);
    CurChar:=i;
    s:=''; {From end of a line is ignoring unnecessary symbols}
    if CurLine<=FLines.Count then for i:=1 to Length(FLines[CurLine-1]) do
      if not AutoDivider(FLines[CurLine-1][i]) then begin
        Result:=Result+s+FLines[CurLine-1][i];
        s:='';
      end
      else s:=s+FLines[CurLine-1][i];
  end
  else if CurLine=0 then CurLine:=1;
end;

procedure TParser.GetStringConst;
var
  SConst:string;
begin
  SConst:='';
  repeat
    if CurChar>Length(FLine) then begin
      NextEr:=5;
      Exit;
    end;
    if FLine[CurChar]='"' then begin
      NextSym:=sStringConst;
      NextStr:=SConst;
      inc(CurChar);
      Exit
    end;
    SConst:=SConst+FLine[CurChar];
    inc(CurChar);
  until false;
end;

procedure TPArser.NextSymbol;
begin
  SavePosition(PrevPos);
  if CSCount=0 then begin
    NextEr:=0;
    NextSym:=sNone;
    if CurLine<=FLines.Count then repeat
      if (Length(FLine)<CurChar)or(NextSym=sApostrophe) then FLine:=GetNextLine;
      if FLine='' then begin
        NextSym:=sNone;
        NextStr:='';
        Exit;
      end
      else begin
        NextSym:=GetSymbol(NextStr,NextEr);
        if NextSym=sQMark then GetStringConst;
      end;
    until (NextSym<>sApostrophe)or(NextEr<>0);
  end
  else if CurCS<CSCount then begin
    RestorePosition(CompiledSymbols[CurCS]);
    inc(CurCs);
  end
  else begin
    NextSym:=sNone;
    NextStr:='';
  end;
end;

function TPArser.GetSymbol(var VarStr:string; var ErCode:integer):Tsymbol;
var
  CurGroup:PSymbolGroup;
  Sym:TSymbol;
  c:char;
  Ext,DividerExpected:boolean;
  M:word;
begin
  Ext:=false;
  if CurChar<=Length(FLine) then repeat
    if AutoDivider(FLine[CurChar]) then begin
      inc(CurChar);
      if CurChar>Length(FLine) then Ext:=true;
    end
    else Ext:=true;
  until Ext;
  DividerExpected:=true;
  VarStr:='';
  Sym:=sNone;
  if CurChar<=Length(FLine) then begin
    CurGroup:=AdCalc.FirstGroup;
    repeat
      c:=AnsiLowerCase(FLine[CurChar])[1];
      VarStr:=VarStr+AnsiLowerCase(c);
      if Length(VarStr)>=MaxIdentLen then ErCode:=1;
      if CurGroup<>nil then if CurGroup^[c].Active then begin
        Sym:=CurGroup^[c].Symbol;
        DividerExpected:=not IsDividerSymbol(Sym);
        CurGroup:=CurGroup^[c].NextGroup;
      end
      else CurGroup:=nil;
      inc(CurChar);
      if CurChar>Length(FLine) then Ext:=true
      else Ext:=AutoDivider(FLine[CurChar]);
      if not Ext and (CurGroup<>nil) then
        if not CurGroup^[AnsiLowerCase(FLine[CurChar])[1]].Active then
          CurGroup:=nil;
    until Ext or (CurGroup=nil);
  end
  else Ext:=true;
  if not Ext then begin
    if IsNumeral(VarStr) then begin
      M:=0;
      Ext:=false;
      while (CurChar<=Length(FLine))and not Ext do begin
        case M of
          0:if IsDivider(FLine[CurChar]) then Ext:=true
            else if LowerCase(FLine[CurChar])='e' then
            if IsNumeral(VarStr) then M:=1
            else Ext:=true;
          1:if IsDivider(FLine[CurChar]) and
            (FLine[CurChar]<>'-') then Ext:=true
            else M:=2;
          2:if IsDivider(FLine[CurChar]) then Ext:=true;
        end;
        if not Ext then begin
          VarStr:=VarStr+AnsiLowerCase(FLine[CurChar]);
          inc(CurChar);
        end;
      end;
      while (CurChar<=Length(FLine))and
        not IsDivider(FLine[CurChar]) do begin
        VarStr:=VarStr+AnsiLowerCase(FLine[CurChar]);
        inc(CurChar);
      end;
    end
    else if DividerExpected and
      not IsDivider(FLine[CurChar]) then begin
      Sym:=sNone;
      repeat
        VarStr:=VarStr+AnsiLowerCase(FLine[CurChar]);
        if Length(VarStr)>=MaxIdentLen then ErCode:=1;
        inc(CurChar);
      until (CurChar>Length(FLine)) or IsDivider(FLine[CurChar]);
    end;
  end;
  Result:=Sym;
end;

procedure TPArser.ExecError(Code:integer);
begin
  if not FixedError and (Code<>0) then begin
    FixedError:=true;
    ErrorCode:=Code;
    ErrorStr:=ExpectionError[Code];
  end;
end;

function TPArser.GetFunctionResult(Sym:TSymbol;FName:string;
  Value:Pointer):boolean;
var
  CurFunct:PFunct;
  Par,CurPar1,CurPar2,BridgeSaver:PFunctParam;
  PCount,Def:integer;
  Expr:TExprType;
begin
  Result:=false;
  Def:=0;
  PCount:=0;
  Par:=nil;
  CurFunct:=AdCalc.GetFunct(FName);
  if FName='' then begin
    PCount:=1;
    New(Par);
    with Par^ do case Sym of
      sExtendedFunct:begin
        ParamType:=expExtended;
        GetMem(ParamValue,SizeOf(extended));
      end;
      sBooleanFunct:begin
        ParamType:=expBoolean;
        GetMem(ParamValue,SizeOf(boolean));
      end;
      sStringFunct: ParamType:=expString;
    end;
    Par^.NextParam:=nil;
  end
  else if FName='param' then begin
    if AdCalc.ParamsBridge<>nil then begin
      PCount:=1;
      New(Par);
      with Par^ do begin
        ParamType:=expExtended;
        GetMem(ParamValue,SizeOf(extended));
      end;
      Par^.NextParam:=nil;
    end
    else begin
      Result:=false;
      RestorePosition(PrevPos);
    end;
  end
  else if CurFunct<>nil then with CurFunct^ do if Params<>nil then begin
    PCount:=ParCount;
    Def:=Definer;
    CurPar1:=Params;
    New(Par);
    CurPar2:=Par;
    while CurPar1<>nil do with CurPar2^ do begin
      ParamType:=CurPar1^.ParamType;
      if ParamType=expExtended then GetMem(ParamValue,SizeOf(extended));
      if ParamType=expBoolean  then GetMem(ParamValue,SizeOf(boolean));
      CurPar1:=CurPar1^.NextParam;
      if CurPar1<>nil then begin
        New(NextParam);
        CurPar2:=NextParam;
      end
      else NextParam:=nil;
    end;
  end;
  if Par<>nil then begin
    BridgeSaver:=AdCalc.ParamsBridge;
    try
      if GetFunctionParams(PCount,Par) then begin
        Result:=true;
        if FName='param' then begin
          case Sym of
            sExtendedFunct:Expr:=expExtended;
            sBooleanFunct: Expr:=expBoolean;
            sStringFunct:  Expr:=expString
            else Expr:=expExtended;
          end;
          Result:=CheckParam(Trunc(extended(Par^.ParamValue^)),Value,Expr);
        end
        else begin
          AdCalc.ParamsBridge:=Par;
          if Def=2 then begin
            case Sym of
              sExtendedFunct:begin
                extended(Value^):=0;
                Result:=CurFunct^.Parser.CalcExtendedFunct(CurFunct^.FunctName,
                  extended(Value^));
              end;
              sBooleanFunct:begin
                Boolean(Value^):=false;
                Result:=CurFunct^.Parser.CalcBooleanFunct(CurFunct^.FunctName,
                  boolean(Value^));
              end;
              sStringFunct:begin
                string(Value^):='';
                Result:=CurFunct^.Parser.CalcStringFunct(CurFunct^.FunctName,
                  string(Value^));
              end;
            end;
          end
          else if Def=1 then case Sym of
            sExtendedFunct:Result:=GetExtendedFunctValue(FName,extended(Value^));
            sBooleanFunct: Result:=GetBooleanFunctValue(FName,boolean(Value^));
            sStringFunct:  Result:=GetStringFunctValue(FName,string(Value^))
            else Result:=false;
          end
          else if Sym=sExtendedFunct then begin
            if FName='' then extended(Value^):=extended(Par^.ParamValue^)
            else if FName='if' then if boolean(AdCalc.GetParam(1)^) then
              extended(Value^):=extended(AdCalc.GetParam(2)^)
              else Extended(Value^):=Extended(AdCalc.GetParam(3)^)
            else if FName='frac' then
              extended(Value^):=Frac(extended(AdCalc.GetParam(1)^))
            else if FName='int' then
              extended(Value^):=Int(extended(AdCalc.GetParam(1)^))
            else if FName='round' then
              extended(Value^):=Round(extended(AdCalc.GetParam(1)^))
            else if FName='exp' then
              extended(Value^):=Exp(extended(AdCalc.GetParam(1)^))
            else if FName='log' then extended(Value^):=
              Logn(extended(AdCalc.GetParam(1)^),extended(AdCalc.GetParam(2)^))
            else if FName='ln' then
              extended(Value^):=Ln(extended(AdCalc.GetParam(1)^))
            else if FName='sin' then
              extended(Value^):=Sin(extended(AdCalc.GetParam(1)^))
            else if FName='cos' then
              extended(Value^):=Cos(extended(AdCalc.GetParam(1)^))
            else if FName='arcsin' then
              extended(Value^):=ArcSin(extended(AdCalc.GetParam(1)^))
            else if FName='arccos' then
              extended(Value^):=ArcCos(extended(AdCalc.GetParam(1)^))
            else if FName='sinh' then
              extended(Value^):=SinH(extended(AdCalc.GetParam(1)^))
            else if FName='cosh' then
              extended(Value^):=CosH(extended(AdCalc.GetParam(1)^))
            else if FName='arcsinh' then
              extended(Value^):=ArcSinH(extended(AdCalc.GetParam(1)^))
            else if FName='arccosh' then
              extended(Value^):=ArcCosH(extended(AdCalc.GetParam(1)^))
            else if FName='tan' then
              extended(Value^):=Tan(extended(AdCalc.GetParam(1)^))
            else if FName='cotan' then
              extended(Value^):=CoTan(extended(AdCalc.GetParam(1)^))
            else if FName='arctan' then
              extended(Value^):=ArcTan(extended(AdCalc.GetParam(1)^))
            else if FName='tanh' then
              extended(Value^):=TanH(extended(AdCalc.GetParam(1)^))
            else if FName='arctanh' then
              extended(Value^):=ArcTanH(extended(AdCalc.GetParam(1)^))
            else if FName='sqr' then
              extended(Value^):=Sqr(extended(AdCalc.GetParam(1)^))
            else if FName='sqrt' then
              extended(Value^):=Sqrt(extended(AdCalc.GetParam(1)^))
            else if FName='abs' then
              extended(Value^):=Abs(extended(AdCalc.GetParam(1)^))
            else if FName='length' then
              extended(Value^):=Length(string(AdCalc.GetParam(1)^))
            else if FName='pos' then
              extended(Value^):=Pos(string(AdCalc.GetParam(1)^),
                string(AdCalc.GetParam(2)^))
            else if FName='strtofloat' then
              extended(Value^):=StrToFloat(string(AdCalc.GetParam(1)^))
            else Result:=false;
          end
          else if Sym=sBooleanFunct then begin
            if FName='' then boolean(Value^):=boolean(Par^.ParamValue^)
            else if FName='not' then
              boolean(Value^):=not boolean(AdCalc.GetParam(1)^)
            else Result:=false;
          end
          else if Sym=sStringFunct then begin
            if FName='' then string(Value^):=string(Par^.StrValue)
            else if FName='ansilowercase' then
              string(Value^):=AnsiLOwerCase(string(AdCalc.GetParam(1)^))
            else if FName='ansiuppercase' then
              string(Value^):=AnsiUpperCase(string(AdCalc.GetParam(1)^))
            else if FName='lowercase' then
              string(Value^):=LowerCase(string(AdCalc.GetParam(1)^))
            else if FName='uppercase' then
              string(Value^):=UpperCase(string(AdCalc.GetParam(1)^))
            else if FName='copy' then
              string(Value^):=Copy(string(AdCalc.GetParam(1)^),
                Trunc(extended(AdCalc.GetParam(2)^)),
                Trunc(extended(AdCalc.GetParam(3)^)))
            else if FName='floattostr' then
              string(Value^):=FloatToStr(extended(AdCalc.GetParam(1)^))
            else Result:=false;
          end
          else Result:=false;
        end;
      end;
    finally
      AdCalc.ParamsBridge:=BridgeSaver;
      while Par<>nil do with Par^ do begin
        if ParamType=expExtended then FreeMem(ParamValue,SizeOf(extended));
        if ParamType=expBoolean  then FreeMem(ParamValue,SizeOf(boolean));
        if ParamType=expString   then StrValue:='';
        CurPar2:=Par;
        Par:=Par^.NextParam;
        Dispose(CurPar2);
      end;
    end;
  end;
end;

function TPArser.GetExtendedFunctValue(FName:string;var Value:extended):boolean;
var
  b:boolean;
begin
  if FName='' then Result:=false
  else begin
    if FName[1] in ['a'..'z'] then begin
      b:=false;
      if Assigned(AdCalc.FOnGetExtendedFunct)and
      not AdCalc.WasError then
        AdCalc.FOnGetExtendedFunct(AdCalc,FName,b,Value);
      Result:=b;
    end
    else Result:=false;
  end;
end;

function TPArser.GetBooleanFunctValue(FName:string;var Value:boolean):boolean;
var
  b:boolean;
begin
  if FName='' then Result:=false
  else begin
    if FName[1] in ['a'..'z'] then begin
      b:=false;
      if Assigned(AdCalc.FOnGetBooleanFunct)and
      not AdCalc.WasError then
        AdCalc.FOnGetBooleanFunct(AdCalc,FName,b,Value);
      Result:=b;
    end
    else Result:=false;
  end;
end;

function TPArser.GetStringFunctValue(FName:string;var Value:string):boolean;
var
  b:boolean;
begin
  if FName='' then Result:=false
  else begin
    if FName[1] in ['a'..'z'] then begin
      b:=false;
      if Assigned(AdCalc.FOnGetStringFunct)and
      not AdCalc.WasError then
        AdCalc.FOnGetStringFunct(AdCalc,FName,b,Value);
      Result:=b;
    end
    else Result:=false;
  end;
end;

function TPArser.GetNumValue(s:string;var EV:extended):boolean;
var
  i:integer;
  b:boolean;
begin
  Result:=false;
  if s<>'' then begin
    Val(s,EV,i);
    if (i=0)and(s[1]<>'e') then Result:=true
    else if s[1] in ['a'..'z'] then
      if AdCalc.GetExtendedVarValue(s,EV) then Result:=true
      else begin
        b:=false;
        if Assigned(AdCalc.FOnGetExtendedVar)and
          not AdCalc.WasError then AdCalc.FOnGetExtendedVar(AdCalc,s,b,EV);
        Result:=b;
      end;
  end;
end;

function TPArser.GetBoolValue(s:string;var BV:boolean):boolean;
var
  b:boolean;
begin
  Result:=false;
  if s<>'' then begin
    if s='true' then begin
      Result:=true;
      BV:=true;
    end
    else if s='false' then begin
      Result:=true;
      BV:=false;
    end
    else if s[1] in ['a'..'z'] then
      if AdCalc.GetBooleanVarValue(s,BV) then Result:=true
      else begin
        b:=false;
        if Assigned(AdCalc.FOnGetBooleanVar)and not AdCalc.WasError then
          AdCalc.FOnGetBooleanVar(AdCalc,s,b,BV);
        Result:=b;
      end;
  end;
end;

function TPArser.GetStringValue(s:string;var SV:string):boolean;
var
  b:boolean;
begin
  Result:=false;
  if NextSym=sStringConst then begin
    Result:=true;
    SV:=NextStr;
  end
  else if s<>'' then if s[1] in ['a'..'z'] then
    if AdCalc.GetStringVarValue(s,SV) then Result:=true
    else begin
      b:=false;
      if Assigned(AdCalc.FOnGetStringVar)and
        not AdCalc.WasError then AdCalc.FOnGetStringVar(AdCalc,s,b,SV);
      Result:=b;
    end;
end;

function TPArser.GetBindingExpection(Exp:TExpection;var Sym:TSymbol):boolean;
begin
  Result:=false;
  if NextEr>0 then ExecError(NextEr)
  else if SymbolTable[NextSym].Expection=Exp then begin
    Sym:=NextSym;
    NextSymbol;
    Result:=true;
  end;
end;

function TPArser.GetBindingSymbol(Sym:TSymbol):boolean;
begin
  Result:=false;
  if NextEr>0 then ExecError(NextEr)
  else if NextSym=Sym then begin
    NextSymbol;
    Result:=true;
  end;
end;

function TPArser.GetRowSymbol(var Sym:TSymbol;Row:TRow;
                              var Last:boolean):boolean;
begin
  Sym:=sNone;
  Last:=false;
  Result:=false;
  if NextEr>0 then ExecError(NextEr)
  else if RowExpection[Row]=SymbolTable[NextSym].Expection then begin
    Last:=false;
    Sym:=NextSym;
    NextSymbol;
    Result:=true;
  end
  else if RowLastExpection[Row]=SymbolTable[NextSym].Expection then begin
    if (NextStr='')or(NextSym<>sNone) then begin
      Last:=true;
      Sym:=NextSym;
      Result:=true;
    end;
  end
  else if RowLastExpection[Row]=exIndef then begin
    Last:=true;
    Result:=true;
  end;
end;

function TPArser.GetFunctName(var FName:string;Sym:TSymbol):boolean;
begin
  Result:=false;
  if NextEr>0 then ExecError(NextEr)
  {"(" Is considered as function by a single parameter}
  else if NextSym=sLeftParenthesis then Result:=true
  else if (NextSym=Sym) or (NextSym=sParam) then begin
    FName:=NextStr;
    NextSymbol;
    Result:=true;
  end;
end;

function TPArser.GetExtendedExpection(var n:extended):boolean;
var
  IdName:string;
begin
  IdName:='';
  Result:=false;
  if NextEr>0 then ExecError(NextEr)
  else if NextSym=sStringConst then Exit
  else if GetFunctName(IdName,sExtendedFunct) then
    Result:=GetFunctionResult(sExtendedFunct,IdName,@n)
  else if GetNumValue(NextStr,n) then begin
    NextSymbol;
    Result:=true;
  end;
end;

function TPArser.GetBooleanExpection(var b:boolean):boolean;
var
  IdName:string;
  Pos:TPosition;
begin
  IdName:='';
  Result:=false;
  if NextEr>0 then ExecError(NextEr)
  else begin
    SavePosition(Pos);
    if GetFunctName(IdName,sBooleanFunct) then begin
      Result:=GetFunctionResult(sBooleanFunct,IdName,@b);
      if not Result and not FixedError then begin
        RestorePosition(Pos);
        Result:=GetCompareResult(b);
      end;
    end
    else if GetBoolValue(NextStr,b) then begin
      NextSymbol;
      Result:=true;
    end
    else Result:=GetCompareResult(b);
  end;
end;

function TPArser.GetStringExpection(var s:string):boolean;
var
  IdName:string;
begin
  IdName:='';
  Result:=false;
  if NextEr>0 then ExecError(NextEr)
  else if GetFunctName(IdName,sStringFunct) then
    Result:=GetFunctionResult(sStringFunct,IdName,@s)
  else if GetStringValue(NextStr,s) then begin
    NextSymbol;
    Result:=true;
  end;
end;

function TPArser.GetRowErrorCode(Row:TRow):integer;
begin
  case Row of
    FirstLevelNumRow:          Result:=7;
    LastParameterNumRow:       Result:=11;
    NonLastParameterNumRow:    Result:=12;
    ComparePrimaryNumRow:      Result:=8;
    CompareSecondNumRow:       Result:=0;
    BlockNumRow:               Result:=17;
    FirstLevelBoolRow:         Result:=9;
    LastParameterBoolRow:      Result:=11;
    NonLastParameterBoolRow:   Result:=12;
    BlockBoolRow:              Result:=17;
    FirstLevelStringRow:       Result:=6;
    LastParameterStringRow:    Result:=11;
    NonLastParameterStringRow: Result:=12;
    ComparePrimaryStringRow:   Result:=8;
    CompareSecondStringRow:    Result:=0;
    BlockStringRow:            Result:=17
    else Result:=7;
  end;
end;

function TPArser.GetLevel(Sym:TSymbol):integer;
begin
  case sym of
    sAddition:Result:=0;
    sSubtract:Result:=0;
    sMultiplic:Result:=1;
    sDivision:Result:=1;
    sPower:Result:=2
    else Result:=-1;
  end;
end;

function TPArser.GetNumRowValue(Row:TRow;var Value:Extended;
                        Level:word;Moment:word):boolean;
var
  Sym,ArithSym:TSymbol;
  n:extended;
  Last,FirstValue:boolean;
procedure NextMoment;
begin
  if Moment=2 then Moment:=3
  else if Moment=3 then Moment:=2;
end;
begin
  ArithSym:=sAddition;
  Result:=true;
  FirstValue:=true;
  repeat
    Sym:=sNone;
    case Moment of
      1:begin {Expected "+", "-" or value of extended type}
        case NextSym of
          sAddition:NextSymbol;
          sSubTract:begin
            ArithSym:=sSubTract;
            NextSymbol;
          end;
        end;
        Moment:=3;
      end;
      2:begin {Expected value of extended type}
        if GetExtendedExpection(n) then begin
          while GetLevel(NextSym)>GetLevel(ArithSym) do
            {Priority of arithmetical tokens}
            if not GetNumRowValue(Row,n,GetLevel(NextSym),3) then begin
              Result:=false;
              Exit;
            end;
          case ArithSym of
            sAddition:Value:=Value+n;
            sSubtract:Value:=Value-n;
            sMultiplic:Value:=Value*n;
            sDivision:if n=0 then begin
              Result:=false;
              RestorePosition(PrevPos);
              ExecError(13);
              Exit;
            end
            else Value:=Value/n;
            sPower:Value:=Power(Value,n);
          end;
        end
        else begin
          Result:=false;
          if (Row=ComparePrimaryNumRow) and FirstValue then ExecError(3)
          else ExecError(2);
          Exit;
        end;
        FirstValue:=false;
      end;
      3:begin {Expected arithmetical token}
        if GetRowSymbol(Sym,Row,Last) then begin
          if Last then Moment:=0
          else ArithSym:=Sym;
        end
        else begin
          Result:=false;
          ExecError(GetRowErrorCode(Row));
          Exit;
        end;
      end;
    end;
    NextMoment;
  until (Moment=0)or((Level>=GetLevel(NextSym))and
                     (Level>0)and(Moment=3));
end;

function TPArser.GetBoolRowValue(Row:TRow;var Value:boolean):boolean;
var
  Moment:0..2;
  Sym,LogSym:TSymbol;
  b,Last:boolean;
procedure NextMoment;
begin
  if Moment=1 then Moment:=2
  else if Moment=2 then Moment:=1;
end;
begin
  Moment:=1;
  LogSym:=sAnd;
  Value:=true;
  Result:=true;
  while Moment>0 do begin
    Sym:=sNone;
    case Moment of
      1:begin {Expected value of boolean type}
        if GetBooleanExpection(b) then begin
          case LogSym of
            sAnd : Value:=Value and b;
            sOr  : Value:=Value or  b;
          end;
        end
        else begin
          Result:=false;
          ExecError(3);
          Exit;
        end;
      end;
      2:begin {Expected "or" or "and"}
        if GetRowSymbol(Sym,Row,Last) then begin
          if Last then Moment:=0
          else LogSym:=Sym;
        end
        else begin
          Result:=false;
          ExecError(GetRowErrorCode(Row));
          Exit;
        end;
      end;
    end;
    NextMoment;
  end;
end;

function TPArser.GetStringRowValue(Row:TRow;var Value:string):boolean;
var
  Moment:0..2;
  s:string;
procedure NextMoment;
begin
  if Moment=1 then Moment:=2
  else if Moment=2 then Moment:=1;
end;
begin
  Moment:=1;
  Value:='';
  Result:=true;
  while Moment>0 do begin
    case Moment of
      1:begin {Expected value of String type}
        if GetStringExpection(s) then Value:=Value+s
        else begin
          Result:=false;
          ExecError(4);
          Exit;
        end;
      end;
      2:begin {Expected "+"}
        if NextEr>0 then ExecError(NextEr)
        else if NextStr='+' then NextSymbol
        else if (RowLastExpection[Row]=SymbolTable[NextSym].Expection)and
          ((NextStr='')or(NextSym<>sNone)) then Moment:=0
        else if RowLastExpection[Row]=exIndef then Moment:=0
        else begin
          Result:=false;
          ExecError(GetRowErrorCode(Row));
          Exit;
        end;
      end;
    end;
    NextMoment;
  end;
end;

function TPArser.GetFunctionParams(PC:integer;FP:PFunctParam):boolean;
var
  Moment:0..4;
  i:integer;
procedure NextMoment;
begin
  if Moment=1 then Moment:=2
  else if Moment=2 then begin
    if PC>i then Moment:=3
    else Moment:=4;
  end
  else if Moment=3 then Moment:=2
  else Moment:=0;
end;
begin
  Moment:=1;
  Result:=true;
  i:=0;
  while Moment>0 do begin
    case Moment of
      1:begin {Expected "("}
        if not GetBindingSymbol(sLeftParenthesis) then begin
          ExecError(10);
          Result:=false;
          Exit;
        end;
      end;
      2:begin {Expected value of Extended, boolean or string type}
        if FP<>nil then begin
          with FP^ do if PC-1>i then case ParamType of
            expExtended : begin
              extended(ParamValue^):=0;
              Result:=GetNumRowValue(NonLastParameterNumRow,
                extended(ParamValue^),0,1);
            end;
            expBoolean  : Result:=GetBoolRowValue(NonLastParameterBoolRow,
              boolean(ParamValue^));
            expString : begin
              StrValue:='';
              Result:=GetStringRowValue(NonLastParameterStringRow,StrValue);
            end;
          end
          else case ParamType of
            expExtended : begin
              extended(ParamValue^):=0;
              Result:=GetNumRowValue(LastParameterNumRow,
                extended(ParamValue^),0,1);
            end;
            expBoolean  : Result:=GetBoolRowValue(LastParameterBoolRow,
              boolean(ParamValue^));
            expString : begin
              StrValue:='';
              Result:=GetStringRowValue(LastParameterStringRow,StrValue);
            end;
          end;
          ParamPos:=PrevPos;
        end;
        if not Result then Exit;
        inc(i);
        FP:=FP^.NextParam;
      end;
      3:begin {Expected ","}
        if not GetBindingSymbol(sComma) then begin
          Result:=false;
          Exit;
        end;
      end;
      4:begin {Expected ")"}
        if not GetBindingSymbol(sRightParenthesis) then begin
          Result:=false;
          Exit;
        end;
      end;
    end;
    NextMoment;
  end;
end;

function TPArser.GetCompareResult(var Value:boolean):boolean;
var
  NumOrStr:0..1;
  Moment:0..3;
  Sym,BoolSymbol:TSymbol;
  s:string;
  A,B:extended;
  AStr,BStr:string;
  Pos:TPosition;
procedure NextMoment;
begin
  if Moment=1 then Moment:=2
  else if Moment=2 then Moment:=3
  else Moment:=0;
end;
begin
  NumOrStr:=0;
  Moment:=1;
  Value:=false;
  BoolSymbol:=sNone;
  Result:=true;
  while Moment>0 do begin
    s:='';
    Sym:=sNone;
    case Moment of
      1:begin {Expected value of extended or string type}
        SavePosition(Pos);
        if (NextSym=sStringFunct) or GetStringExpection(AStr) then begin
          RestorePosition(Pos);
          AStr:='';
          if not GetStringRowValue(ComparePrimaryStringRow,AStr) then begin
            Result:=false;
            Exit;
          end;
          NumOrStr:=1;
        end
        else begin
          RestorePosition(Pos);
          A:=0;
          if not GetNumRowValue(ComparePrimaryNumRow,A,0,1) then begin
            Result:=false;
            Exit;
          end;
        end
      end;
      2:begin {Expected comparison token}
        if not GetBindingExpection(exCompareSign,Sym) then begin
          Result:=false;
          Exit;
        end
        else BoolSymbol:=Sym;
      end;
      3:begin {Expected value of extended or string type}
        case NumOrStr of
          0:begin
            B:=0;
            if not GetNumRowValue(CompareSecondNumRow,B,0,1) then begin
              Result:=false;
              Exit;
            end;
            case BoolSymbol of
              sGreaterOrEqual : Value:= A >= B;
              sLessOrEqual    : Value:= A <= B;
              sGreaterOrLess  : Value:= A <> B;
              sEqual          : Value:= A  = B;
              sGreater        : Value:= A  > B;
              sLess           : Value:= A  < B;
            end;
          end;
          1:begin
            BStr:='';
            if not GetStringRowValue(CompareSecondStringRow,BStr) then begin
              Result:=false;
              Exit;
            end;
            case BoolSymbol of
              sGreaterOrEqual : Value:= AStr >= BStr;
              sLessOrEqual    : Value:= AStr <= BStr;
              sGreaterOrLess  : Value:= AStr <> BStr;
              sEqual          : Value:= AStr  = BStr;
              sGreater        : Value:= AStr  > BStr;
              sLess           : Value:= AStr  < BStr;
            end;
          end;
        end;
      end;
    end;
    NextMoment;
  end;
end;

function TPArser.ExecBlock:boolean;
var
  VName,Descr:string;
  VType:TExprType;
  Value:pointer;
  BVal:boolean;
  EVal:extended;
  SVal:string;
begin
  VName:='';
  Result:=true;
  while NextStr<>'' do begin
    if AdCalc.GetVarProperties(NextStr,VType,Value,Descr) then begin
      VName:=NextStr;
      NextSymbol;
    end
    else begin
      Result:=false;
      ExecError(15);
      Exit;
    end;
    if not GetBindingSymbol(sEqual) then begin
      Result:=false;
      ExecError(16);
      Exit;
    end;
    case VType of
      expExtended: begin
        EVal:=0;
        if GetNumRowValue(BlockNumRow,EVal,0,1) then begin
          AdCalc.SetExtendedVarValue(VName,EVal);
          NextSymbol;
        end
        else Result:=false;
      end;
      expBoolean: begin
        BVal:=false;
        if GetBoolRowValue(BlockBoolRow,BVal) then begin
          AdCalc.SetBooleanVarValue(VName,BVal);
          NextSymbol;
        end
        else Result:=false;
      end;
      expString: begin
        SVal:='';
        if GetStringRowValue(BlockStringRow,SVal) then begin
          AdCalc.SetStringVarValue(VName,SVal);
          NextSymbol;
        end
        else Result:=false;
      end;
    end;
    if not Result then Exit;
  end;
end;

function TPArser.CalcExtended(var Value:extended):boolean;
begin
  InitNewformula;
  Value:=0;
  Result:=GetNumRowValue(FirstLevelNumRow,Value,0,1);
  if not Result then if Assigned(AdCalc.FOnError)and
                                    not AdCalc.WasError then begin
    AdCalc.WasError:=true;
    AdCalc.FOnError(AdCalc,CurLine,CurChar-1,
      GlobChar+CurChar-1,ErrorCode,ErrorStr,Handle);
  end;
end;

function TPArser.CalcBoolean(var Value:boolean):boolean;
begin
  InitNewformula;
  Value:=false;
  Result:=GetBoolRowValue(FirstLevelBoolRow,Value);
  if not Result then if Assigned(AdCalc.FOnError)and
    not AdCalc.WasError then begin
    AdCalc.WasError:=true;
    AdCalc.FOnError(AdCalc,CurLine,CurChar-1,
      GlobChar+CurChar-1,ErrorCode,ErrorStr,Handle);
  end;
end;

function TPArser.CalcString(var Value:string):boolean;
begin
  InitNewformula;
  Value:='';
  Result:=GetStringRowValue(FirstLevelStringRow,Value);
  if not Result then if Assigned(AdCalc.FOnError)and
    not AdCalc.WasError then begin
    AdCalc.WasError:=true;
    AdCalc.FOnError(AdCalc,CurLine,CurChar-1,
      GlobChar+CurChar-1,ErrorCode,ErrorStr,Handle);
  end;
end;

function TPArser.CalcExtendedFunct(FName:string;var Value:extended):boolean;
begin
  InitNewformula;
  Value:=0;
  Result:=GetNumRowValue(FirstLevelNumRow,Value,0,1);
  if not Result then if Assigned(AdCalc.FOnFunctError)and
    not AdCalc.WasError then begin
    AdCalc.WasError:=true;
    AdCalc.FOnFunctError(AdCalc,FName,
      CurLine,CurChar-1,GlobChar+CurChar-1,ErrorCode,ErrorStr);
  end;
end;

function TPArser.CalcBooleanFunct(FName:string;var Value:boolean):boolean;
begin
  InitNewformula;
  Value:=false;
  Result:=GetBoolRowValue(FirstLevelBoolRow,Value);
  if not Result then if Assigned(AdCalc.FOnFunctError)and
    not AdCalc.WasError then begin
    AdCalc.WasError:=true;
    AdCalc.FOnFunctError(AdCalc,FName,
      CurLine,CurChar-1,GlobChar+CurChar-1,ErrorCode,ErrorStr);
  end;
end;

function TPArser.CalcStringFunct(FName:string;var Value:string):boolean;
begin
  InitNewformula;
  Value:='';
  Result:=GetStringRowValue(FirstLevelStringRow,Value);
  if not Result then if Assigned(AdCalc.FOnFunctError)and
    not AdCalc.WasError then begin
    AdCalc.WasError:=true;
    AdCalc.FOnFunctError(AdCalc,FName,
      CurLine,CurChar-1,GlobChar+CurChar-1,ErrorCode,ErrorStr);
  end;
end;

function TPArser.CalcBlock:boolean;
begin
  InitNewformula;
  Result:=ExecBlock;
  if not Result then if Assigned(AdCalc.FOnError)and
    not AdCalc.WasError then begin
    AdCalc.WasError:=true;
    AdCalc.FOnError(AdCalc,CurLine,CurChar-1,
      GlobChar+CurChar-1,ErrorCode,ErrorStr,Handle);
  end;
end;

procedure TPArser.ClearCompiledSymbols;
begin
  SetLength(CompiledSymbols,0);
  CSCount:=0;
end;

procedure TPArser.CompileLines;
begin
  ClearCompiledSymbols;
  InitNewFormula;
  while (NextSym<>sNone)or(NextStr<>'') do begin
    SetLength(CompiledSymbols,CurCS+1);
    SavePosition(CompiledSymbols[CurCS]);
    inc(CurCS);
    NextSymbol;
  end;
  CSCount:=CurCS;
end;

end.

