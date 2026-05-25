# ANG-Impianti Installer
# Esegui con: Click destro -> Esegui con PowerShell (come amministratore)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ANG-Impianti AI - Installazione v2.4  " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$bundlePath = "C:\ProgramData\Autodesk\ApplicationPlugins\ANGImpianti.bundle"
$tempZip = "$env:TEMP\ANGImpianti_v24.zip"
$downloadUrl = "https://ang-gest.vercel.app/api/ang-impianti-download"

# Step 1: Download
Write-Host "1. Download in corso..." -ForegroundColor Yellow
try {
    Invoke-WebRequest -Uri $downloadUrl -OutFile $tempZip -UseBasicParsing
    Write-Host "   Download completato! ($([math]::Round((Get-Item $tempZip).Length/1KB)) KB)" -ForegroundColor Green
} catch {
    Write-Host "   ERRORE download: $_" -ForegroundColor Red
    pause
    exit
}

# Step 2: Chiudi AutoCAD
Write-Host "2. Chiusura AutoCAD..." -ForegroundColor Yellow
Stop-Process -Name "acad" -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
Write-Host "   OK" -ForegroundColor Green

# Step 3: Rimuovi vecchia versione
Write-Host "3. Rimozione versione precedente..." -ForegroundColor Yellow
if (Test-Path $bundlePath) {
    Remove-Item $bundlePath -Recurse -Force
    Write-Host "   Rimosso: $bundlePath" -ForegroundColor Green
}
$oldPath = "C:\ProgramData\Autodesk\ApplicationPlugins\ANGImpianti"
if (Test-Path $oldPath) {
    Remove-Item $oldPath -Recurse -Force
}

# Step 4: Estrai
Write-Host "4. Estrazione..." -ForegroundColor Yellow
$extractPath = "C:\ProgramData\Autodesk\ApplicationPlugins"
Expand-Archive -Path $tempZip -DestinationPath $extractPath -Force

# Step 5: Rinomina se serve
if (Test-Path "$extractPath\ANGImpianti") {
    Rename-Item "$extractPath\ANGImpianti" "ANGImpianti.bundle" -Force
    Write-Host "   Cartella rinominata in ANGImpianti.bundle" -ForegroundColor Green
}

# Verifica
if (Test-Path $bundlePath) {
    Write-Host "   Bundle installato correttamente!" -ForegroundColor Green
} else {
    Write-Host "   ATTENZIONE: bundle non trovato in $bundlePath" -ForegroundColor Red
}

# Cleanup
Remove-Item $tempZip -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Installazione completata!             " -ForegroundColor Green
Write-Host "  Ora puoi aprire AutoCAD               " -ForegroundColor Green  
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Apri AutoCAD
$acadPath = "${env:ProgramFiles}\Autodesk\AutoCAD 2025\acad.exe"
if (Test-Path $acadPath) {
    Write-Host "Avvio AutoCAD..." -ForegroundColor Yellow
    Start-Process $acadPath
} else {
    Write-Host "Apri AutoCAD manualmente." -ForegroundColor Yellow
}

pause
