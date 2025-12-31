# Deploy script for SYSTEM QAI Client to AWS ECR/Fargate
# Usage: .\deploy.ps1 [-Environment dev|prod]

param(
    [ValidateSet("dev", "prod", "development", "production", "test")]
    [string]$Environment = "dev"
)

$ErrorActionPreference = "Stop"

# Configuration
$AwsAccountId = "225284252441"
$AwsRegion = "us-east-1"
$EcrRepo = "system-qai"
$EcsCluster = "system-qai"

# Normalize environment
switch ($Environment) {
    { $_ -in "dev", "development", "test" } {
        $TaskDefinition = "system-qai-dev"
        $DotnetEnv = "Development"
        $ImageTag = "dev"
        Write-Host "Deploying to DEVELOPMENT environment" -ForegroundColor Cyan
    }
    { $_ -in "prod", "production" } {
        $TaskDefinition = "system-qai-prod"
        $DotnetEnv = "Production"
        $ImageTag = "prod"
        Write-Host "Deploying to PRODUCTION environment" -ForegroundColor Yellow
    }
}

$EcrUri = "${AwsAccountId}.dkr.ecr.${AwsRegion}.amazonaws.com/${EcrRepo}"

Write-Host "============================================" -ForegroundColor Green
Write-Host "SYSTEM QAI Deployment"
Write-Host "============================================" -ForegroundColor Green
Write-Host "Environment: ${Environment}"
Write-Host "DOTNET_ENVIRONMENT: ${DotnetEnv}"
Write-Host "ECR URI: ${EcrUri}:${ImageTag}"
Write-Host "Task Definition: ${TaskDefinition}"
Write-Host "============================================" -ForegroundColor Green

# Step 1: Login to ECR
Write-Host ""
Write-Host "[1/4] Logging into ECR..." -ForegroundColor Yellow
$loginPassword = aws ecr get-login-password --region $AwsRegion
docker login --username AWS --password $loginPassword $EcrUri

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to login to ECR" -ForegroundColor Red
    exit 1
}

# Step 2: Build Docker image
Write-Host ""
Write-Host "[2/4] Building Docker image..." -ForegroundColor Yellow
Push-Location (Join-Path $PSScriptRoot "..")
try {
    docker build -t "${EcrRepo}:${ImageTag}" -f Dockerfile ..
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Failed to build Docker image" -ForegroundColor Red
        exit 1
    }
} finally {
    Pop-Location
}

# Step 3: Tag and push to ECR
Write-Host ""
Write-Host "[3/4] Pushing image to ECR..." -ForegroundColor Yellow
docker tag "${EcrRepo}:${ImageTag}" "${EcrUri}:${ImageTag}"
docker tag "${EcrRepo}:${ImageTag}" "${EcrUri}:latest-${Environment}"
docker push "${EcrUri}:${ImageTag}"
docker push "${EcrUri}:latest-${Environment}"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to push Docker image" -ForegroundColor Red
    exit 1
}

# Step 4: Complete
Write-Host ""
Write-Host "[4/4] Deployment complete!" -ForegroundColor Green
Write-Host ""
Write-Host "To run a Fargate task manually:" -ForegroundColor Cyan
Write-Host "  aws ecs run-task ``"
Write-Host "    --cluster ${EcsCluster} ``"
Write-Host "    --task-definition ${TaskDefinition} ``"
Write-Host "    --launch-type FARGATE ``"
Write-Host "    --network-configuration 'awsvpcConfiguration={subnets=[subnet-xxx],securityGroups=[sg-xxx],assignPublicIp=ENABLED}'"
Write-Host ""
Write-Host "To update the task definition:" -ForegroundColor Cyan
Write-Host "  aws ecs register-task-definition --cli-input-json file://aws/task-definition-${Environment}.json"
