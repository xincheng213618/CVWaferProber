# CVWaferProber - Execution Flow Documentation

## 📖 START HERE

This repository now contains comprehensive documentation of the CVWaferProber application's architecture and execution flow.

### Pick Your Read:

**⏱️ 5-10 minutes? (Quick Overview)**
- Read: `QUICK_REFERENCE.md`
- Perfect for: Orientation, quick answers during development

**⏱️ 30 minutes? (Architecture Understanding)**
- Read: `README_EXECUTION_FLOW.md` (navigation guide)
- Then: `EXECUTION_FLOW_SUMMARY.md` (comprehensive overview)
- Perfect for: Understanding how the app works

**⏱️ 1-2 hours? (Complete Technical Deep-Dive)**
- Read: `EXECUTION_FLOW_SUMMARY.md` (architecture + flows)
- Then: `EXECUTION_FLOW_ANALYSIS.md` (detailed technical breakdown)
- Then: Review source code with documentation as guide
- Perfect for: Implementation details, debugging, extending features

---

## 📚 Documentation Files

| File | Size | Purpose |
|------|------|---------|
| `QUICK_REFERENCE.md` | 6 KB | Quick lookup guide (198 lines) |
| `EXECUTION_FLOW_SUMMARY.md` | 16 KB | Architecture & flow overview (500+ lines) |
| `EXECUTION_FLOW_ANALYSIS.md` | 24 KB | Complete technical details (636 lines) |
| `README_EXECUTION_FLOW.md` | 12 KB | Navigation & index (260 lines) |

**Total: 1,594 lines of documentation, 57 KB**

---

## 🎯 What You'll Learn

### How the App Works
```
App Startup → 3 Startup Tasks → Main Window → Ready

User selects die(s) → Test execution via MQTT → Results display
                                ↓
                          Auto CSV export
```

### Key Concepts
- **MVVM Architecture**: ViewModels + Services + Data Models
- **MQTT Communication**: Test control via message broker
- **Batch Processing**: Recursive loop for multi-die testing
- **Crash Recovery**: Automatic breakpoint saving & restoration
- **Real-Time Updates**: 350ms timer for live chart refresh

### Key Services
1. **MainService** (998 lines) - Core orchestration
2. **BaseService** + Flow Services - Test execution
3. **FlowMQTTService** - MQTT communication
4. **ProberClientService** - Hardware control

### Key ViewModels
1. **MainViewModel** - Central coordinator
2. **MappingDataViewModel** - Die mapping & test control
3. **DieViewModel** - Single die state machine

---

## 🚀 Quick Start for Common Tasks

### Understanding Test Execution
1. Read QUICK_REFERENCE.md → Manual/Auto Test sections
2. Check EXECUTION_FLOW_SUMMARY.md → Test Execution Flows
3. Deep dive: EXECUTION_FLOW_ANALYSIS.md → Sections 6-7

### Understanding UI Panel Switching
1. Check QUICK_REFERENCE.md → UI Panel Activation
2. Deep dive: EXECUTION_FLOW_ANALYSIS.md → Section 8 (Control Flow)
3. Look at: IVLService.StartTestingAsync() → Calls ActivateSpectralInnerTabAction

### Understanding Crash Recovery
1. Quick overview: QUICK_REFERENCE.md → Breakpoint Recovery
2. Full details: EXECUTION_FLOW_SUMMARY.md → Breakpoint Recovery
3. Complete flow: EXECUTION_FLOW_ANALYSIS.md → Section 8 (Error Handling)

### Adding a New Test Type
1. Study EXECUTION_FLOW_SUMMARY.md → Service Architecture
2. Examine BaseService.cs in code
3. Look at IVLService.cs as example
4. Follow the pattern: Subclass BaseService + implement 2 methods

### Debugging Test Issues
1. Check QUICK_REFERENCE.md → Debugging Checklist
2. Review relevant flow in EXECUTION_FLOW_ANALYSIS.md
3. Look at MainService methods (StartAutoTesting, DoNextDieFlowExec)
4. Check FlowMQTTService for MQTT issues

---

## 📊 Application Architecture (One-Page View)

```
┌─────────────────────────────────────────────────┐
│  PRESENTATION                                    │
│  DockMainWindow (AvalonDock 4-panel layout)     │
│  ├─ Left: Mapping panel                         │
│  ├─ Right-Top: Camera panel                     │
│  ├─ Right-Mid: Spectrometer/IVL/AOI/EQE/VAM   │
│  └─ Bottom: Log panel                           │
└────────────────┬────────────────────────────────┘
                 │ Binding
┌────────────────▼────────────────────────────────┐
│  VIEWMODELS                                      │
│  MainViewModel + 9 supporting VMs               │
│  ├─ MappingDataViewModel (die control)         │
│  ├─ DieViewModel (state machine)               │
│  └─ Others (Camera, Spectrometer, etc.)        │
└────────────────┬────────────────────────────────┘
                 │ Commands/Events
┌────────────────▼────────────────────────────────┐
│  SERVICES                                        │
│  MainService + BaseService + Flow Services      │
│  ├─ IVLService (Spectrometer)                  │
│  ├─ AOIService (Optical inspection)            │
│  ├─ EQEService (Quantum efficiency)            │
│  └─ VAMService (Visual analysis)               │
└────────────────┬────────────────────────────────┘
                 │ Protocol
┌────────────────▼────────────────────────────────┐
│  COMMUNICATION                                   │
│  MQTT (127.0.0.1:1883) + Database + File System│
└─────────────────────────────────────────────────┘
```

---

## 🔑 Key Methods You Need to Know

| Method | Location | Purpose |
|--------|----------|---------|
| `StartAutoTesting()` | MainService | Start batch test |
| `DoNextDieFlowExec()` | MainService | Recursive loop for each die |
| `RunFlowAsync()` | BaseService | Core test execution |
| `FlowRunAndWaitResponseAsync()` | FlowMQTTService | MQTT communication |
| `StartTestingAsync()` | Flow services | Service-specific setup |
| `ActivateSpectralInnerTab()` | DockMainWindow | Panel switching |
| `SaveBreakpointAsync()` | MainService | Crash recovery |

---

## 💡 Core Flows (One-Line Each)

**Manual Test**: Select die → Start → Prober moves → MQTT test → CSV export → Done

**Batch Test**: Select dies → Start → Loop(Test each) → Errors check → CSV summary → Done

**Startup**: Mutex → Logging → Config → DLL → 3 Tasks → MainWindow → Ready

**Crash**: Exception saved → Restart → Load saved state → Resume or fresh

---

## ❓ FAQ

**Q: How does the app decide which panel to show?**  
A: Service calls MainViewModel.ActivateSpectralInnerTabAction delegate → Window switches tabs

**Q: What if MQTT server is down?**  
A: Timeout after WPFlowViewModel.Timeout seconds → Die marked as FAILED → Continue batch

**Q: How to resume a crashed batch test?**  
A: App loads saved breakpoint → Shows dialog → User clicks Resume → Continues from saved index

**Q: Where are test results saved?**  
A: CSV to D:\CVTest\{Category}\, Summary.csv updated, session to database

**Q: How to add a new test type?**  
A: Subclass BaseService + implement StartTestingAsync() + FlowResultDisplayAsync()

---

## 📝 Files Generated

All files are in: `/home/runner/work/CVWaferProber/CVWaferProber/`

```
├─ QUICK_REFERENCE.md                    [Read first]
├─ EXECUTION_FLOW_SUMMARY.md             [Deep understanding]
├─ EXECUTION_FLOW_ANALYSIS.md            [Complete details]
├─ README_EXECUTION_FLOW.md              [Navigation guide]
└─ START_HERE.md                         [This file]
```

---

## ✅ What This Documentation Covers

- ✅ Application startup flow (3 startup tasks)
- ✅ MainWindow initialization (AvalonDock 4 panels)
- ✅ ViewModel hierarchy & responsibilities
- ✅ Service layer architecture (9+ services)
- ✅ Manual test execution (30-60 seconds)
- ✅ Batch test execution (recursive loop)
- ✅ MQTT communication protocol
- ✅ Panel activation & tab switching
- ✅ Real-time chart updates (350ms timer)
- ✅ Crash recovery & breakpoint system
- ✅ Error handling & break-on-error logic
- ✅ CSV export & data persistence
- ✅ Configuration sources & priority
- ✅ Threading model & thread safety
- ✅ Performance considerations

---

## 🎓 Learning Path

### Week 1: Understand the Architecture
- Day 1: Read QUICK_REFERENCE.md
- Day 2: Read EXECUTION_FLOW_SUMMARY.md
- Day 3: Review App.xaml.cs → DockMainWindow.xaml.cs → MainViewModel.cs
- Day 4: Review MainService.cs → BaseService.cs → IVLService.cs
- Day 5: Read EXECUTION_FLOW_ANALYSIS.md completely

### Week 2: Deep Implementation Study
- Day 1: Trace manual test flow in code
- Day 2: Trace batch test flow in code
- Day 3: Study MQTT communication (FlowMQTTService)
- Day 4: Study UI panel switching
- Day 5: Study crash recovery & breakpoint system

### Week 3+: Add Features
- Study existing flow services as examples
- Implement new features following patterns
- Refer to documentation for context

---

## 📞 Need Help?

- **Architecture questions?** → Check EXECUTION_FLOW_SUMMARY.md
- **Implementation details?** → Check EXECUTION_FLOW_ANALYSIS.md
- **Quick lookup?** → Check QUICK_REFERENCE.md
- **Navigation?** → Check README_EXECUTION_FLOW.md

---

**Last Updated**: March 14, 2024  
**Documentation Version**: 1.0  
**Coverage**: Complete codebase analysis

Start with QUICK_REFERENCE.md! ⭐

