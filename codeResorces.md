# Refactoring Summary: OrderProcessor

### 1. Fixed SQL Injection (Security Vulnerability)

- **What:** Replaced string-concatenated SQL syntax with parameterized queries using `command.Parameters.AddWithValue`.
- **Why:** The original code appended raw variables directly into raw SQL strings, which creates a critical SQL Injection risk and risks application data leakage.
- **How:** Parameterization abstracts the input parameters, ensuring that the database framework treats input exclusively as literal data values rather than executable code scripts.

### 2. Elimination of Magic Error Codes

- **What:** Removed all instances of `return -1;` and replaced them with custom-typed exceptions (`CustomerException`, `ProductUnavailableException`).
- **Why:** Returning `-1` is an ambiguous code smell. It forces developers to guess why a method failed and fails to convey the underlying system contexts.
- **How:** Custom typed exceptions convey clear semantic reasons for failures, making code handling expressive, informative, and standardized across business pipelines.

### 3. Structural Decomposition (Single Responsibility Principle)

- **What:** Segmented the massive `proc()` script into 5 dedicated helper methods (`ValidateAndGetCustomerEmail`, `ProcessProductSelection`, etc.), each limited to under 15 lines.
- **Why:** The original monolithic architecture held multiple responsibilities: validating users, managing inventories, calculating balances, running DB operations, and managing emails.
- **How:** Splitting workflows makes operations highly maintainable, testable, and scannable by encapsulating specialized domain actions inside isolated methods.

### 4. Memory Resource Management (Leaky Database Connections)

- **What:** Injected C# `using` blocks around disposable system instances (`SqlConnection`, `SqlCommand`, `SqlDataReader`, and `MailMessage`).
- **Why:** The legacy code manually called `.Close()`. If any runtime failure occurred prior to termination lines, database handles remained open, causing unmanaged resource leaks.
- **How:** The `using` syntax implicitly calls `.Dispose()` upon scope completion, ensuring robust connection closures even during severe unexpected runtime panics.

### 5. Naming Convention and Typo Fixes

- **What:** Rebuilt class variables and methods to use explicit domain nomenclature and proper casing architectures (`op` became `OrderProcessor`, `proc` became `ProcessOrder`).
- **Why:** Single-letter parameters (`cid`, `pids`, `sc`) reduce structural readability, forcing engineers to rely on external context to understand core operational schemas.
- **How:** Using expressive casing (`customerId`, `productIds`) provides immediate domain clarity, rendering code completely self-documenting.

### 6. Eradicating Swallowed Exception Blocks

- **What:** Exchanged the unhandled, silent, and blank `catch { }` block inside the mailing utility for standardized processing flow.
- **Why:** Swallowing exception outputs isolates failures, entirely masking mail server offline faults and locking errors from engineering dashboards.
- **How:** System alerts are now naturally allowed to bubble up up-stream, providing diagnostic tracking capabilities to system monitors.
