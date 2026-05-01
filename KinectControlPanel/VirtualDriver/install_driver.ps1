# ================================================
# install_driver.ps1 — Kinect 360 Virtual Camera
# Execute como Administrador
# ================================================

$clsid   = "{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}"
$dllPath = "C:\KinectCam\KinectVirtualCamera.dll"
$name    = "Kinect 360"

Write-Host "=== Kinect 360 Virtual Camera - Instalador ===" -ForegroundColor Cyan
Write-Host ""

# ── Verifica a DLL ──────────────────────────────────────────────────────────
if (-not (Test-Path $dllPath)) {
    Write-Host "ERRO: DLL nao encontrada em C:\KinectCam\" -ForegroundColor Red
    Write-Host "Copie KinectVirtualCamera.dll para C:\KinectCam\ primeiro." -ForegroundColor Red
    Read-Host "Enter para fechar"
    exit 1
}

# ── 1. regsvr32 — chama DllRegisterServer dentro da DLL ────────────────────
Write-Host "1. Rodando regsvr32..." -ForegroundColor Yellow
$r = Start-Process regsvr32.exe -ArgumentList "/s `"$dllPath`"" -Wait -PassThru
if ($r.ExitCode -ne 0) {
    Write-Host "   regsvr32 retornou $($r.ExitCode) — continuando com registro manual." -ForegroundColor Yellow
}

# ── 2. Registro manual HKCR (garante mesmo se regsvr32 falhar) ─────────────
Write-Host "2. Registrando CLSID em HKCR..." -ForegroundColor Yellow

# CLSID principal
$base = "Registry::HKEY_CLASSES_ROOT\CLSID\$clsid"
New-Item -Path $base                           -Force | Out-Null
New-Item -Path "$base\InprocServer32"          -Force | Out-Null
Set-ItemProperty -Path $base                  -Name "(Default)"       -Value $name
Set-ItemProperty -Path "$base\InprocServer32" -Name "(Default)"       -Value $dllPath
Set-ItemProperty -Path "$base\InprocServer32" -Name "ThreadingModel"  -Value "Both"

# ── 3. Categorias DirectShow — forma CORRETA ────────────────────────────────
# A chave certa é: HKCR\CLSID\{categoria}\Instance\{nosso-clsid}
Write-Host "3. Registrando categorias DirectShow..." -ForegroundColor Yellow

$categories = @{
    # VideoInputDevice — o que DirectShow enumera como webcam
    "{860BB310-5D01-11d0-BD3B-00A0C911CE86}" = "VideoInputDevice"
    # AMKSCategory_Video_Camera — Windows 10/11 lista aqui tambem
    "{e5323777-f976-4f5b-9b55-b94699c46e44}" = "VideoCamera"
}

foreach ($cat in $categories.Keys) {
    $catBase = "Registry::HKEY_CLASSES_ROOT\CLSID\$cat"
    $inst    = "$catBase\Instance\$clsid"

    # Cria a categoria pai se nao existir
    New-Item -Path $catBase -Force -ErrorAction SilentlyContinue | Out-Null
    New-Item -Path "$catBase\Instance" -Force -ErrorAction SilentlyContinue | Out-Null

    # Registra nosso filtro como instancia dessa categoria
    New-Item -Path $inst -Force | Out-Null
    Set-ItemProperty -Path $inst -Name "(Default)"    -Value $name
    Set-ItemProperty -Path $inst -Name "FriendlyName" -Value $name
    Set-ItemProperty -Path $inst -Name "CLSID"        -Value $clsid

    Write-Host "   OK: $($categories[$cat])" -ForegroundColor Green
}

# ── 4. Registro HKLM 64-bit (para apps 64-bit como Discord) ─────────────────
Write-Host "4. Registrando em HKLM 64-bit..." -ForegroundColor Yellow

$lm64 = "HKLM:\SOFTWARE\Classes\CLSID\$clsid"
New-Item -Path $lm64                    -Force | Out-Null
New-Item -Path "$lm64\InprocServer32"   -Force | Out-Null
Set-ItemProperty -Path $lm64                  -Name "(Default)"      -Value $name
Set-ItemProperty -Path "$lm64\InprocServer32" -Name "(Default)"      -Value $dllPath
Set-ItemProperty -Path "$lm64\InprocServer32" -Name "ThreadingModel" -Value "Both"

foreach ($cat in $categories.Keys) {
    $inst = "HKLM:\SOFTWARE\Classes\CLSID\$cat\Instance\$clsid"
    New-Item -Path "HKLM:\SOFTWARE\Classes\CLSID\$cat"          -Force -ErrorAction SilentlyContinue | Out-Null
    New-Item -Path "HKLM:\SOFTWARE\Classes\CLSID\$cat\Instance" -Force -ErrorAction SilentlyContinue | Out-Null
    New-Item -Path $inst -Force | Out-Null
    Set-ItemProperty -Path $inst -Name "(Default)"    -Value $name
    Set-ItemProperty -Path $inst -Name "FriendlyName" -Value $name
    Set-ItemProperty -Path $inst -Name "CLSID"        -Value $clsid
}

# ── 5. Registro WOW64 32-bit (para apps 32-bit) ──────────────────────────────
Write-Host "5. Registrando em WOW64 32-bit..." -ForegroundColor Yellow

$lm32 = "HKLM:\SOFTWARE\WOW6432Node\Classes\CLSID\$clsid"
New-Item -Path $lm32                    -Force | Out-Null
New-Item -Path "$lm32\InprocServer32"   -Force | Out-Null
Set-ItemProperty -Path $lm32                  -Name "(Default)"      -Value $name
Set-ItemProperty -Path "$lm32\InprocServer32" -Name "(Default)"      -Value $dllPath
Set-ItemProperty -Path "$lm32\InprocServer32" -Name "ThreadingModel" -Value "Both"

foreach ($cat in $categories.Keys) {
    $inst = "HKLM:\SOFTWARE\WOW6432Node\Classes\CLSID\$cat\Instance\$clsid"
    New-Item -Path "HKLM:\SOFTWARE\WOW6432Node\Classes\CLSID\$cat"          -Force -ErrorAction SilentlyContinue | Out-Null
    New-Item -Path "HKLM:\SOFTWARE\WOW6432Node\Classes\CLSID\$cat\Instance" -Force -ErrorAction SilentlyContinue | Out-Null
    New-Item -Path $inst -Force | Out-Null
    Set-ItemProperty -Path $inst -Name "(Default)"    -Value $name
    Set-ItemProperty -Path $inst -Name "FriendlyName" -Value $name
    Set-ItemProperty -Path $inst -Name "CLSID"        -Value $clsid
}

Write-Host ""
Write-Host "=== Instalacao concluida! ===" -ForegroundColor Green
Write-Host ""
Write-Host "Proximo passo:" -ForegroundColor White
Write-Host "  1. Reinicie o PC" -ForegroundColor Cyan
Write-Host "  2. Abra o KinectControlPanel como Administrador" -ForegroundColor Cyan
Write-Host "  3. Clique em 'VCam: OFF' para ligar" -ForegroundColor Cyan
Write-Host "  4. No Discord: Configuracoes > Voz e Video > Camera = 'Kinect 360'" -ForegroundColor Cyan
Write-Host ""
Read-Host "Enter para fechar"