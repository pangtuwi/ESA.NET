//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
// IniFile Monitor : Engine Simulation and Analysis (ESA)
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

unit IniValues;

interface

uses
 IniFiles;

const
 IniFilename = 'ESA.ini';

Procedure LoadInivalues;
Procedure SaveIniValues;

var
 iniErrorLogfileName,
 iniTextSavefileName,
 iniEngineFileName,
 iniEngineSpeed,
 iniNoCycles,
 iniNo1zCycles,
 iniMassBalance : String;


//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

implementation

Procedure LoadInivalues;
var IniF : Tinifile;
begin
 IniF := TIniFile.Create (inifilename);
 with IniF do
  begin
   iniErrorLogFileName := ReadString('DefaultFiles', 'ErrorLog', 'ESA2z1z.err');
   iniTextSavefileName := ReadString('DefaultFiles', 'TextSave', 'Lastcyc.txt');
   iniEnginefileName := ReadString('DefaultFiles', 'Engine', 'Default.eng');
   iniEngineSpeed := ReadString ('Simulation', 'EngineSpeed', '4000');
   iniNoCycles := ReadString ('Simulation', 'Nocycles', '6');
   iniNo1zCycles := ReadString ('Simulation', 'No1zcycles', '1');
   iniMassBalance := ReadString ('Simulation', 'MassBalance', '1');
  end;
 IniF.Free;
end; //LoadiniValues

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//

Procedure SaveIniValues;
begin
end; //SaveIniValues

//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~//
end.
