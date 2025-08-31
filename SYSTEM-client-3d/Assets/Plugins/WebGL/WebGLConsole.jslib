mergeInto(LibraryManager.library, {
    
    // Basic console logging
    JSConsoleLog: function(message) {
        console.log(UTF8ToString(message));
    },
    
    JSConsoleWarn: function(message) {
        console.warn(UTF8ToString(message));
    },
    
    JSConsoleError: function(message) {
        console.error(UTF8ToString(message));
    },
    
    // Group logging for better organization
    JSConsoleGroup: function(label) {
        console.group(UTF8ToString(label));
    },
    
    JSConsoleGroupEnd: function() {
        console.groupEnd();
    },
    
    // Table display for structured data
    JSConsoleTable: function(jsonData) {
        try {
            var data = JSON.parse(UTF8ToString(jsonData));
            console.table(data);
        } catch (e) {
            console.error("Failed to parse table data:", e);
        }
    },
    
    // Enhanced error logging with stack trace
    JSConsoleErrorWithStack: function(message, stackTrace) {
        console.error(UTF8ToString(message));
        if (stackTrace) {
            console.error("Stack Trace:\n" + UTF8ToString(stackTrace));
        }
    },
    
    // Clear console
    JSConsoleClear: function() {
        console.clear();
    },
    
    // Time tracking
    JSConsoleTime: function(label) {
        console.time(UTF8ToString(label));
    },
    
    JSConsoleTimeEnd: function(label) {
        console.timeEnd(UTF8ToString(label));
    },
    
    // Assert for debugging
    JSConsoleAssert: function(condition, message) {
        console.assert(condition, UTF8ToString(message));
    },
    
    // Trace current call stack
    JSConsoleTrace: function(message) {
        console.trace(UTF8ToString(message));
    }
});