Assignment 5

* **Topic:** Drone Energy Management and Delivery Cost Calculation.
* **Intended Users:** Logistics operators and drone fleet managers.
* **Planned Team:** Student project (Assignment 5 module).
* **Input Fields:** `UnitId` (string), `InitialCharge` (decimal), `DistanceKm` (decimal).
* **Business Rules:**
    1. Records with non-positive initial charge or negative distances are rejected.
    2. Energy consumption is calculated as 2 units of charge per kilometer.
    3. Cost is determined dynamically via configurable pricing policies (Standard vs Express).
* **Expected Result:** A sequence of processed records containing final battery charges and computed delivery costs under the selected policy.

1. **`FilterValidRecords`**: Pure function acting as Stage 1, filtering out records with invalid charges or negative distances.
2. **`ComputeConsumption`**: Pure function acting as Stage 2, calculating energy depletion based on distance.
3. **`ApplyPricingPolicy`**: Higher-order pure function acting as Stage 3, accepting a configurable pricing policy delegate (`Func<decimal, decimal>`) to compute final costs.

* **StandardPolicy**: `500 + 100 * distance`
* **ExpressPolicy**: `800 + 150 * distance`

* **Student Name / ID:** Implemented the functional core, pipeline stages, pricing policies, immutability checks, and all 6 automated unit tests.


Assignment 6

* **Topic:** Drone Energy Management and Delivery Cost Calculation.
* **Intended Users:** Logistics operators and drone fleet managers.
* **Workflow:** The application validates raw drone energy records via a secure factory, transforms them through explicit states using pattern matching, and calculates final battery charges and delivery costs based on configurable pricing policies.

* **The Unsafe Design (Before):** Previously, correctness depended on remembering a hidden order of method calls (e.g., calling `Validate()` manually before `Calculate()`). If a developer accidentally omitted validation or passed raw, unchecked data directly into the processing pipeline, the application could fail at runtime or produce incorrect financial calculations.
* **The Refactored Design (After):** We eliminated temporal coupling by introducing **validated factories** and **explicit states** (`BatteryState`). External callers cannot bypass validation because constructors are restricted, and pipelines accept strictly typed states (`BatteryState.Validated`), making prerequisites visible at the type level.

The workflow uses a closed record hierarchy (discriminated union style) representing mutually exclusive states:
* **`Pending`**: Represents raw, unvalidated input data.
* **`Validated`**: Represents data successfully checked by the factory, making it safe to process.
* **`Processed`**: Represents successfully computed results (final charge and cost). Repeated operations safely return the existing outcome.
* **`Failed`**: Captures validation errors or prerequisite violations with descriptive reasons, keeping the original state consistent.

* **Factory Validation (`BatteryFactory.cs`):** Enforces rules ensuring `UnitId` is not empty, `InitialCharge > 0`, and `DistanceKm >= 0`.
* **Pattern Matching (`EnergyPipeline.cs`):** Uses C# `switch` expressions to handle every distinct state safely without null-forgiving hacks or unsafe casts.
* **Automated Tests:** 8 cumulative unit tests covering factory success/rejection, distinct cases, normal operations, missing prerequisites, repeated operations, boundary values, and state preservation.

* **Student Name / Group:** Implemented the validated factory model, explicit state hierarchy, pattern-matching transitions, refactored workflow, and all 8 automated unit tests.Assignment 5

* **Topic:** Drone Energy Management and Delivery Cost Calculation.
* **Intended Users:** Logistics operators and drone fleet managers.
* **Planned Team:** Student project (Assignment 5 module).
* **Input Fields:** `UnitId` (string), `InitialCharge` (decimal), `DistanceKm` (decimal).
* **Business Rules:**
    1. Records with non-positive initial charge or negative distances are rejected.
    2. Energy consumption is calculated as 2 units of charge per kilometer.
    3. Cost is determined dynamically via configurable pricing policies (Standard vs Express).
* **Expected Result:** A sequence of processed records containing final battery charges and computed delivery costs under the selected policy.

1. **`FilterValidRecords`**: Pure function acting as Stage 1, filtering out records with invalid charges or negative distances.
2. **`ComputeConsumption`**: Pure function acting as Stage 2, calculating energy depletion based on distance.
3. **`ApplyPricingPolicy`**: Higher-order pure function acting as Stage 3, accepting a configurable pricing policy delegate (`Func<decimal, decimal>`) to compute final costs.

* **StandardPolicy**: `500 + 100 * distance`
* **ExpressPolicy**: `800 + 150 * distance`

* **Student Name / ID:** Implemented the functional core, pipeline stages, pricing policies, immutability checks, and all 6 automated unit tests.


Assignment 6

* **Topic:** Drone Energy Management and Delivery Cost Calculation.
* **Intended Users:** Logistics operators and drone fleet managers.
* **Workflow:** The application validates raw drone energy records via a secure factory, transforms them through explicit states using pattern matching, and calculates final battery charges and delivery costs based on configurable pricing policies.

* **The Unsafe Design (Before):** Previously, correctness depended on remembering a hidden order of method calls (e.g., calling `Validate()` manually before `Calculate()`). If a developer accidentally omitted validation or passed raw, unchecked data directly into the processing pipeline, the application could fail at runtime or produce incorrect financial calculations.
* **The Refactored Design (After):** We eliminated temporal coupling by introducing **validated factories** and **explicit states** (`BatteryState`). External callers cannot bypass validation because constructors are restricted, and pipelines accept strictly typed states (`BatteryState.Validated`), making prerequisites visible at the type level.

The workflow uses a closed record hierarchy (discriminated union style) representing mutually exclusive states:
* **`Pending`**: Represents raw, unvalidated input data.
* **`Validated`**: Represents data successfully checked by the factory, making it safe to process.
* **`Processed`**: Represents successfully computed results (final charge and cost). Repeated operations safely return the existing outcome.
* **`Failed`**: Captures validation errors or prerequisite violations with descriptive reasons, keeping the original state consistent.

* **Factory Validation (`BatteryFactory.cs`):** Enforces rules ensuring `UnitId` is not empty, `InitialCharge > 0`, and `DistanceKm >= 0`.
* **Pattern Matching (`EnergyPipeline.cs`):** Uses C# `switch` expressions to handle every distinct state safely without null-forgiving hacks or unsafe casts.
* **Automated Tests:** 8 cumulative unit tests covering factory success/rejection, distinct cases, normal operations, missing prerequisites, repeated operations, boundary values, and state preservation.

* **Student Name / Group:** Implemented the validated factory model, explicit state hierarchy, pattern-matching transitions, refactored workflow, and all 8 automated unit tests.

Assignment 8

* **Topic:** Drone Energy Management and Advanced Functional Operations.
* **Intended Users:** Logistics operators and drone fleet managers.
* **Workflow:** The application extends the drone energy processing pipeline by incorporating custom extension methods, complex LINQ reporting with deterministic tie-breaking, curried scoring functions, lazy evaluation streams, safe resource management using `using` blocks, and native C-library integration via P/Invoke.

* **The Unsafe Design (Before):** Previously, data processing and resource handling relied on standard imperative boilerplate, direct file reads without guaranteed closures, and rigid hardcoded calculations without functional composition or lazy loading controls.
* **The Refactored Design (After):** We introduced advanced functional features, modular architecture across dedicated files, custom extension methods, curried partial applications, lazy streams (`yield return`), safe resource disposal with `using`, and interop boundaries with a native C calculation library.

The architecture incorporates specialized functional components:
* **`Extensions.cs`:** Implements custom extension methods like `FilterHighConsumers` and `MapResult` for clean functional composition.
* **`Reports.cs`:** Implements advanced LINQ reports (`GenerateFleetStatusReport`, `GenerateEfficiencySummaryReport`) utilizing filtering, grouping, aggregation, and deterministic sorting with `ThenBy`.
* **`CurriedScoring.cs`:** Implements curried scoring functions and partially applied configurations (`StandardScorer`, `AggressiveScorer`).
* **`DeferredWorkflow.cs`:** Implements lazy evaluation streams using `yield return` (`StreamTelemetryWithProbe`).
* **`ResourceStreamHandler.cs`:** Manages secure file streaming and resource lifecycles using `using` blocks.
* **`NativeInterop.cs`:** Integrates native C calculation libraries via P/Invoke (`DllImport`) with strict error and boundary handling.

* **LINQ Reports & Extensions:** Full utilization of `Where`, `Select`, `OrderBy`, `ThenBy`, `GroupBy`, `Sum`, and `Count` operators with robust empty-dataset handling.
* **Native Interop & Resource Safety:** Guaranteed stream disposal via `using` blocks and robust native error code translation via `Result<T, E>`.
* **Automated Tests:** 12+ comprehensive cumulative unit tests covering custom extensions, LINQ reports, deterministic tie-breaks, currying variations, lazy execution timing, resource disposal, and native interop success and failure paths.

* **Student Name / Group:** Implemented the extension modules, advanced LINQ reporting engines, currying workflows, lazy evaluation streams, safe resource handlers, native C interop integration, and all 12 cumulative unit tests.