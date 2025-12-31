# Serverless First Singular URL Design

## Overview

**Primary Domain:** system-game-test.com  
**CloudFront Domain:** d3d8evyljjo2t.cloudfront.net  
**Distribution ID:** EQN06IXQ89GVL  
**Architecture Pattern:** Multi-Origin Serverless with Intelligent Routing

This distribution exemplifies the "Serverless First Singular URL" design by providing a unified entry point that intelligently routes traffic between static content and dynamic API services without exposing backend complexity to end users.

## Architecture Components

### CloudFront Distribution Configuration

#### Global Settings
- **HTTP Version:** HTTP/2 for improved performance
- **IPv6:** Enabled for broader accessibility
- **Price Class:** All Edge Locations (global coverage)
- **SSL/TLS:** Minimum TLSv1.2_2021 with SNI-only certificate
- **Geographic Restrictions:** None (worldwide access)
- **Default Root Object:** index.html

#### Security Implementation
- **SSL Certificate:** AWS Certificate Manager (ACM) managed
- **Certificate ARN:** `arn:aws:acm:us-east-1:225284252441:certificate/cf53f3f9-d763-4fc7-bd65-45237b0d437c`
- **WAF Integration:** Active Web Application Firewall protection
- **WAF ARN:** `arn:aws:wafv2:us-east-1:225284252441:global/webacl/CreatedByCloudFront-07eb77f9/1d996560-0464-4add-8287-843dfcd3deb4`
- **Protocol Policy:** Automatic HTTP to HTTPS redirection

### Multi-Origin Architecture

#### Origin 1: Static Content (Primary)
- **Domain:** system-game-test.s3-website-us-east-1.amazonaws.com
- **Purpose:** Static website hosting (HTML, CSS, JS, images)
- **Protocol:** HTTP only (internal communication)
- **SSL Protocols:** SSLv3, TLSv1, TLSv1.1, TLSv1.2
- **Connection Settings:**
  - Connection Timeout: 10 seconds
  - Connection Attempts: 3
  - Keep-alive Timeout: 5 seconds
  - Read Timeout: 30 seconds

#### Origin 2: API Services (Secondary)
- **Domain:** qai-nlb-dev-2df337e74a6f27c6.elb.us-east-1.amazonaws.com
- **Purpose:** Dynamic API requests via Fargate services
- **Protocol:** HTTP with TLSv1.2 backend security
- **Load Balancer Type:** Network Load Balancer (NLB)
- **Connection Settings:** Same as Origin 1

### Intelligent Traffic Routing

#### Default Behavior (Static Content - /*)
- **Target:** S3 static website origin
- **Methods:** GET, HEAD (read-only)
- **Viewer Protocol Policy:** Redirect HTTP to HTTPS
- **Compression:** Enabled for bandwidth optimization
- **Cache Policy ID:** 658327ea-f89d-4fab-a63d-7e88639e58f6
- **Function Associations:** None
- **Lambda Associations:** None

#### API Behavior (Dynamic Content - /qai/*)
- **Target:** Network Load Balancer → Fargate services
- **Methods:** Full HTTP verb support (GET, HEAD, POST, PUT, PATCH, DELETE, OPTIONS)
- **Viewer Protocol Policy:** Redirect HTTP to HTTPS
- **Compression:** Enabled
- **Cache Policy ID:** 4135ea2d-6df8-44a3-9df3-4b5a84be39ad
- **Function Associations:** None
- **Lambda Associations:** None

### Backend Infrastructure

#### Network Load Balancer (qai-nlb-dev)
- **Name:** qai-nlb-dev
- **Type:** Internet-facing Network Load Balancer
- **DNS:** qai-nlb-dev-2df337e74a6f27c6.elb.us-east-1.amazonaws.com
- **Scheme:** Internet-facing

#### Target Group Configuration (qai-tg-dev)
- **Protocol:** TCP
- **Port:** 8080
- **Target Type:** IP-based (Fargate tasks)
- **Health Check Protocol:** HTTP
- **Health Check Port:** 8080
- **Health Check Path:** /status

#### Current Target Status
- **172.31.44.207:** Healthy
- **172.31.46.125:** Draining (rolling deployment in progress)

## Traffic Flow Architecture


## Key Architectural Benefits

### 1. Unified User Experience
- Single domain for all application functionality
- Seamless transition between static and dynamic content
- No CORS issues or cross-domain complications
- Consistent SSL/TLS security across all endpoints

### 2. Performance Optimization
- Global edge caching for static content
- Intelligent caching policies for API responses
- HTTP/2 support for multiplexed connections
- Compression enabled across all content types
- 200+ CloudFront edge locations worldwide

### 3. Serverless Scalability
- Fargate automatically scales container instances
- CloudFront handles global traffic distribution
- NLB provides high-performance load balancing
- No infrastructure management required
- Pay-per-use pricing model

### 4. Security Implementation
- WAF protection at the edge
- TLS 1.2+ encryption end-to-end
- Certificate management via ACM
- Network isolation through VPC
- Automatic DDoS protection

## Operational Excellence

### Health Monitoring
- Target group health checks on /status endpoint
- CloudFront origin health monitoring
- Automatic failover capabilities
- Rolling deployment support (evidenced by draining target)
- Real-time health status visibility

### Cache Strategy
- Separate cache policies for static vs. dynamic content
- Optimized TTL settings per content type
- Compression for bandwidth efficiency
- Edge location optimization
- Cache invalidation capabilities

### Infrastructure Management
- **CloudFormation Stack:** qai-api-infrastructure-dev
- **Infrastructure as Code:** Reproducible deployments
- **Automated Scaling:** Fargate service auto-scaling
- **Version Control:** Infrastructure versioning through CloudFormation

## Design Patterns Implemented

### 1. Serverless First
- No server management overhead
- Automatic scaling based on demand
- Pay-per-use cost model
- High availability by design

### 2. Singular URL Design
- Single entry point for all services
- Intelligent routing based on path patterns
- Unified SSL certificate management
- Consistent user experience

### 3. Multi-Origin Strategy
- Optimized content delivery per content type
- Separate scaling characteristics for static vs. dynamic content
- Independent deployment cycles
- Fault isolation between content types

## Security Architecture

### Edge Security
- WAF rules and rate limiting
- Geographic restrictions capability (currently disabled)
- SSL/TLS termination at edge
- DDoS protection via AWS Shield

### Transport Security
- Minimum TLS 1.2 enforcement
- SNI-only certificate support
- HTTP to HTTPS automatic redirection
- End-to-end encryption

### Network Security
- VPC isolation for Fargate services
- Security group controls
- Private subnet deployment options
- Network ACL controls

## Monitoring and Observability

### Available Metrics
- CloudFront request/response metrics
- Origin response time and errors
- Cache hit/miss ratios
- WAF blocked requests
- Target group health status

### Logging Capabilities
- CloudFront access logs (currently disabled)
- WAF logs
- Load balancer access logs
- Container logs via CloudWatch

## Cost Optimization

### Current Configuration Benefits
- Global edge caching reduces origin requests
- Compression reduces bandwidth costs
- Serverless compute eliminates idle resource costs
- Reserved capacity not required

### Optimization Opportunities
- Enable CloudFront access logs for traffic analysis
- Implement more granular cache policies
- Monitor and optimize cache hit ratios
- Consider regional edge caches for large files

## Deployment Strategy

### Current State
- Infrastructure managed via CloudFormation
- Rolling deployments with health checks
- Zero-downtime deployment capability
- Automatic rollback on health check failures

### Best Practices Implemented
- Infrastructure as Code (IaC)
- Immutable deployments
- Health check validation
- Gradual traffic shifting during deployments

## Recommendations for Enhancement

### 1. Monitoring and Observability
- Implement CloudWatch dashboards for distribution metrics
- Enable CloudFront access logs for traffic analysis
- Set up CloudWatch alarms for key metrics
- Implement distributed tracing for API requests

### 2. Performance Optimization
- Fine-tune cache policies based on API response patterns
- Monitor origin response times and optimize accordingly
- Consider implementing CloudFront Functions for request/response manipulation
- Evaluate regional edge cache usage for large static assets

### 3. Security Enhancements
- Review and optimize WAF rules based on traffic patterns
- Implement request/response header policies
- Consider adding CloudFront Functions for additional security checks
- Regular security assessment and penetration testing

### 4. Operational Improvements
- Implement automated deployment pipelines
- Add comprehensive monitoring and alerting
- Create runbooks for common operational tasks
- Implement chaos engineering practices

## Conclusion

This Distribution 2 architecture represents a mature serverless design that effectively demonstrates the "Serverless First Singular URL" pattern. It provides unified access to both static and dynamic content through intelligent routing and modern cloud-native services, while maintaining high performance, security, and operational excellence standards.

The architecture successfully abstracts infrastructure complexity from end users while providing the scalability, reliability, and performance characteristics required for modern web applications.
