│ (IVLService → Spectrometer, AOIService → AOI Panel, etc.)       │
└──────────────┬──────────────────────────────────────────────────┘
               │
┌──────────────▼──────────────────────────────────────────────────┐
│          FLOW EXECUTION & RESULT POLLING                         │
│ BaseService.RunFlowAsync() → FlowMQTTService.FlowRunAndWait()   │
│ └─ Timer: ResultDisplay() updates UI every 350ms                │
└──────────────┬──────────────────────────────────────────────────┘
               │
┌──────────────▼──────────────────────────────────────────────────┐
│            COMPLETION & DATA EXPORT                              │
│ Status update → Auto-export CSV → Save session → Next die/end   │
└─────────────────────────────────────────────────────────────────┘
```
EOF
cat /tmp/cvwaferprober_execution_flow.md
                EC=$?
                echo "___BEGIN___COMMAND_DONE_MARKER___$EC"
            }
___BEGIN___COMMAND_OUTPUT_MARKER___
# CVWaferProber Application - Task Execution Logic Chain

## 1. APPLICATION STARTUP FLOW

### Entry Point: App.xaml.cs (OnStartup)
```
App.OnStartup()
├─ Check single instance (Mutex)
├─ Initialize logging (log4net)
├─ Load configuration (ConfigHandler, ConfigManager)
├─ Setup exception handlers (DispatcherUnhandledExceptionHandler)
├─ Initialize language settings (AppSettingsManager.InitializeLanguage)
├─ Register global UI styles (RegisterGlobalStyles)
├─ Load CV algorithm DLL (CV_Ali_initial)
└─ Create and show startup window with 3 startup tasks
```

### Startup Window: WaferProberStartupWindow.xaml.cs
```
WaferProberStartupWindow.xaml.cs
├─ Initialize system info (version, build date)
├─ Create BackgroundWorker for parallel task execution
├─ Add three startup tasks:
│  ├─ MainStartupTask: Hardware interface registration
│  │  └─ MainService.TryRegistAsync() [MQTT registration]
│  ├─ MotionStartupTask: Motion control system connection
│  │  └─ MainService.TryConnectAsync() [Prober client connection]
│  └─ CommStartupTask: Communication system startup
│     └─ MainService.Startup() [MQTT & ProberClient startup]
│
├─ Execute tasks sequentially with progress reporting
├─ Update UI with task status (Completed/Failed)
└─ On completion:
   └─ Show DockMainWindow (main application window)
   └─ App.OnStartupCompleted()
```

---

## 2. MAIN WINDOW: DockMainWindow.xaml.cs

### Window Initialization
```
DockMainWindow (AvalonDock-based layout)
├─ DataContext = MainViewModel
├─ Initialize SP (Spectrometer) controls
├─ Bind camera, mapping, and spectrometer panels
├─ Setup menu bar with language selection
└─ Initialize logging panel
```

### AvalonDock Layout Structure
```
DockMainWindow (Grid-based with AvalonDock)
├─ MenuBar & Toolbar (Commands)
├─ DockingManager (AvalonDock main container)
│  ├─ AnchorableMapping (Left panel) - ChipMapping control
│  ├─ AnchorableCamera (Right top) - Camera stream display
│  ├─ AnchorableSP (Right middle) - Spectrometer analyzer
│  │  ├─ outerTabControl (2 tabs)
│  │  │  ├─ Tab[0]: Inner spectral tabs (Overview, IVL, EQE, VAM, IVLCamera)
│  │  │  └─ Tab[1]: EQE mode
│  │  └─ innerTabControl (5 tabs for IVL/EQE/VAM/IVLCamera)
│  └─ AnchorableLog (Bottom) - Log viewer
└─ StatusBar (Connection info)
```

---

## 3. VIEWMODEL HIERARCHY

### MainViewModel
- **Role**: Central coordinator of entire application
- **Key VMs**:
  - `DataMappingVM` (MappingDataViewModel): Manages die mapping and test sequencing
  - `CustomMappingVM` (ChipMappingControlViewModel): Chip layout visualization
  - `CustomImageVM` (CVCamImagerViewModel): Camera feed control
  - `CustomIVLVM` (CVSpectrumViewModel): Spectrometer control
  - `ToolsVM` (ToolsBarViewModel): Toolbar commands

- **Key Properties**:
  - Panel visibility flags: IsMappingPanelVisible, IsCameraPanelVisible, IsSPPanelVisible
  - Tab switching actions: ActivateSpectralInnerTabAction, ActivateIVLCameraInnerTabAction, ActivateEQEOuterTabAction

### MappingDataViewModel
- **Role**: Manages wafer die mapping and test execution
- **Key Collections**:
  - `TestResults`: ObservableCollection<DieViewModel> - All dies from mapping file
  - `WPFlows`: ObservableCollection<WPFlowViewModel> - Available test procedures

- **Key Commands**:
  - `LoadMappingFileCommand`: Load wafer die layout from CSV
  - `FlowLoadCommand`: Load test procedures from database
  - `StartManTestCommand`: Start manual test on selected die
  - `RefreshStatusCommand`: Sync test status with prober
  - `SaveTestResultCommand`: Export test results

### Services and Flow Services
```
FlowServices (registered in MainService.InitializeService)
├─ IVLService (IVL test flow)
├─ AOIService (AOI inspection flow)
├─ EQEService (EQE measurement flow)
└─ VAMService (Visual analysis flow)
```

---

## 4. KEY SERVICES

### MainService (Singleton)
**Location**: Services/MainService.cs (998 lines)

**Initialization Chain**:
```
MainService()
├─ Initialize FlowMQTTService (MQTT communication)
├─ Initialize ProberClientService (Hardware communication)
└─ Initialize breakpoint recovery timer
```

**Event Handlers**:
- `OnAutoTestingNextCompleted`: Handle individual die completion
- `OnTestingCompleted`: Handle batch test completion
- `OnMotionAxisUpdated`: Update die motion status

### Task Execution Methods

#### StartAutoTesting(WPFlowViewModel, List<DieViewModel>)
```
StartAutoTesting()
├─ Create AutoTestingItem (test batch container)
├─ Save breakpoint
├─ Invoke DoNextDieFlowExec() for first die
└─ Register pause/resume handlers
```

#### DoNextDieFlowExec(AutoTestingItem)
```
DoNextDieFlowExec()
├─ Get next die from AutoTestingItem
├─ Invoke MoveToDieAndTestingAsync()
│  ├─ Move prober to die position
│  ├─ Trigger ActivateCorrespondingPanel event
│  └─ Invoke ExecuteDieTestWithProgress()
└─ Handle die completion
```

#### ExecuteDieTestWithProgress(WPFlowViewModel, DieViewModel, ...)
```
ExecuteDieTestWithProgress()
├─ Update progress UI (CompletedTestCount++)
├─ Generate unique SN (SerialNumber)
├─ Call service.StartTestingAsync()
│  ├─ IVLService
│  ├─ AOIService
│  ├─ EQEService
│  └─ VAMService
└─ Handle completion
```

### BaseService (Abstract)
**Location**: Services/BaseSerivce.cs

**Key Method**: RunFlowAsync()
```
RunFlowAsync(WPFlowViewModel, DieViewModel, hasNext, isAuto)
├─ Call _flowService.FlowRunAndWaitResponseAsync()
│  ├─ MQTT sends flow execution request
│  ├─ Wait for response (timeout = WPFlowViewModel.Timeout)
│  └─ Receive flow result
├─ If success:
│  ├─ Call FlowResultDisplayAsync() - Update UI with results
│  ├─ Change DieViewModel status to appropriate ChipStatus
│  └─ Call DoAutoTestingNextCompleted() if hasNext
└─ If failure:
   ├─ Log error
   ├─ Change DieViewModel status to FAILED/OVERTIME
   └─ Call DoEndTesting() if !hasNext
```

### IVLService (Spectrometer Control Example)
**Location**: Services/IVLService.cs

**Task Execution**:
```
IVLService.StartTestingAsync(DieViewModel, WPFlowViewModel, ...)
├─ Switch UI to Overview tab (ActivateSpectralInnerTabAction)
├─ Set DieViewModel.Status = ChipStatus.IVL_TESTING
├─ Initialize result display timer (350ms refresh)
├─ Call RunFlowAsync() [from BaseService]
│  ├─ MQTT sends "IVL" flow request
│  ├─ Prober executes IV/light testing
│  └─ Waits for completion
├─ Timer callback: RefreshResultDisplay() every 350ms
│  ├─ Query spectrometer data
│  ├─ Update chart/graph in UI
│  └─ Caches SpectrumMeasurement data
└─ On task completion:
   ├─ Stop timer
   ├─ Call FlowResultDisplayAsync()
   ├─ Auto-export CSV (if enabled)
   └─ Invoke DoAutoTestingNextCompleted()
```

### FlowMQTTService (MQTT Communication)
**Location**: Services/FlowMQTTService.cs

**Key Methods**:
```
TryRegistAsync() → Register device to MQTT broker
TryConnectAsync() → (Via ProberClientService) Connect to prober hardware
Startup() → Initialize MQTT client

FlowRunAndWaitResponseAsync(flowId, flowName, serialNumber, timeout)
├─ Publish MQTT message: {flowName, serialNumber, timestamp, ...}
├─ Subscribe to result topic
├─ Wait for response (with timeout)
└─ Return DeviceResponseMessageHeader
```

### ProberClientService (Hardware Communication)
**Location**: Services/ProberClientService.cs

**Role**: WebSocket/TCP communication with actual prober hardware
- Sends motion commands (move to die)
- Receives motion status updates
- Transmits test results back to prober

---

## 5. DATA MODELS

### DieViewModel
**Location**: ViewModels/DieViewModel.cs

**Key Properties**:
```csharp
MapX, MapY           // Position on wafer map
Status               // ChipStatus (PENDING, TESTING, PASS, FAIL, OVERTIME, etc.)
SerialNumber         // Unique ID for test record (format: proberId_timestamp_x_y)
IsSelected          // User selection flag
IsNG                // Pass/Fail result
FinalClass          // Classification result

// IVL/AOI/EQE/VAM specific properties
IsIVLCameraEnabled
IsAOISelected
IsEQESelected
IsVAMSelected
```

### WPFlowViewModel
**Location**: ViewModels/FlowViewModel.cs

**Key Properties**:
```csharp
Name                 // Flow name (e.g., "IVL", "AOI", "EQE", "VAM")
FlowType             // CVWaferProberFlowType enum
Timeout              // Execution timeout (seconds)
Description
Enabled
```

### AutoTestingItem
**Location**: Models/AutoTestingItem.cs

**Role**: Container for active batch test

**Key Properties**:
```csharp
TestingDieVMList     // List of DieViewModel to test
CurSelectedWPFlow    // Active WPFlowViewModel
CurTestingIndex      // Current position in test list
IsPaused             // Pause flag
HasNext              // Check if more dies to test
```

**Key Methods**:
```csharp
GetNextDieVM()       // Return (previous, current) dies
RollbackToPrevious() // Resume from breakpoint
IsEnd                // Check if batch complete
```

### StartupTasks
**Location**: Models/StartupTasks.cs

**Hierarchy**:
```
StartupTask (abstract)
├─ MainStartupTask (Hardware registration)
├─ MotionStartupTask (Motion system)
└─ CommStartupTask (Communication)

Properties:
- Status: TaskStatus (Pending, Running, Completed, Failed)
- Description: Task name
- StatusColor: UI color code
- StatusIcon: ✓ or ✗

Method:
- Exec(): Execute task synchronously
```

---

## 6. EXECUTION FLOW CHAIN: Manual Test

```
User clicks StartManTestCommand in MappingDataViewModel
│
└─ StartManTest(selectedDie)
   ├─ Validate selected die
   ├─ Get selected WPFlow
   ├─ Call MainService.StartDieTestingAsync(flow, die)
   │  ├─ Send breakpoint recovery check
   │  └─ Call MoveToDieAndTestingAsync(flow, die, isFirst=true, hasNext=false, isAuto=false)
   │     ├─ Prober moves to die position (ProberClientService)
   │     ├─ Trigger ActivateCorrespondingPanel(flow) - Switch UI tab
   │     └─ Call ExecuteDieTestWithProgress(flow, die, ...)
   │        ├─ Generate SN = ProberId_Timestamp_X_Y
   │        ├─ Update UI: CurrentDieInfo, SingleDieTestProgress
   │        └─ Call service.StartTestingAsync(die, flow, hasNext=false, isAuto=false)
   │           └─ (See BaseService.RunFlowAsync flow below)
   │
   └─ User sees:
      ├─ Prober moves to position
      ├─ Die status changes to TESTING
      ├─ Corresponding panel tab activates (IVL/AOI/EQE/VAM)
      ├─ Real-time result updates (chart/data)
      └─ On completion: Status = PASS/FAIL, Result displayed
```

---

## 7. EXECUTION FLOW CHAIN: Auto Test (Batch)

```
User selects multiple dies + clicks StartManTestCommand

└─ MappingDataViewModel.StartManTest()
   ├─ Get selected dies list
   ├─ Get selected WPFlow
   └─ Call MainService.StartAutoTesting(flow, selectedDiesList)
      ├─ Create AutoTestingItem(diesList, flow)
      ├─ Save breakpoint (BreakpointMemoryService)
      ├─ Update UI: CanStartAuto = false, IsProcessing = true
      ├─ Call DoNextDieFlowExec(autoTestingItem) [Loop entry]
      │  │
      │  └─ For each die in the batch:
      │     ├─ Get next die: (previous, current) = item.GetNextDieVM()
      │     ├─ Call MoveToDieAndTestingAsync(flow, die, isFirst, hasNext=true, isAuto=true)
      │     │  ├─ Prober moves to die
      │     │  ├─ Activate UI tab panel
      │     │  └─ Call ExecuteDieTestWithProgress()
      │     │     ├─ Generate unique SN
      │     │     ├─ Call service.StartTestingAsync(die, flow, hasNext=true, isAuto=true)
      │     │     │  └─ BaseService.RunFlowAsync()
      │     │     │     ├─ MQTT: FlowRunAndWaitResponseAsync()
      │     │     │     │  ├─ Send request to prober
      │     │     │     │  ├─ Poll/wait for completion
      │     │     │     │  └─ Receive result
      │     │     │     │
      │     │     │     ├─ Timer callback (e.g., 350ms): ResultDisplay()
      │     │     │     │  └─ Fetch and update UI with results
      │     │     │     │
      │     │     │     └─ On completion:
      │     │     │        ├─ Update DieViewModel.Status
      │     │     │        ├─ Auto-export CSV
      │     │     │        └─ Call DoAutoTestingNextCompleted(die)
      │     │     │
      │     │     └─ Update progress: CompletedTestCount++, SingleDieTestProgress=100
      │     │
      │     └─ [OnAutoTestingNextCompleted]
      │        ├─ Save die result to database
      │        ├─ Check for errors (break on error logic)
      │        ├─ If hasNext && !isPaused:
      │        │  └─ Recursively call DoNextDieFlowExec() [Next die]
      │        └─ Else:
      │           └─ Call DoAutoTestEnd()
      │              ├─ Save final session
      │              ├─ Clear breakpoint
      │              └─ Update UI: CanStartAuto = true, IsProcessing = false
      │
      └─ [Event chain]:
         ├─ AutoTestingNextCompleted (OnAutoTestingNextCompleted)
         ├─ TestingCompleted (OnTestingCompleted)
         └─ ChipSelected (triggered by mapping view)
```

---

## 8. CONTROL FLOW CONNECTIONS

### Panel Activation (Tab Switching)
```
When executing flow_type = IVL/AOI/EQE/VAM:
├─ DieViewModel status changes
├─ MainService.OnAutoTestingNextCompleted() triggered
├─ Gets corresponding flow service
├─ Invokes ActivateCorrespondingPanel(wpflowvm)
│  └─ BaseService/IVLService/AOIService/.../Exec()
│     ├─ Switch outer tab based on flow type
│     ├─ Switch inner tab based on specific mode
│     └─ Example: IVLService calls:
│        └─ mainVm.ActivateSpectralInnerTabAction?.Invoke()
│           └─ DockMainWindow.ActivateSpectralInnerTab()
│              ├─ SwitchOuterTab(0)
│              ├─ SwitchInnerTab(0) [Overview]
│              └─ Reset SP panel state
│
└─ User sees correct analysis panel for current test
```

### Result Display Loop
```
While test is running:
├─ BaseService (or subclass) creates Timer (e.g., 350ms interval)
├─ Timer elapsed callback:
│  └─ Call UI thread: ResultDisplay(currentDie)
│     ├─ Query test results from hardware/MQTT
│     ├─ Update chart/graph with latest data
│     └─ Caches data for export
└─ When test completes:
   ├─ Stop and dispose timer
   ├─ Call FlowResultDisplayAsync() for final display
   └─ Auto-export CSV
```

### Error Handling & Breakpoint Recovery
```
If test is interrupted (pause/crash):

Save Breakpoint:
├─ MainService.SaveBreakpointAsync()
├─ Save current AutoTestingItem state
├─ Save current DieViewModel status
└─ Persist to BreakpointMemoryService

Resume from Breakpoint:
├─ Next app startup:
│  └─ MainService.TryRecoverFromBreakpointAsync()
│     ├─ Load saved AutoTestingItem
│     ├─ Set CurTestingIndex to saved position
│     └─ Resume DoNextDieFlowExec()
└─ Test continues from interrupted die
```

---

## 9. KEY COMMANDS & EVENT FLOW

### User Action → Command → Service

```
Mapping Panel:
├─ LoadMappingFileCommand
│  └─ LoadMappingFileAsync() → ParseCSV → Populate TestResults
│
├─ FlowLoadCommand
│  └─ LoadBuzWPFlows() → Query DB → Populate WPFlows
│
├─ StartManTestCommand
│  └─ StartManTest() → MainService.StartDieTestingAsync() / StartAutoTesting()
│
├─ RefreshStatusCommand
│  └─ RefreshStatus() → Query all die status from prober
│
├─ ResetStatusCommand
│  └─ ResetAllStatus() → Clear all test results
│
└─ SaveTestResultCommand
   └─ SaveTestResults() → Export to CSV/Database

Main Window:
├─ SysFlowCfgCommand
│  └─ Open SysFlowCfgWindow → Manage test procedures
│
├─ ShowConnectionSettingsCommand
│  └─ Show connection dialog for prober
│
└─ OpenProberDeviceDebugCommand
   └─ Show DevProberDebugWindow → Debug hardware

```

---

## 10. LOGGING & MONITORING

### Log Management
**Location**: Services/LogManagerService.cs
- Uses log4net for structured logging
- Logs to file: Log/ directory
- Dashboard: DockMainWindow bottom panel shows real-time logs

### Visible State Tracking
**ProberStateStatus** (Models/Enums)
- Displays prober connection state
- Shows last error
- Updates in real-time

---

## 11. DATA EXPORT & PERSISTENCE

### CSV Export (Auto-Export)
```
After each die test completion:
├─ IVLService.AutoExportData()
│  └─ Generate IV-curve CSV
├─ AOIService.AutoExportData()
│  └─ Generate AOI inspection CSV
├─ EQEService.AutoExportData()
│  └─ Generate EQE measurement CSV
└─ VAMService.AutoExportData()
   └─ Generate VAM analysis CSV

Auto-Export Helper:
├─ Base path: D:\CVTest\
├─ Category folders: AOI, IVL, EQE, VAM
├─ File format: {Category}_{SerialNumber}_{Timestamp}.csv
└─ Also updates Summary.csv with batch record
```

### Session Save
```
Save Last Session:
├─ MappingDataViewModel.SaveLastSessionAsync()
├─ Persist mapping state (selected dies, test status)
├─ Save to local database
└─ Auto-triggered:
   - After each die completion
   - After batch completion
   - On app crash (via exception handler)
```

### Breakpoint Memory
```
BreakpointMemoryService:
├─ Save: Current AutoTestingItem + DieVM state
├─ Load: Recover batch test from checkpoint
└─ Clear: After successful batch completion
```

---

## 12. COMMUNICATION LAYERS

### Layer 1: MQTT (Test Flow Control)
```
FlowMQTTService:
├─ Host: Configured in MQTT.config
├─ Topics:
│  ├─ Publish: flow/execute → {flowName, serialNumber, timestamp}
│  └─ Subscribe: flow/result → {status, data, timestamp}
└─ Used by: All flow services for test execution
```

### Layer 2: WebSocket/TCP (Hardware Motion Control)
```
ProberClientService:
├─ Host: Configured in system settings
├─ Commands:
│  ├─ Move to position (X, Y)
│  ├─ Send results
│  └─ Request status
└─ Events:
   └─ MotionAxisUpdatedEvent → Update die status
```

### Layer 3: Database (Persistent Storage)
```
Used for:
├─ Storing test results
├─ Retrieving WPFlow definitions
├─ Persisting session state
└─ Managing prober configuration
```

---

## SUMMARY: END-TO-END TEST EXECUTION

```
┌─────────────────────────────────────────────────────────────────┐
│                    APPLICATION STARTUP                          │
│ App.xaml.cs → WaferProberStartupWindow → DockMainWindow (MainVM)│
└──────────────┬──────────────────────────────────────────────────┘
               │
┌──────────────▼──────────────────────────────────────────────────┐
│          USER SELECTS DIE(S) & TEST PROCEDURE                    │
│ MappingDataViewModel.StartManTest() [manual] or                  │
│ MainService.StartAutoTesting() [batch]                           │
└──────────────┬──────────────────────────────────────────────────┘
               │
┌──────────────▼──────────────────────────────────────────────────┐
│            PROBER MOTION & PANEL ACTIVATION                      │
│ ProberClientService.MoveToDie() → UI Tab Switch                  │
│ (IVLService → Spectrometer, AOIService → AOI Panel, etc.)       │
└──────────────┬──────────────────────────────────────────────────┘
               │
┌──────────────▼──────────────────────────────────────────────────┐
│          FLOW EXECUTION & RESULT POLLING                         │
│ BaseService.RunFlowAsync() → FlowMQTTService.FlowRunAndWait()   │
│ └─ Timer: ResultDisplay() updates UI every 350ms                │
└──────────────┬──────────────────────────────────────────────────┘
               │
┌──────────────▼──────────────────────────────────────────────────┐
│            COMPLETION & DATA EXPORT                              │
│ Status update → Auto-export CSV → Save session → Next die/end   │
└─────────────────────────────────────────────────────────────────┘
```

___BEGIN___COMMAND_DONE_MARKER___0
