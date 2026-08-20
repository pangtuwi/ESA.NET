 ----------------------------------------
 Advanced Calculator v4.1 for Delphi
 Copyright 1999-2000 by George Barbakadze
 All rights reserved

 http://adcalc.homepage.com
 giga@fnmail.com
 ----------------------------------------

 Compatible with Delphi4, Delphi5

 A fast expression evaluator which can be used to evaluate extended,
 boolean and string type expressions at run-time. There is no restriction
 in depth or complexity of expression. Saves in file and loades from file
 a library of registered variables and functions. Supports complete error
 handling.

 1. An expression can contain:

 Arithmetical operations ( +, -, *, /, ^, (...) );
 Comparison operations ( >, <, =, <>, >=, <= ) for Extended an String types;
 Logical operators (And, Or);
 More than 30 standard functions of Extended, Boolean and string type;
 Unlimited number of user-defined variables and functions editable at
 design-time and at run-time, no restriction in an amount of variables
 and functions and their parameters.

 2. Default functions

 Functions of EXTENDED and REAL type:
 Frac, Int, Round, Exp, Log, Ln,
 Sin, Cos, ArcSin, ArcCos, Sinh, Cosh, ArcSinh, ArcCosh,
 Tan, CoTan, ArcTan, ArcTanh, Tanh,
 Sqr, Sqrt, Abs.

 Ranges of these functions in full likeness with same name functions of
 Delphi.

 Function If(logical_test, value_if_true, value_if_false)
 Logical_test - is any value or expression that can be evaluated to TRUE or FALSE.
 Value_if_true - is the value or expression that is returned if logical_test is True.
 Value_if_false - is the value or expression that is returned if logical_test is False.

 Function of BOOLEAN type: Not(logical_value) returns opposite value of
 boolean type.

 Functions for work with STRING types:
 Length, Pos, LowerCase, AnsiLowerCase, UpperCase, AnsiApperCase,
 Copy, StrToFloat, FloatToStr.


 3. Aditional tools

 1) It is possible to evaluate a text box of expressions, appropriating
 values to variables of unlimited amount;
 2) It is possible to organize connection between variables of your
 program (global or local) and variables of TAdCalc so that appropriating
 values to one from them, was reflected on other;
 3) In a text box of expression it is possible to use the token " ' " for
 comments;
 4) It is possible to compile a text box of expression for increase of
 velocity of further repeated evaluations.

 4. Codes and messages of error

 1. Line is Long;
 2. Extended type expression expected;
 3. Boolean type expression expected;
 4. String type expression expected;
 5. " expected;
 6. "+" expected;
 7. Arithmetical token expected;
 8. Comparison token expected;
 9. "AND" or "OR" expected;
 10. "(" expected;
 11. ")" expected;
 12. "," expected;
 13. Division by zero;
 14. The number of parameter is out of range;
 15. Variable name expected;
 16. "=" expected;
 17. ";" expected.


 5. Installation

 To use this component in your applications:
 Copy the following files in a directory of your choice:
 1) AdCReg.pas
 2) AdCReg.dcr
 3) AdCalc.pas or AdCalc.dcu
 4) AdCMsg.pas
 Then: Component/Install Component/Browse/AdCReg.pas/Ok/Compile

 6. What's new

 Version 4.0.
 Increase of velocity of the analyzer in cyclical evaluations of a formula with the help of preliminary compilation of expression.
 Current version 4.1.
 Is added the new base type - STRING.
