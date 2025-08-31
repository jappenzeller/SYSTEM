# WebGL Console Logging System - Integration Guide

## Overview

This logging system redirects all Unity Debug.Log output and exceptions to the browser console, providing full visibility into WebGL build errors that are normally hidden.

## Files Created

1. **WebGLConsoleLogger.cs** - Main logging component
2. **WebGLConsole.jslib** - JavaScript bridge for browser console
3. **WebGLExceptionDebugger.cs** - Enhanced exception analysis
4. **LoginUIDebugWrapper.cs** - Specific debugging for LoginUI issues

## Quick Setup

### Step 1: Add to Scene

1. **Create a Debug GameObject** in your Login scene:
   ```
   GameObject → Create Empty → Name it "DebugSystem"
   ```

2. **Add the logging components**:
   - Add `WebGLConsoleLogger` component
   - Add `WebGLExceptionDebugger` component
   - Enable all checkboxes for maximum debugging

3. **Add to LoginUI GameObject**:
   - Find your LoginUI GameObject
   - Add `LoginUIDebugWrapper` component
   - This will monitor initialization

### Step 2: Set Script Execution Order

**CRITICAL**: Set proper execution order to catch early errors:

1. Go to: Edit → Project Settings → Script Execution Order
2. Set these values:
   ```
   GameEventBus: -200
   WebGLConsoleLogger: -150
   WebGLExceptionDebugger: -140
   GameManager: -100
   LoginUIDebugWrapper: -50
   LoginUIController: Default (0)
   ```

### Step 3: Build and Test

1. **Build WebGL**: Build → Build Local WebGL
2. **Serve locally**: 
   ```bash
   cd Build/Local
   python -m http.server 8000
   ```
3. **Open browser**: http://localhost:8000
4. **Open DevTools**: F12 → Console tab

## What You'll See in Browser Console

### Successful Initialization
```
🎮 Unity WebGL Console Logger Initialized
Unity Version: 2022.3.x
Platform: WebGLPlayer
Scene: Login
✅ WebGL Console Logger is now capturing Unity logs

🔧 LoginUI Debug Wrapper - Awake
GameObject: LoginUI
✅ LoginUIController component found
✅ UIDocument component found
✅ GameEventBus ready - State: Disconnected
✅ GameManager ready - Connected: false
```

### NullReferenceException Details
```
════════════════════════════════════════
🔴 NULL REFERENCE EXCEPTION DETECTED
════════════════════════════════════════
Context: LoginUIController.SubscribeToEvents
Message: Object reference not set to an instance of an object

📍 Critical Line: at LoginUIController.SubscribeToEvents() LoginUIController.cs:45

🔍 Analyzing Null Reference...
⚠️ Likely Cause: GameEventBus.Instance is null
   Fix: Ensure GameEventBus exists in scene

Full Stack Trace:
  at LoginUIController.SubscribeToEvents() in LoginUIController.cs:line 45
  at LoginUIController.Start() in LoginUIController.cs:line 23
════════════════════════════════════════
```

## Debugging Your Specific Issue

### Finding the LoginUI NullReferenceException

The debug wrapper will help identify what's null:

1. **Check Singleton Initialization**:
   ```
   ❌ CRITICAL: GameEventBus.Instance is NULL at Start!
   ```
   **Fix**: Add GameEventBus to scene or check script execution order

2. **Check UI Elements**:
   ```
   ❌ Field 'usernameField' is NULL!
   ```
   **Fix**: Ensure UIDocument has correct VisualTreeAsset assigned

3. **Check Event Subscriptions**:
   ```
   ❌ Event subscription test FAILED!
   ```
   **Fix**: GameEventBus not initialized before LoginUI

## Common WebGL-Specific Issues

### Issue 1: Singletons Not Initialized
**Symptom**: `Instance is null` errors
**Cause**: WebGL may have different initialization timing
**Fix**: 
- Use script execution order
- Add null checks with retry logic
- Initialize singletons in Awake() not Start()

### Issue 2: UI Document Not Ready
**Symptom**: `rootVisualElement is null`
**Cause**: UI Toolkit may initialize differently in WebGL
**Fix**:
```csharp
void Start() {
    StartCoroutine(WaitForUIDocument());
}

IEnumerator WaitForUIDocument() {
    while (uiDocument.rootVisualElement == null) {
        yield return null;
    }
    // Now safe to query elements
}
```

### Issue 3: Event System Race Condition
**Symptom**: Events not received or subscription fails
**Cause**: Components initializing out of order
**Fix**: Add initialization state machine:
```csharp
if (GameEventBus.Instance == null) {
    StartCoroutine(WaitForEventBus());
    return;
}
```

## Advanced Debugging

### Manual Inspection
```csharp
// Add this anywhere to inspect a component
WebGLExceptionDebugger.InspectComponent(GetComponent<LoginUIController>());
```

### Wrap Risky Code
```csharp
WebGLExceptionDebugger.SafeExecute(() => {
    // Your risky code here
    GameEventBus.Instance.Subscribe<SomeEvent>(handler);
}, "Subscribing to SomeEvent");
```

### Custom Logging
```csharp
#if UNITY_WEBGL && !UNITY_EDITOR
    WebGLConsoleLogger.Log("Custom debug message");
    WebGLConsoleLogger.LogError("Custom error");
#endif
```

## Browser Console Features

The system uses browser console features:
- **console.group()** - Groups related logs
- **console.table()** - Shows data in table format
- **console.time()** - Performance timing
- **Color coding** - Errors in red, warnings in yellow

## Performance Considerations

- **Disable in Production**: Remove or disable debug components
- **Use Conditional Compilation**: Wrap debug code in `#if UNITY_WEBGL && !UNITY_EDITOR`
- **Minimal Overhead**: Logger only processes when DevTools is open

## Troubleshooting the Logger

### Logger Not Working
1. Check `.jslib` file is in `Assets/Plugins/WebGL/`
2. Rebuild project after adding .jslib
3. Check browser console for JavaScript errors

### No Output in Console
1. Ensure DevTools is open before page loads
2. Check console filter isn't hiding messages
3. Verify components are in scene and active

### Stack Traces Missing
1. Enable `Development Build` in Build Settings
2. Set `Exception Support` to `Full With Stacktrace`
3. Check `enableStackTraces` in WebGLConsoleLogger

## Quick Fixes for Common LoginUI Issues

### Fix 1: Add Missing GameEventBus
```csharp
void Awake() {
    if (GameEventBus.Instance == null) {
        var go = new GameObject("GameEventBus");
        go.AddComponent<GameEventBus>();
    }
}
```

### Fix 2: Delay Event Subscription
```csharp
void Start() {
    StartCoroutine(DelayedInit());
}

IEnumerator DelayedInit() {
    yield return new WaitForSeconds(0.1f);
    SubscribeToEvents();
}
```

### Fix 3: Null-Safe Event Subscription
```csharp
private void SubscribeToEvents() {
    if (GameEventBus.Instance == null) {
        Debug.LogError("GameEventBus not ready!");
        return;
    }
    // Subscribe here
}
```

## Summary

With this logging system, you should be able to:
1. **See exact line numbers** where NullReferenceExceptions occur
2. **Identify which objects are null** at initialization
3. **Track component initialization order** in WebGL
4. **Debug event system issues** specific to WebGL builds

The LoginUIDebugWrapper will specifically help identify why your LoginUI is failing in WebGL but not in the editor.