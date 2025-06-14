# Deploy-UnityWebGL.ps1
param(
    
    [string]$BucketName = 'system-unity-game',
    
    [string]$BuildPath = '.\SYSTEM-client-3d\WebBuild',
    
    [string]$Region = 'us-east-1'
)

# Check if build directory exists
if (-not (Test-Path $BuildPath)) {
    Write-Error 'Build directory not found: $BuildPath'
    exit 1
}

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

