# AWS Infrastructure Setup for SYSTEM QAI
# Run this once to create all required AWS resources

param(
    [switch]$DryRun,
    [switch]$SkipSecrets
)

$ErrorActionPreference = "Stop"

# Configuration
$AwsAccountId = "225284252441"
$AwsRegion = "us-east-1"
$EcrRepo = "system-qai"
$EcsCluster = "system-qai"

Write-Host "============================================" -ForegroundColor Green
Write-Host "AWS Infrastructure Setup for SYSTEM QAI"
Write-Host "============================================" -ForegroundColor Green
Write-Host "Account: ${AwsAccountId}"
Write-Host "Region: ${AwsRegion}"
if ($DryRun) {
    Write-Host "DRY RUN - No changes will be made" -ForegroundColor Yellow
}
Write-Host "============================================" -ForegroundColor Green

function Invoke-AwsCommand {
    param([string]$Description, [string]$Command)

    Write-Host ""
    Write-Host ">> $Description" -ForegroundColor Cyan
    Write-Host "   $Command" -ForegroundColor Gray

    if (-not $DryRun) {
        try {
            Invoke-Expression $Command
            Write-Host "   OK" -ForegroundColor Green
        } catch {
            Write-Host "   FAILED (may already exist): $_" -ForegroundColor Yellow
        }
    }
}

# 1. Create ECR Repository
Invoke-AwsCommand `
    -Description "Creating ECR repository: $EcrRepo" `
    -Command "aws ecr create-repository --repository-name $EcrRepo --region $AwsRegion"

# 2. Create ECS Cluster
Invoke-AwsCommand `
    -Description "Creating ECS cluster: $EcsCluster" `
    -Command "aws ecs create-cluster --cluster-name $EcsCluster --capacity-providers FARGATE FARGATE_SPOT --region $AwsRegion"

# 3. Create CloudWatch Log Groups
Invoke-AwsCommand `
    -Description "Creating CloudWatch log group: /ecs/system-qai-dev" `
    -Command "aws logs create-log-group --log-group-name /ecs/system-qai-dev --region $AwsRegion"

Invoke-AwsCommand `
    -Description "Creating CloudWatch log group: /ecs/system-qai-prod" `
    -Command "aws logs create-log-group --log-group-name /ecs/system-qai-prod --region $AwsRegion"

# 4. Create Secrets (placeholder values - update these!)
if (-not $SkipSecrets) {
    Write-Host ""
    Write-Host "Creating Secrets Manager secrets with PLACEHOLDER values" -ForegroundColor Yellow
    Write-Host "You MUST update these secrets with real QAI credentials!" -ForegroundColor Red

    $devSecret = '{"username":"qai-dev","pin":"change-me","displayName":"QAI-Dev"}'
    $prodSecret = '{"username":"qai","pin":"change-me","displayName":"QAI"}'

    Invoke-AwsCommand `
        -Description "Creating dev secrets: system-qai-credentials-dev" `
        -Command "aws secretsmanager create-secret --name system-qai-credentials-dev --secret-string '$devSecret' --region $AwsRegion"

    Invoke-AwsCommand `
        -Description "Creating prod secrets: system-qai-credentials-prod" `
        -Command "aws secretsmanager create-secret --name system-qai-credentials-prod --secret-string '$prodSecret' --region $AwsRegion"
}

# 5. Register Task Definitions
$scriptDir = $PSScriptRoot
Invoke-AwsCommand `
    -Description "Registering dev task definition" `
    -Command "aws ecs register-task-definition --cli-input-json file://$scriptDir/task-definition-dev.json --region $AwsRegion"

Invoke-AwsCommand `
    -Description "Registering prod task definition" `
    -Command "aws ecs register-task-definition --cli-input-json file://$scriptDir/task-definition-prod.json --region $AwsRegion"

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "Infrastructure setup complete!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Ensure ecsTaskExecutionRole exists with proper permissions"
Write-Host "2. Update secrets with real QAI credentials:"
Write-Host "   aws secretsmanager update-secret --secret-id system-qai-credentials-dev --secret-string '{...}'"
Write-Host "3. Note your VPC subnet IDs and security group IDs"
Write-Host "4. Run: .\deploy.ps1 -Environment dev"
Write-Host ""
Write-Host "To check ecsTaskExecutionRole:" -ForegroundColor Yellow
Write-Host "   aws iam get-role --role-name ecsTaskExecutionRole"
Write-Host ""
Write-Host "If role doesn't exist, create it with:" -ForegroundColor Yellow
Write-Host "   aws iam create-role --role-name ecsTaskExecutionRole --assume-role-policy-document file://ecs-trust-policy.json"
Write-Host "   aws iam attach-role-policy --role-name ecsTaskExecutionRole --policy-arn arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
