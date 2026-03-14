# CVWaferProber Execution Flow Analysis - Documentation Index

This directory contains comprehensive documentation of the CVWaferProber application's task execution logic chain and architecture.

## 📋 Generated Documentation Files

### 1. **QUICK_REFERENCE.md** (198 lines) ⭐ START HERE
- Quick lookup guide for developers
- File structure overview
- Key methods and data models
- Simple flow diagrams
- Common debugging scenarios
- **Best for**: Quick answers, reference during development

### 2. **EXECUTION_FLOW_SUMMARY.md** (16 KB)
- Application layers (Presentation → VM → Service → Communication)
- Key files & responsibilities table
- Test execution flow (Manual + Auto)
- MQTT communication details
- Result display & export
- Breakpoint recovery mechanism
- UI panel activation logic
- Error handling strategy
- Performance considerations
- Testing path examples
- **Best for**: Understanding overall architecture and data flow

### 3. **EXECUTION_FLOW_ANALYSIS.md** (24 KB)
- Detailed step-by-step execution flows
- Complete application startup sequence
- Main window initialization (AvalonDock layout)
- ViewModel hierarchy explanation
- Service layer architecture
- Data model specifications
- Manual vs Auto test execution chains
- Control flow connections
- Commands & event flow
- Logging & monitoring
- Data export & persistence details
- Communication layers (MQTT, WebSocket/TCP, Database)
- Complete state machine diagrams
- Full configuration system
- **Best for**: Deep understanding, implementation details, debugging

## 🎯 Quick Navigation

### For Understanding the Big Picture
1. Start with **QUICK_REFERENCE.md** - Get oriented
2. Read **EXECUTION_FLOW_SUMMARY.md** - Understand layers & flow
3. Dive into **EXECUTION_FLOW_ANALYSIS.md** - Deep details

### For Specific Topics

#### Application Startup
→ EXECUTION_FLOW_ANALYSIS.md: Section 1 (Startup Flow)
→ QUICK_REFERENCE.md: Application Startup

#### Manual Test Execution
→ EXECUTION_FLOW_ANALYSIS.md: Section 6 (Manual Test Flow)
→ EXECUTION_FLOW_SUMMARY.md: Manual Test section

#### Batch (Auto) Test Execution
→ EXECUTION_FLOW_ANALYSIS.md: Section 7 (Auto Test Flow)
→ EXECUTION_FLOW_SUMMARY.md: Auto Test section

#### MQTT Communication
→ EXECUTION_FLOW_ANALYSIS.md: Section 12 (Communication Layers)
→ EXECUTION_FLOW_SUMMARY.md: MQTT Communication Flow

#### Crash Recovery & Breakpoints
→ EXECUTION_FLOW_ANALYSIS.md: Section 8 (Error Handling & Breakpoint Recovery)
→ EXECUTION_FLOW_SUMMARY.md: Breakpoint Recovery section

#### UI Panel Switching
→ EXECUTION_FLOW_ANALYSIS.md: Section 8 (Control Flow Connections)
→ EXECUTION_FLOW_SUMMARY.md: UI Panel Activation section

---

## 📊 Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│  PRESENTATION (DockMainWindow - AvalonDock)             │
│  ├─ Mapping Panel (left)                               │
│  ├─ Camera Panel (right-top)                           │
│  ├─ Spectrometer/IVL/AOI/EQE/VAM Panels (right-mid)   │
│  └─ Log Panel (bottom)                                 │
└────────────────┬────────────────────────────────────────┘
                 │ (Data Binding)
┌────────────────▼────────────────────────────────────────┐
│  VIEWMODELS (MVVM Pattern)                              │
│  ├─ MainViewModel (coordinator)                         │
│  ├─ MappingDataViewModel (test control)                │
│  ├─ DieViewModel (die state)                           │
│  └─ Other VMs (Camera, Spectrometer, etc.)             │
└────────────────┬────────────────────────────────────────┘
                 │ (Command/Event)
┌────────────────▼────────────────────────────────────────┐
│  SERVICES (Business Logic)                              │
│  ├─ MainService (orchestration)                        │
│  ├─ BaseService + IVL/AOI/EQE/VAM Services             │
│  ├─ FlowMQTTService (MQTT comm)                        │
│  └─ ProberClientService (hardware control)             │
└────────────────┬────────────────────────────────────────┘
                 │ (Protocol)
┌────────────────▼────────────────────────────────────────┐
│  COMMUNICATION (External Systems)                       │
│  ├─ MQTT (Test Execution)                              │
│  ├─ WebSocket/TCP (Hardware)                           │
│  ├─ Database (Persistence)                             │
│  └─ File System (Logs & Exports)                       │
└─────────────────────────────────────────────────────────┘
```

---

## 🔄 Execution Flow Chain Summary

### Startup (3 min)
```
App.xaml → Log init → Config load → DLL load → 
ShowStartupWindow → 3 Tasks → ShowMainWindow → Ready
```

### Single Test (30-60 sec)
```
User clicks die → StartManTest → MoveToDie → 
SwitchPanel → MQTT test → 350ms timer loop → 
ExportCSV → Done
```

### Batch Test (5-10 min for 10 dies)
```
User selects dies → StartAutoTesting → Loop:
├─ Get next die
├─ Test it (same as single)
├─ Auto-export
├─ Check errors
└─ Repeat or finalize
→ Summary.csv → Clear breakpoint → Done
```

---

## 📁 Key Files by Functionality

### Test Execution Entry Points
- `App.xaml.cs` - Application startup
- `Views/DockMainWindow.xaml.cs` - Main UI & panel switching
- `Views/WaferProberStartupWindow.xaml.cs` - Startup sequence

### Test Control
- `Services/MainService.cs` - Core orchestration (998 lines)
- `ViewModels/MappingDataViewModel.cs` - Die selection & test start
- `Models/AutoTestingItem.cs` - Batch test container

### Flow Execution
- `Services/BaseService.cs` - Abstract test executor
- `Services/IVLService.cs` - Spectrometer test
- `Services/AOIService.cs` - AOI inspection
- `Services/EQEService.cs` - EQE measurement
- `Services/VAMService.cs` - Visual analysis

### Communication
- `Services/FlowMQTTService.cs` - MQTT messaging
- `Services/ProberClientService.cs` - Hardware motion
- `MQTT/MQTTConfig.cs` - Configuration

### Data & Recovery
- `Models/AutoTestingItem.cs` - Batch container
- `ViewModels/DieViewModel.cs` - Die state
- `Services/BreakpointManagerService.cs` - Crash recovery

### Utilities
- `Utils/AutoExportHelper.cs` - CSV export
- `Utils/SNBuilder.cs` - Serial number generation

---

## 🔑 Key Concepts

### AutoTestingItem
- Container for batch test execution
- Tracks which dies are done (CurTestingIndex)
- Enables pause/resume functionality
- Serializable for breakpoint recovery

### DieViewModel Status Machine
```
PENDING → TESTING → PASS/FAIL/TIMEOUT/NG
```

### MQTT Communication
- Host:Port from config (default: 127.0.0.1:1883)
- Request: {flowName, serialNumber, timestamp}
- Response: {status, resultData, errorMessage}
- Timeout: Configurable per WPFlow (default 30-60 sec)

### Breakpoint System
- Saves on: Each die completion, app crash
- Loads on: App startup if interrupted batch exists
- Restores: CurTestingIndex + die states
- Clears on: Successful batch completion

### Timer-Based Result Display
- Refreshes every 350ms (configurable)
- Runs during test execution only
- Queries latest data from service
- Updates chart/graph in real-time
- Stopped after test completion

---

## 🧪 Testing Scenarios

### Scenario 1: Single Test
```
1. Load CSV mapping
2. Load procedures
3. Click 1 die + "Start"
→ 30-60 seconds later: Results displayed
```

### Scenario 2: Batch Test
```
1. Load CSV mapping
2. Load procedures  
3. Select 10 dies + "Start"
→ 5-10 minutes: Auto-processes all dies
→ Summary.csv created with all results
```

### Scenario 3: Resume After Crash
```
1. Batch starts (testing die 5/10)
2. App crashes
3. Restart app → Startup window → Recovery dialog
4. Select "Resume" → Continues from die 5
```

---

## 📌 Important Notes

- **Thread Safety**: All UI updates via Dispatcher.Invoke()
- **Async Operations**: Services use async/await extensively
- **Resource Management**: Timers properly disposed to prevent leaks
- **Error Resilience**: Breakpoints saved after each die completion
- **Configuration Flexibility**: MQTT config loadable from database
- **Extensibility**: New test types added by subclassing BaseService

---

## 🚀 Getting Started

1. **New to the codebase?**
   - Read QUICK_REFERENCE.md (5 min read)
   - Skim EXECUTION_FLOW_SUMMARY.md (10 min read)

2. **Need to add a new test type?**
   - Study BaseService.cs structure
   - Look at IVLService.cs as example
   - Implement StartTestingAsync() + FlowResultDisplayAsync()
   - Register in MainService.InitializeService()

3. **Debugging test execution?**
   - Check MainService.cs methods (StartAutoTesting, DoNextDieFlowExec)
   - Review FlowMQTTService.FlowRunAndWaitResponseAsync()
   - Enable logging: EXECUTION_FLOW_ANALYSIS.md Section 10

4. **Understanding UI updates?**
   - Read DockMainWindow.xaml.cs
   - Study MainViewModel panel switching logic
   - Check BaseService timer implementations

---

## 📞 Quick Q&A

**Q: How does the app know when to switch to the spectrometer panel?**
A: IVLService.StartTestingAsync() calls mainVm.ActivateSpectralInnerTabAction?.Invoke()
See: EXECUTION_FLOW_SUMMARY.md → UI Panel Activation

**Q: What happens if test timeout?**
A: FlowRunAndWaitResponseAsync() catches TaskCanceledException,
   sets die.Status = FAILED, continues to next die
See: EXECUTION_FLOW_ANALYSIS.md → Error Handling

**Q: How to resume a crashed batch?**
A: MainService.TryRecoverFromBreakpointAsync() loads saved AutoTestingItem,
   restores CurTestingIndex, resumes DoNextDieFlowExec()
See: EXECUTION_FLOW_SUMMARY.md → Breakpoint Recovery

**Q: Where are CSV exports saved?**
A: D:\CVTest\{Category}\{SN}_{Timestamp}.csv
   Also updates D:\CVTest\Summary.csv
See: EXECUTION_FLOW_ANALYSIS.md → Data Export & Persistence

---

## 📈 File Statistics

| Document | Lines | Size | Purpose |
|----------|-------|------|---------|
| QUICK_REFERENCE.md | 198 | 6 KB | Quick lookup |
| EXECUTION_FLOW_SUMMARY.md | 500+ | 16 KB | Architecture & flows |
| EXECUTION_FLOW_ANALYSIS.md | 636 | 24 KB | Deep technical details |

**Total**: 1,334+ lines of comprehensive documentation

---

## ✅ Verification Checklist

- [x] Application startup flow documented
- [x] MainWindow initialization explained
- [x] ViewModels hierarchy detailed
- [x] Services architecture described
- [x] Data models specified
- [x] Manual test execution flow charted
- [x] Auto/batch test execution flow charted
- [x] MQTT communication flow detailed
- [x] UI panel activation mechanism explained
- [x] Breakpoint recovery system documented
- [x] Error handling strategies detailed
- [x] CSV export process documented
- [x] Configuration system explained
- [x] Threading and thread-safety addressed
- [x] Code examples and paths provided

---

**Generated**: March 14, 2024
**Application**: CVWaferProber
**Version**: Current
**Purpose**: Developer reference and architectural understanding

