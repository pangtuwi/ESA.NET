(************************************************)
(*  Written by DPM                              *)
(*  Provides specialized mathematical functions *)
(*                                              *)
(************************************************)
(*  Modification history:                       *)
(*  96/03/36 :- Creation                        *)
(*  96/11/12 :- Added i_max, i_min              *)
(*  96/11/24 :- Added GaussReduce, and          *)
(*                   and GaussReduct            *)
(*  96/12/03 :- ZERO_UNDERFLOW & r_min & r_max  *)
(*  96/12/29 :- Added dNoZero                   *)
(*  97/01/15 :- Fixed SciExp as per Art's fix   *)
(*              (underflow seen as a zero)      *)
(*  97/02/15 :- Added double_bound              *)
(*  97/04/28 :- Added dNoZero2openfile          *)
(************************************************)


unit MATHDPM;

interface

Const ARRSIZE = 4;
      ZERO_UNDERFLOW = 1E-35;

Type Tnxn_Array = array [0..ARRSIZE-1,0..ARRSIZE-1] of extended;
     Tnx1_Array = array [0..ARRSIZE-1] of extended;

function dNoZero(x:double;source:PChar):double;
function dNoZero2openfile(var opened_logfile:text;x:double;source:PChar):double;
function GaussReduce (gA : Tnxn_Array; var gb : Tnx1_Array): integer;
procedure GaussReduct (a, b, c, d, e, f, g : extended;
                         var l, m, n, o : extended);
function  SciExp(x:double):integer;      {returns the exponent of x in Scientific notation}
function  i_max(a,b:integer):integer;
function  i_min(a,b:integer):integer;
function  r_max(a,b:double):double;
function  r_min(a,b:double):double;
function  rmod(a,b:double): double;
function  imod(a,b:integer): integer;
function  sign(x:double):double;
function  positive(x:double):double;
function  ZeroFilter(x:double): double;
function  rtrunc(a:double):integer;
procedure Pow(var a,b:double);
function  doublePower(a,b:double): double;
function  logbase(x,base:double): double;
function  log10(x:double): double;  {log to the base 10}
function  Bound(x,y,z:double): double;
function  double_Bound(x,y,z:double): double;
Function  double_Max(y,z:double): double;
Function  double_Min(y,z:double): double;
Function  bit_on(numb, bit: integer):boolean  ;
Function  RisingEdge(Last, Latest, Separator: double): boolean;
Function  FallingEdge(Last, Latest, Separator: double): boolean;
Function valhexdig(c :char) :integer;
Function valhex(s :string) :integer;
{Procedure writehex(x :integer);}
function sqrtd(x : double): double;
function InterpFc(c,a,Fa,b,Fb:double):double;  {linear interpolation of F(c)}
function d_imod(a,b:double): integer;
function deg_to_rad(x:double):double;
procedure DDecr(var i:integer);
function RoundUpToNearest(x:double; i:integer): integer;


implementation

function dNoZero2openfile(var opened_logfile:text;x:double;source:PChar):double;
begin
  if positive(x) < 1E-12 then begin
    dNoZero2openfile:=1E-12*sign(x);
    writeln(opened_logfile,'Possible Div0 crash averted by dNoZero; Source was [',source,']');
  end
  else dNoZero2openfile:=x;
end;

function dNoZero(x:double;source:PChar):double;
begin
  if positive(x) < 1E-12 then begin
    dNoZero:=1E-12*sign(x);
    writeln('Possible Div0 crash averted by dNoZero; Source was [',source,']');
  end
  else dNoZero:=x;
end;

function GaussReduce (gA : Tnxn_Array; var gb : Tnx1_Array): integer;
{dpm :- 96/11/24 }
{gauss reduction of array system Ay=b}
{ result is returned in b }
{ function returns the minimum decimal place accuracy to which
  each and every number in the solution is guaranteed
  ie: for our y solution, Ay = b up to at least xx decimal places
  if the number returned is less than zero (say -2), then the solution
  is accurate to (the nearest hundred).  This probably means that your
  setup has not been solved properly, so you should CHECK that the
  returned accuracy is at least what you want it (say to 3 decimals)}
var row, col, pivot, rowinc, colinc : Integer;
    MultFactor, Valpp, gResult, Accuracy: extended;
    tA: Tnxn_Array;
    tb: Tnx1_Array;
begin
  ta:=gA; tb:=gb;
  for pivot := 0 to (ARRSIZE-1) do begin
    Valpp:=ga[pivot,pivot];
    for rowinc := (ARRSIZE-1) downto 0 do begin
      row := (rowinc+pivot) mod ARRSIZE;
      MultFactor:=ga[row,pivot]/Valpp;
      for colinc := (ARRSIZE-1) downto 0  do begin
        col:=(pivot + colinc) mod ARRSIZE;
        if rowinc <> 0 then ga[row,col]:=ga[row,col]-ga[pivot,col]*MultFactor
        else ga[row,col]:=ga[row,col]/Valpp;
      end; {for colinc}
      if rowinc <> 0 then gb[row]:=gb[row]-gb[pivot]*MultFactor
      else gb[row]:=gb[row]/Valpp;
    end; {for rowinc}
  end; {for pivot}

  {All that remains is to report the accuracy of the solution}
  Accuracy:=0;
  for rowinc := 0 to (ARRSIZE-1) do begin
    gResult:=0;
    for colinc := 0 to (ARRSIZE-1) do begin
      gResult:=gResult+tA[rowinc,colinc]*gb[colinc];
    end;
    gResult:=abs(gResult - tb[rowinc]);
    if gResult>Accuracy then Accuracy:=gResult;
  end;
  GaussReduce:=-SciExp(Accuracy);
end;
(**************************************************)

procedure GaussReduct (a, b, c, d, e, f, g : extended;
                         var l, m, n, o : extended);
{gauss reduction of 4x4 symmetrical matrix}
{by D. P. Moran & P. N. T. Williams - 23 Nov 1995}
{this routine DOES work}
type gaussarr = array[0..3, 0..4] of extended;
var ga : gaussarr;
    Lc,row, col, pivot, rowinc, colinc : Integer;
begin
  ga[0, 4] := l;  ga[1, 4] := m;  ga[2, 4] := n;  ga[3, 4] := o;
  ga[0,0] := a;  ga[0,1] := b; ga[0,2] := c; ga[0,3] := d;
  for Lc := 0 to 2 do ga[1, LC] := ga[0, LC+1];
  ga[1, 3] := e;
  for Lc := 0 to 2 do ga[2, LC] := ga[1, LC+1];
  ga[2, 3] := f;
  for Lc := 0 to 2 do ga[3, LC] := ga[2, LC+1];
  ga[3, 3] := g;

 {print out matrix}
 { for LC := 1 to 3 do begin For pivot := 1 to 4 do write (ga [LC, pivot]:10:2); writeln; end;}

  for pivot := 0 to 3 do
    for rowinc := 3 downto 0 do
       begin
        row := (rowinc+pivot) mod 4;
        for colinc := 4 downto 0  do
          begin
           col:=(pivot + colinc) mod 5;
           if rowinc <> 0
            then ga[row,col]:=ga[row,col]-ga[pivot,col]*ga[row,pivot]/ga[pivot,pivot]
            else ga[row,col]:=ga[row,col]/ga[pivot,pivot];
          end; {for colinc}
    end; {for rowinc}
  l := ga [0,4];  m := ga [1,4];  n := ga [2,4];  o := ga [3,4];
end;

(**************************************************)

function SciExp(x:double):integer;
begin
     x:=abs(x);
     if( x<ZERO_UNDERFLOW )then x:=ZERO_UNDERFLOW;
     SciExp:=trunc(log10(x));
end;

(**************************************************)

function i_max(a,b:integer):integer; {returns the numeric max of two integer numbers}
begin
     if a>=b then i_max:=a else i_max:=b;
end;

function i_min(a,b:integer):integer; {returns the numeric min of two integer numbers}
begin
     if a<=b then i_min:=a else i_min:=b;
end;

function r_max(a,b:double):double; {returns the numeric max of two double numbers}
begin
     if a>=b then r_max:=a else r_max:=b;
end;

function r_min(a,b:double):double; {returns the numeric min of two double numbers}
begin
     if a<=b then r_min:=a else r_min:=b;
end;

{rounds x up to nearest whole integer factor of i; and returns this value}
function RoundUpToNearest(x:double; i:integer): integer;
begin
     RoundUpToNearest:= ((trunc(x)+1) div i)*i + i;
end;

{Decrements the variable, but not below zero}
procedure DDecr(var i:integer);
begin
     i:=i-1;
     if i<0 then i:=0;
end;

function deg_to_rad(x:double):double;
begin
     deg_to_rad:=x*pi/180.0
end;

function InterpFc(c,a,Fa,b,Fb:double):double;
begin
     InterpFc:=(c-a)/(b-a)*(Fb-Fa)+Fa;
end;

function sqrtd(x : double): double;
var xr2,sqrx:double;
begin
     if x<-0.001 then writeln('Sqrtd(',x,') =0');
     if x<0 then begin x:=0; end;
     xr2:=x*1E-8;

     sqrx:=sqrt(xr2);
     if x<0 then writeln('....is = ',sqrx*1e+4);
     sqrtd:=sqrx*1e4;
end;

(*Procedure writehex(x :integer);
{displays x as 4 hex digits}
begin
     writehexdig(x div $1000);
     writehexdig(x div $100);
     writehexdig(x div $10);
     writehexdig(x);
end;*)

Function valhexdig(c :char) :integer;
(*converts hex digit in c into an integer *)
begin
     c := upcase(c);
     if c>='A' then valhexdig := ord(c)-55
     else valhexdig := ord(c)-48;
end;

Function valhex(s :string) :integer;
(*converts hex number in s into an integer*)
begin
     while length(s)<4 do s:='0'+s;
     valhex := valhexdig(s[1])*4096 +
               valhexdig(s[2])*256  +
               valhexdig(s[3])*16    +
               valhexdig(s[4]);
end;

Function RisingEdge(Last, Latest, Separator: double): boolean;
{Written by DPM: When mV reading first rises above a critical threshold (Separator)
 then Rising Edge is detected}
{IF want to check for falling edge, then call RisingEdge(LATEST,LAST,xxx)}
begin
     if (Latest>Separator) and (Last<Separator) then RisingEdge:= true
     else RisingEdge:= false;
end;

Function FallingEdge(Last, Latest, Separator: double): boolean;
{Written by DPM: When mV reading first falls below a critical threshold (Separator)
 then Falling Edge is detected}
begin
     if (Latest<Separator) and (Last>Separator) then FallingEdge:= true
     else FallingEdge:= false;
end;

Function bit_on(numb, bit: integer):boolean  ;   {Written by DPM}
var ReturnInt:integer;
begin                                            {returns status of binary bit # 'bit' }
     ReturnInt:=((numb shr bit) mod 2);             {of the number 'numb'}
     if ReturnInt=1 then bit_on:=true
     else bit_on:=false;
end;

(*  CALCULATES THE NON-NEGATIVE MODULUS FOR FLOATING POINT NUMBERS ie not integers
    always returns a non-negative ie: rmod(-18.3,10) is +2.7;
*)
function rmod(a,b:double): double;
var md:double;
begin
{writeln('rmod used... ',a:9:2,', theta= ',a-b*trunc(a/b):9:2);}
     md:=a-b*trunc(a/b);
     if md<0 then md:=b+md;
     rmod:=md;
end;

(*  CALCULATES THE NON-NEGATIVE MODULUS FOR INTEGERS ie not integers
    always returns a non-negative ie: rmod(-18,10) is +2;
    (REM: usually -18 mod 10 returns -8;
*)
function imod(a,b:integer): integer;
begin
     imod:=abs(a mod b);
end;

function sign(x:double):double;
begin
     if x>=0 then sign:=1 else sign:=-1;
end;

(*  CHANGES THE SIGN OF THE VARIABLE TO POSITIVE*)
function positive(x:double):double;
begin
     if x>=0 then positive:=x else positive:=-x;
end;

function ZeroFilter(x:double): double;
begin
     if Positive(x)<1e-8 then ZeroFilter:=0 else ZeroFilter:=x;
end;

function rtrunc(a:double):integer;
begin
     if a<0 then rtrunc:= trunc(a-1)  {return the type of trunc function that I want}
     else rtrunc:= trunc(a);
end;

(*  CALCULTES " a TO THE POWER OF b".  Returns double part of answer in a, and complex part
    (if any) in b
    IF b is negative (ie a root), it returns the PRINCIPLE root, ie the root with the
    largest double component*)
procedure Pow(var a,b:double);
var r,angle: double;
begin
     if( a=0 )then begin a:=0; b:=0; end
     else if( b=0  )then begin a:=1; b:=0; end
     else begin
        if sign(a) >0 then angle:=0 else angle:=-pi;
        r:=exp(b*ln(Positive(a)));
        angle:=angle*(b);
        a:=r*cos(angle);  b:=r*sin(angle);
        a:=ZeroFilter(a); b:=ZeroFilter(b);
     end;
end;

{this is a weaker function.... returns zero unless the result is totally double}
function doublePower(a,b:double): double;
var r,angle: double;
begin
     if( a=0 )then begin a:=0; b:=0; end
     else if( b=0  )then begin a:=1; b:=0; end
     else begin
        if sign(a) >0 then angle:=0 else angle:=-pi;
        r:=exp(b*ln(Positive(a)));
        angle:=angle*(b);
        a:=r*cos(angle);  b:=r*sin(angle);
        a:=ZeroFilter(a); b:=ZeroFilter(b);
     end;
     if b=0 then doublePower:=a else doublePower:=0;
end;

function logbase(x,base:double): double;
begin
     if x>0 then
        logbase:= ln(x)/ln(base)  {sexy change of base}
     else logbase:=0;  {prevent crashes}
end;

function log10(x:double): double;
begin
     if x>0 then
          log10:= ln(x)/2.302585093  {sexy change to base 10}
     else log10:=0;    {prevent crashes}
end;

Function Max(y,z:double): double;
begin
     if z>y then Max:=z else Max:=y;
end;

Function Min(y,z:double): double;
begin
     if z<y then Min:=z else Min:=y;
end;

Function double_Max(y,z:double): double;
begin
     if z>y then double_Max:=z else double_Max:=y;
end;

Function double_Min(y,z:double): double;
begin
     if z<y then double_Min:=z else double_Min:=y;
end;

(*Returns x if x is in between y and z;  if x is bigger than both, returns the max
  if x is smaller than both, returns the min
  eg Bound(13,2,10) returns 10, since 10 is the maximum in the allowed range (2..10)
     Bound(6,2,10) returns 6 since it is in the allowed range*)
function Bound(x,y,z:double): double;
var mn:double;
begin
     mn:=Min(y,z);
     if x>mn then Bound:=Min(x,Max(y,z)) else Bound:=mn;
end;

function double_Bound(x,y,z:double): double;
var mn:double;
begin
     mn:=double_Min(y,z);
     if x>mn then double_Bound:=double_Min(x,double_Max(y,z)) else double_Bound:=mn;
end;

function d_imod(a,b:double): integer;
begin
     d_imod:=round(rmod(a,b));
end;

BEGIN
END.




