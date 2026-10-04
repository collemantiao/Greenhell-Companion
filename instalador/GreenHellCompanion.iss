; Instalador do Green Hell Companion (Inno Setup 6).
; Gere com: powershell -ExecutionPolicy Bypass -File instalador\montar-instalador.ps1

#define MeuApp "Green Hell Companion"
#ifndef Versao
  #define Versao "1.0.0"
#endif
#define Plugin "..\mod\bin\Release\net472"
#define BepInEx "cache\BepInEx_5.4.23.5"
#define PastaPlugin "{app}\BepInEx\plugins\GreenHellCompanion"

[Setup]
AppId={{6E1C2B7A-4F0D-4C55-9A7E-3B8D2F1C9E41}
AppName={#MeuApp}
AppVersion={#Versao}
AppVerName={#MeuApp} {#Versao}
AppPublisher=Felipe Carvalho
UninstallDisplayName={#MeuApp} (mod do Green Hell)
UninstallDisplayIcon={#PastaPlugin}\logo.ico
; A "pasta de instalação" é a pasta do jogo; o mod vai para BepInEx\plugins dentro dela.
DefaultDirName={code:PastaDoJogo}
AppendDefaultDirName=no
DirExistsWarning=no
UsePreviousAppDir=yes
DisableDirPage=no
DisableProgramGroupPage=yes
DisableWelcomePage=no
UninstallFilesDir={#PastaPlugin}\desinstalar
#ifndef Privilegios
  #define Privilegios "admin"
#endif
PrivilegesRequired={#Privilegios}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
WizardImageFile=arte\assistente.bmp,arte\assistente@2x.bmp
WizardSmallImageFile=arte\icone.bmp,arte\icone@2x.bmp
SetupIconFile=arte\logo.ico
OutputDir=saida
OutputBaseFilename=GreenHellCompanion-Setup-{#Versao}
Compression=lzma2/max
SolidCompression=yes
CloseApplications=no

[Languages]
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Messages]
ptbr.WelcomeLabel2=Este assistente vai instalar o [name/ver] no seu Green Hell.%n%nO mod mostra avisos de saúde, ajuda na construção, marcadores com distância e um guia dentro do jogo. Ele não altera status nem cria itens.%n%nFeche o jogo antes de continuar.
ptbr.WizardSelectDir=Pasta do jogo
ptbr.SelectDirDesc=Onde o Green Hell está instalado?
ptbr.SelectDirLabel3=Escolha a pasta do Green Hell, a que contém o arquivo GH.exe.
ptbr.SelectDirBrowseLabel=Se a pasta abaixo não for a do jogo, clique em Procurar. Na Steam, ela fica em steamapps\common\Green Hell.
ptbr.FinishedHeadingLabel=Pronto!
ptbr.FinishedLabelNoIcons=O [name] foi instalado. Abra o Green Hell e use as teclas:%n%nF1   Guia%nF2   Marcar um ponto%nF3   Lista de marcadores%nF4   Configurações%nF5   Construção%n%nOs avisos de saúde e a ajuda de construção aparecem sozinhos.

[Files]
; BepInEx 5 (carregador de mods). Só é copiado se o jogo ainda não tiver; nunca é removido na desinstalação,
; porque outros mods podem depender dele.
Source: "{#BepInEx}\winhttp.dll";          DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall; Check: PrecisaBepInEx
Source: "{#BepInEx}\doorstop_config.ini";  DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall; Check: PrecisaBepInEx
Source: "{#BepInEx}\.doorstop_version";    DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall; Check: PrecisaBepInEx
Source: "{#BepInEx}\BepInEx\core\*";       DestDir: "{app}\BepInEx\core"; Flags: onlyifdoesntexist uninsneveruninstall; Check: PrecisaBepInEx

; O mod
Source: "{#Plugin}\GreenHellCompanion.dll"; DestDir: "{#PastaPlugin}"; Flags: ignoreversion
Source: "{#Plugin}\Data\*";                 DestDir: "{#PastaPlugin}\Data"; Flags: ignoreversion recursesubdirs
Source: "arte\logo.ico";                    DestDir: "{#PastaPlugin}"; Flags: ignoreversion
Source: "..\mod\README.md";                 DestDir: "{#PastaPlugin}"; DestName: "LEIA-ME.md"; Flags: ignoreversion

[Dirs]
Name: "{app}\BepInEx\plugins"; Flags: uninsneveruninstall

[UninstallDelete]
; Remove só o mod. Configurações e marcadores (BepInEx\config) ficam, caso você reinstale.
Type: filesandordirs; Name: "{#PastaPlugin}"

[Code]
const
  SteamAppId = '815370';

function TemJogo(const Pasta: String): Boolean;
begin
  Result := (Pasta <> '') and FileExists(AddBackslash(Pasta) + 'GH.exe');
end;

function TemBepInEx(const Pasta: String): Boolean;
begin
  Result := FileExists(AddBackslash(Pasta) + 'BepInEx\core\BepInEx.dll');
end;

function PrecisaBepInEx: Boolean;
begin
  Result := not TemBepInEx(ExpandConstant('{app}'));
end;

{ Lê as bibliotecas da Steam em libraryfolders.vdf e procura o jogo em cada uma. }
function ProcurarNasBibliotecas(const Steam: String): String;
var
  Linhas: TArrayOfString;
  I, P: Integer;
  L, Caminho: String;
begin
  Result := '';
  if TemJogo(AddBackslash(Steam) + 'steamapps\common\Green Hell') then
  begin
    Result := AddBackslash(Steam) + 'steamapps\common\Green Hell';
    Exit;
  end;
  if not LoadStringsFromFile(AddBackslash(Steam) + 'steamapps\libraryfolders.vdf', Linhas) then Exit;
  for I := 0 to GetArrayLength(Linhas) - 1 do
  begin
    L := Trim(Linhas[I]);
    if Pos('"path"', L) = 1 then
    begin
      Caminho := Trim(Copy(L, 7, Length(L)));
      P := Pos('"', Caminho);
      if P > 0 then
      begin
        Delete(Caminho, 1, P);
        P := Pos('"', Caminho);
        if P > 0 then Caminho := Copy(Caminho, 1, P - 1);
        StringChangeEx(Caminho, '\\', '\', True);
        Caminho := AddBackslash(Caminho) + 'steamapps\common\Green Hell';
        if TemJogo(Caminho) then
        begin
          Result := Caminho;
          Exit;
        end;
      end;
    end;
  end;
end;

function PastaDoJogo(Param: String): String;
var
  S: String;
begin
  { 1. Registro de desinstalação que a Steam cria para o jogo }
  if RegQueryStringValue(HKLM64, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App ' + SteamAppId, 'InstallLocation', S) and TemJogo(S) then
  begin
    Result := S;
    Exit;
  end;
  if RegQueryStringValue(HKLM32, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App ' + SteamAppId, 'InstallLocation', S) and TemJogo(S) then
  begin
    Result := S;
    Exit;
  end;
  { 2. Bibliotecas da Steam }
  if RegQueryStringValue(HKCU, 'Software\Valve\Steam', 'SteamPath', S) then
  begin
    StringChangeEx(S, '/', '\', True);
    Result := ProcurarNasBibliotecas(S);
    if Result <> '' then Exit;
  end;
  if RegQueryStringValue(HKLM32, 'SOFTWARE\Valve\Steam', 'InstallPath', S) then
  begin
    Result := ProcurarNasBibliotecas(S);
    if Result <> '' then Exit;
  end;
  { 3. Não achou: sugere o local padrão da Steam e o usuário escolhe }
  Result := ExpandConstant('{commonpf32}\Steam\steamapps\common\Green Hell');
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = wpSelectDir) and not TemJogo(WizardDirValue) then
  begin
    MsgBox('Não encontrei o GH.exe em:' + #13#10 + WizardDirValue + #13#10#13#10 +
      'Escolha a pasta onde o Green Hell está instalado. Na Steam: clique com o botão direito no jogo > Gerenciar > Ver arquivos locais.',
      mbError, MB_OK);
    Result := False;
  end;
end;

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo,
  MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
var
  Carregador: String;
begin
  if TemBepInEx(WizardDirValue) then
    Carregador := 'já instalado no jogo, será mantido'
  else
    Carregador := 'será instalado (BepInEx 5.4.23.5)';
  Result := 'Pasta do jogo:' + NewLine + Space + WizardDirValue + NewLine + NewLine +
    'Carregador de mods:' + NewLine + Space + Carregador + NewLine + NewLine +
    'O mod vai para:' + NewLine + Space + AddBackslash(WizardDirValue) + 'BepInEx\plugins\GreenHellCompanion';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    MsgBox('O Green Hell Companion foi removido.' + #13#10#13#10 +
      'O BepInEx (carregador de mods) continua no jogo, porque outros mods podem usá-lo. ' +
      'Para desligar todos os mods, apague o arquivo winhttp.dll da pasta do jogo.', mbInformation, MB_OK);
end;
