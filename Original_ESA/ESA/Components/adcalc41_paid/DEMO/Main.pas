unit Main;

interface

uses
  Windows, Messages, SysUtils, Classes, Graphics, Controls, Forms, Dialogs,
  StdCtrls, AdCalc, Menus, ExtCtrls, ComCtrls, TeEngine, Series, TeeProcs,
  Chart;

const
  ArgumentVar:string='x';

  Memo2Handle   = 0;
  Memo1Handle   = 1;
  Edit1Handle   = 2;
  Edit2Handle   = 3;
  Edit3Handle   = 4;
  MessageHandle = 5;
  
type
  TMainForm = class(TForm)
    PageControl1: TPageControl;
    TabSheet1: TTabSheet;
    TabSheet2: TTabSheet;
    Label1: TLabel;
    Label6: TLabel;
    Label7: TLabel;
    Memo1: TMemo;
    Memo2: TMemo;
    Panel1: TPanel;
    Label4: TLabel;
    Button2: TButton;
    Button3: TButton;
    Button4: TButton;
    ListBox1: TListBox;
    Panel2: TPanel;
    Label5: TLabel;
    Button5: TButton;
    Button6: TButton;
    Button7: TButton;
    ListBox2: TListBox;
    Button1: TButton;
    Chart1: TChart;
    Series1: TFastLineSeries;
    Panel3: TPanel;
    Label2: TLabel;
    Label3: TLabel;
    Label8: TLabel;
    Label9: TLabel;
    Edit1: TEdit;
    Edit2: TEdit;
    ComboBox1: TComboBox;
    Button10: TButton;
    Edit3: TEdit;
    Label10: TLabel;
    CheckBox1: TCheckBox;
    Label11: TLabel;
    AdCalc1: TAdCalc;
    Label12: TLabel;
    Label13: TLabel;
    Button11: TButton;
    Button12: TButton;
    ComboBox2: TComboBox;
    PopupMenu1: TPopupMenu;
    Save1: TMenuItem;
    Evaluateextended1: TMenuItem;
    Evaluateboolean1: TMenuItem;
    Evaluatestring1: TMenuItem;
    Label14: TLabel;
    procedure Button1Click(Sender: TObject);
    procedure FormCreate(Sender: TObject);
    procedure Button2Click(Sender: TObject);
    procedure Button3Click(Sender: TObject);
    procedure ListBox1DblClick(Sender: TObject);
    procedure Button4Click(Sender: TObject);
    procedure Button5Click(Sender: TObject);
    procedure Button7Click(Sender: TObject);
    procedure Button6Click(Sender: TObject);
    procedure ListBox2DblClick(Sender: TObject);
    procedure AdCalc1FunctError(Sender: TObject; FunctName: String;
      ErrorLine, ErrorPosition, ErrorPlace, ErrorCode: Integer;
      ErrorStr: String);
    procedure AdCalc1Error(Sender: TObject; ErrorLine, ErrorPosition,
      ErrorPlace, ErrorCode: Integer; ErrorStr: String; Handle: integer);
    procedure Button10Click(Sender: TObject);
    procedure ComboBox1Change(Sender: TObject);
    procedure AdCalc1GetExtendedVar(Sender: TObject; VarName: String;
      var Found: Boolean; var Value: Extended);
    procedure AdCalc1FunctList(Sender: TObject; FunctName: String;
      FunctType: TExprType; Formula: TStrings; Params: array of TExprType;
      Description: String);
    procedure AdCalc1VarList(Sender: TObject; VarName: String;
      VarType: TExprType; Value: Pointer; Description: String);
    procedure ListBox1Click(Sender: TObject);
    procedure ListBox2Click(Sender: TObject);
    procedure ListBox2Enter(Sender: TObject);
    procedure ListBox1Enter(Sender: TObject);
    procedure Button11Click(Sender: TObject);
    procedure Button12Click(Sender: TObject);
    procedure AdCalc1GetStringFunct(Sender: TObject; FunctName: String;
      var Found: Boolean; var Value: String);
    procedure Evaluateextended1Click(Sender: TObject);
    procedure Evaluateboolean1Click(Sender: TObject);
    procedure Evaluatestring1Click(Sender: TObject);
    procedure Save1Click(Sender: TObject);
    procedure ComboBox2Click(Sender: TObject);
  private
    { Private declarations }
    procedure RefreshVarList;
    procedure RefreshFunctList;
    procedure ApplyFunction;
    function GetVarName:string;
    procedure MakeChart;
    procedure RefreshLib;
    procedure RefreshCombo;
  public
    { Public declarations }
  end;

var
  MainForm: TMainForm;

implementation

uses AddF, AddV;

{$R *.DFM}

procedure TMainForm.RefreshVarList;
var
  i:integer;
begin
  ComboBox1.Items.Clear;
  i:=ListBox2.ItemIndex;
  ListBox2.Items.Clear;
  AdCalc1.GetVarList;
  if i>ListBox2.Items.Count-1 then
    ListBox2.ItemIndex:=ListBox2.Items.Count-1
  else ListBox2.ItemIndex:=i;
  ListBox2Click(AdCalc1);
end;

procedure TMainForm.RefreshFunctList;
var
  i:integer;
begin
  i:=ListBox1.ItemIndex;
  ListBox1.Items.Clear;
  AdCalc1.GetFunctList;
  if i>ListBox1.Items.Count-1 then
    ListBox1.ItemIndex:=ListBox1.Items.Count-1
  else ListBox1.ItemIndex:=i;
  ListBox1Click(AdCalc1);
end;

procedure TMainForm.FormCreate(Sender: TObject);
begin
  Memo1.WordWrap:=false; {Is necessary for correct returning of error place}
  Memo2.WordWrap:=false;
  PageControl1.ActivePage:=TabSheet1;
  ActiveControl:=Memo1;
  with AdCalc1 do begin
    {The method declares function, which is determined in
    event OnGetFunct at design-time}
    DefStringFunct('IfStr',[expBoolean,expString,expString]);
  end;
  RefreshCombo;
  RefreshLib;
end;

procedure TMainForm.ApplyFunction;
var
  FParams:array of TExprType;
  i:integer;
begin
  with AddFunct do begin
    if Edit1.Text='' then begin
      ShowMessage('Function name missing');
      Exit;
    end;
    if Memo1.Lines.Count=0 then begin
      ShowMessage('Function expression missing');
      Exit;
    end;
    SetLength(FParams,ListBox1.Items.Count);
    with ListBox1 do for i:=0 to Items.Count-1 do begin
      if Items[i]=ComboBox2.Items[0] then FParams[i]:=expExtended;
      if Items[i]=ComboBox2.Items[1] then FParams[i]:=expBoolean;
      if Items[i]=ComboBox2.Items[2] then FParams[i]:=expString;
    end;
    if FParams<>nil then begin
      case ComboBox1.ItemIndex of
        {The method declares function, which is determined
        in field "Formula" at run-time}
        0:MainForm.AdCalc1.RegExtendedFunct(Edit1.Text,
          FParams,Memo1.Lines,Edit2.Text);
        1:MainForm.AdCalc1.RegBooleanFunct (Edit1.Text,
          FParams,Memo1.Lines,Edit2.Text);
        2:MainForm.AdCalc1.RegStringFunct (Edit1.Text,
          FParams,Memo1.Lines,Edit2.Text);
      end;
      ClearFields;
    end
    else begin
      ShowMessage('Function parameters missing');
      Exit;
    end;
    RefReshFunctList;
  end;
end;

procedure TMainForm.Button2Click(Sender: TObject);
begin
  with AddFunct do begin
    ClearFields;
    Caption:='Add function';
    Edit1.Enabled:=true;
    ShowModal;
    if ModalResult=mrOk then ApplyFunction;
  end;
end;

procedure TMainForm.Button3Click(Sender: TObject);
var
  FN,Description:string;
  FT:TExprType;
  Formula:TStrings;
  i:integer;
begin
  try
    Formula:=TStringList.Create;
    if (ListBox1.ItemIndex>-1)and(ListBox1.Items.Count>0) then begin
      FN:=ListBox1.Items[ListBox1.ItemIndex];
      if AdCalc1.GetFunctProperties(FN,FT,Formula,Description) then
      with AddFunct do begin
        Caption:='Edit function';
        Edit1.Text:=FN;
        Edit1.Enabled:=false;
        Edit2.Text:=Description;
        case FT of
          expExtended : ComboBox1.ItemIndex:=0;
          expBoolean  : ComboBox1.ItemIndex:=1;
          expString   : ComboBox1.ItemIndex:=2;
        end;
        Memo1.Lines.Clear;
        for i:=0 to Formula.Count-1 do
          Memo1.Lines.Add(Formula[i]);
        ListBox1.Clear;
        for i:=0 to High(AdCalc1.FunctParams) do
          {Dinamic array Variable "FunctParams" accepts values of
          parameters of the given function. This is used only after
          call of function "GetFunctProperties"}
          case AdCalc1.FunctParams[i] of
            expExtended : ListBox1.Items.Add(ComboBox2.Items[0]);
            expBoolean  : ListBox1.Items.Add(ComboBox2.Items[1]);
            expString   : ListBox1.Items.Add(ComboBox2.Items[2]);
          end;
        ShowModal;
        if ModalResult=mrOk then ApplyFunction;
      end;
    end;
  finally
    Formula.Destroy;
  end;
end;

procedure TMainForm.ListBox1DblClick(Sender: TObject);
begin
  Button3Click(Sender);
end;

procedure TMainForm.Button4Click(Sender: TObject);
var
  i:integer;
begin
  with ListBox1 do if (ItemIndex>-1)and
    (ListBox1.Items.Count>0) then with AdCalc1 do begin
    if MessageDlg('Are you sure to delete function "'+
      UpperCase(ListBox1.Items[ListBox1.ItemIndex])+'"',
      mtConfirmation, [mbYes, mbNo], 0) = mrYes then begin
      RemoveFunct(Items[ItemIndex]);
      i:=ListBox1.ItemIndex;
      RefReshFunctList;
      RefReshVarList;
      if i>ListBox1.Items.Count-1 then
        ListBox1.ItemIndex:=ListBox1.Items.Count-1
      else ListBox1.ItemIndex:=i;
    end;
  end;
end;

function TMainForm.GetVarName:string;
var
  i:integer;
begin
  Result:='';
  with ListBox2 do if (ItemIndex>-1)and
    (Items.Count>0) then begin
    i:=1;
    while (Items[ItemIndex][i]<>#32)and
          (i<=Length(Items[ItemIndex])) do begin
      Result:=Result+Items[ItemIndex][i];
      inc(i);
    end;
  end;
end;

procedure TMainForm.Button5Click(Sender: TObject);
var
  EV:extended;
  BV:boolean;
  SV:string;
  Formula:TStrings;
begin
  with AddVar do begin
    Caption:='Add variable';
    Label4.Visible:=false;
    Label5.Visible:=false;
    ClearFields;
    Edit1.Enabled:=true;
    ComboBox1.Enabled:=true;
    ActiveControl:=Edit1;
    ShowModal;
    if ModalResult=mrOk then begin
      Formula:=TStringList.Create;
      Formula.Clear;
      Formula.Add(Edit3.Text);
      try
        case ComboBox1.ItemIndex of
          0:begin
            AdCalc1.RegVariable(Edit1.Text,expExtended,Edit2.Text);
            if Edit3.Text<>'' then
              if AdCalc1.GetExtendedResult(Formula,EV,MessageHandle) then
                AdCalc1.SetExtendedVarValue(Edit1.Text,EV);
          end;
          1:begin
            AdCalc1.RegVariable(Edit1.Text,expBoolean,Edit2.Text);
            if Edit3.Text<>'' then
              if AdCalc1.GetBooleanResult(Formula,BV,MessageHandle) then
                AdCalc1.SetBooleanVarValue(Edit1.Text,BV);
          end;
          2:begin
            AdCalc1.RegVariable(Edit1.Text,expString,Edit2.Text);
            if Edit3.Text<>'' then
              if AdCalc1.GetStringResult(Formula,SV,MessageHandle) then
                AdCalc1.SetStringVarValue(Edit1.Text,SV);
          end;
        end;
      finally
        RefReshVarList;
        Formula.Destroy;
      end;
    end;
  end;
end;

procedure TMainForm.Button7Click(Sender: TObject);
var
  i:integer;
  VN:string;
begin
  VN:=GetVarName;
  if VN<>'' then with AdCalc1 do begin
    if MessageDlg('Are you sure to delete variable "'+VN+'"',
      mtConfirmation, [mbYes, mbNo], 0) = mrYes then begin
      RemoveVar(VN);
      i:=ListBox2.ItemIndex;
      RefReshVarList;
      if i>ListBox2.Items.Count-1 then
        ListBox2.ItemIndex:=ListBox2.Items.Count-1
      else ListBox2.ItemIndex:=i;
    end;
  end;
end;

procedure TMainForm.Button6Click(Sender: TObject);
const
  BoolWords: array[Boolean] of string = ('False', 'True');
var
  VN,Description:string;
  VT:TExprType;
  Value:pointer;
  EV:extended;
  BV:boolean;
  SV:string;
  Formula:TStrings;
begin
  VN:=GetVarName;
  if VN<>'' then begin
    if AdCalc1.GetVarProperties(VN,VT,Value,Description) then
    with AddVar do begin
      Caption:='Edit variable';
      Label4.Visible:=true;
      Label5.Visible:=true;
      Edit1.Enabled:=false;
      ComboBox1.Enabled:=false;
      Edit1.Text:=VN;
      Edit2.Text:=Description;
      case VT of
        expExtended:begin
          ComboBox1.ItemIndex:=0;
          Label5.Caption:=FloatToStr(extended(Value^));
        end;
        expBoolean :begin
          ComboBox1.ItemIndex:=1;
          Label5.Caption:=BoolWords[boolean(Value^)];
        end;
        expString :begin
          ComboBox1.ItemIndex:=2;
          Label5.Caption:='"'+string(Value^)+'"';
        end;
      end;
      ShowModal;
      if (ModalResult=mrOk)and(Edit1.Text<>'') then begin
        Formula:=TStringList.Create;
        Formula.Clear;
        Formula.Add(Edit3.Text);
        try
          case VT of
            expExtended:begin
              AdCalc1.SetVarDescription(VN,Edit2.Text);
              if Edit3.Text<>'' then
                if AdCalc1.GetExtendedResult(Formula,EV,MessageHandle) then begin
                  AdCalc1.SetExtendedVarValue(VN,EV);
                  ClearFields;
                end;
            end;
            expBoolean :begin
              AdCalc1.SetVarDescription(VN,Edit2.Text);
              if Edit3.Text<>'' then
                if AdCalc1.GetBooleanResult(Formula,BV,MessageHandle) then begin
                  AdCalc1.SetBooleanVarValue(VN,BV);
                  ClearFields;
                end;
            end;
            expString :begin
              AdCalc1.SetVarDescription(VN,Edit2.Text);
              if Edit3.Text<>'' then
                if AdCalc1.GetStringResult(Formula,SV,MessageHandle) then begin
                  AdCalc1.SetStringVarValue(VN,SV);
                  ClearFields;
                end;
            end;
          end;
        finally
          RefReshVarList;
          Formula.Destroy;
        end;
      end;
    end;
  end;
end;

procedure TMainForm.ListBox2DblClick(Sender: TObject);
begin
  Button6Click(Sender);
end;

procedure TMainForm.AdCalc1FunctError(Sender: TObject; FunctName: String;
  ErrorLine, ErrorPosition, ErrorPlace, ErrorCode: Integer;
  ErrorStr: String);
begin
  ShowMessage('Function - "'+UpperCase(FunctName)+
    '"; Line - '+IntToStr(ErrorLine)+
    '; Col - '+IntToStr(ErrorPosition)+'; '+ErrorStr);
end;

procedure TMainForm.AdCalc1Error(Sender: TObject; ErrorLine, ErrorPosition,
  ErrorPlace, ErrorCode: Integer; ErrorStr: String; Handle: integer);
begin
  case Handle of
    Memo2Handle:begin
      ActiveControl:=Memo2;
      Label1.Caption:='Error '+IntToStr(ErrorCode)+', Line '+
        IntToStr(ErrorLine)+', '+ErrorStr;
      Memo2.SelStart:=ErrorPlace;
      Memo2.SelLength:=0;
    end;
    Memo1Handle:begin
      ActiveControl:=Memo1;
      Label1.Caption:='Error '+IntToStr(ErrorCode)+', Line '+
        IntToStr(ErrorLine)+', '+ErrorStr;
      Memo1.SelStart:=ErrorPlace;
      Memo1.SelLength:=0;
    end;
    Edit1Handle:begin
      ActiveControl:=Edit1;
      Label1.Caption:=ErrorStr+'; Col - '+IntToStr(ErrorPlace);
      Edit1.SelStart:=ErrorPosition;
      Edit1.SelLength:=0;
    end;
    Edit2Handle:begin
      ActiveControl:=Edit2;
      Label1.Caption:=ErrorStr+'; Col - '+IntToStr(ErrorPlace);
      Edit2.SelStart:=ErrorPosition;
      Edit2.SelLength:=0;
    end;
    Edit3Handle:begin
      ActiveControl:=Edit3;
      Label1.Caption:=ErrorStr+'; Col - '+IntToStr(ErrorPlace);
      Edit3.SelStart:=ErrorPosition;
      Edit3.SelLength:=0;
    end;
    MessageHandle:begin
      ShowMessage(ErrorStr+'; Col - '+IntToStr(ErrorPosition));
    end;
  end;
end;

procedure TMainForm.MakeChart;
var
  X,FX,MinX,MaxX,DeltaX:extended;
  Strs:TStrings;
begin
  Strs:=TStringList.Create;
  try
    if AdCalc1.GetBlockResult(Memo2.Lines,Memo2Handle) then begin
      if AdCalc1.ConnectExtendedVar(ArgumentVar,X,pvAdCalc) then begin
        try
          Strs.Clear;
          Strs.Add(Edit1.Text);
          if AdCalc1.GetExtendedResult(Strs,MinX,Edit1Handle) then begin
            Strs.Clear;
            Strs.Add(Edit2.Text);
            if AdCalc1.GetExtendedResult(Strs,MaxX,Edit2Handle) then begin
              Strs.Clear;
              Strs.Add(Edit3.Text);
              if AdCalc1.GetExtendedResult(Strs,DeltaX,Edit3Handle) then begin
                X:=MinX;
                Series1.Clear;
                AdCalc1.CompileText(Memo1.Lines,Memo1Handle);
                while X<=MaxX do begin
                  if AdCalc1.ExecuteExtended(FX,Memo1Handle) then begin
                    Series1.Add(FX,FloatToStr(X),clRed);
                    X:=X+DeltaX;
                  end
                  else Exit;
                end;
                PageControl1.ActivePage:=TabSheet2;
              end;
            end;
          end;
        finally
          AdCalc1.ClearExpressions;
          if CheckBox1.Checked then
            AdCalc1.DisconnectVar(ArgumentVar,pvAdCalc)
          else AdCalc1.DisconnectVar(ArgumentVar,pvCode);
          RefReshVarList;
        end;
      end;
    end;
  finally
    Strs.Destroy;
  end;
end;

procedure TMainForm.Button10Click(Sender: TObject);
begin
  Label1.Caption:='';
  MakeChart;
end;

procedure TMainForm.ComboBox1Change(Sender: TObject);
begin
  ArgumentVar:=LowerCase(ComboBox1.Text);
end;

procedure TMainForm.AdCalc1GetExtendedVar(Sender: TObject; VarName: String;
  var Found: Boolean; var Value: Extended);
begin
  if VarName=LowerCase('Argument') then
    if AdCalc1.GetExtendedVarValue(ArgumentVar,Value) then Found:=true;
end;

procedure TMainForm.AdCalc1FunctList(Sender: TObject; FunctName: String;
  FunctType: TExprType; Formula: TStrings; Params: array of TExprType;
  Description: String);
begin
  ListBox1.Items.Add(UpperCase(FunctName));
end;

procedure TMainForm.AdCalc1VarList(Sender: TObject; VarName: String;
  VarType: TExprType; Value: Pointer; Description: String);
const
  BoolWords: array[Boolean] of string = ('False', 'True');
var
  vs:string;
begin
  vs:='';
  case VarType of
    expExtended:begin
      if Value<>nil then vs:=FloatToStr(extended(Value^));
      ComboBox1.Items.Add(UpperCase(VarName));
      if ArgumentVar=VarName then
        ComboBox1.ItemIndex:=ComboBox1.Items.Count-1;
    end;
    expBoolean :if Value<>nil then vs:=BoolWords[boolean(Value^)];
    expString  :if Value<>nil then vs:='"'+string(Value^)+'"';
  end;
  ListBox2.Items.Add(UpperCase(VarName)+' = '+vs);
end;

procedure TMainForm.ListBox1Click(Sender: TObject);
var
  s:string;
begin
  with ListBox1 do if (ItemIndex>-1)and(Items.Count>0) then
    if AdCalc1.GetFunctDescription(Items[ItemIndex],s) then
      Label12.Caption:=s;
end;

procedure TMainForm.ListBox2Click(Sender: TObject);
var
  s,VN:string;
begin
  VN:=GetVarName;
  if AdCalc1.GetVarDescription(GetVarName,s) then
    Label12.Caption:=s;
end;

procedure TMainForm.ListBox2Enter(Sender: TObject);
begin
  Listbox1.ItemIndex:=-1;
end;

procedure TMainForm.ListBox1Enter(Sender: TObject);
begin
  Listbox2.ItemIndex:=-1;
end;

procedure TMainForm.Button1Click(Sender: TObject);
var
  n:Extended;
begin
  Label1.Caption:='';
  if AdCalc1.GetBlockResult(Memo2.Lines,Memo2Handle) then begin
    if AdCalc1.GetExtendedResult(Memo1.Lines,n,Memo1Handle) then begin
      Label1.Caption:='Result = '+FloatToStr(n);
      Memo1.SelectAll;
    end;
    RefReshVarList;
  end;
end;

procedure TMainForm.Button11Click(Sender: TObject);
const
  BoolWords: array[Boolean] of string = ('False', 'True');
var
  b:boolean;
begin
  Label1.Caption:='';
  if AdCalc1.GetBlockResult(Memo2.Lines,Memo2Handle) then begin
    if AdCalc1.GetBooleanResult(Memo1.Lines,b,Memo1Handle) then begin
      Label1.Caption:='Result = '+BoolWords[b];
      Memo1.SelectAll;
    end;
    RefReshVarList;
  end;
end;

procedure TMainForm.Button12Click(Sender: TObject);
var
  s:string;
begin
  Label1.Caption:='';
  if AdCalc1.GetBlockResult(Memo2.Lines,Memo2Handle) then begin
    if AdCalc1.GetStringResult(Memo1.Lines,s,Memo1Handle) then begin
      Label1.Caption:='Result = "'+s+'"';
      Memo1.SelectAll;
    end;
    RefReshVarList;
  end;
end;

procedure TMainForm.AdCalc1GetStringFunct(Sender: TObject;
  FunctName: String; var Found: Boolean; var Value: String);
begin
  if FunctName=LowerCase('IfStr') then with AdCalc1 do begin
    Found:=true;
    if boolean(GetParam(1)^) then Value:=string(GetParam(2)^)
    else Value:=string(GetParam(3)^);
  end;
end;

procedure TMainForm.RefreshLib;
begin
  try
    Label12.Caption:='';
    Memo2.Lines.LoadFromFile(ComboBox2.Text+'.var');
    Memo1.Lines.LoadFromFile(ComboBox2.Text+'.exp');
    AdCalc1.LoadLibrary(ComboBox2.Text+'.acl');
    RefReshVarList;
    RefReshFunctList;
  finally
    RefreshCombo;
  end;
end;

procedure TMainForm.Evaluateextended1Click(Sender: TObject);
begin
  Button1Click(Sender);
end;

procedure TMainForm.Evaluateboolean1Click(Sender: TObject);
begin
  Button11Click(Sender);
end;

procedure TMainForm.Evaluatestring1Click(Sender: TObject);
begin
  Button12Click(Sender);
end;

procedure TMainForm.Save1Click(Sender: TObject);
begin
  Memo2.Lines.SaveToFile(ComboBox2.Text+'.var');
  Memo1.Lines.SaveToFile(ComboBox2.Text+'.exp');
  AdCalc1.SaveLibrary(ComboBox2.Text+'.acl',CurrentSavingVersion);
  RefreshCombo;
end;

procedure TMainForm.RefreshCombo;
var
  sr: TSearchRec;
  FileAttrs,i: Integer;
  FName,s:string;
begin
  i:=-1;
  s:=ComboBox2.Text;
  ComboBox2.Items.Clear;
  FileAttrs := faAnyFile;
  if FindFirst('*.exp',FileAttrs,sr)=0 then repeat
    FName:=copy(sr.Name,1,Length(sr.Name)-
      Length(ExtractFileExt(sr.Name)));
    ComboBox2.Items.Add(Fname);
    if FName=s then i:=ComboBox2.Items.Count-1;
  until FindNext(sr)<>0;
  FindClose(sr);
  If (ComboBox2.Items.Count>0)and(i=-1) then begin
    ComboBox2.ItemIndex:=0;
    RefreshLib;
  end
  else ComboBox2.ItemIndex:=i;
end;

procedure TMainForm.ComboBox2Click(Sender: TObject);
begin
  RefreshLib;
end;

end.
