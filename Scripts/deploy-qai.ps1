#!/usr/bin/env pwsh
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("test", "production")]
    [string]$Environment,

    [switch]$SkipBuild,

    [int]$WaitTimeout = 300
)

$ErrorActionPreference = "Stop"
$Region = "us-east-1"
$AccountId = "225284252441"
$RepoUri = "$AccountId.dkr.ecr.$Region.amazonaws.com/system-qai"
$Timestamp = Get-Date -Format 'yyyyMMddHHmmss'
$ImageTag = "$Environment-$Timestamp"

$Config = @{
    test = @{
        Cluster = "system-qai"
        Service = "qai-service-test"
        TaskFamily = "system-qai-test"
    }
    production = @{
        Cluster = "system-qai"
        Service = "qai-service-prod"
        TaskFamily = "system-qai-prod"
    }
}

$Env = $Config[$Environment]

function Write-Step { param([string]$Message); Write-Host "`n=== $Message ===" -ForegroundColor Cyan }
function Write-Success { param([string]$Message); Write-Host "[OK] $Message" -ForegroundColor Green }
function Write-Failure { param([string]$Message); Write-Host "[FAIL] $Message" -ForegroundColor Red }

# Step 1: Build Docker Image
if (-not $SkipBuild) {
    Write-Step "Building Docker image: $ImageTag"
    Set-Location "$PSScriptRoot\.."

    docker build -t "system-qai:$ImageTag" -f SYSTEM-headless-client/Dockerfile .
    if ($LASTEXITCODE -ne 0) { Write-Failure "Docker build failed"; exit 1 }
    Write-Success "Image built: system-qai:$ImageTag"
}

# Step 2: Login to ECR
Write-Step "Authenticating with ECR"
aws ecr get-login-password --region $Region | docker login --username AWS --password-stdin "$AccountId.dkr.ecr.$Region.amazonaws.com"
if ($LASTEXITCODE -ne 0) { Write-Failure "ECR login failed"; exit 1 }
Write-Success "ECR login successful"

# Step 3: Push Image
Write-Step "Pushing image to ECR"
docker tag "system-qai:$ImageTag" "${RepoUri}:$ImageTag"
docker push "${RepoUri}:$ImageTag"
if ($LASTEXITCODE -ne 0) { Write-Failure "Image push failed"; exit 1 }
Write-Success "Image pushed: ${RepoUri}:$ImageTag"

# Step 4: Get current task definition
Write-Step "Updating task definition"
$CurrentTaskDef = aws ecs describe-task-definition --task-definition $Env.TaskFamily --region $Region | ConvertFrom-Json
$ContainerDef = $CurrentTaskDef.taskDefinition.containerDefinitions[0]
$ContainerDef.image = "${RepoUri}:$ImageTag"

$NewTaskDef = @{
    family = $Env.TaskFamily
    containerDefinitions = @($ContainerDef)
    requiresCompatibilities = @("FARGATE")
    networkMode = "awsvpc"
    cpu = $CurrentTaskDef.taskDefinition.cpu
    memory = $CurrentTaskDef.taskDefinition.memory
    executionRoleArn = $CurrentTaskDef.taskDefinition.executionRoleArn
    taskRoleArn = $CurrentTaskDef.taskDefinition.taskRoleArn
}

$TaskDefPath = "$env:TEMP\taskdef-$Timestamp.json"
$NewTaskDef | ConvertTo-Json -Depth 20 | Out-File -FilePath $TaskDefPath -Encoding utf8

$RegisterResult = aws ecs register-task-definition --cli-input-json "file://$TaskDefPath" --region $Region | ConvertFrom-Json
$NewRevision = $RegisterResult.taskDefinition.revision
Write-Success "Task definition registered: $($Env.TaskFamily):$NewRevision"

# Step 5: Update service
Write-Step "Deploying to ECS"
aws ecs update-service --cluster $Env.Cluster --service $Env.Service --task-definition "$($Env.TaskFamily):$NewRevision" --force-new-deployment --region $Region | Out-Null
Write-Success "Deployment initiated"

# Step 6: Wait for deployment
Write-Step "Waiting for deployment (timeout: ${WaitTimeout}s)"
$StartTime = Get-Date
$LastStatus = ""

while ($true) {
    $Elapsed = ((Get-Date) - $StartTime).TotalSeconds
    if ($Elapsed -gt $WaitTimeout) { Write-Failure "Deployment timed out"; exit 1 }

    $ServiceStatus = aws ecs describe-services --cluster $Env.Cluster --services $Env.Service --region $Region | ConvertFrom-Json
    $Deployments = $ServiceStatus.services[0].deployments
    $Primary = $Deployments | Where-Object { $_.status -eq "PRIMARY" }

    $Status = "Running: $($Primary.runningCount)/$($Primary.desiredCount)"
    if ($Status -ne $LastStatus) { Write-Host "  [$([math]::Floor($Elapsed))s] $Status"; $LastStatus = $Status }

    if ($Deployments.Count -eq 1 -and $Primary.runningCount -eq $Primary.desiredCount -and $Primary.desiredCount -gt 0) {
        Write-Success "Deployment complete"
        break
    }
    Start-Sleep -Seconds 10
}

# Step 7: Validate
Write-Step "Validating deployment"
$Tasks = aws ecs list-tasks --cluster $Env.Cluster --service-name $Env.Service --region $Region | ConvertFrom-Json
$TaskArn = $Tasks.taskArns[0]
$TaskInfo = aws ecs describe-tasks --cluster $Env.Cluster --tasks $TaskArn --region $Region | ConvertFrom-Json
$Task = $TaskInfo.tasks[0]

$RunningImage = $Task.containers[0].image
if ($RunningImage -ne "${RepoUri}:$ImageTag") { Write-Failure "Wrong image: $RunningImage"; exit 1 }
Write-Success "Correct image: $ImageTag"
Write-Success "Task status: $($Task.lastStatus)"

Write-Host "`n=== DEPLOYMENT SUCCESSFUL ===" -ForegroundColor Green
Write-Host "  Environment: $Environment"
Write-Host "  Image: $ImageTag"
Write-Host "  Task Def: $($Env.TaskFamily):$NewRevision"

Remove-Item -Path $TaskDefPath -ErrorAction SilentlyContinue
