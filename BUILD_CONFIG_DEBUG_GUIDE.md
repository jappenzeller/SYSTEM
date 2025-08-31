# Build Configuration Debug Guide

## Problem Fixed

The build-config.json file was being generated correctly but wasn't loading properly in WebGL builds because:

1. **WebGL can't use File.ReadAllText()** - StreamingAssets in WebGL requires UnityWebRequest
2. **Fallback logic defaulted to Test** - When config loading failed, WebGL always defaulted to test environment
3. **No async handling** - WebGL loading is asynchronous but wasn't being handled properly

## Changes Made

### 1. Updated BuildConfiguration.cs

**Key Changes:**
- Added WebGL-specific loading using UnityWebRequest
- Implemented coroutine-based async loading for WebGL
- Improved fallback logic to detect local WebGL (checks for localhost/127.0.0.1 in URL)
- Added detailed logging throughout the loading process
- Fixed race condition where config was accessed before loading completed

**WebGL Loading Flow:**
```csharp
#if UNITY_WEBGL && !UNITY_EDITOR
    // Uses UnityWebRequest in coroutine
    LoadConfigurationWebGL();
#else
    // Uses direct file access
    LoadConfigurationStandalone();
#endif
```

### 2. Enhanced BuildScript.cs

**Improvements:**
- Added detailed logging to track config generation
- Verifies file creation after writing
- Forces synchronous asset database refresh
- Adds small delay to ensure file system sync
- Deletes old config before writing new one

### 3. Created BuildConfigDebugger.cs

**Debug Features:**
- Visual overlay showing loaded configuration
- Platform detection and logging
- Manual reload button for testing
- Detailed console logging of loading process

## How to Test

### 1. Build Local WebGL
```
Unity Menu: Build → Build Local WebGL
```

Watch console for:
```
[SaveRuntimeConfig] Starting config generation for local environment
[SaveRuntimeConfig] Using LOCAL settings: http://127.0.0.1:3000/system
[SaveRuntimeConfig] ✅ Saved config to: Assets/StreamingAssets/build-config.json
[SaveRuntimeConfig] ✅ Verified file exists, size: 168 bytes
```

### 2. Verify Config File
Check that `Assets/StreamingAssets/build-config.json` contains:
```json
{
    "environment": "local",
    "serverUrl": "http://127.0.0.1:3000",
    "moduleName": "system",
    "enableDebugLogging": true,
    "developmentBuild": true
}
```

### 3. Test WebGL Build
```bash
cd SYSTEM-client-3d/Build/Local
python -m http.server 8000
# Open http://localhost:8000
```

### 4. Check Browser Console
Look for these logs:
```
[BuildConfiguration] Loading WebGL config from: .../StreamingAssets/build-config.json
[BuildConfiguration] WebGL loaded JSON: {"environment":"local"...}
[BuildConfiguration] WebGL Loaded - Environment: local
[BuildConfiguration] Server URL: http://127.0.0.1:3000
[BuildConfiguration] Module: system
```

### 5. Use Debug Overlay
Add BuildConfigDebugger component to a GameObject in your scene. It will show:
- Current platform
- Config loaded status
- Environment, Server URL, and Module Name

## Verification Checklist

- [ ] BuildScript shows config generation logs
- [ ] StreamingAssets/build-config.json exists after build
- [ ] Config file has correct environment settings
- [ ] Build/Local/StreamingAssets/build-config.json exists in output
- [ ] Browser console shows successful config loading
- [ ] Game connects to correct server (localhost:3000 for local)
- [ ] Debug overlay shows correct configuration

## Troubleshooting

### Config Not Loading in WebGL

1. **Check browser console** for errors
2. **Verify StreamingAssets path** in build output
3. **Check network tab** for 404 on build-config.json
4. **Ensure WebGL build** includes StreamingAssets folder

### Wrong Environment Detected

1. **Check build-config.json** content in build folder
2. **Clear browser cache** and reload
3. **Verify BuildScript** used correct environment parameter
4. **Check fallback logic** if running from unexpected URL

### Config Loading But Not Applied

1. **Check GameManager** is using BuildConfiguration.Config
2. **Verify timing** - config loads asynchronously in WebGL
3. **Check for race conditions** - wait for config before connecting

## Testing Different Environments

### Local Build Test
```powershell
# Build
Unity: Build → Build Local WebGL

# Verify config
cat Build/Local/StreamingAssets/build-config.json
# Should show: "environment": "local", "serverUrl": "http://127.0.0.1:3000"

# Test
cd Build/Local && python -m http.server 8000
```

### Test Build Test
```powershell
# Build
Unity: Build → Build Test WebGL

# Verify config
cat Build/Test/StreamingAssets/build-config.json
# Should show: "environment": "test", "moduleName": "system-test"

# Deploy and test
./Deploy-SYSTEM.ps1 -Environment test -DeployOnly
```

### Production Build Test
```powershell
# Build
Unity: Build → Build Production WebGL

# Verify config
cat Build/Production/StreamingAssets/build-config.json
# Should show: "environment": "production", "moduleName": "system"

# Deploy and test
./Deploy-SYSTEM.ps1 -Environment production -DeployOnly
```

## Summary

The build configuration system now properly:
1. **Generates environment-specific configs** during build
2. **Loads configs correctly in WebGL** using UnityWebRequest
3. **Falls back intelligently** based on hosting URL
4. **Provides detailed debugging** for troubleshooting
5. **Ensures proper file inclusion** in build output

Each environment (Local/Test/Production) will now correctly connect to its intended server without relying on runtime platform detection.