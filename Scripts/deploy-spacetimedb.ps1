# Deploy-SpacetimeDB.ps1 - Unified SpacetimeDB Deployment System
# Comprehensive deployment script for local, test, and production environments

param(
    [ValidateSet('local', 'test', 'production')]
    [string]$Environment = 'test',
    
    [switch]$DeleteData,
    [switch]$InvalidateCache,
    [switch]$PublishOnly,
    [switch]$Verify,
    [switch]$BuildConfig,
    [switch]$Yes,
    [switch]$SkipBuild,
    [switch]$DeployWebGL,
    [string]$ModulePath = ".\SYSTEM-server",
    [string]$LogPath = ".\Logs\deployment",
    [string]$WebGLBuildPath = ".\SYSTEM-client-3d\Build",
    [int]$Timeout = 300
)

# Initialize deployment configuration
$script:DeploymentStart = Get-Date
$script:ErrorCount = 0
$script:WarningCount = 0

# Environment configuration mapping
$EnvironmentConfig = @{
    'local' = @{
        Server = '127.0.0.1:3000'
        Module = 'system'
        RequiresAuth = $false
        CloudFront = $null
    }
    'test' = @{
        Server = 'https://maincloud.spacetimedb.com'
        Module = 'system-test'
        RequiresAuth = $true
        CloudFront = 'EQN06IXQ89GVL'
        S3Bucket = 'system-game-test'
    }
    'production' = @{
        Server = 'https://maincloud.spacetimedb.com'
        Module = 'system'
        RequiresAuth = $true
        CloudFront = 'E3HQWKXYZ9MNOP'  # Production CloudFront ID
        S3Bucket = 'system-unity-game'  # Assuming this is production
    }
}

# Ensure log directory exists
$resolvedLogPath = [System.IO.Path]::GetFullPath($LogPath)
if (!(Test-Path $resolvedLogPath)) {
    New-Item -ItemType Directory -Path $resolvedLogPath -Force | Out-Null
}
$script:LogFile = Join-Path $resolvedLogPath "deployment_$(Get-Date -Format 'yyyyMMdd_HHmmss').log"

# Logging functions
function Write-Log {
    param(
        [string]$Message,
        [ValidateSet('Info', 'Warning', 'Error', 'Success', 'Debug')]
        [string]$Level = 'Info'
    )
    
    $Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $LogMessage = "[$Timestamp] [$Level] $Message"
    
    # Write to log file
    Add-Content -Path $script:LogFile -Value $LogMessage
    
    # Write to console with color
    switch ($Level) {
        'Error'   { 
            Write-Host $LogMessage -ForegroundColor Red
            $script:ErrorCount++
        }
        'Warning' { 
            Write-Host $LogMessage -ForegroundColor Yellow
            $script:WarningCount++
        }
        'Success' { Write-Host $LogMessage -ForegroundColor Green }
        'Debug'   { Write-Host $LogMessage -ForegroundColor Gray }
        default   { Write-Host $LogMessage }
    }
}

function Show-Banner {
    Write-Host ""
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host "       SpacetimeDB Unified Deployment System v2.0            " -ForegroundColor Cyan
    Write-Host "                   SYSTEM Game Server                        " -ForegroundColor Cyan
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Log "Starting deployment to $Environment environment" -Level Info
    Write-Log "Configuration: $($EnvironmentConfig[$Environment] | ConvertTo-Json -Compress)" -Level Debug
}

function Test-Prerequisites {
    Write-Log "Checking prerequisites..." -Level Info
    
    # Check Rust/Cargo
    try {
        $cargoVersion = cargo --version
        Write-Log "Found $cargoVersion" -Level Success
    } catch {
        Write-Log "Cargo not found. Please install Rust." -Level Error
        return $false
    }
    
    # Check SpacetimeDB CLI
    try {
        $stVersion = spacetime version 2>&1
        Write-Log "Found SpacetimeDB CLI: $stVersion" -Level Success
    } catch {
        Write-Log "SpacetimeDB CLI not found. Please install from https://spacetimedb.com" -Level Error
        return $false
    }
    
    # Check module path
    if (!(Test-Path $ModulePath)) {
        Write-Log "Module path not found: $ModulePath" -Level Error
        return $false
    }
    
    # Check for Cargo.toml
    $cargoToml = Join-Path $ModulePath "Cargo.toml"
    if (!(Test-Path $cargoToml)) {
        Write-Log "Cargo.toml not found in $ModulePath" -Level Error
        return $false
    }
    
    # Ensure server fingerprint is saved (for non-local environments)
    if ($Environment -ne 'local') {
        $config = $EnvironmentConfig[$Environment]
        Write-Log "Checking server fingerprint for $($config.Server)..." -Level Debug
        
        # Use echo to auto-accept fingerprint if needed
        $fingerprintCmd = "echo y | spacetime server fingerprint $($config.Server) 2>&1"
        $fingerprintOutput = Invoke-Expression $fingerprintCmd
        
        if ($fingerprintOutput -match "Saving config") {
            Write-Log "Server fingerprint saved" -Level Success
        } elseif ($fingerprintOutput -match "already saved") {
            Write-Log "Server fingerprint already configured" -Level Debug
        }
    }
    
    return $true
}

function Build-Module {
    if ($SkipBuild) {
        Write-Log 'Skipping module build - SkipBuild flag set' -Level Info
        return $true
    }
    
    Write-Log "Building Rust module..." -Level Info
    
    Push-Location $ModulePath
    try {
        # Clean previous build
        Write-Log "Cleaning previous build artifacts..." -Level Info
        cargo clean 2>&1 | Out-Null
        
        # Build release version
        Write-Log "Compiling release build..." -Level Info
        $buildOutput = cargo build --release 2>&1
        
        if ($LASTEXITCODE -ne 0) {
            Write-Log "Build failed: $buildOutput" -Level Error
            return $false
        }
        
        Write-Log "Module built successfully" -Level Success
        
        # Generate C# bindings
        Write-Log "Generating C# bindings..." -Level Info
        $genOutput = spacetime generate --lang cs --out-dir ../SYSTEM-client-3d/Assets/Scripts/autogen 2>&1
        
        if ($LASTEXITCODE -ne 0) {
            Write-Log "Failed to generate bindings: $genOutput" -Level Warning
        } else {
            Write-Log "C# bindings generated successfully" -Level Success
        }
        
        return $true
    } finally {
        Pop-Location
    }
}

function Get-DatabaseState {
    param([string]$Environment)
    
    Write-Log "Checking current database state..." -Level Info
    
    $config = $EnvironmentConfig[$Environment]
    
    try {
        # Try to get module info
        $moduleInfo = spacetime info $config.Module --server $config.Server 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Log "Module exists: $($config.Module)" -Level Info
            
            # Try to count tables
            $sqlQuery = 'SELECT COUNT(*) FROM Player'
            $result = spacetime sql $config.Module --server $config.Server $sqlQuery 2>&1
            
            if ($LASTEXITCODE -eq 0) {
                Write-Log "Database contains player data" -Level Info
                return "exists_with_data"
            }
            return "exists_empty"
        }
        return "not_exists"
    } catch {
        Write-Log "Could not determine database state: $_" -Level Warning
        return "unknown"
    }
}

function Deploy-Module {
    param(
        [string]$Environment,
        [bool]$ClearData
    )
    
    $config = $EnvironmentConfig[$Environment]
    Write-Log "Publishing to $Environment ($($config.Server))..." -Level Info
    
    Push-Location $ModulePath
    try {
        # Build publish command
        $publishCmd = "spacetime publish"
        
        if ($ClearData) {
            $publishCmd += " -c"  # Clear all data
            Write-Log "WARNING: Clearing all database data!" -Level Warning
        }
        
        if ($PublishOnly) {
            Write-Log "Publishing module only (no data operations)" -Level Info
        }
        
        # Add -y flag to auto-confirm for non-local environments
        if ($Environment -ne 'local') {
            $publishCmd += " -y"
        }
        
        $publishCmd += " --server $($config.Server) $($config.Module)"
        
        # Execute publish with proper output capture
        Write-Log "Executing: $publishCmd" -Level Debug
        
        # Create a process to run the command with timeout
        $pinfo = New-Object System.Diagnostics.ProcessStartInfo
        $pinfo.FileName = "cmd.exe"
        $pinfo.RedirectStandardError = $true
        $pinfo.RedirectStandardOutput = $true
        $pinfo.UseShellExecute = $false
        $pinfo.Arguments = "/c $publishCmd"
        $pinfo.WorkingDirectory = Get-Location
        
        $p = New-Object System.Diagnostics.Process
        $p.StartInfo = $pinfo
        
        Write-Log "Starting publish process..." -Level Info
        $p.Start() | Out-Null
        
        # Wait for completion with timeout (60 seconds)
        $completed = $p.WaitForExit(60000)
        
        if (!$completed) {
            Write-Log "Publish command timed out after 60 seconds" -Level Error
            $p.Kill()
            return $false
        }
        
        $stdout = $p.StandardOutput.ReadToEnd()
        $stderr = $p.StandardError.ReadToEnd()
        $exitCode = $p.ExitCode
        
        Write-Log "Publish output: $stdout" -Level Debug
        if ($stderr) {
            Write-Log "Publish errors: $stderr" -Level Debug
        }
        
        # Check for failure indicators
        if ($exitCode -ne 0 -or 
            $stderr -match "Error:" -or 
            $stdout -match "Error:" -or 
            $stdout -match "rejected:" -or 
            $stdout -match "requires a manual migration" -or
            $stdout -match "Aborting") {
            
            Write-Log "Publish failed with exit code: $exitCode" -Level Error
            if ($stdout) { Write-Log "Output: $stdout" -Level Error }
            if ($stderr) { Write-Log "Error: $stderr" -Level Error }
            
            # Check specifically for migration errors
            if ($stdout -match "requires a manual migration" -or $stderr -match "requires a manual migration") {
                Write-Log "Database schema changes require manual migration or -DeleteData flag" -Level Error
                Write-Log "To reset the database completely, use: -DeleteData -Yes" -Level Warning
            }
            
            # Check for abort
            if ($stdout -match "Aborting") {
                Write-Log "Publish was aborted - likely due to confirmation prompt" -Level Error
                Write-Log "Script should have used -y flag to auto-confirm" -Level Error
            }
            
            return $false
        }
        
        # Check if output indicates success
        if ($stdout -match "Published" -or 
            $stdout -match "Successfully published" -or
            $stdout -match "Created new database" -or
            $stdout -match "Publishing module\.\.\." -or
            $stdout -match "Updated database") {
            Write-Log "Module published successfully" -Level Success
            return $true
        } elseif ($stdout.Length -eq 0 -and $exitCode -eq 0) {
            Write-Log "Module appears to have published successfully (no output)" -Level Success
            return $true
        } else {
            Write-Log "Publish status unclear - output: $stdout" -Level Warning
            # Assume success if no clear error indicators
            return $exitCode -eq 0
        }
        
    } finally {
        Pop-Location
    }
}

function Deploy-WebGLToS3 {
    param([string]$Environment)
    
    $config = $EnvironmentConfig[$Environment]
    
    if ([string]::IsNullOrEmpty($config.S3Bucket)) {
        Write-Log "No S3 bucket configured for $Environment environment" -Level Warning
        return $false
    }
    
    Write-Log "Deploying WebGL build to S3 bucket: $($config.S3Bucket)" -Level Info
    
    # Check if WebGL build exists
    $buildPath = Join-Path $WebGLBuildPath $Environment
    if ($Environment -eq 'test') {
        $buildPath = Join-Path $WebGLBuildPath 'Test'
    } elseif ($Environment -eq 'production') {
        $buildPath = Join-Path $WebGLBuildPath 'Production'
    } elseif ($Environment -eq 'local') {
        $buildPath = Join-Path $WebGLBuildPath 'Local'
    }
    
    if (!(Test-Path $buildPath)) {
        Write-Log "WebGL build not found at: $buildPath" -Level Error
        Write-Log "Please build the WebGL client first using Unity" -Level Info
        return $false
    }
    
    # Check AWS CLI
    try {
        $awsVersion = aws --version 2>&1
        Write-Log "Found AWS CLI" -Level Success
    } catch {
        Write-Log "AWS CLI not found or configured" -Level Error
        return $false
    }
    
    # Sync files to S3
    Write-Log "Syncing files from $buildPath to s3://$($config.S3Bucket)/" -Level Info
    
    $syncCmd = "aws s3 sync `"$buildPath`" s3://$($config.S3Bucket)/ --delete --exclude `"*.meta`" --exclude `".DS_Store`""
    Write-Log "Executing: $syncCmd" -Level Debug
    
    $syncOutput = Invoke-Expression $syncCmd 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Log "WebGL build deployed successfully to S3" -Level Success
        
        # Count uploaded files
        $uploadCount = ($syncOutput | Where-Object { $_ -match 'upload:' }).Count
        if ($uploadCount -gt 0) {
            Write-Log "Uploaded $uploadCount files" -Level Info
        }
        
        return $true
    } else {
        Write-Log "S3 sync failed: $syncOutput" -Level Error
        return $false
    }
}

function Invalidate-CloudFront {
    param([string]$DistributionId)
    
    if ([string]::IsNullOrEmpty($DistributionId)) {
        Write-Log "No CloudFront distribution configured for this environment" -Level Info
        return $true
    }
    
    Write-Log "Invalidating CloudFront cache (Distribution: $DistributionId)..." -Level Info
    
    try {
        # Check AWS CLI
        $awsVersion = aws --version 2>&1
        
        # Create invalidation
        $invalidation = aws cloudfront create-invalidation `
            --distribution-id $DistributionId `
            --paths "/*" `
            --output json 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            $invalidationId = ($invalidation | ConvertFrom-Json).Invalidation.Id
            Write-Log "CloudFront invalidation created: $invalidationId" -Level Success
            return $true
        } else {
            Write-Log "CloudFront invalidation failed: $invalidation" -Level Warning
            return $false
        }
    } catch {
        Write-Log "AWS CLI not available or configured: $_" -Level Warning
        return $false
    }
}

function Update-BuildConfig {
    param([string]$Environment)
    
    if (!$BuildConfig) {
        return $true
    }
    
    Write-Log "Updating build configuration for WebGL..." -Level Info
    
    $config = $EnvironmentConfig[$Environment]
    $buildConfigPath = ".\SYSTEM-client-3d\Assets\StreamingAssets\build-config.json"
    
    # Ensure StreamingAssets directory exists
    $streamingAssetsDir = Split-Path $buildConfigPath -Parent
    if (!(Test-Path $streamingAssetsDir)) {
        New-Item -ItemType Directory -Path $streamingAssetsDir -Force | Out-Null
    }
    
    $buildConfig = @{
        environment = $Environment
        serverUrl = $config.Server
        moduleName = $config.Module
        buildTime = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        version = "2.0.0"
    }
    
    $buildConfig | ConvertTo-Json -Depth 10 | Set-Content $buildConfigPath
    Write-Log "Build configuration updated: $buildConfigPath" -Level Success
    
    return $true
}

function Verify-Deployment {
    param([string]$Environment)
    
    if (!$Verify) {
        return $true
    }
    
    Write-Log "Verifying deployment..." -Level Info
    
    $config = $EnvironmentConfig[$Environment]
    $verificationPassed = $true
    
    # Test 1: Module info
    Write-Log "Test 1: Checking module info..." -Level Info
    $moduleInfo = spacetime info $config.Module --server $config.Server 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Log "Module info retrieved successfully" -Level Success
    } else {
        Write-Log "Failed to retrieve module info: $moduleInfo" -Level Error
        $verificationPassed = $false
    }
    
    # Test 2: Table existence
    Write-Log "Test 2: Verifying table structure..." -Level Info
    $tables = @('Player', 'World', 'Orb', 'WavePacket', 'Crystal')
    
    foreach ($table in $tables) {
        $query = 'SELECT COUNT(*) FROM ' + $table
        $result = spacetime sql $config.Module --server $config.Server $query 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Log "Table $table verified" -Level Success
        } else {
            Write-Log "Table $table not accessible" -Level Warning
        }
    }
    
    # Test 3: Connection test (if not local)
    if ($Environment -ne 'local') {
        Write-Log "Test 3: Testing connection latency..." -Level Info
        $pingStart = Get-Date
        $testQuery = 'SELECT 1'
        $result = spacetime sql $config.Module --server $config.Server $testQuery 2>&1
        $pingTime = (Get-Date) - $pingStart
        
        if ($LASTEXITCODE -eq 0) {
            $pingMs = [Math]::Round($pingTime.TotalMilliseconds, 2)
            Write-Log "Connection test passed: $pingMs ms" -Level Success
        } else {
            Write-Log "Connection test failed" -Level Error
            $verificationPassed = $false
        }
    }
    
    # Load and execute SQL verification queries
    $verifyScript = ".\Scripts\post-deploy-verify.sql"
    if (Test-Path $verifyScript) {
        Write-Log "Running verification queries from $verifyScript..." -Level Info
        
        $queries = Get-Content $verifyScript -Raw
        # Split by GO statements
        $queryBatch = $queries -split '\nGO\n'
        
        foreach ($query in $queryBatch) {
            if (![string]::IsNullOrWhiteSpace($query)) {
                $query = $query.Trim()
                if ($query.StartsWith('--')) { continue }
                
                Write-Log "Executing: $($query.Substring(0, [Math]::Min(50, $query.Length)))..." -Level Debug
                $result = spacetime sql $config.Module --server $config.Server $query 2>&1
                
                if ($LASTEXITCODE -eq 0) {
                    Write-Log "Query executed successfully" -Level Success
                } else {
                    Write-Log "Query failed: $result" -Level Warning
                }
            }
        }
    }
    
    return $verificationPassed
}

function Show-Summary {
    $duration = (Get-Date) - $script:DeploymentStart
    
    Write-Host ""
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host "                    Deployment Summary                       " -ForegroundColor Cyan
    Write-Host "================================================================" -ForegroundColor Cyan
    
    Write-Host "Environment:      $Environment" -ForegroundColor White
    Write-Host "Duration:         $($duration.TotalSeconds) seconds" -ForegroundColor White
    Write-Host "Errors:           $script:ErrorCount" -ForegroundColor $(if ($script:ErrorCount -gt 0) { 'Red' } else { 'Green' })
    Write-Host "Warnings:         $script:WarningCount" -ForegroundColor $(if ($script:WarningCount -gt 0) { 'Yellow' } else { 'Green' })
    Write-Host "Log File:         $script:LogFile" -ForegroundColor Gray
    
    if ($script:ErrorCount -eq 0) {
        Write-Host ""
        Write-Host "DEPLOYMENT SUCCESSFUL!" -ForegroundColor Green
        
        $config = $EnvironmentConfig[$Environment]
        Write-Host ""
        Write-Host "Connection Details:" -ForegroundColor Cyan
        Write-Host "  Server:  $($config.Server)" -ForegroundColor White
        Write-Host "  Module:  $($config.Module)" -ForegroundColor White
        
        if ($Environment -eq 'local') {
            Write-Host ""
            Write-Host "Local server command:" -ForegroundColor Yellow
            Write-Host "  spacetime start" -ForegroundColor White
        }
    } else {
        Write-Host ""
        Write-Host "DEPLOYMENT FAILED!" -ForegroundColor Red
        Write-Host "Check the log file for details: $script:LogFile" -ForegroundColor Yellow
    }
}

function Confirm-Action {
    param([string]$Message)
    
    if ($Yes) {
        Write-Log "Auto-confirmed: $Message" -Level Info
        return $true
    }
    
    Write-Host ""
    Write-Host $Message -ForegroundColor Yellow
    Write-Host "Continue? (Y/N): " -NoNewline -ForegroundColor Cyan
    
    $response = Read-Host
    return ($response -eq 'Y' -or $response -eq 'y')
}

# Main deployment flow
function Start-Deployment {
    Show-Banner
    
    # Step 1: Prerequisites
    if (!(Test-Prerequisites)) {
        Write-Log "Prerequisites check failed" -Level Error
        Show-Summary
        exit 1
    }
    
    # Step 2: Check database state
    $dbState = Get-DatabaseState -Environment $Environment
    Write-Log "Database state: $dbState" -Level Info
    
    # Step 3: Confirm destructive operations
    if ($DeleteData -and $dbState -eq "exists_with_data") {
        if (!(Confirm-Action "WARNING: This will DELETE ALL DATA in the $Environment database!")) {
            Write-Log "Deployment cancelled by user" -Level Info
            exit 0
        }
    }
    
    # Step 4: Build module
    if (!(Build-Module)) {
        Write-Log "Module build failed" -Level Error
        Show-Summary
        exit 1
    }
    
    # Step 5: Update build config if needed
    if ($BuildConfig) {
        Update-BuildConfig -Environment $Environment
    }
    
    # Step 6: Publish module
    if (!(Deploy-Module -Environment $Environment -ClearData $DeleteData)) {
        Write-Log "Module publish failed" -Level Error
        Show-Summary
        exit 1
    }
    
    # Step 7: Deploy WebGL to S3 if requested
    if ($DeployWebGL) {
        if (!(Deploy-WebGLToS3 -Environment $Environment)) {
            Write-Log "WebGL deployment to S3 failed" -Level Warning
        }
    }
    
    # Step 8: Invalidate cache if requested
    if ($InvalidateCache) {
        $config = $EnvironmentConfig[$Environment]
        Invalidate-CloudFront -DistributionId $config.CloudFront
    }
    
    # Step 9: Verify deployment
    if ($Verify) {
        $verifyResult = Verify-Deployment -Environment $Environment
        if (!$verifyResult) {
            Write-Log "Deployment verification failed" -Level Warning
        }
    }
    
    # Show summary
    Show-Summary
    
    # Return exit code
    if ($script:ErrorCount -gt 0) {
        exit 1
    }
    exit 0
}

# Execute deployment
Start-Deployment