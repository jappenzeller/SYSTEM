# Apply MCP-Optimized CloudFront Configuration
# Increases timeouts for long-running AI operations and adds proper header forwarding

$ErrorActionPreference = "Stop"

$DistributionId = "EQN06IXQ89GVL"
$ConfigFile = "cf-config-mcp-optimized.json"

Write-Host "Applying MCP-optimized CloudFront configuration..." -ForegroundColor Cyan

# Update distribution
aws cloudfront update-distribution `
    --id $DistributionId `
    --if-match (Get-Content $ConfigFile | ConvertFrom-Json).ETag `
    --distribution-config (Get-Content $ConfigFile | ConvertFrom-Json).DistributionConfig `
    --output json

Write-Host "`nConfiguration applied. Changes:" -ForegroundColor Green
Write-Host "  - NLB origin read timeout: 30s → 300s (5 min)" -ForegroundColor Yellow
Write-Host "  - NLB origin keepalive: 5s → 60s" -ForegroundColor Yellow
Write-Host "  - Added AllViewer origin request policy for /qai/*" -ForegroundColor Yellow
Write-Host "`nCloudFront will propagate changes to edge locations (5-10 min)" -ForegroundColor Cyan
