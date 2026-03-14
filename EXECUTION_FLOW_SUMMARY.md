# CVWaferProber - Execution Flow Summary

## Quick Reference: Architecture & Data Flow

### Application Layers (Bottom to Top)

```
┌─────────────────────────────────────────────────────────────────┐
│                         PRESENTATION                             │
│  DockMainWindow (AvalonDock) with 4 panels + menu/toolbar       │
│  - Left: ChipMapping (die selection)                             │
│  - Right-Top: Camera feed                                        │
│  - Right-Mid: Spectrometer/IVL/AOI/EQE/VAM analysis panels      │
│  - Bottom: Log viewer                                            │
└──────────────────────────┬──────────────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────────────┐
│                      VIEW MODEL LAYER                            │
│  MainViewModel (central coordinator)                             │
│  ├─ MappingDataViewModel: die mapping & test sequencing         │
│  ├─ ChipMappingControlViewModel: die visualization              │
│  ├─ CVCamImagerViewModel: camera control                        │
│  ├─ CVSpectrumViewModel: spectrometer/IVL control               │
│  └─ ToolsBarViewModel: toolbar commands                         │
└──────────────────────────┬──────────────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────────────┐
│                     SERVICE LAYER                                │
│  MainService (test orchestration)                               │
│  ├─ IVLService: Spectrometer IV/light curve measurement         │
│  ├─ AOIService: Automated optical inspection                    │
│  ├─ EQEService: External quantum efficiency measurement         │
│  ├─ VAMService: Visual analysis & measurement                   │
│  ├─ FlowMQTTService: MQTT communication to prober               │
│  ├─ ProberClientService: Hardware motion control                │
│  └─ BreakpointMemoryService: Crash recovery                     │
└──────────────────────────┬──────────────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────────────┐
│               COMMUNICATION & PERSISTENCE                        │
│  ├─ MQTT (Test Flow Control)                                    │
│  ├─ WebSocket/TCP (Hardware Motion/Status)                      │
│  ├─ Database (Results Storage)                                  │
│  └─ File System (CSV Export & Config)                           │
└─────────────────────────────────────────────────────────────────┘
```

---

## Key Files & Responsibilities

### Core Entry Points
| File | Purpose | Key Methods |
|------|---------|-------------|
| `App.xaml.cs` | Application startup | OnStartup(), Initialize DLL & tasks |
| `Views/DockMainWindow.xaml.cs` | Main UI window | AvalonDock layout, Tab switching |
| `Views/WaferProberStartupWindow.xaml.cs` | Splash screen | Execute 3 startup tasks |

### ViewModels
| File | Responsibility | Key Collections |
|------|-----------------|-----------------|
| `MainViewModel.cs` | Central coordinator | DataMappingVM, CustomMappingVM, CustomIVLVM |
| `MappingDataViewModel.cs` | Die mapping & test control | TestResults, WPFlows |
| `DieViewModel.cs` | Single die state | Status, SerialNumber, Results |
| `FlowViewModel.cs` | Test procedure definition | Name, Type, Timeout |

### Services
| File | Function | Key Methods |
|------|----------|-------------|
| `MainService.cs` | Test orchestration | StartAutoTesting(), DoNextDieFlowExec() |
| `BaseService.cs` | Abstract flow executor | RunFlowAsync(), FlowResultDisplayAsync() |
| `IVLService.cs` | Spectrometer control | StartTestingAsync() with 350ms timer |
| `FlowMQTTService.cs` | MQTT communication | FlowRunAndWaitResponseAsync() |
| `ProberClientService.cs` | Hardware control | MoveToDie(), SendResults() |

### Models
| File | Purpose |
|------|---------|
| `StartupTasks.cs` | Startup task hierarchy (3 types) |
| `AutoTestingItem.cs` | Batch test container & iterator |
| `DieViewModel.cs` | Single die state machine |

---

## Test Execution Flow (Step-by-Step)

### Manual Test (Single Die)
```
1. User loads mapping (CSV file) → MappingDataViewModel.TestResults
2. User loads procedures (DB) → MappingDataViewModel.WPFlows
3. User selects die + procedure + clicks "Start Manual Test"
   ↓
4. MappingDataViewModel.StartManTest() calls:
   MainService.StartDieTestingAsync(flow, die)
   ↓
5. MainService generates SerialNumber: ProberId_Timestamp_X_Y
   ↓
6. ProberClientService moves to die position
   ↓
7. DockMainWindow activates corresponding panel (IVL/AOI/EQE/VAM)
   ↓
8. Corresponding Service.StartTestingAsync() executes:
   - Change die.Status → TESTING
   - FlowMQTTService sends MQTT request
   - Start 350ms refresh timer (ResultDisplay)
   - Wait for completion
   ↓
9. On completion:
   - Update die.Status → PASS/FAIL/TIMEOUT
   - Auto-export CSV
   - Display results
```

### Auto Test (Batch - Multiple Dies)
```
1-2. Same as manual: Load mapping & procedures
3. User selects MULTIPLE dies + clicks "Start Manual Test"
   ↓
4. MappingDataViewModel detects multiple selection:
   MainService.StartAutoTesting(flow, selectedDiesList)
   ↓
5. MainService creates AutoTestingItem (batch container)
   ├─ TestingDieVMList = selectedDiesList
   ├─ CurSelectedWPFlow = flow
   └─ CurTestingIndex = 0
   ↓
6. Save breakpoint (for crash recovery)
   ↓
7. Call DoNextDieFlowExec(autoTestingItem) [LOOP START]
   ├─ Get next die: item.GetNextDieVM() → (prev, current)
   ├─ MoveToDieAndTestingAsync(flow, current, hasNext=true)
   │  ├─ Prober moves
   │  ├─ Panel switches
   │  └─ ExecuteDieTestWithProgress()
   │     └─ service.StartTestingAsync(die, flow, hasNext=true)
   │        └─ BaseService.RunFlowAsync()
   │           └─ FlowMQTTService waits for test completion
   └─ Test completes
   ↓
8. OnAutoTestingNextCompleted() triggered:
   ├─ Update die.Status
   ├─ Auto-export CSV
   ├─ Save session
   ├─ Check for error (break-on-error logic)
   └─ IF more dies left:
      └─ Recursively call DoNextDieFlowExec() [NEXT DIE]
      ELSE:
      └─ Call DoAutoTestEnd() → Finalize batch
```

---

## MQTT Communication Flow

```
┌─ User clicks "Test" ─────────────────────────────────────────┐
│                                                               │
├─ Service.StartTestingAsync()                                 │
│  └─ BaseService.RunFlowAsync()                               │
│     └─ FlowMQTTService.FlowRunAndWaitResponseAsync()         │
│        │                                                     │
│        ├─ Publish: {flowName, serialNumber, timestamp, ...}  │
│        │  to MQTT broker                                     │
│        │                                                     │
│        ├─ Wait for response (configurable timeout)           │
│        │  Default: WPFlowViewModel.Timeout seconds           │
│        │                                                     │
│        └─ Receive: {status, resultData, timestamp}           │
│           from MQTT broker                                   │
│                                                               │
└─ If success: Update UI ──────────────────────────────────────┘
```

**MQTT Configuration**: `cfg/MQTT.config` or `App.config`
- Default Host: 127.0.0.1:1883
- Can be overridden from database (CVConfigType.MQTT)

---

## Result Display & Export

### Real-Time Display (While Testing)
```
For IVLService (example):
├─ Timer every 350ms: refreshTimer.Elapsed event
├─ Callback: ResultDisplay(currentDie)
│  ├─ Query SpectrumViewModel for latest results
│  ├─ Update chart/graph in UI
│  └─ Cache SpectrumMeasurement data
└─ Continues until test completion
```

### Auto-Export CSV (After Each Die)
```
AutoExportHelper.ExportCategoryCsv()
├─ Path: D:\CVTest\{Category}\{Category}_{SN}_{Timestamp}.csv
├─ Categories: AOI, IVL, EQE, VAM
└─ Also updates D:\CVTest\Summary.csv with batch record
```

### Session Save (Auto-triggered)
```
MappingDataViewModel.SaveLastSessionAsync()
├─ Save die mapping state
├─ Save test results
└─ Triggered after:
   - Each die completion
   - Batch completion
   - App crash (via exception handlers)
```

---

## Breakpoint Recovery (Crash Resilience)

```
On Crash or Pause:
├─ App exception handler catches
├─ MainService.SaveBreakpointAsync()
│  ├─ Save AutoTestingItem (with CurTestingIndex)
│  ├─ Save all completed die states
│  └─ Persist to BreakpointMemoryService
└─ App closes gracefully

On Next Startup:
├─ App.OnStartup() completes
├─ MainService.TryRecoverFromBreakpointAsync() called
├─ If breakpoint exists:
│  ├─ Load AutoTestingItem
│  ├─ Set CurTestingIndex to saved position
│  ├─ Offer user choice: Resume or Start Fresh
│  └─ If Resume:
│     └─ Resume DoNextDieFlowExec() from checkpoint
└─ Clear breakpoint on successful completion
```

---

## UI Panel Activation (Dynamic Tab Switching)

```
MainViewModel has 3 action delegates:
├─ ActivateSpectralInnerTabAction
│  └─ Calls DockMainWindow.ActivateSpectralInnerTab()
│     └─ SwitchOuterTab(0) → SwitchInnerTab(0) [Overview]
│
├─ ActivateIVLCameraInnerTabAction
│  └─ SwitchOuterTab(0) → SwitchInnerTab(5) [IVLCamera]
│
└─ ActivateEQEOuterTabAction
   └─ SwitchOuterTab(1) [EQE mode]

When die test starts:
├─ Service.StartTestingAsync() determines flow type
├─ Service calls appropriate action delegate
└─ DockMainWindow switches to correct panel

Result: User automatically sees relevant analysis panel
```

---

## Error Handling Strategy

### Break-On-Error (Batch Test)
```
After each die completion:
├─ MainService checks AutoTestingItem.IsPaused
├─ If ConfigManager.Config.IsBreakOnError:
│  ├─ Count consecutive errors (max 10)
│  ├─ If count >= BreakOnErrorNum:
│  │  └─ Pause batch (show pause UI)
│  │     └─ User can Resume or Cancel
│  └─ Else:
│     └─ Continue to next die
└─ Single test errors don't stop batch
```

### Timeout Handling
```
BaseService.RunFlowAsync() has timeout:
├─ FlowRunAndWaitResponseAsync(timeout = WPFlowViewModel.Timeout)
├─ If MQTT response timeout:
│  ├─ Catch TaskCanceledException
│  ├─ Log error
│  └─ Change die.Status → FAILED or OVERTIME
└─ Continue with next die (batch) or finalize (single)
```

### Unhandled Exception Handler
```
App_DispatcherUnhandledException() / CurrentDomain_UnhandledException()
├─ Log exception
├─ Try to save breakpoint (critical!)
├─ Show error message
└─ Allow user to:
   - Close app (next startup can recover)
   - Or continue (if recoverable)
```

---

## State Machine: DieViewModel Status

```
PENDING
   ↓
TESTING ← (IVL_TESTING, AOI_TESTING, etc.)
   ↓
┌──────────┬──────────┬──────────┬──────────┐
│          │          │          │          │
PASS    FAIL      TIMEOUT    NG (from prober)
│          │          │          │          │
└──────────┴──────────┴──────────┴──────────┘
   ↓         ↓         ↓         ↓
[Result stored, exported, displayed]
```

---

## Configuration Sources (Priority Order)

```
1. Database (CVConfigType.MQTT)
   └─ If NodeName exists in App.config, load from DB first
   
2. App.config / Custom config files
   └─ cfg/MQTT.config, cfg/SystemConfig.config
   
3. Code defaults
   └─ MQTTConfig.Init() defaults
```

---

## Performance Considerations

- **UI Updates**: Always via Dispatcher (thread-safe)
- **Async Operations**: MainService uses async/await extensively
- **Timer-based Refresh**: 350ms for real-time chart updates (tunable)
- **Batch Processing**: Sequential die execution (not parallel)
- **Breakpoint Saving**: Debounced & batched to avoid I/O storms
- **Auto-Save Session**: Debounced timer (100ms default)

---

## Testing Path Examples

### Scenario 1: Single IV Test
```
1. Load mapping CSV → 10 dies visible
2. Load IVL procedure
3. Click die [3,3]
4. Click "Start Manual Test"
   → Die [3,3] status: TESTING
   → Spectrometer panel activates
   → Chart updates every 350ms
   → After ~30sec: Status → PASS/FAIL
   → CSV exported to D:\CVTest\IVL\IVL_ProberId_Timestamp_3_3.csv
5. User can click another die and repeat
```

### Scenario 2: Batch Test (10 Dies)
```
1. Load mapping CSV
2. Load IVL procedure
3. Select dies: [0,0], [0,1], [1,0], [1,1], etc. (10 dies)
4. Click "Start Manual Test"
   → AutoTestingItem created
   → Batch starts
   → For each die:
      ├─ Prober moves to position
      ├─ Status → TESTING
      ├─ MQTT sends test command
      ├─ Wait for completion (~30sec per die)
      ├─ Auto-export CSV
      ├─ Check for errors
      └─ If no error: proceed to next die
   → After all 10 complete:
      ├─ Update Summary.csv
      ├─ Save final session
      ├─ Offer resume breakpoint for next run
      └─ UI unlocks for new operations
```

### Scenario 3: Crash & Recovery
```
1. Start batch test (10 dies)
2. Dies [0,0] through [2,1] complete (6 dies)
3. App crashes during [2,2] testing
   → Exception handler saves breakpoint with CurTestingIndex=6
   → AutoTestingItem state saved
4. User restarts application
   → Startup window shows 3 tasks
   → On completion: MainVM.TryRecoverFromBreakpointAsync()
   → Dialog: "Batch interrupted, resume from die 7/10?"
   → User clicks "Resume"
      → Batch resumes from [2,2] onwards
      → No re-test of completed 6 dies
```

---

## Summary: Key Takeaways

✅ **Modular Architecture**: Clear separation between UI, VM, Service layers
✅ **Event-Driven**: Commands → Services → Events → UI updates
✅ **MQTT Integration**: Flexible communication with external prober hardware
✅ **Resilient**: Breakpoint recovery, crash handling, error detection
✅ **Real-Time**: 350ms refresh timers for live chart updates
✅ **Batch Support**: Sequential multi-die testing with pause/resume
✅ **Data Export**: Auto CSV export + Summary.csv tracking
✅ **Extensible**: New test types via BaseService subclassing (IVL, AOI, EQE, VAM)

