# SYSTEM - Rebuild and Deploy Script (PowerShell)
# Rebuilds server, generates Unity bindings, and deploys to local SpacetimeDB

Clear-Host

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "SYSTEM - Rebuild and Deploy" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Delete existing module
Write-Host "[1/4] Deleting existing module..." -ForegroundColor Yellow
spacetime delete system --server local
Write-Host ""

# Step 2: Build the Rust module
Write-Host "[2/4] Building Rust module..." -ForegroundColor Yellow
cargo build --release
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "ERROR: Build failed!" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit $LASTEXITCODE
}
Write-Host ""

# Step 3: Generate C# bindings for Unity
Write-Host "[3/4] Generating C# bindings..." -ForegroundColor Yellow
spacetime generate --lang cs --out-dir ../SYSTEM-client-3d/Assets/scripts/autogen
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "ERROR: Failed to generate bindings!" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit $LASTEXITCODE
}
Write-Host ""

# Step 4: Publish to local SpacetimeDB
Write-Host "[4/4] Publishing to local SpacetimeDB..." -ForegroundColor Yellow
spacetime publish --server local system
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "ERROR: Failed to publish module!" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "SUCCESS! Module deployed to local server" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""


Write-Host "You can now test with:" -ForegroundColor Cyan
Write-Host "  spacetime call system debug_test_quanta_emission" -ForegroundColor White
Write-Host "  spacetime call system debug_quanta_status" -ForegroundColor White
Write-Host ""
Read-Host "Press Enter to exit"