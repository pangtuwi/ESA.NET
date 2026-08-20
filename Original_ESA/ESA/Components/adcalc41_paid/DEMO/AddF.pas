unit AddF;

interface

uses
  Windows, Messages, SysUtils, Classes, Graphics, Controls, Forms, Dialogs,
  StdCtrls;

type
  TAddFunct = class(TForm)
    Button1: TButton;
    Button2: TButton;
    Memo1: TMemo;
    Edit1: TEdit;
    Label1: TLabel;
    ComboBox1: TComboBox;
    Label2: TLabel;
    ListBox1: TListBox;
    Label3: TLabel;
    ComboBox2: TComboBox;
    Button3: TButton;
    Button4: TButton;
    Fo: TLabel;
    Edit2: TEdit;
    Label4: TLabel;
    procedure Button3Click(Sender: TObject);
    procedure FormCreate(Sender: TObject);
    procedure Button4Click(Sender: TObject);
  private
    { Private declarations }
  public
    { Public declarations }
    procedure ClearFields;
  end;

var
  AddFunct: TAddFunct;

implementation

uses Main;

{$R *.DFM}

procedure TAddFunct.Button3Click(Sender: TObject);
begin
  ListBox1.Items.Add(ComboBox2.Items[ComboBox2.ItemIndex]);
end;

procedure TAddFunct.FormCreate(Sender: TObject);
begin
  ComboBox1.ItemIndex:=0;
  ComboBox2.ItemIndex:=0;
  Memo1.WordWrap:=false; {Is necessary for correct returning of error place}
end;

procedure TAddFunct.ClearFields;
begin
  Edit1.Text:='';
  Edit2.Text:='';
  Combobox1.ItemIndex:=0;
  Combobox2.ItemIndex:=0;
  ListBox1.Items.Clear;
  Memo1.Lines.Clear;
end;

procedure TAddFunct.Button4Click(Sender: TObject);
var
  i:integer;
begin
  i:=ListBox1.ItemIndex;
  with ListBox1 Do if ItemIndex>-1 then Items.Delete(ItemIndex);
  if i>ListBox1.Items.Count-1 then
    ListBox1.ItemIndex:=ListBox1.Items.Count-1
  else ListBox1.ItemIndex:=i;
end;

end.

