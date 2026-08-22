//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// Cam / Valve Profile Unit : Engine Simulation and Analysis (ESA)
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

unit Profiles;

interface

uses Classes;
const BigNeg = -999999;

type
  PPoint = ^TPoint;
  TPoint = Record
    x, y : Double;
    next : PPoint;
  end; // TPoint

 TProfile = Class
   NoPoints : Integer;
   Spacing : Double;
   First, Current : PPoint;
   OldFirst, OldCurrent : PPoint;
   ProfileOK, Modifying : Boolean;
   xmin, xmax, ymin, ymax : Double;
   lift, duration : Double;
   filename : String;
   constructor create;
   procedure Addpoint (inx, iny : Double);
   Procedure LoadText (inFileName : String);
   Procedure SaveText (inFileName : String);
   Function Gety (inx : Double) : Double;
   Function PointToText : ShortString;
   Procedure Getlimits;
   Procedure Scale (inXMin, inXMax, inYmin, inYMax : Double;
                    inNoPoints : Integer);
   Procedure Clear;                 
   destructor Destroy; override;
  private
 end; //TProfile

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

implementation

uses
 SysUtils, // FloattoStrF
 Dialogs;

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

constructor TProfile.Create;
begin
 inherited;
 NoPoints := 0;
 First := nil;
 Current := nil;
 ProfileOK := FALSE;
 Modifying := FALSE;
end; //TProfile.Create

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Procedure TProfile.Getlimits;
// Obtains maximums and minimums of profile
begin
  NoPoints := 0;
  xmin := -BigNeg;
  xmax := BigNeg;
  ymin := -BigNeg;
  ymax := BigNeg;
  Current := First;
  repeat
   if current^.x > xmax then xmax := current^.x;
   if current^.x < xmin then xmin := current^.x;
   if current^.y > ymax then ymax := current^.y;
   if current^.y < ymin then ymin := current^.y;
   current := current^.next;
   inc (NoPoints);
  until current = nil;
  Spacing := First^.next^.x - First^.x;
  Lift := ymax-ymin;
  Duration := xmax-xmin;
end; //TProfile.Getlimits

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

procedure TProfile.Addpoint (inx, iny : Double);
var NewPoint : PPoint;
begin
  New (NewPoint);
  NewPoint^.x := inx;
  NewPoint^.y := iny;
  NewPoint^.next := nil;
  inc (NoPoints);
  if NoPoints = 1 // This is the first point
   then First := NewPoint
    else Current^.next := NewPoint;
  Current := NewPoint;
  //Sort;
end; //TProfile.Addpoint

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function TProfile.Gety (inx : Double) : Double;
var
 Temp : PPoint;
 Gotit : Boolean;
 Tempy : Double;
begin
  if NOT Modifying then
  if (NOT ProfileOk) or (NoPoints < 2) then
   begin
    Gety := -1;
    Exit;
   end;
  Tempy := 0;
  if Modifying then Temp := OldFirst else Temp := First;
  Gotit := FALSE;

  repeat
    if (inx < Temp^.x) then
     begin
      Tempy := First^.y;
      Gotit := TRUE;
     end
    else if (Temp^.next = nil) then
     begin
      Tempy := Temp^.y;
      Gotit := TRUE;
     end
    else if inx <= Temp^.next^.x then
     begin
      Tempy := Temp^.y + ((inx-Temp^.x)/
               (Temp^.next^.x - Temp^.x) *
               (Temp^.next^.y - Temp^.y)); //Interpolate
      Gotit := TRUE;
     end
    else Temp := Temp^.next;
  until Gotit;
  Gety := Tempy;
end; //TProfile.Gety

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Function TProfile.PointToText : ShortString;
begin
 PointToText := '(' + FloatToStrF (Current^.x, fffixed, 8, 2) + ',' +
                FloatToStrF (Current^.y, fffixed, 8, 3) + ')';
end; //TProfile.PointToText

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Procedure TProfile.LoadText (inFileName : String);
var
 TF : TextFile;
 inx, iny : Double;
begin
  try
   AssignFile (TF, inFileName);
   Reset (TF);
  except
   ShowMessage ('Cannot Find cam file : ' + inFilename);
  end;
  Try
   Clear;
   while NOT EOF (TF) do
    begin
     Readln (TF, inx, iny);
     AddPoint (inx, iny);
    end;
  Except
    on EInOutError do
     begin
      MessageDlg ('Error loading file (' + Filename + ')' + #13 +
                  'File format incorrect' , mtwarning, [mbok], 0);
      CloseFile(TF);
      Exit;
     end; // on EInOutError
  end; //Try Except
  ProfileOK := TRUE;
  Filename := infilename;
  CloseFile (TF);
  GetLimits;
end; //TProfile.LoadText

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Procedure TProfile.SaveText (inFileName : String);
var
 TF : TextFile;
 inX, inY, inCd : Double;
begin
  if Not ProfileOK then Exit;
  AssignFile (TF, inFileName);
  Rewrite (TF);
  Try
   Current := First;
   repeat
     Writeln (TF, Current^.x:10:3, Current^.y:10:3);
     Current := Current^.next;
   until Current = nil;
  Except
    on EInOutError do
     begin
      MessageDlg ('Error saving file (' + Filename + ')' + #13 +
                  'Disk Error' , mtwarning, [mbok], 0);
      CloseFile(TF);
      Exit;
     end; // on EInOutError
  end; //Try Except
  Filename := infilename;
  CloseFile (TF);
end;  //TProfile.SaveText

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Procedure TProfile.Scale (inXMin, inXMax, inYmin, inYMax : Double;
                          inNoPoints : Integer);
var
 LC : Integer; // Loop Counter
 Xincr : Double;
 tx, ty : Double; // Temp values
 Temp : PPoint;
begin
  Modifying := TRUE;
  OldFirst := First;
  NoPoints := 0;
  XIncr := (inxMax - inXmin) / (inNoPoints -1);
  for LC := 1 to inNoPoints do
   begin
    tx := inXMin + (LC-1)*XIncr;
    ty := GetY (XMin + (LC-1)/(inNoPoints-1)*(XMax-XMin));
    ty := ty * (inYMax-inYMin)/(YMax-YMin);
    ty := ty + inYMin;
    AddPoint (tx, ty);
   end;
  Modifying := FALSE;

 //Chuck old Profile
  while OldFirst^.next <> nil do
    begin
      Temp := OldFirst^.next;
      Dispose (OldFirst);
      OldFirst := Temp;
    end; // while
  Dispose (OldFirst);

  FileName := 'Scaled : ' + FileName;
end; //TProfile.Scale

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Procedure TProfile.Clear;
var Temp : PPoint;
begin
  if NoPoints > 0 then
   begin
    while First^.next <> nil do
     begin
       Temp := First^.next;
       Dispose (First);
       First := Temp;
     end; // while
    Dispose (First);
   end; // if NoPoints
   NoPoints := 0;
   ProfileOK := FALSE;
end; //TProfile.Clear

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

destructor TProfile.Destroy;
begin
  Clear;
  inherited;
end; // Destructor Destroy

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
end.


