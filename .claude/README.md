# .claude/ Directory

This directory contains session-specific documentation and investigation logs for Claude Code sessions.

## Files

### 📋 current-session-status.md
**Purpose:** Current session state and where to continue from
**When to Read:** Start of every new session
**Contains:**
- Current problem being worked on
- Investigation progress summary
- Next steps and action items
- User constraints and preferences
- Files modified in current session

### 📊 performance-investigation-log.md
**Purpose:** Detailed timeline of performance investigation
**When to Read:** When continuing performance work
**Contains:**
- Chronological hypothesis testing
- Test results and conclusions
- Solution options with pros/cons
- Technical analysis and statistics
- Code changes made

### 📖 debug-commands-reference.md
**Purpose:** SpacetimeDB debug commands reference
**When to Read:** When debugging server/database issues
**Contains:**
- Orb management commands
- Mining debug commands
- SQL queries for data inspection
- Server status commands

## Quick Start for New Sessions

1. **Read `current-session-status.md`** - Get current state
2. **Check main `CLAUDE.md`** - See status banner at top
3. **Reference investigation log** - If continuing performance work
4. **Use debug commands** - For server/database testing

## Current Status Indicator

The main `CLAUDE.md` file has a status banner at the top:

```markdown
## 🔴 CURRENT SESSION STATUS

**Active Investigation:** [Current task]
**Status:** [In Progress/Blocked/Awaiting Test/Complete]
**Priority:** [HIGH/MEDIUM/LOW]

📋 See: `.claude/current-session-status.md`
📊 See: `.claude/performance-investigation-log.md`
```

🔴 Red = Active work in progress
🟡 Yellow = Blocked/waiting for user input
🟢 Green = Complete, ready for new tasks

## Maintenance

### When to Update
- **Start of session:** Review and update status files
- **During work:** Add to investigation log as you test hypotheses
- **End of session:** Update status with next steps
- **Task complete:** Clear status banner, archive investigation log if needed

### Archiving
When a task is complete, move detailed logs to an archive folder:
```
.claude/
├── archive/
│   └── 2025-01-06-mining-packet-freeze/
│       ├── investigation-log.md
│       └── test-results/
```

Keep `current-session-status.md` updated for the latest active work.
