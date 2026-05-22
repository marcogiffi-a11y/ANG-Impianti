[Setup]
AppName=ANG-Impianti AI
AppVersion=1.0
AppPublisher=Studio Athena
DefaultDirName={autopf}\ANGImpianti
DefaultGroupName=ANG-Impianti
OutputDir=installer
OutputBaseFilename=ANGImpianti_Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
DisableDirPage=yes

[Languages]
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"

[Files]
Source: "ANGImpianti.bundle\*"; DestDir: "{commonappdata}\Autodesk\ApplicationPlugins\ANGImpianti.bundle"; Flags: ignoreversion recursesubdirs createallsubdirs

[Messages]
WelcomeLabel1=Installazione ANG-Impianti AI
WelcomeLabel2=Questo programma installerà il plugin AI per AutoCAD.%n%nChiudi AutoCAD prima di continuare.
FinishedLabel=ANG-Impianti AI installato! Apri AutoCAD per iniziare.
