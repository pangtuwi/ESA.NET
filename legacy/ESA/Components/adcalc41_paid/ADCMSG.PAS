unit AdCMsg;

interface
uses Dialogs;

type
  TExpectionError=array[1..17] of string;

const
  ExpectionError:TExpectionError=(
    ('Line is Long'),                            {1}
    ('Extended type expression expected'),       {2}
    ('Boolean type expression expected'),        {3}
    ('String type expression expected'),         {4}
    ('" expected'),                              {5}
    ('"+" expected'),                            {6}
    ('Arithmetical token expected'),             {7}
    ('Comparison token expected'),               {8}
    ('"AND" or "OR" expected'),                  {9}
    ('"(" expected'),                            {10}
    ('")" expected'),                            {11}
    ('"," expected'),                            {12}
    ('Division by zero'),                        {13}
    ('The number of parameter is out of range'), {14}
    ('Variable name expected'),                  {15}
    ('"=" expected'),                            {16}
    ('";" expected'));                           {17}

  procedure ShowIdNameMissingMessage;
  procedure ShowResWordMessage(IdName:string);
  procedure ShowIncorrectIdNameMessage;
  procedure ShowCanNotCreateFileMessage(FileName:string);
  procedure ShowErrorInFileMessage;
  procedure ShowCanNotOpenFileMessage(FileName:string);

implementation

procedure ShowIdNameMissingMessage;
{This message is arisen, when identifier of a variable
 or function is an empty line}
begin
  ShowMessage('Identifier name missing');
end;

procedure ShowResWordMessage(IdName:string);
{This message is arisen, when by the identifier of variable
 or function, is used a reserved word (param, and, or, true, false)}
begin
  ShowMessage('"'+IdName+'" is a reserved word');
end;

procedure ShowIncorrectIdNameMessage;
{This message is arisen, when the identifier of function
 or variable begins by a number or contains inadmissible symbols}
begin
  ShowMessage('Incorrect identifier name');
end;

procedure ShowCanNotCreateFileMessage(FileName:string);
{This message is arisen, when for any reasons, it is not possible
 to create the file of library of variables and functions}
begin
  ShowMessage('Can not create file '+FileName);
end;

procedure ShowErrorInFileMessage;
{This message is arisen, when the version number of file format
 is out of allowed range}
begin
  ShowMessage('Error in file');
end;

procedure ShowCanNotOpenFileMessage(FileName:string);
{This message is arisen, when for any reasons, it is not possible
 to open the file of library of variables and functions}
begin
  ShowMessage('Can not open file '+FileName);
end;

end.
