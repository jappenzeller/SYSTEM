# AWS Fargate Deployment for SYSTEM Headless Client

## Overview
Deploy the headless SpacetimeDB bot client to AWS Fargate for persistent bot hosting.
Both development and production environments run in the same AWS account (225284252441).

## Environments

| Environment | DOTNET_ENVIRONMENT | SpacetimeDB Module | Secrets |
|-------------|-------------------|-------------------|---------|
| Development | `Development` | `system-test` | `system-bot-credentials-dev` |
| Production | `Production` | `system` | `system-bot-credentials-prod` |

## Infrastructure Requirements

### 1. ECR Repository
```bash
aws ecr create-repository --repository-name system-headless-client --region us-east-1
```

### 2. ECS Cluster
```bash
aws ecs create-cluster --cluster-name system-bots --capacity-providers FARGATE FARGATE_SPOT
```

### 3. Secrets Manager (per environment)
```bash
# Development secrets
aws secretsmanager create-secret --name system-bot-credentials-dev \
  --secret-string '{"username":"bot1","pin":"1234","displayName":"TestBot"}'

# Production secrets
aws secretsmanager create-secret --name system-bot-credentials-prod \
  --secret-string '{"username":"prodbot1","pin":"securepin","displayName":"ProdBot"}'
```

### 4. CloudWatch Log Groups
```bash
aws logs create-log-group --log-group-name /ecs/system-headless-client-dev
aws logs create-log-group --log-group-name /ecs/system-headless-client-prod
```

### 5. IAM Role (ecsTaskExecutionRole)
Standard ECS task execution role with:
- ECR pull permissions
- CloudWatch Logs permissions
- Secrets Manager read permissions for `system-bot-credentials-*`

### 6. VPC/Networking
- Needs outbound internet access for SpacetimeDB connection (wss://)
- No inbound ports required (client-only)
- Assign public IP or use NAT gateway

## Quick Deploy

### Using deploy.sh
```bash
# Deploy to development (test server)
./aws/deploy.sh dev

# Deploy to production
./aws/deploy.sh prod
```

### Manual Deployment Steps

#### 1. Login to ECR
```bash
aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin 225284252441.dkr.ecr.us-east-1.amazonaws.com
```

#### 2. Build and Push Image
```bash
# From SYSTEM-headless-client directory
docker build -t system-headless-client -f Dockerfile ..

# Tag for environment
docker tag system-headless-client:latest 225284252441.dkr.ecr.us-east-1.amazonaws.com/system-headless-client:dev
docker push 225284252441.dkr.ecr.us-east-1.amazonaws.com/system-headless-client:dev
```

#### 3. Register Task Definition
```bash
# Development
aws ecs register-task-definition --cli-input-json file://aws/task-definition-dev.json

# Production
aws ecs register-task-definition --cli-input-json file://aws/task-definition-prod.json
```

#### 4. Run Fargate Task
```bash
aws ecs run-task \
  --cluster system-bots \
  --task-definition system-headless-client-dev \
  --launch-type FARGATE \
  --network-configuration "awsvpcConfiguration={subnets=[subnet-xxx],securityGroups=[sg-xxx],assignPublicIp=ENABLED}"
```

## Task Definitions

| File | Environment | Image Tag | Secret Prefix |
|------|-------------|-----------|---------------|
| `task-definition-dev.json` | Development | `:dev` | `system-bot-credentials-dev` |
| `task-definition-prod.json` | Production | `:prod` | `system-bot-credentials-prod` |

## Environment Variables

| Variable | Description | Source |
|----------|-------------|--------|
| `DOTNET_ENVIRONMENT` | `Development` or `Production` | Task definition |
| `BOT__USERNAME` | Bot account username | Secrets Manager |
| `BOT__PIN` | Bot account PIN | Secrets Manager |
| `BOT__DISPLAYNAME` | In-game display name | Secrets Manager |

## Cost Estimate (Fargate)

| Resource | Specification | Estimated Cost |
|----------|--------------|----------------|
| CPU | 256 (0.25 vCPU) | ~$0.01/hour |
| Memory | 512 MB | Included |
| **Fargate Spot** | 70% discount | ~$0.003/hour |

Consider Fargate Spot for non-critical bots to reduce costs significantly.

## Monitoring

### View Logs
```bash
# Development logs
aws logs tail /ecs/system-headless-client-dev --follow

# Production logs
aws logs tail /ecs/system-headless-client-prod --follow
```

### List Running Tasks
```bash
aws ecs list-tasks --cluster system-bots --desired-status RUNNING
```

### Stop All Tasks
```bash
# Get task ARNs and stop them
for task in $(aws ecs list-tasks --cluster system-bots --query 'taskArns[]' --output text); do
  aws ecs stop-task --cluster system-bots --task $task
done
```

## Amazon Q Prompt for Infrastructure Setup

Use this prompt with Amazon Q CLI or console to generate CloudFormation/CDK:

```
Create AWS infrastructure for a .NET 8 Fargate task that:
1. Runs a long-lived bot client connecting to SpacetimeDB (wss://maincloud.spacetimedb.com)
2. Needs outbound internet only (no inbound)
3. Pulls credentials from Secrets Manager (system-bot-credentials-dev and system-bot-credentials-prod)
4. Logs to CloudWatch (/ecs/system-headless-client-dev and /ecs/system-headless-client-prod)
5. Uses Fargate Spot for cost savings
6. ECR repository: system-headless-client
7. ECS cluster: system-bots
8. Supports both dev and prod environments in same account
9. AWS Account: 225284252441, Region: us-east-1

Please provide CloudFormation or CDK template.
```

## NLB Infrastructure for QAI API

To expose the QAI command API via CloudFront with fixed IPs:

### Option 1: CloudFormation (Recommended)

```bash
# Deploy the NLB infrastructure stack
aws cloudformation create-stack \
  --stack-name qai-api-infrastructure-dev \
  --template-body file://aws/qai-api-infrastructure.yaml \
  --parameters \
    ParameterKey=Environment,ParameterValue=dev \
    ParameterKey=VpcId,ParameterValue=vpc-xxx \
    ParameterKey=SubnetId1,ParameterValue=subnet-xxx \
    ParameterKey=SubnetId2,ParameterValue=subnet-yyy

# Wait for stack completion
aws cloudformation wait stack-create-complete --stack-name qai-api-infrastructure-dev

# Get outputs (NLB DNS for CloudFront)
aws cloudformation describe-stacks --stack-name qai-api-infrastructure-dev --query 'Stacks[0].Outputs'
```

### Option 2: PowerShell Script

```powershell
# Run the setup script
.\aws\setup-nlb.ps1 -VpcId vpc-xxx -SubnetId1 subnet-xxx -SubnetId2 subnet-yyy -Environment dev

# Dry run first
.\aws\setup-nlb.ps1 -VpcId vpc-xxx -SubnetId1 subnet-xxx -SubnetId2 subnet-yyy -Environment dev -DryRun
```

### CloudFront Configuration

After NLB is created, add an origin to your CloudFront distribution:

| Setting | Value |
|---------|-------|
| Origin Domain | `<NLB-DNS-name>` |
| Origin Protocol | HTTP only |
| Origin Port | 8080 |
| Path Pattern | `/qai/*` |
| Cache Policy | CachingDisabled |
| Origin Request Policy | AllViewer |

### API Endpoints

Once configured, access the QAI API at:

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/qai/status` | GET | Get QAI status |
| `/qai/sources` | GET | List sources in range |
| `/qai/walk` | GET/POST | Walking status/control |
| `/qai/plan` | GET/POST | Plan execution status/control |
| `/qai/mine/start` | POST | Start mining |
| `/qai/mine/stop` | POST | Stop mining |
| `/qai/move` | POST | Move QAI |
| `/qai/scan` | POST | Force source scan |

## Troubleshooting

### Task Fails to Start
- Check CloudWatch logs for error messages
- Verify secrets exist and have correct format
- Ensure security group allows outbound HTTPS (443)

### Connection Errors
- Verify `DOTNET_ENVIRONMENT` is set correctly
- Check SpacetimeDB module exists (`system-test` for dev, `system` for prod)
- Ensure network has internet access

### Authentication Failures
- Verify secrets contain valid credentials
- Check if bot account exists on SpacetimeDB server
- Review CloudWatch logs for specific error messages
