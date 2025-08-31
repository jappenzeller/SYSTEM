# Build Configuration Changes Summary

## Overview
Updated the Unity build system to use **build-time configuration** instead of runtime platform detection. Each environment now has its own build directory with proper connection settings baked in at build time.

## Key Changes

### 1. New Build Output Structure
- **Local builds**: `SYSTEM-client-3d/Build/Local/`
- **Test builds**: `SYSTEM-client-3d/Build/Test/`
- **Production builds**: `SYSTEM-client-3d/Build/Production/`

(Previously all went to `WebBuild/`)

### 2. Build-Time Connection Configuration

Each build now has connection settings configured at build time:

| Environment | Server URL | Module Name |
|------------|------------|-------------|
| Local | http://127.0.0.1:3000 | system |
| Test | https://maincloud.spacetimedb.com | system-test |
| Production | https://maincloud.spacetimedb.com | system |

### 3. Files Modified

#### New Files
- `Assets/Scripts/BuildConfiguration.cs` - Loads build config from StreamingAssets
- `BUILD_CONFIGURATION_CHANGES.md` - This documentation

#### Updated Files
- `Assets/Editor/BuildScript.cs` - Creates environment-specific builds with proper config
- `Assets/Scripts/GameManager.cs` - Uses build config instead of runtime detection
- `Deploy-UnityWebGL.ps1` - Updated to support new directory structure
- `Deploy-Complete.ps1` - Updated to support new directory structure

### 4. How It Works

1. **Build Time**: BuildScript saves a `build-config.json` to StreamingAssets with:
   - Environment name
   - Server URL
   - Module name
   - Debug settings

2. **Runtime**: GameManager loads this config via BuildConfiguration class
   - No more runtime platform detection for WebGL
   - Each build connects to its intended server

3. **Editor**: Still uses inspector values or defaults when no build config exists

## Usage

### Building
Use the Unity menu as before:
- `Build → Build Local WebGL` → Creates build in `Build/Local/`
- `Build → Build Test WebGL` → Creates build in `Build/Test/`
- `Build → Build Production WebGL` → Creates build in `Build/Production/`

### Deploying
Updated deployment scripts now accept environment parameter:

```powershell
# Deploy test build to S3
.\Deploy-UnityWebGL.ps1 -Environment test

# Deploy production build with CloudFront invalidation
.\Deploy-Complete.ps1 -Environment production -InvalidateCache
```

### Testing Local WebGL
You can now test local WebGL builds that connect to `127.0.0.1:3000`:

1. Build: `Build → Build Local WebGL`
2. Serve locally: `cd SYSTEM-client-3d/Build/Local && python -m http.server 8000`
3. Open: `http://localhost:8000`

## Benefits

1. **Clear separation** between environments
2. **No runtime guessing** - each build knows its target
3. **Local WebGL testing** now possible
4. **Explicit configuration** visible in build logs
5. **Backwards compatible** - Editor still works with inspector values

## Verification

After building, check the logs for confirmation:
```
========================================
Building WebGL for TEST environment
Output Directory: H:\SpaceTime\SYSTEM\SYSTEM-client-3d\Build\Test
Connection Configuration:
  Server URL: https://maincloud.spacetimedb.com
  Module Name: system-test
========================================
```

The build will create `Assets/StreamingAssets/build-config.json` with the correct settings.