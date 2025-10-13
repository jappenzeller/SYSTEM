param(
    [string]$Environment = 'test'
)

Write-Host "Starting minimal deployment test for $Environment"

function Test-Prerequisites {
    Write-Host "Testing prerequisites..."
    return $true
}

function Start-Deployment {
    Write-Host "In Start-Deployment"
    
    if (!(Test-Prerequisites)) {
        Write-Host "Prerequisites failed"
        exit 1
    }
    
    Write-Host "Prerequisites passed"
}

# Execute
Write-Host "About to call Start-Deployment"
Start-Deployment
Write-Host "Deployment completed"