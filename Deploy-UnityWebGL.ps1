# Deploy-UnityWebGL.ps1
param(
    
    [string]$BucketName = 'system-unity-game',
    
    [string]$BuildPath = '',
    
    [ValidateSet('local', 'test', 'production')]
    [string]$Environment = 'test',
    
    [string]$Region = 'us-east-1',
    
    [switch]$PublishDatabase,
    
    [ValidateSet('local', 'test', 'production')]
    [string]$DatabaseEnvironment = 'test',
    
    [switch]$SkipS3Upload,
    
    [switch]$InvalidateCache,
    
    [string]$DistributionId = 'ENIM1XA5ZCZOT'
)

# Set build path based on environment if not explicitly provided
if ([string]::IsNullOrEmpty($BuildPath)) {
    $BuildPath = ".\SYSTEM-client-3d\Build\$([char]::ToUpper($Environment[0]) + $Environment.Substring(1))"
}

# Function to publish database module
function Publish-DatabaseModule {
    param(
        [string]$Environment
    )
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Publishing Database Module to: $Environment" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    
    # Change to server directory
    Push-Location ".\SYSTEM-server"
    
    try {
        # Build the Rust module
        Write-Host "[1/3] Building Rust module..." -ForegroundColor Yellow
        cargo build --release
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed!"
        }
        Write-Host ""
        
        # Generate C# bindings for Unity
        Write-Host "[2/3] Generating C# bindings..." -ForegroundColor Yellow
        spacetime generate --lang cs --out-dir ../SYSTEM-client-3d/Assets/scripts/autogen
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to generate bindings!"
        }
        Write-Host ""
        
        # Publish to SpacetimeDB
        Write-Host "[3/3] Publishing to SpacetimeDB..." -ForegroundColor Yellow
        
        switch ($Environment) {
            'local' {
                # Delete existing module for local
                spacetime delete system --server local 2>$null
                spacetime publish --server local system
            }
            'test' {
                # Publish to test cloud
                spacetime publish --server maincloud.spacetimedb.com system-test
            }
            'production' {
                # Publish to production cloud
                Write-Host "WARNING: Publishing to PRODUCTION!" -ForegroundColor Red
                $confirm = Read-Host "Are you sure? (yes/no)"
                if ($confirm -ne 'yes') {
                    throw "Production publish cancelled by user"
                }
                spacetime publish --server maincloud.spacetimedb.com system
            }
        }
        
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to publish module!"
        }
        
        Write-Host ""
        Write-Host "========================================" -ForegroundColor Green
        Write-Host "Database module published successfully!" -ForegroundColor Green
        Write-Host "========================================" -ForegroundColor Green
        Write-Host ""
    }
    catch {
        Write-Host ""
        Write-Host "ERROR: $_" -ForegroundColor Red
        Pop-Location
        exit 1
    }
    finally {
        Pop-Location
    }
}

# Publish database if requested
if ($PublishDatabase) {
    Publish-DatabaseModule -Environment $DatabaseEnvironment
}

# Skip S3 upload if requested
if ($SkipS3Upload) {
    Write-Host "Skipping S3 upload as requested" -ForegroundColor Yellow
    exit 0
}

# Check if build directory exists
if (-not (Test-Path $BuildPath)) {
    Write-Error 'Build directory not found: $BuildPath'
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Deploying Unity WebGL Build to S3" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Upload HTML file (no cache)
Write-Host ' Uploading index.html...' -ForegroundColor Yellow
aws s3 cp "$BuildPath\index.html" "s3://$BucketName/"  --content-type 'text/html' --cache-control 'no-cache, no-store, must-revalidate'

# Upload TemplateData
Write-Host ' Uploading TemplateData...' -ForegroundColor Yellow
aws s3 sync "$BuildPath\TemplateData" "s3://$BucketName/TemplateData" --delete

# Upload Build folder with correct content types
Write-Host ' Uploading Build files...' -ForegroundColor Yellow

# Upload framework and loader JS files
Get-ChildItem "$BuildPath\Build\*.js" | ForEach-Object {
    Write-Host "   Uploading $($_.Name)" -ForegroundColor Gray
    aws s3 cp $_.FullName "s3://$BucketName/Build/"  --content-type 'application/javascript'
}

# Upload WASM files
Get-ChildItem "$BuildPath\Build\*.wasm" | ForEach-Object {
    Write-Host "   Uploading $($_.Name)" -ForegroundColor Gray
    aws s3 cp $_.FullName "s3://$BucketName/Build/" --content-type 'application/wasm'
}

# Upload data files
Get-ChildItem "$BuildPath\Build\*.data" | ForEach-Object {
    Write-Host "   Uploading $($_.Name)" -ForegroundColor Gray
    aws s3 cp $_.FullName "s3://$BucketName/Build/"  --content-type 'application/octet-stream'
}

# Upload any .gz compressed files with proper encoding
Get-ChildItem "$BuildPath\Build\*.gz" -ErrorAction SilentlyContinue | ForEach-Object {
    $OriginalName = $_.Name -replace "\.gz$", ''
    $ContentType = switch -Regex ($OriginalName) {
        "\.js$" { 'application/javascript' }
        "\.wasm$" { 'application/wasm' }
        "\.data$" { 'application/octet-stream' }
        default { 'application/gzip' }
    }
    aws s3 cp $_.FullName "s3://$BucketName/Build/" --content-type $ContentType  --content-encoding 'gzip'
}

Get-ChildItem "$BuildPath\Build\*.br" | ForEach-Object {
    $file = $_.FullName
    $name = $_.Name
    
    # Determine content type
    $contentType = "application/octet-stream"
    if ($name -like "*.js.br") { $contentType = "application/javascript" }
    if ($name -like "*.wasm.br") { $contentType = "application/wasm" }
    
    Write-Host "Uploading $name"
    aws s3 cp $file "s3://$BucketName/Build/" --content-encoding br --content-type $contentType --cache-control "max-age=31536000"
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Unity WebGL deployment complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

# Invalidate CloudFront cache if requested
if ($InvalidateCache -and $DistributionId) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Invalidating CloudFront Cache" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    
    Write-Host "Creating invalidation for distribution: $DistributionId" -ForegroundColor Yellow
    
    try {
        $InvalidationResult = aws cloudfront create-invalidation `
            --distribution-id $DistributionId `
            --paths "/*" | ConvertFrom-Json
        
        Write-Host "Invalidation started successfully!" -ForegroundColor Green
        Write-Host "Invalidation ID: $($InvalidationResult.Invalidation.Id)" -ForegroundColor Green
        Write-Host "Status: $($InvalidationResult.Invalidation.Status)" -ForegroundColor Green
        Write-Host ""
        Write-Host "Note: Cache invalidation typically takes 10-15 minutes to complete." -ForegroundColor Yellow
    }
    catch {
        Write-Host "ERROR: Failed to create CloudFront invalidation" -ForegroundColor Red
        Write-Host "Error details: $_" -ForegroundColor Red
        Write-Host ""
        Write-Host "Make sure:" -ForegroundColor Yellow
        Write-Host "  - AWS CLI is configured with proper credentials" -ForegroundColor Yellow
        Write-Host "  - The distribution ID is correct: $DistributionId" -ForegroundColor Yellow
        Write-Host "  - You have CloudFront permissions" -ForegroundColor Yellow
    }
    
    Write-Host ""
}

<#
.SYNOPSIS
    Deploy Unity WebGL build to S3, optionally publish SpacetimeDB module and invalidate CloudFront cache

.DESCRIPTION
    This script deploys the Unity WebGL build to an S3 bucket, can optionally
    publish the SpacetimeDB server module to different environments, and
    invalidate CloudFront cache for immediate content updates.

.PARAMETER BucketName
    The S3 bucket to deploy to (default: system-unity-game)

.PARAMETER BuildPath
    Path to the Unity WebGL build (default: .\SYSTEM-client-3d\WebBuild)

.PARAMETER PublishDatabase
    Include this switch to also publish the SpacetimeDB module

.PARAMETER DatabaseEnvironment
    The environment to publish the database to: local, test, or production (default: test)

.PARAMETER SkipS3Upload
    Skip the S3 upload and only publish the database

.PARAMETER InvalidateCache
    Invalidate CloudFront cache after deployment to ensure users get the latest version

.PARAMETER DistributionId
    CloudFront distribution ID for cache invalidation (default: ENIM1XA5ZCZOT)

.EXAMPLE
    # Deploy WebGL build only
    .\Deploy-UnityWebGL.ps1

.EXAMPLE
    # Deploy WebGL with CloudFront cache invalidation
    .\Deploy-UnityWebGL.ps1 -InvalidateCache

.EXAMPLE
    # Deploy WebGL and publish database to test environment
    .\Deploy-UnityWebGL.ps1 -PublishDatabase

.EXAMPLE
    # Full deployment: WebGL, database to test, and CloudFront invalidation
    .\Deploy-UnityWebGL.ps1 -PublishDatabase -InvalidateCache

.EXAMPLE
    # Deploy WebGL and publish database to production with cache invalidation
    .\Deploy-UnityWebGL.ps1 -PublishDatabase -DatabaseEnvironment production -InvalidateCache

.EXAMPLE
    # Only publish database to local environment (no S3 upload)
    .\Deploy-UnityWebGL.ps1 -PublishDatabase -DatabaseEnvironment local -SkipS3Upload
#>

