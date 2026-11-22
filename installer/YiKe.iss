#ifndef AppVersion
  #define AppVersion "2.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish-" + AppVersion
#endif
#include "model-downloads.iss"
[Setup]
#ifdef InstallerSmoke
AppId=YiKe.Installer.ModelSmoke
#else
AppId={{3C0A80D1-1257-472C-BC71-91BEE87C63F9}
#endif
AppName=译刻
AppPublisher=opens
VersionInfoCompany=opens
AppVersion={#AppVersion}
AppVerName=译刻 {#AppVersion}
VersionInfoDescription=译刻 · Windows AI 划词翻译
DefaultDirName={localappdata}\Programs\YiKe
#ifdef InstallerSmoke
DefaultGroupName=译刻安装验收
#else
DefaultGroupName=译刻
#endif
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19045
#ifdef InstallerSmoke
OutputDir=..\artifacts
OutputBaseFilename=YiKe-Installer-Smoke
#else
OutputDir=..\dist
OutputBaseFilename=YiKe-Setup-{#AppVersion}-win-x64
#endif
SetupIconFile=..\assets\yike.ico
UninstallDisplayIcon={app}\YiKe-Prism.ico
UninstallDisplayName=译刻
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern dynamic
WizardSizePercent=120,120
WizardImageFile=..\assets\wizard-1.bmp,..\assets\wizard-2.bmp
WizardImageFileDynamicDark=..\assets\wizard-1.bmp,..\assets\wizard-2.bmp
WizardImageStretch=yes
DisableWelcomePage=no
DisableDirPage=no
DisableProgramGroupPage=yes
DisableReadyPage=no
UsePreviousTasks=yes
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
Uninstallable=yes
ChangesEnvironment=no
#ifdef SignToolCommand
SignTool=YiKeSign
SignedUninstaller=yes
#ifdef SignedUninstallerPath
SignedUninstallerDir={#SignedUninstallerPath}
#endif
#endif
[Languages]
Name: "zhcn"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
[Messages]
zhcn.WelcomeLabel1=让理解，自然发生。
zhcn.WelcomeLabel2=译刻 {#AppVersion} · Windows 划词翻译%n%n选中文字，在原处读懂。%n%n已内置英译中节能引擎与免费词库，无需另外安装运行环境。%n%n其他语言可选下载通用模型，或自行配置在线 API。
zhcn.ButtonNext=继续(&N) >
zhcn.ButtonInstall=开始安装(&I)
zhcn.FinishedHeadingLabel=译刻已安装
zhcn.FinishedLabel=现在可以开始使用译刻。%n%n内置节能引擎可直接离线英译中。%n%n其他语言可在设置切换通用模型或配置在线 API。
[Tasks]
Name: "dictja"; Description: "下载日语研习词典"; Flags: unchecked checkedonce
Name: "dictko"; Description: "下载韩语研习词典"; Flags: unchecked checkedonce
Name: "dictfr"; Description: "下载法语研习词典"; Flags: unchecked checkedonce
Name: "dictde"; Description: "下载德语研习词典"; Flags: unchecked checkedonce
Name: "dictes"; Description: "下载西班牙语研习词典"; Flags: unchecked checkedonce
Name: "desktopicon"; Description: "创建桌面快捷方式"; Flags: unchecked
Name: "startup"; Description: "登录 Windows 时启动译刻"; Flags: unchecked
Name: "vscode"; Description: "安装 VS Code 编辑区与终端选区适配（无需读屏模式）"; Flags: unchecked
[Files]
Source: "..\assets\study-sources.json"; DestDir: "{app}"; Flags: ignoreversion

Source: "{#PublishDir}\translation-runtime\*"; DestDir: "{app}\translation-runtime"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PublishDir}\YiKe.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\assets\yike.ico"; DestDir: "{app}"; DestName: "YiKe-Prism.ico"; Flags: ignoreversion
Source: "..\THIRD_PARTY_NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\assets\opens-codesign.cer"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\assets\DOTNET-*.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\local-runtime\engine\*"; DestDir: "{app}\local-runtime\engine"; Flags: ignoreversion
Source: "{tmp}\{#LightModelFile}"; DestDir: "{app}\local-runtime\models"; ExternalSize: {#LightModelBytes}; Flags: external ignoreversion; Check: NeedLightModel
Source: "{tmp}\{#StandardModelFile}"; DestDir: "{app}\local-runtime\models"; ExternalSize: {#StandardModelBytes}; Flags: external ignoreversion; Check: NeedStandardModel
Source: "..\assets\local-licenses\*"; DestDir: "{app}\licenses"; Flags: ignoreversion
[INI]
Filename: "{app}\local-model.ini"; Section: "LocalModel"; Key: "Selection"; String: "{code:ChosenModelPreference}"; Flags: uninsdeleteentry uninsdeletesectionifempty; Check: ModelPreferenceSelected
[UninstallDelete]
Type: files; Name: "{app}\local-model.ini"
[Icons]
Name: "{group}\译刻"; Filename: "{app}\YiKe.exe"; IconFilename: "{app}\YiKe-Prism.ico"
Name: "{autodesktop}\译刻"; Filename: "{app}\YiKe.exe"; IconFilename: "{app}\YiKe-Prism.ico"; Tasks: desktopicon
[Run]
Filename: "{app}\YiKe.exe"; Description: "启动译刻"; Flags: nowait postinstall skipifsilent
[Code]
var DeleteUserData, StartupWasEnabled, IsUpgrade, TasksInitialized: Boolean;
    ModelPage: TInputOptionWizardPage;
    DownloadPage: TDownloadWizardPage;
    ModelArgument, ModelInfoPath, CheckedDir, DefaultDataDirectory, UserDataDirectory: String;
    LightChecked, StandardChecked, LightValid, StandardValid, DownloadsReady: Boolean;

function ChosenModelPreference(Param: String): String;
begin
  Result := 'auto';
  if ModelPage.SelectedValueIndex = 1 then Result := 'light'
  else if ModelPage.SelectedValueIndex = 2 then Result := 'standard';
end;
function ModelPreferenceSelected(): Boolean;
begin Result := ModelPage.SelectedValueIndex <> 0; end;
function ExistingModelValid(const FileName, Hash: String): Boolean;
var Path: String;
begin
  Path := ExpandConstant('{app}\local-runtime\models\') + FileName;
  Result := False;
  if FileExists(Path) then
    try Result := CompareText(GetSHA256OfFile(Path), Hash) = 0; except Log('Existing model could not be verified.'); end;
end;
procedure CheckModelDirectory;
begin
  if CheckedDir <> ExpandConstant('{app}') then begin
    CheckedDir := ExpandConstant('{app}'); LightChecked := False; StandardChecked := False;
  end;
end;
function NeedLightModel(): Boolean;
begin
  Result := (ModelPage.SelectedValueIndex = 1) or (ModelPage.SelectedValueIndex = 3);
  if not Result then exit;
  CheckModelDirectory;
  if not LightChecked then begin LightValid := ExistingModelValid('{#LightModelFile}', '{#LightModelSha256}'); LightChecked := True; end;
  Result := not LightValid;
end;
function NeedStandardModel(): Boolean;
begin
  Result := (ModelPage.SelectedValueIndex = 2) or (ModelPage.SelectedValueIndex = 3);
  if not Result then exit;
  CheckModelDirectory;
  if not StandardChecked then begin StandardValid := ExistingModelValid('{#StandardModelFile}', '{#StandardModelSha256}'); StandardChecked := True; end;
  Result := not StandardValid;
end;
procedure InitializeWizard;
var Code, RecommendedIndex: Integer; Recommended, Device: String;
begin
  ModelInfoPath := ExpandConstant('{tmp}\model-hardware.ini');
  Device := '未能检测电脑配置，可跳过模型下载；稍后重新运行安装包。'; Recommended := 'none';
  try
    ExtractTemporaryFile('YiKe.exe');
    if Exec(ExpandConstant('{tmp}\YiKe.exe'), '--installer-info "' + ModelInfoPath + '"', '', SW_HIDE, ewWaitUntilTerminated, Code) and (Code = 0) then begin
      Device := GetIniString('Hardware', 'Description', Device, ModelInfoPath);
      Recommended := GetIniString('Hardware', 'Recommended', 'none', ModelInfoPath);
    end;
  except Log('Hardware detection unavailable; defaulting to no model download.'); end;
  ModelPage := CreateInputOptionPage(wpSelectDir, '本地模型（可选下载）', '根据电脑配置选择，下载后直接离线使用。',
    Device + #13#10 + '可跳过下载，完成后可离线使用。已有模型会校验并复用。', True, False);
  ModelPage.Add('本次不下载模型（保留已有模型；新安装可稍后配置在线 API）');
  ModelPage.Add('轻量：Qwen3 0.6B Q4_0 · 下载约 429 MB · 约 8 GB 内存起');
  ModelPage.Add('标准：Qwen3 1.7B Q4_K_M · 下载约 1.28 GB · 约 16 GB 内存起');
  ModelPage.Add('两种都下载 · 共约 1.71 GB · 使用时根据可用内存自动切换');
  RecommendedIndex := 0;
  if Recommended = 'light' then RecommendedIndex := 1 else if Recommended = 'standard' then RecommendedIndex := 2;
  if GetIniString('Hardware', 'LightEligible', '0', ModelInfoPath) <> '1' then begin
    ModelPage.CheckListBox.ItemEnabled[1] := False; ModelPage.CheckListBox.ItemEnabled[3] := False;
  end;
  if GetIniString('Hardware', 'StandardEligible', '0', ModelInfoPath) <> '1' then begin
    ModelPage.CheckListBox.ItemEnabled[2] := False; ModelPage.CheckListBox.ItemEnabled[3] := False;
  end;
  if (ModelArgument <> '') or WizardSilent then begin
    ModelPage.SelectedValueIndex := 0;
    if ModelArgument = 'light' then ModelPage.SelectedValueIndex := 1
    else if ModelArgument = 'standard' then ModelPage.SelectedValueIndex := 2
    else if ModelArgument = 'auto' then ModelPage.SelectedValueIndex := 3;
  end else if IsUpgrade then ModelPage.SelectedValueIndex := 0
  else ModelPage.SelectedValueIndex := RecommendedIndex;
  DownloadPage := CreateDownloadPage('下载所选本地模型', '下载并校验后再安装。可取消下载，返回选择不下载或稍后重试。', nil);
  DownloadPage.ShowBaseNameInsteadOfUrl := True;
end;
function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = ModelPage.ID) or (CurPageID = wpReady) then
    if not ModelPage.CheckListBox.ItemEnabled[ModelPage.SelectedValueIndex] then begin
      SuppressibleMsgBox('此电脑不满足所选模型的基本运行要求，请选择推荐项或本次不下载。', mbError, MB_OK, IDOK); Result := False; exit;
    end;
  if CurPageID <> wpReady then exit;
  Result := False; DownloadsReady := False;
  DownloadPage.Clear;
  if NeedLightModel then
#if defined(InstallerSmoke) && defined(InstallerSmokeBadDownload)
    DownloadPage.Add('https://huggingface.co/ggml-org/Qwen3-0.6B-GGUF/raw/13ac06624948e4d8589cb4b6f90100837342fcb8/README.md', '{#LightModelFile}', '{#LightModelSha256}');
#else
    DownloadPage.Add('{#LightModelUrl}', '{#LightModelFile}', '{#LightModelSha256}');
#endif
  if NeedStandardModel then DownloadPage.Add('{#StandardModelUrl}', '{#StandardModelFile}', '{#StandardModelSha256}');
  DownloadPage.Show;
  try
    try
      DownloadPage.Download;
      if NeedLightModel then
        if not FileExists(ExpandConstant('{tmp}\{#LightModelFile}')) or
           (CompareText(GetSHA256OfFile(ExpandConstant('{tmp}\{#LightModelFile}')), '{#LightModelSha256}') <> 0) then
          RaiseException('Light model SHA-256 verification failed.');
      if NeedStandardModel then
        if not FileExists(ExpandConstant('{tmp}\{#StandardModelFile}')) or
           (CompareText(GetSHA256OfFile(ExpandConstant('{tmp}\{#StandardModelFile}')), '{#StandardModelSha256}') <> 0) then
          RaiseException('Standard model SHA-256 verification failed.');
      DownloadsReady := True; Result := True;
    except
      Log('Model download rejected before installation: ' + GetExceptionMessage);
      if not WizardSilent and not DownloadPage.AbortedByUser then
        SuppressibleMsgBox('模型下载未完成或校验失败。程序尚未开始安装。可点击“安装”重试，或返回模型页面选择“本次不下载”。' + #13#10 + GetExceptionMessage, mbError, MB_OK, IDOK);
      Result := False;
    end;
  finally DownloadPage.Hide; end;
end;
function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo,
  MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
var Choice: String;
begin
  case ModelPage.SelectedValueIndex of
    1: Choice := '轻量 Qwen3 0.6B，约 429 MB';
    2: Choice := '标准 Qwen3 1.7B，约 1.28 GB';
    3: Choice := '轻量和标准两种模型，共约 1.71 GB';
  else Choice := '本次不下载；保留已有模型及其选择'; end;
  Result := MemoDirInfo + NewLine + NewLine + '本地模型：' + NewLine + Space + Choice + NewLine +
    Space + '已有且校验通过的模型将直接复用，不再下载。' + NewLine + NewLine + MemoTasksInfo;
end;
function InitializeSetup(): Boolean;
var Installed: String; OldVersion, NewVersion: Int64;
begin
  Result := True;
  ModelArgument := Lowercase(ExpandConstant('{param:LOCALMODELS|}'));
  if (ModelArgument <> '') and (ModelArgument <> 'none') and (ModelArgument <> 'light') and (ModelArgument <> 'standard') and (ModelArgument <> 'auto') then begin
    SuppressibleMsgBox('LOCALMODELS 参数只能为 none、light、standard 或 auto。', mbError, MB_OK, IDOK); Result := False; exit;
  end;
  StartupWasEnabled := RegValueExists(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'YiKe');
#ifdef InstallerSmoke
  IsUpgrade := RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\YiKe.Installer.ModelSmoke_is1', 'DisplayVersion', Installed);
#else
  IsUpgrade := RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{3C0A80D1-1257-472C-BC71-91BEE87C63F9}_is1', 'DisplayVersion', Installed);
#endif
  if IsUpgrade then
    if StrToVersion(Installed, OldVersion) and StrToVersion('{#AppVersion}', NewVersion) and (ComparePackedVersion(OldVersion, NewVersion) > 0) then begin
      SuppressibleMsgBox('已安装较新版本。为保护配置，安装器拒绝降级。', mbError, MB_OK, IDOK); Result := False;
    end;
end;
function StartupTaskSpecified(): Boolean;
var I: Integer; Argument, Tasks: String;
begin
  Result := False;
  for I := 1 to ParamCount do begin
    Argument := Lowercase(ParamStr(I));
    if Pos('/tasks=', Argument) = 1 then begin Result := True; exit; end;
    if Pos('/mergetasks=', Argument) = 1 then begin
      Tasks := ',' + Copy(Argument, 13, Length(Argument)) + ',';
      StringChangeEx(Tasks, '"', '', True);
      if (Pos(',startup,', Tasks) > 0) or (Pos(',!startup,', Tasks) > 0) then begin Result := True; exit; end;
    end;
  end;
end;
procedure CurPageChanged(CurPageID: Integer);
begin
  if (CurPageID = wpSelectTasks) and not TasksInitialized then begin
    if not StartupTaskSpecified then
      if StartupWasEnabled then WizardSelectTasks('startup') else WizardSelectTasks('!startup');
    TasksInitialized := True;
  end;
end;
function StopInstalledApp(): Boolean;
var Code, Attempts: Integer;
begin
  Result := True;
  if FileExists(ExpandConstant('{app}\YiKe.exe')) then begin
    Exec(ExpandConstant('{app}\YiKe.exe'), '--shutdown', '', SW_HIDE, ewWaitUntilTerminated, Code);
    Attempts := 0;
    while CheckForMutexes('Local\YiKe.3C0A80D1.App') and (Attempts < 30) do begin Sleep(100); Attempts := Attempts + 1; end;
    Result := not CheckForMutexes('Local\YiKe.3C0A80D1.App');
  end;
end;
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not DownloadsReady then begin Result := '模型下载未完成或校验失败，尚未修改程序文件。请重试，或选择本次不下载。'; exit; end;
#ifndef InstallerSmoke
  if not StopInstalledApp then Result := '译刻仍在运行，请从托盘退出后重试安装。';
#endif
end;
procedure InstallStudy(Code, Language: String);
var ResultCode: Integer;
begin
  if not WizardIsTaskSelected('dict' + Code) then exit;
  WizardForm.StatusLabel.Caption := '正在下载并校验' + Language + '词典…';
  if not Exec(ExpandConstant('{app}\YiKe.exe'), '--install-study ' + Code, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then begin
    Log('Study dictionary download failed: ' + Code);
    SuppressibleMsgBox(Language + '词典下载失败，可在译刻设置中重试。', mbInformation, MB_OK, IDOK);
  end;
end;
procedure CurStepChanged(CurStep: TSetupStep);
var AdapterCode: Integer;
begin
  if CurStep = ssPostInstall then begin
#ifndef InstallerSmoke
    if (WizardSilent and not StartupTaskSpecified and IsUpgrade and StartupWasEnabled) or
       ((not WizardSilent or StartupTaskSpecified) and WizardIsTaskSelected('startup')) then
      RegWriteStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'YiKe', '"' + ExpandConstant('{app}\YiKe.exe') + '"')
    else RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'YiKe');
    InstallStudy('ja', '日语');
    InstallStudy('ko', '韩语');
    InstallStudy('fr', '法语');
    InstallStudy('de', '德语');
    InstallStudy('es', '西班牙语');
    if WizardIsTaskSelected('vscode') then
      if not Exec(ExpandConstant('{app}\YiKe.exe'), '--install-vscode', '', SW_HIDE, ewWaitUntilTerminated, AdapterCode) or (AdapterCode <> 0) then begin
        Log('VS Code adapter installation failed. The main application remains installed.');
        SuppressibleMsgBox('VS Code 适配未能安装。译刻已安装，可从设置的“外观与行为”页重试。', mbInformation, MB_OK, IDOK);
      end;
#endif
  end;
end;
function ReadDataPath(const FileName, Fallback: String): String;
var Lines: TArrayOfString; Value: String;
begin
  Result := Fallback;
  if not FileExists(FileName) then exit;
  if not LoadStringsFromFile(FileName, Lines) then exit;
  if Length(Lines) = 0 then exit;
  Value := Trim(Lines[0]);
  if Value <> '' then Result := Value;
end;
procedure ResolveUninstallDataDirectories;
begin
  DefaultDataDirectory := ReadDataPath(ExpandConstant('{app}\yike-data-root.txt'), ExpandConstant('{localappdata}\YiKe'));
  UserDataDirectory := ReadDataPath(AddBackslash(DefaultDataDirectory) + 'data-location.txt', DefaultDataDirectory);
end;
procedure DeleteDataFiles(const Directory: String);
begin
  DeleteFile(AddBackslash(Directory) + 'settings.json');
  DelTree(AddBackslash(Directory) + 'credentials\*.bin', False, True, False);
  RemoveDir(AddBackslash(Directory) + 'credentials');
  RemoveDir(Directory);
end;
function InitializeUninstall(): Boolean;
var Form: TSetupForm; Description: TNewStaticText; Check: TNewCheckBox; OkButton, CancelButton: TNewButton; Scope: String;
begin
  DeleteUserData := False; ResolveUninstallDataDirectories;
#ifdef InstallerSmoke
  Result := True; exit;
#endif
  if UninstallSilent then begin DeleteUserData := ExpandConstant('{param:DELETEDATA|0}') = '1'; Result := StopInstalledApp; exit; end;
  Form := CreateCustomForm(ScaleX(560), ScaleY(310), False, True);
  try
    Form.Caption := '卸载译刻'; Form.ClientWidth := ScaleX(560); Form.ClientHeight := ScaleY(310);
    Description := TNewStaticText.Create(Form); Description.Parent := Form; Description.Left := ScaleX(24); Description.Top := ScaleY(18); Description.Width := ScaleX(510); Description.Height := ScaleY(200); Description.AutoSize := False; Description.WordWrap := True;
    Scope := '设置：' + AddBackslash(UserDataDirectory) + 'settings.json' + #13#10 + '密钥：' + AddBackslash(UserDataDirectory) + 'credentials\*.bin';
    if CompareText(DefaultDataDirectory, UserDataDirectory) <> 0 then
      Scope := Scope + #13#10 + '旧位置设置：' + AddBackslash(DefaultDataDirectory) + 'settings.json' + #13#10 + '旧位置密钥：' + AddBackslash(DefaultDataDirectory) + 'credentials\*.bin';
    Description.Caption := '将删除应用和快捷方式。默认保留设置与密钥，重新安装后可以继续使用。' + #13#10#13#10 + '勾选下方选项时仅删除：' + #13#10 + Scope + #13#10#13#10 + '收藏本、词包、数据位置记录和同目录其他文件保留。';
    Check := TNewCheckBox.Create(Form); Check.Parent := Form; Check.Left := ScaleX(24); Check.Top := ScaleY(230); Check.Width := ScaleX(510); Check.Caption := '同时删除上述设置和已保存的 API 密钥'; Check.Checked := False;
    OkButton := TNewButton.Create(Form); OkButton.Parent := Form; OkButton.Caption := '继续卸载'; OkButton.ModalResult := mrOk; OkButton.Default := True; OkButton.SetBounds(ScaleX(350),ScaleY(268),ScaleX(90),ScaleY(28));
    CancelButton := TNewButton.Create(Form); CancelButton.Parent := Form; CancelButton.Caption := '取消'; CancelButton.ModalResult := mrCancel; CancelButton.Cancel := True; CancelButton.SetBounds(ScaleX(450),ScaleY(268),ScaleX(80),ScaleY(28));
    Result := Form.ShowModal = mrOk; DeleteUserData := Result and Check.Checked;
  finally Form.Free; end;
  if Result and not StopInstalledApp then begin
    SuppressibleMsgBox('译刻仍在运行，请从托盘退出后重试卸载。设置与密钥已保留。', mbError, MB_OK, IDOK);
    Result := False;
  end;
end;
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then begin
#ifndef InstallerSmoke
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'YiKe');
    if DeleteUserData then begin
      DeleteDataFiles(UserDataDirectory);
      if CompareText(DefaultDataDirectory, UserDataDirectory) <> 0 then DeleteDataFiles(DefaultDataDirectory);
    end;
#endif
  end;
end;
