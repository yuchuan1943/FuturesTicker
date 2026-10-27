; ============================================================
; FuturesTicker — Inno Setup 安装脚本
; 用法:
;   1. dotnet publish -c Release      （生成发布产物）
;   2. 用 Inno Setup Compiler 打开此文件，点击「编译」
;   3. 输出 installer\FuturesTicker-{版本号}-setup.exe
; ============================================================

#define MyAppName       "FuturesTicker"
#define MyAppVersion    "1.0.2"
#define MyAppPublisher  "FuturesTicker Contributors"
#define MyAppURL        "https://github.com/yuchuan1943/FuturesTicker"
#define MyAppExeName    "FuturesTicker.exe"

; 发布产物路径（SelfContained + win-x64）
#define MySourcePath    "bin\Release\net10.0-windows\publish"

[Setup]
AppId={{EBEAF91D-77ED-439A-9A5D-C3DB2C3E661F}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppSupportURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=installer
OutputBaseFilename=FuturesTicker-{#MyAppVersion}-setup
SetupIconFile=app.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; 仅允许安装在 64 位系统上（对应 win-x64）
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "chinese"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "额外图标:"; Flags: unchecked

[Files]
Source: "{#MySourcePath}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MySourcePath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}";          Filename: "{app}\{#MyAppExeName}"
Name: "{group}\卸载 FuturesTicker";      Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}";    Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 FuturesTicker"; Flags: nowait postinstall skipifsilent
