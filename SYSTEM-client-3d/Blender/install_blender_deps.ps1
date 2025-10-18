# Install Quantum Orbital Framework Dependencies for Blender 3.4
# This script installs scipy and scikit-image into Blender's Python environment

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "Quantum Orbital Framework - Blender 3.4 Dependency Installer" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host ""

# Blender Python path
$blenderPython = "H:\Program Files\Blender Foundation\Blender 3.4\3.4\python\bin\python.exe"

# Verify Blender Python exists
if (-not (Test-Path $blenderPython)) {
    Write-Host "ERROR: Blender Python not found at:" -ForegroundColor Red
    Write-Host "  $blenderPython" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please verify your Blender 3.4 installation path." -ForegroundColor Yellow
    exit 1
}

Write-Host "Found Blender Python:" -ForegroundColor Green
Write-Host "  $blenderPython" -ForegroundColor White
Write-Host ""

# Check Python version
Write-Host "Checking Python version..." -ForegroundColor Yellow
$pythonVersion = & $blenderPython --version 2>&1
Write-Host "  $pythonVersion" -ForegroundColor White
Write-Host ""

# Check current installed packages
Write-Host "Checking currently installed scientific packages..." -ForegroundColor Yellow
$installedPackages = & $blenderPython -m pip list 2>&1 | Select-String -Pattern "numpy|scipy|scikit"
if ($installedPackages) {
    Write-Host "  Currently installed:" -ForegroundColor White
    $installedPackages | ForEach-Object { Write-Host "    $_" -ForegroundColor White }
} else {
    Write-Host "  No scientific packages found" -ForegroundColor White
}
Write-Host ""

# Install scipy
Write-Host "Installing scipy..." -ForegroundColor Yellow
Write-Host "  (This may take a few minutes)" -ForegroundColor Gray
& $blenderPython -m pip install scipy --quiet
if ($LASTEXITCODE -eq 0) {
    Write-Host "  SUCCESS: scipy installed" -ForegroundColor Green
} else {
    Write-Host "  ERROR: Failed to install scipy" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Install scikit-image
Write-Host "Installing scikit-image..." -ForegroundColor Yellow
Write-Host "  (This may take a few minutes)" -ForegroundColor Gray
& $blenderPython -m pip install scikit-image --quiet
if ($LASTEXITCODE -eq 0) {
    Write-Host "  SUCCESS: scikit-image installed" -ForegroundColor Green
} else {
    Write-Host "  ERROR: Failed to install scikit-image" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Verify installations
Write-Host "Verifying installations..." -ForegroundColor Yellow
$verification = & $blenderPython -c "import numpy; import scipy; import skimage; print('All imports successful!')" 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "  $verification" -ForegroundColor Green
} else {
    Write-Host "  ERROR: Import verification failed" -ForegroundColor Red
    Write-Host "  $verification" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Show final package list
Write-Host "Final installed packages:" -ForegroundColor Yellow
$finalPackages = & $blenderPython -m pip list 2>&1 | Select-String -Pattern "numpy|scipy|scikit"
$finalPackages | ForEach-Object { Write-Host "  $_" -ForegroundColor White }
Write-Host ""

# Success message
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "SUCCESS! All dependencies installed successfully!" -ForegroundColor Green
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Open Blender 3.4" -ForegroundColor White
Write-Host "  2. Switch to Scripting workspace" -ForegroundColor White
Write-Host "  3. Load 'test_in_blender.py' from this directory" -ForegroundColor White
Write-Host "  4. Run the script (Alt+P) to verify installation" -ForegroundColor White
Write-Host ""
Write-Host "Or load 'example_blender_script.py' to create orbital meshes!" -ForegroundColor White
Write-Host ""
