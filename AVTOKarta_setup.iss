; Inno Setup Script for AVTOKarta v1.1.0
; Requires Inno Setup 6+

#define MyAppName "АВТОКАРТА МЧС"
#define MyAppVersion "1.1.0"
#define MyAppPublisher "WebARTup - Studio: Technologies"
#define MyAppCopyright "Copyright © 2026 WebARTup - Studio: Technologies"
#define MyAppExeName "AVTOKarta.exe"
#define MyAppSource "AVTOKarta\bin\Release"
#define MyAppLicense "LICENSE"
#define MyAppURL "https://github.com/hedgehog200/avtokarta"

[Setup]
AppId={{A7F3B2C1-5E4D-4F6A-8B9C-0D1E2F3A4B5C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppCopyright={#MyAppCopyright}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\AVTOKarta
DefaultGroupName={#MyAppName}
OutputDir=Output
OutputBaseFilename=AVTOKarta_v{#MyAppVersion}_setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
PrivilegesRequired=lowest
ArchitecturesAllowed=x86compatible
ArchitecturesInstallIn64BitMode=
DisableProgramGroupPage=yes
LicenseFile={#MyAppLicense}
Password=reApCeVAfuHV
Encryption=yes
VersionInfoVersion={#MyAppVersion}.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoCopyright={#MyAppCopyright}
VersionInfoDescription={#MyAppName}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
SetupIconFile={#SourcePath}\AVTOKarta\app.ico
UninstallDisplayName={#MyAppName}

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

[Files]
Source: "{#MyAppSource}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyAppSource}\ClosedXML.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyAppSource}\DocumentFormat.OpenXml.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyAppSource}\ExcelNumberFormat.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyAppSource}\FastMember.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyAppSource}\HandyControl.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyAppSource}\HandyControl.resources.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyAppSource}\Newtonsoft.Json.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyAppSource}\System.IO.Packaging.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyAppLicense}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
