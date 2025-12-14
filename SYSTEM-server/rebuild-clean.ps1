# SYSTEM - Clean Rebuild Script (PowerShell)
# DESTRUCTIVE: Deletes database, rebuilds, and deploys fresh
# Use this when you need a clean slate (schema changes, corrupted data, etc.)

Clear-Host

Write-Host ""
Write-Host "========================================" -ForegroundColor Red
Write-Host "SYSTEM - CLEAN Rebuild (DESTRUCTIVE)" -ForegroundColor Red
Write-Host "========================================" -ForegroundColor Red
Write-Host ""
Write-Host "WARNING: This will DELETE all data!" -ForegroundColor Yellow
Write-Host "  - All players" -ForegroundColor Yellow
Write-Host "  - All inventories" -ForegroundColor Yellow
Write-Host "  - All sources" -ForegroundColor Yellow
Write-Host "  - All storage devices" -ForegroundColor Yellow
Write-Host ""

$confirm = Read-Host "Type 'DELETE' to confirm"
if ($confirm -ne "DELETE") {
    Write-Host ""
    Write-Host "Aborted. Use .\rebuild.ps1 for safe rebuild." -ForegroundColor Green
    Read-Host "Press Enter to exit"
    exit 0
}

Write-Host ""

# Step 1: Delete existing module (DESTRUCTIVE)
Write-Host "[1/4] Deleting existing module..." -ForegroundColor Red
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
Write-Host "SUCCESS! Fresh database deployed" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

Write-Host "You can now test with:" -ForegroundColor Cyan
Write-Host "  spacetime call system debug_test_quanta_emission" -ForegroundColor White
Write-Host "  spacetime call system debug_quanta_status" -ForegroundColor White
Write-Host ""
Read-Host "Press Enter to exit"
