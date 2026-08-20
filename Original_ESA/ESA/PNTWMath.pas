//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// FREQUENTLY USED MATHEMATICS PROCEDURES
// P.N.T. WILLIAMS
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Unit PNTWMath;

interface

const R = 287;   {J/kg K}

Procedure PositCam (CR, CTh, Gamma, BaseD : Extended; var X, Y : Extended);
Function  Ipol (x1, y1, x2, x3, y3: Double) : Double; {Interpolation : Returns y2}
Function  Pwr (i, j : Double) : Double; { Returns i to the Power j}
Function  ArcCos (x : Double): Double;
Function  ArcSin (x : Extended): Extended;
Function  Dist (x1, y1, x2, y2 : Extended) : Extended;
Procedure Pol (x, y : Extended; var th, r : Extended);
Procedure Rect (th,r : Extended; var xx, yy : Extended);
procedure CartPol (x, y : Extended; var th, r : Extended);
function  AbsVal (x : Extended): extended;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

implementation

Procedure PositCam (CR, CTh, Gamma, BaseD : Extended; var X, Y : Extended);

// Gets a Cam Profile point in X, Y coordinates at a given angle from the R, theta
// coordinates (on a given base radius at camshaft angle gamma)
// rotates clockwize with gamma zero point at top (not at right as in cartesian)

var R, Th : Extended;
begin
  R := CR + (BaseD/2);
  Th := Pi/180 * (CTh - Gamma + 90);
  repeat if Th >= (2*PI) then Th := Th - (2*PI); until th < (2*PI);
  Rect (Th, r, x, y);
end; //PositCam

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function IPol (x1, y1, x2, x3, y3: Double) : Double;
begin
  if (x1-x3) <= 0.0000001 then IPol := y1 else IPol := (((x2-x3)*(y1-y3))/(x1-x3))+ y3;
end; //IPol

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function Pwr (i, j : Double) : Double;
{obtains i to the power j}
begin
  if i <= 0 then Pwr := 0
    else if j = 0 then Pwr := 1
      else Pwr := exp (j * ln (i));
end; //Pwr

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function Arccos (x : double): Double;
begin
  if x = 0 then ArcCos := Pi/2 else ArcCos := ArcTan (sqrt(1-sqr(x))/x);
end; //Arccos

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function ArcSin (x : Extended): Extended;
begin
  if x >= 1 then  ArcSin := PI/2
    else  ArcSin := ArcTan (x / sqrt (1-sqr(x)));
end; //ArcSin

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function Dist (x1, y1, x2, y2 : Extended) : Extended;
begin
  Dist := sqrt(sqr(x1-x2)+sqr(y1-y2));
end; //Dist

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure Pol (x, y : Extended; var th, r : Extended);
begin
  r := sqrt (sqr(x)+ sqr(y));
  {arg := Abs (x/r);}
  th := ArcCos (x/r);
  if (x <= 0) and (y > 0) then th := th + (Pi);
  if (x <= 0) and (y <= 0)  then th := (Pi) - th;
  if (x > 0) and (y <= 0) then th := (2*PI) - th;
end; //Pol

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure CartPol (x, y : Extended; var th, r : Extended);
{converts x, y to r, th in full cartesian - checked OK 18/12/95}
begin
  r := sqrt (sqr(x)+ sqr(y));
  {arg := Abs (x/r);}
  th := ArcCos (x/r);
  if (x <= 0) and (y > 0) then th := Pi + th;
  if (x <= 0) and (y <= 0)  then th := Pi - th;
  if (x > 0) and (y <= 0) then th := (2*PI) - th;
end; //CartPol

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure Rect (th, r : Extended; var xx, yy : Extended);    {Tested 14/07}
begin
  xx := r * Cos (th);
  yy := r * Sin (th);
end; //Rect

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

function Absval (x : Extended): Extended;
begin
 if x < 0 then Absval := -1*x else Absval := x;
end; //Absval

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
begin
end.
