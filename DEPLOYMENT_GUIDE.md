# SYSTEM Deployment Guide

## Overview

The updated `Deploy-SYSTEM.ps1` script provides comprehensive deployment automation for the SYSTEM game, supporting multiple environments with proper build directory management and database coordination.

## Quick Start

### Basic Usage

```powershell
# Deploy to different environments
.\Deploy-SYSTEM.ps1 -Environment local      # Local development
.\Deploy-SYSTEM.ps1 -Environment test       # Test environment
.\Deploy-SYSTEM.ps1 -Environment production # Production release
```

### Common Workflows

```powershell
# Build only (no deployment)
.\Deploy-SYSTEM.ps1 -Environment test -BuildOnly

# Deploy existing build only
.\Deploy-SYSTEM.ps1 -Environment test -DeployOnly

# Full deployment with server update
.\Deploy-SYSTEM.ps1 -Environment test -DeployServer

# Production deployment with cache invalidation
.\Deploy-SYSTEM.ps1 -Environment production -InvalidateCache
```

## Environment Configuration

### Build Directory Structure

The script automatically uses the correct build directory for each environment:

| Environment | Build Directory | Server URL | Module Name |
|------------|----------------|------------|-------------|
| **local** | `SYSTEM-client-3d/Build/Local/` | http://127.0.0.1:3000 | system |
| **test** | `SYSTEM-client-3d/Build/Test/` | https://maincloud.spacetimedb.com | system-test |
| **production** | `SYSTEM-client-3d/Build/Production/` | https://maincloud.spacetimedb.com | system |

### AWS Configuration

Test and production environments require AWS S3 buckets and CloudFront distributions:

- **Test**: S3 bucket `system-game-test`, CloudFront ID `EQN06IXQ89GVL`
- **Production**: S3 bucket `system-unity-game`, CloudFront ID `ENIM1XA5ZCZOT`

## Script Parameters

| Parameter | Description | Example |
|-----------|-------------|---------|
| `-Environment` | Target environment (required) | `local`, `test`, `production` |
| `-BuildOnly` | Only build Unity, skip deployment | `-BuildOnly` |
| `-DeployOnly` | Deploy existing build only | `-DeployOnly` |
| `-DeployServer` | Also deploy SpacetimeDB module | `-DeployServer` |
| `-InvalidateCache` | Invalidate CloudFront cache | `-InvalidateCache` |
| `-RebuildUnity` | Force rebuild even if exists | `-RebuildUnity` |
| `-SkipValidation` | Skip build validation checks | `-SkipValidation` |
| `-UnityPath` | Custom Unity executable path | `-UnityPath "C:\Unity\..."` |

## Deployment Workflows

### 1. Local Development

```powershell
# Build and prepare for local testing
.\Deploy-SYSTEM.ps1 -Environment local

# With local server deployment
.\Deploy-SYSTEM.ps1 -Environment local -DeployServer
```

**Result**: 
- Builds to `Build/Local/`
- Configured for `localhost:3000`
- No AWS deployment
- Instructions for local testing provided

### 2. Test Environment Deployment

```powershell
# Full test deployment (build + deploy)
.\Deploy-SYSTEM.ps1 -Environment test

# With database update
.\Deploy-SYSTEM.ps1 -Environment test -DeployServer

# Deploy existing build only
.\Deploy-SYSTEM.ps1 -Environment test -DeployOnly
```

**Result**:
- Builds to `Build/Test/`
- Deploys to S3 test bucket
- Connects to `system-test` module
- Optional CloudFront invalidation

### 3. Production Release

```powershell
# Full production deployment with all options
.\Deploy-SYSTEM.ps1 -Environment production -DeployServer -InvalidateCache

# Build first, deploy later
.\Deploy-SYSTEM.ps1 -Environment production -BuildOnly
.\Deploy-SYSTEM.ps1 -Environment production -DeployOnly -InvalidateCache
```

**Result**:
- Builds to `Build/Production/`
- Deploys to production S3
- Connects to `system` module
- CloudFront cache invalidation

## Build Validation

The script automatically validates builds before deployment:

### Validation Checks
- Build directory exists
- `index.html` present
- `Build/` subdirectory exists
- `StreamingAssets/build-config.json` configured correctly

### Skip Validation
```powershell
# Force deployment without validation (use carefully)
.\Deploy-SYSTEM.ps1 -Environment test -DeployOnly -SkipValidation
```

## Database Coordination

### Local Database
```powershell
# Deploy local SpacetimeDB module
.\Deploy-SYSTEM.ps1 -Environment local -DeployServer
```
- Checks if SpacetimeDB is running
- Starts if needed
- Runs `rebuild.ps1` script
- Generates C# bindings

### Cloud Database
```powershell
# Deploy to test cloud
.\Deploy-SYSTEM.ps1 -Environment test -DeployServer

# Deploy to production cloud
.\Deploy-SYSTEM.ps1 -Environment production -DeployServer
```
- Builds Rust module
- Generates C# bindings
- Removes old module
- Publishes to cloud

## Unity Build Integration

The script automatically calls the correct Unity build method:

| Environment | Unity Build Method |
|------------|-------------------|
| local | `BuildScript.BuildWebGLLocal` |
| test | `BuildScript.BuildWebGLTest` |
| production | `BuildScript.BuildWebGLProduction` |

### Unity Auto-Detection
The script attempts to auto-detect Unity installations from common paths:
- `C:\Program Files\Unity\Hub\Editor\2022.3.x\Editor\Unity.exe`

### Manual Unity Path
```powershell
# Specify custom Unity installation
.\Deploy-SYSTEM.ps1 -Environment test -UnityPath "D:\Unity\2022.3.15f1\Editor\Unity.exe"
```

## Error Handling

### Common Issues and Solutions

#### No Build Found
```
❌ No valid build found at: .\SYSTEM-client-3d\Build\Test\
```
**Solution**: Build first or use Unity menu: `Build → Build Test WebGL`

#### Unity Not Found
```
❌ Unity executable not found. Please specify -UnityPath
```
**Solution**: Install Unity 2022.3+ or specify path with `-UnityPath`

#### AWS Not Configured
```
❌ Not logged in to AWS. Please run: aws configure
```
**Solution**: Configure AWS CLI with your credentials

#### S3 Bucket Missing
```
⚠️ S3 bucket 'system-game-test' may not exist
```
**Solution**: Create S3 bucket or verify permissions

## Build Output Information

After successful build/deployment, the script displays:

### Build Configuration
```
✅ Unity build completed successfully!
   Configuration:
     Environment: test
     Server URL:  https://maincloud.spacetimedb.com
     Module:      system-test
```

### Deployment URLs
```
📍 Access your game at:
   S3 URL: https://system-game-test.s3.amazonaws.com/index.html
   CDN URL: https://[your-cloudfront-domain]

🔧 Server Configuration:
   SpacetimeDB: https://maincloud.spacetimedb.com
   Module: system-test
```

## Advanced Usage

### Rebuild Workflow
```powershell
# Force rebuild even if build exists
.\Deploy-SYSTEM.ps1 -Environment test -RebuildUnity
```

### Staged Deployment
```powershell
# Stage 1: Build only
.\Deploy-SYSTEM.ps1 -Environment production -BuildOnly

# Stage 2: Test locally
cd SYSTEM-client-3d\Build\Production
python -m http.server 8000

# Stage 3: Deploy when ready
.\Deploy-SYSTEM.ps1 -Environment production -DeployOnly -InvalidateCache
```

### CI/CD Integration
```powershell
# Automated deployment with all checks
.\Deploy-SYSTEM.ps1 `
    -Environment production `
    -DeployServer `
    -InvalidateCache `
    -UnityPath "C:\Unity\2022.3.15f1\Editor\Unity.exe"
```

## Verification Steps

After deployment, verify your build:

### Local Testing
1. Check SpacetimeDB: `spacetime status`
2. Serve locally: `python -m http.server 8000`
3. Open browser: `http://localhost:8000`
4. Check console for connection to `localhost:3000`

### Test Environment
1. Visit S3 URL or CloudFront domain
2. Check browser console for connection to `system-test`
3. Monitor logs: `spacetime logs system-test`
4. Test multiplayer functionality

### Production
1. Visit production URL
2. Monitor CloudWatch metrics
3. Check server health
4. Verify connection to `system` module

## Tips and Best Practices

1. **Always test in test environment first** before production deployment
2. **Use `-BuildOnly` first** to verify build success before deployment
3. **Keep build directories** for rollback capability
4. **Use `-InvalidateCache`** for immediate updates in production
5. **Monitor build logs** in `.\builds\logs\` for troubleshooting
6. **Backup production** before major updates
7. **Coordinate database updates** with client deployments

## Troubleshooting Checklist

- [ ] Unity 2022.3+ installed
- [ ] SpacetimeDB CLI installed and configured
- [ ] AWS CLI installed and configured (for test/production)
- [ ] S3 buckets created with proper permissions
- [ ] CloudFront distributions configured
- [ ] Rust toolchain installed (for server deployment)
- [ ] Build directories have write permissions
- [ ] Network access to SpacetimeDB cloud