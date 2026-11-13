# Drone Delivery Energy Planner

**Introduction to Functional Programming | IT-2512 | 2026–2027 | Final project**

Team: **Almukhanov Yernur, Temirgali Amir, Bissengali Sanzhar**

A console application for a drone dispatcher: it checks whether a drone can take an order (zone, payload, battery),
prices the delivery, reserves the drone and keeps the fleet state as immutable snapshots.
It is the integrated and improved version of our code from Assignments 5–8.

## 1. Submitted version

- Git repository URL: https://github.com/almuhanovernur4-hue/DroneDeliveryFinal
- Branch: `main`
- Final commit: `3872d46c4d7836d06b47fb3ff5735b607b5a9f9a`

The archive uploaded to Moodle has the content of this commit. The one commit after it only records this hash in this README and in the four PDF reports.

## 2. Prerequisites

- **.NET SDK 10.x** (target framework `net10.0`). Check with `dotnet --version`.
- No NuGet packages, database or network access are needed.
- A C compiler is needed **only** to rebuild the native library:
  - Windows x64: a prebuilt `runtime/win-x64/drone_native.dll` is included; MinGW-w64 GCC rebuilds it.
  - Linux x64: a prebuilt `runtime/linux-x64/libdrone_native.so` is included; GCC rebuilds it.
  - macOS: no prebuilt binary; build it once with the Xcode command line tools (section 3).

## 3. Build, test, run

Run every command from the repository root.

**Windows (PowerShell)** — or double-click `RUN_01_SETUP_TEST.bat`, `RUN_02_DEMO.bat`, `RUN_03_MENU.bat`.

```powershell
# optional, only if gcc is installed: rebuild the native DLL
powershell -ExecutionPolicy Bypass -File native/build-windows.ps1

dotnet build DroneDeliveryFinal.slnx -c Release
dotnet run --project tests/DronePlanner.Tests -c Release
dotnet run --project src/DronePlanner.App -c Release -- --demo
dotnet run --project src/DronePlanner.App -c Release
```

**Linux x64**

```bash
bash native/build-linux.sh        # optional: rebuild the native library
dotnet build DroneDeliveryFinal.slnx -c Release
dotnet run --project tests/DronePlanner.Tests -c Release
dotnet run --project src/DronePlanner.App -c Release -- --demo
dotnet run --project src/DronePlanner.App -c Release
```

**macOS**

```bash
bash native/build-macos.sh        # required once, before dotnet build
dotnet build DroneDeliveryFinal.slnx -c Release
dotnet run --project tests/DronePlanner.Tests -c Release
dotnet run --project src/DronePlanner.App -c Release -- --demo
```

The build copies the native library and the sample JSON files next to the executable.
If booking reports `Native library: Library not found`, the library for your platform is missing from `runtime/`:
build it with the script above and run `dotnet build` again.

Options: `--demo` runs a scripted scenario without typing; `--data-dir <folder>` loads `fleet.json` and `orders.json`
from another folder; `--help` prints usage.

## 4. Test results

The tests are a deterministic assertion runner (`dotnet run`, not `dotnet test`). Every case compares an expected
value with the actual one; the process exit code is 0 only when all cases pass.

Verified on Linux x64 with .NET SDK 10.0.115: build with 0 warnings and 0 errors, and

```text
RESULT: 36/36 tests passed, 0 failed
```

| Group | Tests | What is checked |
|---|---|---|
| Pure functions and validation | 01–12, 33–34 | factories, boundaries, energy (including the exact reserve boundary), zone, payload, pricing policies, currying, Map/Bind, Option, extensions, order lookup, pending orders |
| Reports | 13–15 | empty data, tie-breaker, grouping and aggregation |
| Complete workflows | 16–17 | book → complete, book → cancel → recharge |
| Operation ordering | 18–19, 29–31, 35–36 | invalid sequence, failed stage leaves state untouched, duplicates, skipped stages, re-booking after cancel, state types without public constructors |
| Lazy processing | 20 | deferred `yield return` and a single `ToArray()` |
| Native call | 21–22, 32 | real P/Invoke result, missing library, rejected input |
| Files | 23–28 | missing file, malformed JSON, invalid data, save/reload, disposal, CSV export |

## 5. Using the application

| Menu | Workflow |
|---|---|
| 1 | **Book** an order onto a drone with a Standard or Express price; the drone becomes Reserved. |
| 2 | **Batch book** all pending orders, including cancelled ones (lazy iterator, materialised once). |
| 3 | **Complete** a planned order: battery is reduced once and the drone is released. |
| 4 | **Recharge** an idle drone, capped at 100; a reserved drone is rejected. |
| 5 | **Cancel** a planned order and release its drone without using energy. |
| 6 | **Reports**: fleet availability (optionally filtered by zone) and zone performance. |
| 7 | **Compare** Standard and Express prices for one order. |
| 8 | **Save** the fleet to `output/fleet-updated.json` and export `output/zone-report.csv` inside the data folder (the full path is printed). |
| 9 | Scripted demo on a fresh copy of the sample data. |

Demo output with the sample data:

```text
=== LIVE DEFENSE DEMO (sample O01 / D01) ===
PRICING: standard 1500 vs express 2300
BOOKED: order=O01, drone=D01, base=1500, native=30, total=1530
EXPECTED/CONTROLLED FAILURE: Cannot book the same order again while state is Planned
EXPECTED/CONTROLLED FAILURE: Cannot recharge while state is Reserved
COMPLETED: energy deducted once
EXPECTED/CONTROLLED FAILURE: Cannot complete while state is Completed
RECHARGED: idle drone
LAZY: batch iterator created, orders processed so far: 0
LAZY: after ToArray(), orders processed: 5
BATCH: 4 accepted / 1 rejected
  O02: Booked
  O03: Booked
  O04: Booked
  O05: Booked
  O06: No eligible available drone
CANCELLED: reserved drone released
--- Fleet availability (charge descending, drone ID tie-breaker) ---
D05  West    100.0 Reserved  risk=35.0
D01  North    95.0 Reserved  risk=20.0
D02  North    95.0 Available risk=17.0
D03  South    65.0 Available risk=11.0
D04  South    45.0 Reserved  risk=14.0
--- Zone utilisation, aggregation and completed revenue ---
North  count=2 sum-charge=190 avg=95.0 booked=1 revenue=1530
South  count=2 sum-charge=110 avg=55.0 booked=1 revenue=0
West   count=1 sum-charge=100 avg=100.0 booked=1 revenue=0
```

## 6. Domain rules

- `Drone`: non-empty ID and zone; charge in `[0,100]`; payload capacity in `(0,100]` kg.
- `DeliveryRequest`: non-empty ID and zone; distance in `(0,200]` km; weight in `(0,100]` kg.
- Booking needs an Available drone in the order's zone, enough payload capacity, and enough battery for
  `distanceKm × 2` while keeping a reserve of 10 units.
- Standard price: `500 + 100 × distanceKm`. Express price: `800 + 150 × distanceKm`.
- The native C function `calculate_surcharge` adds an operational surcharge of `3 × distanceKm` (whole kilometres).
- Complete and Cancel need a Planned order; Recharge needs an Available drone.
- An order that is Planned or Completed cannot be booked again. A Cancelled order can: the new plan replaces the cancelled one.
- A failed operation returns a `PlannerError` and the previous snapshot stays valid and unchanged.

## 7. Architecture

```text
Console input  +  local JSON files
             │
             ▼
DronePlanner.App   (side effects)
  Program.cs        menu, demo, printing
  JsonStore.cs      JSON load/save, CSV export, I/O exceptions → Result
  NativeAdapter.cs  P/Invoke to native/drone_native.c, loader errors → Result
             │
             ▼
DronePlanner.Core  (pure logic, no Console / File / native calls)
  Domain.cs      validated factories, immutable records, FleetSnapshot, lookups returning Option
  Functional.cs  Result, Option, PlannerError, Map, Bind, ToResult, Describe (pattern matching)
  Pipeline.cs    ValidateStage → EnergyStage → PricingStage, pricing delegates, the workflows, lazy BatchPlan
  Reports.cs     two LINQ reports
```

Where each requirement is implemented:

| Requirement | Location |
|---|---|
| Immutable models, validated factories | `Domain.cs`: `Drone.Create`, `DeliveryRequest.Create`, `FleetSnapshot.Create` |
| Pattern matching | `Pipeline.cs`: `DeliveryWorkflows.Book` (decision on the existing plan), `PricingPolicies.Select`; `Functional.cs`: `Map`, `Bind`, `ToResult`, `Describe` |
| Three-stage pipeline | `Pipeline.cs`: `DeliveryPipeline.Run` |
| Higher-order function changing a business rule | `Pipeline.cs`: `PricingStage(…, Func<EnergyContext,decimal>)`, `PricingPolicies` |
| Safe operation sequence | `Pipeline.cs`: state types `ValidatedContext` and `EnergyContext` with internal constructors (a stage cannot be skipped); `Book`, `Complete`, `Recharge`, `Cancel` return `InvalidSequence`; private constructors in `Domain.cs` |
| Result with Map and Bind | `Pipeline.cs`: `DeliveryWorkflows.Book`, `Cancel`, `Compare` |
| Missing lookups | `Domain.cs`: `FindDrone`, `FindPlan`, `FindOrder`, `FindBestDrone` return `Option<T>` |
| Boundary exceptions | `JsonStore.cs`, `NativeAdapter.cs` |
| Extension methods | `FindDrone`, `FindPlan`, `FindOrder`, `FindBestDrone`, `PendingOrders`, `InZone`, `Map`, `Bind`, `ToResult`, `Describe` |
| LINQ reports | `Reports.cs`: `Availability` (filter, projection, ordering with tie-breaker), `ZonePerformance` (GroupBy, Count, Sum, Average) |
| `yield return`, deferred execution, materialisation | `Pipeline.cs`: `BatchPlan`; `Domain.cs`: `PendingOrders`; `Program.cs`: `Demo` prints the counter before and after `.ToArray()` |
| Persistence | `JsonStore.cs`: `LoadFleet`, `LoadOrders`, `SaveFleet`, `ExportCsv`; samples in `data/` |

## 8. Project tree

```text
DroneDeliveryFinal.slnx
README.md
data/                  fleet.json, orders.json (sample data)
src/DronePlanner.Core/ pure domain logic
src/DronePlanner.App/  console, files, native adapter
tests/DronePlanner.Tests/
native/                drone_native.c, drone_native.h, build scripts
runtime/               prebuilt native libraries (win-x64, linux-x64)
docs/                  group report and three individual reports (PDF)
legacy/                code from Assignments 5–8, kept for reference, not compiled
.github/workflows/     CI: build, tests, demo
```

## 9. Limitations

- A teaching simulation: no weather, real routes, concurrency, login or hardware control.
- The native library is platform-specific; macOS needs a local build.
- The saved `fleet-updated.json` is not loaded automatically on the next start; pass its folder with `--data-dir`
  after renaming it to `fleet.json`.
- The scripted demo expects the original sample data, where order O01 is not booked yet.


