# AWS NLB Infrastructure Setup for SYSTEM QAI API
# Creates NLB with fixed Elastic IPs for CloudFront routing
#
# Prerequisites:
# - AWS CLI configured
# - VPC with subnets in 2 AZs
# - Existing ECS cluster (system-qai)

param(
    [Parameter(Mandatory=$true)]
    [string]$VpcId,

    [Parameter(Mandatory=$true)]
    [string]$SubnetId1,

    [Parameter(Mandatory=$true)]
    [string]$SubnetId2,

    [ValidateSet("dev", "prod")]
    [string]$Environment = "dev",

    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# Configuration
$AwsRegion = "us-east-1"
$NlbName = "qai-nlb-$Environment"
$TgName = "qai-tg-$Environment"
$SgName = "qai-nlb-sg-$Environment"
$EipName1 = "qai-nlb-eip-1-$Environment"
$EipName2 = "qai-nlb-eip-2-$Environment"

Write-Host "============================================" -ForegroundColor Green
Write-Host "SYSTEM QAI - NLB Infrastructure Setup"
Write-Host "============================================" -ForegroundColor Green
Write-Host "Environment: $Environment"
Write-Host "VPC: $VpcId"
Write-Host "Subnets: $SubnetId1, $SubnetId2"
Write-Host "Region: $AwsRegion"
if ($DryRun) {
    Write-Host "MODE: DRY RUN (no changes will be made)" -ForegroundColor Yellow
}
Write-Host "============================================" -ForegroundColor Green
Write-Host ""

function Invoke-AwsCommand {
    param(
        [string]$Description,
        [string]$Command,
        [switch]$CaptureOutput
    )

    Write-Host ">> $Description" -ForegroundColor Cyan
    Write-Host "   $Command" -ForegroundColor Gray

    if (-not $DryRun) {
        try {
            if ($CaptureOutput) {
                $result = Invoke-Expression $Command
                Write-Host "   OK" -ForegroundColor Green
                return $result
            } else {
                Invoke-Expression $Command
                Write-Host "   OK" -ForegroundColor Green
            }
        } catch {
            Write-Host "   FAILED: $_" -ForegroundColor Red
            throw
        }
    } else {
        Write-Host "   [DRY RUN - skipped]" -ForegroundColor Yellow
        return $null
    }
}

# Step 1: Create Security Group
Write-Host "`n[1/6] Creating Security Group..." -ForegroundColor Yellow

$sgId = $null
try {
    $sgResult = Invoke-AwsCommand `
        -Description "Creating security group: $SgName" `
        -Command "aws ec2 create-security-group --group-name $SgName --description 'Security group for QAI NLB' --vpc-id $VpcId --region $AwsRegion --output json" `
        -CaptureOutput

    if ($sgResult) {
        $sgId = ($sgResult | ConvertFrom-Json).GroupId
        Write-Host "   Security Group ID: $sgId" -ForegroundColor Green
    }
} catch {
    # Try to get existing SG
    Write-Host "   Checking for existing security group..." -ForegroundColor Yellow
    $existingSg = aws ec2 describe-security-groups --filters "Name=group-name,Values=$SgName" --region $AwsRegion --output json | ConvertFrom-Json
    if ($existingSg.SecurityGroups.Count -gt 0) {
        $sgId = $existingSg.SecurityGroups[0].GroupId
        Write-Host "   Using existing Security Group: $sgId" -ForegroundColor Green
    }
}

# Add inbound rule for port 8080
if ($sgId) {
    Invoke-AwsCommand `
        -Description "Adding inbound rule for port 8080" `
        -Command "aws ec2 authorize-security-group-ingress --group-id $sgId --protocol tcp --port 8080 --cidr 0.0.0.0/0 --region $AwsRegion 2>&1 || Write-Host '   (rule may already exist)'"
}

# Step 2: Allocate Elastic IPs
Write-Host "`n[2/6] Allocating Elastic IPs..." -ForegroundColor Yellow

$eipAlloc1 = $null
$eipAlloc2 = $null

$eipResult1 = Invoke-AwsCommand `
    -Description "Allocating EIP 1: $EipName1" `
    -Command "aws ec2 allocate-address --domain vpc --tag-specifications 'ResourceType=elastic-ip,Tags=[{Key=Name,Value=$EipName1}]' --region $AwsRegion --output json" `
    -CaptureOutput

if ($eipResult1) {
    $eipAlloc1 = ($eipResult1 | ConvertFrom-Json).AllocationId
    $eipIp1 = ($eipResult1 | ConvertFrom-Json).PublicIp
    Write-Host "   EIP 1: $eipIp1 (Allocation: $eipAlloc1)" -ForegroundColor Green
}

$eipResult2 = Invoke-AwsCommand `
    -Description "Allocating EIP 2: $EipName2" `
    -Command "aws ec2 allocate-address --domain vpc --tag-specifications 'ResourceType=elastic-ip,Tags=[{Key=Name,Value=$EipName2}]' --region $AwsRegion --output json" `
    -CaptureOutput

if ($eipResult2) {
    $eipAlloc2 = ($eipResult2 | ConvertFrom-Json).AllocationId
    $eipIp2 = ($eipResult2 | ConvertFrom-Json).PublicIp
    Write-Host "   EIP 2: $eipIp2 (Allocation: $eipAlloc2)" -ForegroundColor Green
}

# Step 3: Create Target Group
Write-Host "`n[3/6] Creating Target Group..." -ForegroundColor Yellow

$tgArn = $null
$tgResult = Invoke-AwsCommand `
    -Description "Creating target group: $TgName" `
    -Command "aws elbv2 create-target-group --name $TgName --protocol TCP --port 8080 --vpc-id $VpcId --target-type ip --health-check-protocol HTTP --health-check-path /status --health-check-port 8080 --region $AwsRegion --output json" `
    -CaptureOutput

if ($tgResult) {
    $tgArn = ($tgResult | ConvertFrom-Json).TargetGroups[0].TargetGroupArn
    Write-Host "   Target Group ARN: $tgArn" -ForegroundColor Green
}

# Step 4: Create Network Load Balancer with Elastic IPs
Write-Host "`n[4/6] Creating Network Load Balancer..." -ForegroundColor Yellow

$nlbArn = $null
$nlbDns = $null

if ($eipAlloc1 -and $eipAlloc2) {
    $subnetMappings = "SubnetId=$SubnetId1,AllocationId=$eipAlloc1 SubnetId=$SubnetId2,AllocationId=$eipAlloc2"

    $nlbResult = Invoke-AwsCommand `
        -Description "Creating NLB: $NlbName with fixed IPs" `
        -Command "aws elbv2 create-load-balancer --name $NlbName --type network --subnet-mappings $subnetMappings --region $AwsRegion --output json" `
        -CaptureOutput

    if ($nlbResult) {
        $nlb = ($nlbResult | ConvertFrom-Json).LoadBalancers[0]
        $nlbArn = $nlb.LoadBalancerArn
        $nlbDns = $nlb.DNSName
        Write-Host "   NLB ARN: $nlbArn" -ForegroundColor Green
        Write-Host "   NLB DNS: $nlbDns" -ForegroundColor Green
    }
}

# Step 5: Create Listener
Write-Host "`n[5/6] Creating NLB Listener..." -ForegroundColor Yellow

if ($nlbArn -and $tgArn) {
    Invoke-AwsCommand `
        -Description "Creating listener on port 8080" `
        -Command "aws elbv2 create-listener --load-balancer-arn $nlbArn --protocol TCP --port 8080 --default-actions Type=forward,TargetGroupArn=$tgArn --region $AwsRegion"
}

# Step 6: Output configuration
Write-Host "`n[6/6] Generating Configuration..." -ForegroundColor Yellow

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "NLB Infrastructure Created Successfully!"
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "Fixed Elastic IPs:" -ForegroundColor Cyan
if ($eipIp1) { Write-Host "  - $eipIp1" }
if ($eipIp2) { Write-Host "  - $eipIp2" }
Write-Host ""
Write-Host "NLB DNS Name:" -ForegroundColor Cyan
if ($nlbDns) { Write-Host "  $nlbDns" }
Write-Host ""
Write-Host "Target Group ARN (update service-definition-$Environment.json):" -ForegroundColor Cyan
if ($tgArn) { Write-Host "  $tgArn" }
Write-Host ""
Write-Host "Security Group ID (update service-definition-$Environment.json):" -ForegroundColor Cyan
if ($sgId) { Write-Host "  $sgId" }
Write-Host ""

# Create output JSON for automation
$output = @{
    environment = $Environment
    nlb = @{
        arn = $nlbArn
        dnsName = $nlbDns
    }
    targetGroup = @{
        arn = $tgArn
    }
    securityGroup = @{
        id = $sgId
    }
    elasticIps = @(
        @{ allocationId = $eipAlloc1; publicIp = $eipIp1 },
        @{ allocationId = $eipAlloc2; publicIp = $eipIp2 }
    )
    subnets = @($SubnetId1, $SubnetId2)
}

$outputPath = Join-Path $PSScriptRoot "nlb-config-$Environment.json"
$output | ConvertTo-Json -Depth 5 | Set-Content $outputPath
Write-Host "Configuration saved to: $outputPath" -ForegroundColor Green
Write-Host ""

Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Update service-definition-$Environment.json with:"
Write-Host "   - targetGroupArn: $tgArn"
Write-Host "   - securityGroups: ['$sgId']"
Write-Host "   - subnets: ['$SubnetId1', '$SubnetId2']"
Write-Host ""
Write-Host "2. Register the updated task definition:"
Write-Host "   aws ecs register-task-definition --cli-input-json file://task-definition-$Environment.json"
Write-Host ""
Write-Host "3. Create the ECS service:"
Write-Host "   aws ecs create-service --cli-input-json file://service-definition-$Environment.json"
Write-Host ""
Write-Host "4. Add NLB origin to CloudFront distribution:"
Write-Host "   Origin Domain: $nlbDns"
Write-Host "   Origin Port: 8080"
Write-Host "   Path Pattern: /qai/*"
Write-Host ""
