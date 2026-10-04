# Compila o mod e gera instalador\saida\GreenHellCompanion-Setup-<versão>.exe
# Uso: powershell -ExecutionPolicy Bypass -File instalador\montar-instalador.ps1
# Requer: .NET SDK 8, Inno Setup 6 e o Green Hell com BepInEx (as DLLs do jogo são usadas só para compilar).
param([string]$Versao = '1.0.0', [string]$GamePath = '')
$ErrorActionPreference = 'Stop'
$aqui = $PSScriptRoot
$raiz = Split-Path $aqui -Parent

# 1. Mod (sem copiar para o jogo)
$argsBuild = @('build', (Join-Path $raiz 'mod\GreenHellCompanion.csproj'), '-c', 'Release', '-p:InstalarNoJogo=false', "-p:Version=$Versao")
if ($GamePath) { $argsBuild += "-p:GamePath=$GamePath" }
& dotnet @argsBuild
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar o mod.' }

# 2. BepInEx oficial (baixa uma vez para instalador\cache)
$bepVer = '5.4.23.5'
$cache = Join-Path $aqui 'cache'
$zip = Join-Path $cache "BepInEx_win_x64_$bepVer.zip"
$dest = Join-Path $cache "BepInEx_$bepVer"
New-Item -ItemType Directory -Force $cache | Out-Null
if (-not (Test-Path $zip)) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest "https://github.com/BepInEx/BepInEx/releases/download/v$bepVer/BepInEx_win_x64_$bepVer.zip" -OutFile $zip
}
if (-not (Test-Path (Join-Path $dest 'BepInEx\core\BepInEx.dll'))) {
    Expand-Archive $zip -DestinationPath $dest -Force
}

# 3. Arte (logo, ícone e imagens do assistente)
if (-not (Test-Path (Join-Path $aqui 'arte\logo.ico'))) { & (Join-Path $aqui 'gerar-logo.ps1') }

# 4. Inno Setup
$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 não encontrado. Instale com: winget install JRSoftware.InnoSetup' }
& $iscc "/DVersao=$Versao" (Join-Path $aqui 'GreenHellCompanion.iss')
if ($LASTEXITCODE -ne 0) { throw 'Falha ao gerar o instalador.' }
# 5. Pacote .zip para extrair na pasta do jogo (não executa nada, então o Windows não bloqueia)
$pacote = Join-Path $cache 'pacote'
if (Test-Path $pacote) { Remove-Item $pacote -Recurse -Force }
$plugin = Join-Path $pacote 'BepInEx\plugins\GreenHellCompanion'
New-Item -ItemType Directory -Force (Join-Path $pacote 'BepInEx\core'), $plugin | Out-Null
foreach ($f in 'winhttp.dll', 'doorstop_config.ini', '.doorstop_version') { Copy-Item (Join-Path $dest $f) $pacote }
Copy-Item (Join-Path $dest 'BepInEx\core\*') (Join-Path $pacote 'BepInEx\core')
$bin = Join-Path $raiz 'mod\bin\Release\net472'
Copy-Item (Join-Path $bin 'GreenHellCompanion.dll') $plugin
Copy-Item (Join-Path $bin 'Data') $plugin -Recurse
Copy-Item (Join-Path $aqui 'arte\logo.ico') $plugin
Copy-Item (Join-Path $raiz 'mod\README.md') (Join-Path $plugin 'LEIA-ME.md')
Copy-Item (Join-Path $aqui 'LEIA-ME.txt') (Join-Path $pacote 'LEIA-ME - Green Hell Companion.txt')
$zipSaida = Join-Path $aqui "saida\GreenHellCompanion-$Versao.zip"
if (Test-Path $zipSaida) { Remove-Item $zipSaida -Force }
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
# Monta as entradas à mão com "/" (o CreateFromDirectory do Windows PowerShell grava "\", que alguns extratores não entendem)
$arq = [System.IO.Compression.ZipFile]::Open($zipSaida, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem $pacote -Recurse -File -Force | ForEach-Object {
        $nome = $_.FullName.Substring($pacote.Length + 1).Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($arq, $_.FullName, $nome, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $arq.Dispose() }

Get-ChildItem (Join-Path $aqui 'saida') -File | Where-Object { $_.Name -like "*$Versao*" } | Select-Object Name, @{n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } }
