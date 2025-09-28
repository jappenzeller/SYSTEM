# SYSTEM Deployment Guide

## Overview

The unified deployment system provides comprehensive automation for deploying SYSTEM across all environments, with integrated database management, verification, and cache invalidation.

## Quick Start

### Basic Commands

```powershell
# Deploy to test environment
./Scripts/deploy-spacetimedb.ps1 -Environment test

# Deploy to production with verification
./Scripts/deploy-spacetimedb.ps1 -Environment production -Verify

# Deploy with complete database reset
./Scripts/deploy-spacetimedb.ps1 -Environment test -DeleteData -Yes

# Non-interactive CI/CD deployment
./Scripts/deploy-spacetimedb.ps1 -Environment production -Yes -Verify
```

### Unix/Linux/macOS

```bash
# Deploy to test environment
./Scripts/deploy-spacetimedb.sh --environment test

# Deploy with verification
./Scripts/deploy-spacetimedb.sh --environment production --verify

# Deploy with cache invalidation
./Scripts/deploy-spacetimedb.sh --environment test --invalidate-cache
```

## Environment Configuration

### Server Endpoints

| Environment | Server URL | Module Name | CloudFront ID |
|------------|------------|-------------|---------------|
| **local** | `127.0.0.1:3000` | `system` | - |
| **test** | `https://maincloud.spacetimedb.com` | `system-test` | `ENIM1XA5ZCZOT` |
| **production** | `https://maincloud.spacetimedb.com` | `system` | `E3HQWKXYZ9MNOP` |

### Build Directories

Unity builds output to environment-specific directories:

```
SYSTEM-client-3d/Build/
├── Local/          # localhost:3000
├── Test/           # SpacetimeDB cloud test
└── Production/     # SpacetimeDB cloud production
```

## Deployment Options

### Command-Line Parameters

| Parameter | Description | Example |
|-----------|-------------|---------|
| `-Environment` | Target deployment environment | `test`, `production`, `local` |
| `-DeleteData` | Complete database wipe (DANGEROUS) | Use with caution |
| `-InvalidateCache` | Clear CloudFront cache after deployment | For production updates |
| `-PublishOnly` | Deploy module without data operations | Module update only |
| `-Verify` | Run post-deployment verification tests | Validates deployment |
| `-BuildConfig` | Generate build-config.json for WebGL | Unity WebGL builds |
| `-SkipBuild` | Skip Rust compilation (use existing) | Speed up deployment |
| `-Yes` | Non-interactive mode for automation | CI/CD pipelines |

## Complete Deployment Workflows

### WebGL Production Deployment

```powershell
# 1. Build WebGL in Unity
# Unity Menu: Build → Build Production WebGL

# 2. Deploy SpacetimeDB module with config
./Scripts/deploy-spacetimedb.ps1 -Environment production -BuildConfig -Verify

# 3. Upload WebGL build to S3
aws s3 sync ./SYSTEM-client-3d/Build/Production s3://system-unity-game/ --delete

# 4. Invalidate CloudFront cache
./Scripts/deploy-spacetimedb.ps1 -Environment production -InvalidateCache
```

### Test Environment with Database Reset

```powershell
# WARNING: This deletes all data!
./Scripts/deploy-spacetimedb.ps1 -Environment test -DeleteData -BuildConfig -Yes

# Build and upload WebGL
# Unity: Build → Build Test WebGL
aws s3 sync ./SYSTEM-client-3d/Build/Test s3://system-test-bucket/ --delete
```

### Local Development Deployment

```powershell
# Start local SpacetimeDB
spacetime start

# Deploy to local
./Scripts/deploy-spacetimedb.ps1 -Environment local

# Unity connects to localhost:3000 automatically
```

## Unity Editor Integration

### Deployment Menu

Access deployment from Unity's menu bar:

- **SYSTEM → Deploy → Deploy to Local** - Quick local deployment
- **SYSTEM → Deploy → Deploy to Test** - Deploy to test server
- **SYSTEM → Deploy → Deploy to Production** - Production deployment (with confirmation)
- **SYSTEM → Deploy → Verify Current Deployment** - Check deployment status

### DeploymentConfig ScriptableObject

Configure deployment settings in Unity:

1. Create asset: `Assets → Create → SYSTEM → Deployment Configuration`
2. Configure environments, paths, and options
3. Use from code: `DeploymentConfig.GetEnvironmentConfig("test")`

## Verification and Testing

### Automatic Verification

The deployment system includes comprehensive verification:

- Table existence checks
- Data integrity validation
- Connection latency testing
- Query performance benchmarks

### Manual Verification

```powershell
# Run verification only
./Scripts/deploy-spacetimedb.ps1 -Environment test -Verify -SkipBuild -PublishOnly

# Check specific tables
spacetime sql system-test --server https://maincloud.spacetimedb.com "SELECT COUNT(*) FROM Player"
```

### SQL Verification Queries

Located in `Scripts/post-deploy-verify.sql`:
- Core table existence
- Index verification
- Data integrity checks
- Performance metrics
- Orphaned data detection

## CI/CD Integration

### GitHub Actions Example

```yaml
- name: Deploy to Test
  run: |
    ./Scripts/deploy-spacetimedb.ps1 `
      -Environment test `
      -Yes `
      -Verify `
      -BuildConfig `
      -InvalidateCache
```

### Jenkins Pipeline

```groovy
stage('Deploy') {
    steps {
        powershell '''
            ./Scripts/deploy-spacetimedb.ps1 -Environment production -Yes -Verify
        '''
    }
}
```

## Troubleshooting

### Common Issues

| Issue | Solution |
|-------|----------|
| "Module not found" | Run deployment without `-SkipBuild` |
| "Connection refused" | Check SpacetimeDB server is running |
| "Build failed" | Check Rust installation and `cargo build` |
| "CloudFront invalidation failed" | Verify AWS CLI credentials |
| "Verification failed" | Check SQL queries in logs |

### Log Files

Deployment logs are saved to:
```
./Logs/deployment/deployment_YYYYMMDD_HHMMSS.log
```

### Debug Mode

For detailed output:
```powershell
$DebugPreference = "Continue"
./Scripts/deploy-spacetimedb.ps1 -Environment test -Verify
```

## Rollback Procedures

### Database Rollback

```powershell
# Create backup before deployment
spacetime sql system-test --server https://maincloud.spacetimedb.com ".backup backup.db"

# Restore if needed
spacetime sql system-test --server https://maincloud.spacetimedb.com ".restore backup.db"
```

### Module Rollback

Keep previous module versions:
```powershell
# Tag before deployment
git tag pre-deploy-$(Get-Date -Format "yyyyMMdd-HHmmss")

# Rollback to previous version
git checkout pre-deploy-20240906-143000
./Scripts/deploy-spacetimedb.ps1 -Environment production
```

## Best Practices

1. **Always verify test deployment before production**
2. **Use `-Verify` flag for production deployments**
3. **Create database backups before using `-DeleteData`**
4. **Test locally first with `-Environment local`**
5. **Use CloudFront invalidation for user-facing updates**
6. **Monitor deployment logs for warnings and errors**
7. **Keep deployment scripts in version control**
8. **Document environment-specific configurations**

## Security Considerations

- Never commit AWS credentials to repository
- Use IAM roles for CI/CD systems
- Restrict production deployment access
- Enable CloudTrail for deployment auditing
- Rotate SpacetimeDB tokens regularly
- Use separate AWS accounts for environments

## Support

For deployment issues:
1. Check deployment logs in `./Logs/deployment/`
2. Verify environment configuration
3. Run verification tests
4. Contact DevOps team if issues persist