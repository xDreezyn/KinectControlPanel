# ================================================
# uninstall_driver.ps1 — Remove Kinect Virtual Camera
# Execute como Administrador
# ================================================
 
$clsid = "{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}"
 
Write-Host "=== Kinect 360 Virtual Camera - Desinstalador ===" -ForegroundColor Cyan
 
# regsvr32 /u
Start-Process regsvr32.exe -ArgumentList "/s /u `"C:\KinectCam\KinectVirtualCamera.dll`"" -Wait -PassThru | Out-Null
 
# Remove HKCR
Remove-Item "Registry::HKEY_CLASSES_ROOT\CLSID\$clsid" -Recurse -Force -ErrorAction SilentlyContinue
 
# Remove das categorias
$cats = @("{860BB310-5D01-11d0-BD3B-00A0C911CE86}", "{e5323777-f976-4f5b-9b55-b94699c46e44}")
foreach ($cat in $cats) {
    Remove-Item "Registry::HKEY_CLASSES_ROOT\CLSID\$cat\Instance\$clsid" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item "HKLM:\SOFTWARE\Classes\CLSID\$cat\Instance\$clsid"       -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item "HKLM:\SOFTWARE\WOW6432Node\Classes\CLSID\$cat\Instance\$clsid" -Recurse -Force -ErrorAction SilentlyContinue
}
 
# Remove HKLM
Remove-Item "HKLM:\SOFTWARE\Classes\CLSID\$clsid"              -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "HKLM:\SOFTWARE\WOW6432Node\Classes\CLSID\$clsid"  -Recurse -Force -ErrorAction SilentlyContinue
 
Write-Host "Desinstalacao concluida!" -ForegroundColor Green
Read-Host "Enter para fechar"
 