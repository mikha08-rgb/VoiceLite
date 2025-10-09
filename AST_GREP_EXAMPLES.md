# ast-grep MCP Examples for Architecture Audit

## What is ast-grep?

ast-grep is a semantic code search tool that understands code structure (AST - Abstract Syntax Tree) instead of just text patterns. This makes it perfect for architecture audits because it can:

- Find all usages of static singletons
- Map service dependencies
- Identify state machine transitions
- Detect coupling patterns

## Installation Status

✅ **Installed**: `@notprolands/ast-grep-mcp` (v1.1.1)
✅ **Configured**: `.claude/mcp.json` updated
🔄 **Restart Required**: Restart Claude Code to activate the MCP

---

## Example Queries for VoiceLite Architecture Audit

### 1. Find All Static Singleton Usages

**Goal**: Identify all places where static singletons are accessed (ApiClient.Client, ErrorLogger.Instance, etc.)

```bash
# Find ApiClient.Client usages
ast-grep --pattern 'ApiClient.Client.$_'

# Find ErrorLogger static calls
ast-grep --pattern 'ErrorLogger.$_'

# Find PersistentWhisperService static references
ast-grep --pattern 'PersistentWhisperService.$_'
```

**What this tells you**:
- How many files depend on each singleton
- What methods are being called on singletons
- Which singletons to refactor first (based on usage count)

---

### 2. Map Service Dependencies in MainWindow

**Goal**: Find all service instantiations and method calls in MainWindow.xaml.cs

```bash
# Find all service instantiations
ast-grep --pattern 'new $_Service($_)'

# Find all coordinator calls
ast-grep --pattern '$coordinator.$_($_)'

# Find all service method calls
ast-grep --pattern 'this.$_Service.$_($_)'
```

**What this tells you**:
- What services MainWindow creates directly
- What services MainWindow calls methods on
- Which responsibilities should move to coordinators

---

### 3. Analyze State Machine Transitions

**Goal**: Find all state transitions in RecordingStateMachine.cs

```bash
# Find all TransitionTo calls
ast-grep --pattern 'TransitionTo($_)'

# Find all state assignments
ast-grep --pattern '_currentState = $_'

# Find all state checks
ast-grep --pattern 'if ($_State == $_)'
```

**What this tells you**:
- How many state transitions exist
- Which states are checked most often
- Where state-related logic could be simplified

---

### 4. Find Settings Access Patterns

**Goal**: Identify how settings are read/written across the codebase

```bash
# Find all settings reads
ast-grep --pattern 'Settings.$_'

# Find all settings writes
ast-grep --pattern 'Settings.$_ = $_'

# Find SaveSettings calls
ast-grep --pattern 'SaveSettings($_)'
```

**What this tells you**:
- Which files directly access Settings
- Where SettingsManager should centralize logic
- What settings-related code to extract from MainWindow

---

### 5. Detect UI Thread Violations

**Goal**: Find Dispatcher.Invoke patterns (potential threading issues)

```bash
# Find all Dispatcher.Invoke calls
ast-grep --pattern 'Dispatcher.Invoke($_)'

# Find all Dispatcher.BeginInvoke calls
ast-grep --pattern 'Dispatcher.BeginInvoke($_)'

# Find Application.Current.Dispatcher usages
ast-grep --pattern 'Application.Current.Dispatcher.$_'
```

**What this tells you**:
- Where UI updates happen from background threads
- Which services need UIUpdateService extraction
- Threading complexity hotspots

---

### 6. Find Child Window Management Code

**Goal**: Identify all places where child windows are created/managed

```bash
# Find window instantiations
ast-grep --pattern 'new $_Window($_)'

# Find ShowDialog calls
ast-grep --pattern '$_.ShowDialog($_)'

# Find window lifecycle events
ast-grep --pattern 'window.Closed += $_'
```

**What this tells you**:
- Where WindowManager should take over
- How many window types exist
- Window lifecycle management patterns

---

### 7. Find Hotkey Registration Logic

**Goal**: Extract all hotkey-related code for HotkeyCoordinator

```bash
# Find RegisterHotKey calls
ast-grep --pattern 'RegisterHotKey($_)'

# Find UnregisterHotKey calls
ast-grep --pattern 'UnregisterHotKey($_)'

# Find WM_HOTKEY message handling
ast-grep --pattern 'WM_HOTKEY'
```

**What this tells you**:
- What hotkey logic to move to HotkeyCoordinator
- How hotkeys are registered/unregistered
- Win32 API interaction patterns

---

### 8. Analyze Recording Workflow

**Goal**: Map the entire recording workflow chain

```bash
# Find AudioRecorder interactions
ast-grep --pattern '$recorder.$_($_)'

# Find WhisperService calls
ast-grep --pattern '$whisper.$_($_)'

# Find TextInjector calls
ast-grep --pattern '$injector.$_($_)'
```

**What this tells you**:
- Service orchestration flow
- What AudioCoordinator should manage
- Coupling between recording services

---

### 9. Find Business Logic in MainWindow

**Goal**: Identify non-UI logic that should move to services

```bash
# Find validation logic
ast-grep --pattern 'if ($_.IsValid($_))'

# Find data processing
ast-grep --pattern 'ProcessTranscription($_)'

# Find calculations
ast-grep --pattern '$_ = Calculate$_($_)'
```

**What this tells you**:
- Business logic mixed with UI code
- What to extract to TranscriptionService
- Calculation logic to move out

---

### 10. Detect Coupling Patterns

**Goal**: Find tight coupling between components

```bash
# Find direct Settings access in services
ast-grep --pattern 'class $_Service { $$$ Settings.$_ $$$ }'

# Find MainWindow references in services
ast-grep --pattern 'MainWindow.$_'

# Find circular dependency patterns
ast-grep --pattern '$service1.$service2.$service1'
```

**What this tells you**:
- Which services are tightly coupled
- Where dependency injection would help
- Circular dependency risks

---

## How to Use ast-grep with Claude Code

After restarting Claude Code, you can ask:

```
"Use ast-grep to find all static singleton usages in the codebase"

"Use ast-grep to map service dependencies in MainWindow.xaml.cs"

"Use ast-grep to identify all state transitions in RecordingStateMachine"
```

Claude Code will use the ast-grep MCP to execute these queries and provide structured results.

---

## Expected Output Format

ast-grep returns results like:

```
VoiceLite/VoiceLite/Services/RecordingCoordinator.cs:45
    ApiClient.Client.ValidateLicense()

VoiceLite/VoiceLite/MainWindow.xaml.cs:234
    ApiClient.Client.SendAnalyticsEvent()

VoiceLite/VoiceLite/Services/AnalyticsService.cs:78
    ApiClient.Client.PostAsync("/api/analytics")
```

This tells you:
- **File**: Where the code is located
- **Line**: Exact line number
- **Context**: What the code is doing

---

## Next Steps After ast-grep Analysis

1. **Create dependency graph** from ast-grep results
2. **Categorize MainWindow methods** by responsibility
3. **Calculate coupling scores** (how many files depend on each class)
4. **Prioritize extractions** (lowest coupling = safest to extract)
5. **Generate refactoring roadmap** with risk assessment

---

## Troubleshooting

**Q: ast-grep MCP not showing up?**
A: Restart Claude Code after updating `.claude/mcp.json`

**Q: ast-grep queries returning too many results?**
A: Add file filters: `--path 'VoiceLite/VoiceLite/*.cs'`

**Q: How to search for C# specific patterns?**
A: ast-grep supports C# AST patterns natively, use C# syntax in patterns

---

## Advanced Patterns

### Find All IDisposable Implementations
```bash
ast-grep --pattern 'class $_ : IDisposable { $$$ }'
```

### Find Async/Await Patterns
```bash
ast-grep --pattern 'async Task<$_> $_($$$) { $$$ await $$$ }'
```

### Find Event Handler Subscriptions
```bash
ast-grep --pattern '$_ += $_'
```

### Find Event Handler Unsubscriptions
```bash
ast-grep --pattern '$_ -= $_'
```

---

## Summary

ast-grep gives you **semantic code search** that understands C# syntax and structure. This is 10x more powerful than text-based grep for architecture audits.

Use it to:
- ✅ Map dependencies automatically
- ✅ Find all usages of a pattern
- ✅ Identify coupling hotspots
- ✅ Prioritize refactoring targets
- ✅ Validate refactoring safety (did we miss any usages?)
