# CVWaferProber - Quick Reference Guide

## File Structure Overview

CVWaferProber/
├─ App.xaml.cs                      [Application entry point]
├─ Views/ → DockMainWindow.xaml.cs  [Main UI: 4-panel AvalonDock]
├─ ViewModels/
│  ├─ MainViewModel.cs              [Central coordinator]
│  ├─ MappingDataViewModel.cs       [Die mapping & test control]
│  └─ DieViewModel.cs               [Single die state machine]
├─ Services/
│  ├─ MainService.cs                [Test orchestration]
│  ├─ BaseService.cs                [Abstract executor]
│  ├─ IVLService/AOIService/...     [Flow-specific services]
│  └─ FlowMQTTService.cs            [MQTT communication]
└─ Models/ → AutoTestingItem.cs     [Batch test container]

---

## Application Startup (120 seconds)

App.xaml.cs OnStartup
  ├─ Single instance check (Mutex)
  ├─ Log4net initialization
  ├─ Load configuration
  ├─ Load CV algorithm DLL
  └─ Show WaferProberStartupWindow
     ├─ Execute 3 tasks:
     │  ├─ MainStartupTask (Hardware registration)
     │  ├─ MotionStartupTask (Prober connection)
     │  └─ CommStartupTask (MQTT startup)
     └─ On completion: Show DockMainWindow

---

## Manual Test Flow (30-60 seconds per die)

User clicks "Start Test" on selected die
  ├─ MainService.StartDieTestingAsync(flow, die)
  │  ├─ Generate SerialNumber (proberId_timestamp_x_y)
  │  ├─ Move prober to die position
  │  ├─ Switch UI panel (IVL/AOI/EQE/VAM)
  │  └─ Execute test
  │     ├─ MQTT sends request
  │     ├─ Timer updates chart every 350ms
  │     └─ Wait for completion
  └─ On completion:
     ├─ Update die status
     ├─ Auto-export CSV
     └─ Ready for next action

---

## Auto Test (Batch) Flow

User selects multiple dies and clicks "Start Test"
  ├─ MainService.StartAutoTesting(flow, selectedDies)
  │  ├─ Create AutoTestingItem
  │  ├─ Save breakpoint
  │  └─ DoNextDieFlowExec(item) [LOOP]
  │     ├─ Get next die
  │     ├─ Test it (same as manual)
  │     └─ OnAutoTestingNextCompleted
  │        ├─ Auto-export CSV
  │        ├─ Check break-on-error
  │        └─ IF more dies: Loop [NEXT DIE]
  │           ELSE: Finalize batch
  └─ On completion:
     ├─ Update Summary.csv
     └─ Clear breakpoint

---

## Key Services & Methods

MainService (998 lines) - Test orchestration
  ├─ StartAutoTesting(flow, dies)
  ├─ DoNextDieFlowExec(item)
  ├─ ExecuteDieTestWithProgress(flow, die)
  ├─ OnAutoTestingNextCompleted(die)
  ├─ SaveBreakpointAsync()
  └─ TryRecoverFromBreakpointAsync()

BaseService - Abstract executor
  └─ RunFlowAsync(flow, die)
     ├─ MQTT: FlowRunAndWaitResponseAsync()
     ├─ Timer: ResultDisplay() every 350ms
     └─ Finalize: FlowResultDisplayAsync()

IVLService/AOIService/EQEService/VAMService
  └─ StartTestingAsync(die, flow)
     ├─ Activate UI panel
     └─ Call RunFlowAsync()

FlowMQTTService - MQTT communication
  └─ FlowRunAndWaitResponseAsync()
     ├─ Publish request
     └─ Wait for response (timeout = WPFlowViewModel.Timeout)

---

## Data Models

AutoTestingItem - Batch container
  ├─ TestingDieVMList (list of dies)
  ├─ CurTestingIndex (current position)
  ├─ CurSelectedWPFlow (procedure)
  └─ GetNextDieVM() → (prev, current)

DieViewModel - Die state machine
  ├─ Status (PENDING→TESTING→PASS/FAIL/TIMEOUT)
  ├─ SerialNumber (proberId_timestamp_x_y)
  ├─ MapX, MapY (position)
  └─ IsSelected, IsNG, FinalClass

WPFlowViewModel - Procedure definition
  ├─ Name ("IVL", "AOI", "EQE", "VAM")
  ├─ FlowType
  ├─ Timeout (seconds)
  └─ Enabled

---

## MQTT Communication

Host: 127.0.0.1:1883 (configurable)

FlowRunAndWaitResponseAsync()
  ├─ Publish: {flowName, serialNumber, timestamp}
  ├─ Wait: response (timeout = WPFlowViewModel.Timeout)
  └─ Return: DeviceResponseMessageHeader

---

## Data Export

Auto-Export (per die):
  └─ D:\CVTest\{Category}\{Category}_{SN}_{Timestamp}.csv

Summary (per batch):
  └─ D:\CVTest\Summary.csv

Session Save (auto-triggered):
  └─ After die completion / batch completion / crash
     → Used for resuming interrupted batches

---

## Breakpoint Recovery

On Crash:
  └─ Save AutoTestingItem state + die statuses
     → BreakpointMemoryService

On Startup:
  ├─ TryRecoverFromBreakpointAsync()
  └─ If breakpoint exists:
     └─ Dialog: "Resume batch (die X/Y)?"
        ├─ Yes: Resume from saved index
        └─ No: Start fresh

---

## UI Panel Activation

MainViewModel delegates:
  ├─ ActivateSpectralInnerTabAction → Overview tab
  ├─ ActivateIVLCameraInnerTabAction → IVLCamera tab
  └─ ActivateEQEOuterTabAction → EQE mode

Service calls delegate during test:
  └─ mainVm.ActivateSpectralInnerTabAction?.Invoke()
     → DockMainWindow switches panel automatically

---

## Configuration Sources

Priority order:
  1. Database (if NodeName in App.config)
  2. cfg/MQTT.config
  3. App.config [appSettings]
  4. Code defaults

---

## Key Takeaways

✅ Modular: Clear UI → VM → Service → MQTT layers
✅ Event-driven: Commands trigger services which raise events
✅ Resilient: Breakpoint recovery + crash handlers
✅ Real-time: 350ms refresh timers for live updates
✅ Batch support: Sequential multi-die testing
✅ Auto-export: CSV export after each die
✅ Extensible: New test types via BaseService subclassing
✅ Thread-safe: UI updates via Dispatcher.Invoke()

