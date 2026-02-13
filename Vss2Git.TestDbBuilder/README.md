# Vss2Git.TestDbBuilder

Creates VSS 6.0 test databases using `ss.exe`/`mkss.exe` and verifies them with `VssLogicalLib`. Each scenario builds a database with specific operations (add, edit, rename, share, branch, pin, delete, etc.) and validates the result by reading back the VSS database.

## Prerequisites

- **Visual SourceSafe 6.0** installed at `C:\Program Files (x86)\Microsoft Visual SourceSafe`
  (or pass a custom path as the first argument)

## Usage

```bash
# Run all scenarios (builds, verifies, cleans up on success)
dotnet run --project Vss2Git.TestDbBuilder

# Custom VSS install path
dotnet run --project Vss2Git.TestDbBuilder -- "D:\VSS"

# Clean leftover test data from failed runs
dotnet run --project Vss2Git.TestDbBuilder -- clean
```

## Scenarios

| # | Name | Tests | Target Bug |
|---|------|-------|------------|
| 01 | Basic | Add, edit, delete, label | Core pipeline |
| 02 | RenamesAndMoves | File/project rename, case-only rename, move | H1 |
| 03 | SharingAndBranching | Share, edit shared, branch, independent edit | H4 |
| 04 | PinsAndLabels | Pin at version, unpin, multiple labels | H5 |
| 05 | DeleteAndRecover | Delete/recover file, destroy project/file | H2 |

## Output

Test databases are created under `Vss2Git.IntegrationTests/TestData/<scenario>/` and cleaned up automatically after successful verification. Failed scenarios leave their database on disk for debugging.
