#!/bin/bash
# Deploy script for SYSTEM QAI Client to AWS ECR/Fargate
# Usage: ./deploy.sh [environment]
# Environments: dev (default), prod

set -e

# Configuration
AWS_ACCOUNT_ID="225284252441"
AWS_REGION="us-east-1"
ECR_REPO="system-qai"
ECS_CLUSTER="system-qai"

# Parse environment argument
ENVIRONMENT="${1:-dev}"

case "$ENVIRONMENT" in
    dev|development|test)
        TASK_DEFINITION="system-qai-dev"
        DOTNET_ENV="Development"
        IMAGE_TAG="dev"
        echo "Deploying to DEVELOPMENT environment"
        ;;
    prod|production)
        TASK_DEFINITION="system-qai-prod"
        DOTNET_ENV="Production"
        IMAGE_TAG="prod"
        echo "Deploying to PRODUCTION environment"
        ;;
    *)
        echo "Unknown environment: $ENVIRONMENT"
        echo "Usage: ./deploy.sh [dev|prod]"
        exit 1
        ;;
esac

ECR_URI="${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com/${ECR_REPO}"

echo "============================================"
echo "SYSTEM QAI Deployment"
echo "============================================"
echo "Environment: ${ENVIRONMENT}"
echo "DOTNET_ENVIRONMENT: ${DOTNET_ENV}"
echo "ECR URI: ${ECR_URI}:${IMAGE_TAG}"
echo "Task Definition: ${TASK_DEFINITION}"
echo "============================================"

# Step 1: Login to ECR
echo ""
echo "[1/4] Logging into ECR..."
aws ecr get-login-password --region ${AWS_REGION} | docker login --username AWS --password-stdin ${ECR_URI}

# Step 2: Build Docker image
echo ""
echo "[2/4] Building Docker image..."
cd "$(dirname "$0")/.."
docker build -t ${ECR_REPO}:${IMAGE_TAG} -f Dockerfile ..

# Step 3: Tag and push to ECR
echo ""
echo "[3/4] Pushing image to ECR..."
docker tag ${ECR_REPO}:${IMAGE_TAG} ${ECR_URI}:${IMAGE_TAG}
docker tag ${ECR_REPO}:${IMAGE_TAG} ${ECR_URI}:latest-${ENVIRONMENT}
docker push ${ECR_URI}:${IMAGE_TAG}
docker push ${ECR_URI}:latest-${ENVIRONMENT}

# Step 4: Update task definition (optional - uncomment if needed)
echo ""
echo "[4/4] Deployment complete!"
echo ""
echo "To run a Fargate task manually:"
echo "  aws ecs run-task \\"
echo "    --cluster ${ECS_CLUSTER} \\"
echo "    --task-definition ${TASK_DEFINITION} \\"
echo "    --launch-type FARGATE \\"
echo "    --network-configuration 'awsvpcConfiguration={subnets=[subnet-xxx],securityGroups=[sg-xxx],assignPublicIp=ENABLED}'"
echo ""
echo "To update the task definition:"
echo "  aws ecs register-task-definition --cli-input-json file://aws/task-definition-${ENVIRONMENT}.json"
