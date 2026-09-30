# CLAUDE.md

Guidance for Claude Code (or any future contributor) working in this repo.

## What this repo is

SQLtoParquet is a Windows-only, dark-mode Avalonia (.NET 8) desktop app that connects to a SQL
Server instance, lets the user pick a database and a set of tables, and exports the selected tables
to local Parquet files — with progress reporting, limited parallelism, and a side-by-side comparison
of SQL Server table size (data + index) vs. the resulting Parquet file size. A second tab embeds
DuckDB so the exported Parquet files can be queried locally with SQL, without leaving the app.

Azure Fabric OneLake / ADLS Gen2 upload (for Power BI) is a stated future goal, not implemented yet —
see the `IExportDestination` extension point below.

The export logic (SQL type → Parquet mapping, row-group batching, progress cadence) is ported from a
reference PowerShell exporter at `C:\Projecten\Parquet` (Microsoft's public
"AzureSynapseScriptsAndAccelerators" scripts by Andrey Mirskiy), adapted to Parquet.Net's current
(v6) schema API rather than the older ParquetSharp-based API those scripts used.

This app follows the same conventions as its sibling project, `SQLModelViewer` (same author) — Core/App
project split, plain constructor-injected ViewModels with no DI container, CommunityToolkit.Mvvm,
a `ConnectionDialog`/`ConnectionViewModel` pattern for SQL Server auth, and an `AboutDialog`. When in
doubt, match that project's style.

## Solution layout

```
SQLtoParquet.sln
src/
  SQLtoParquet.Core/     # net8.0 class library — no Avalonia/UI reference, fully unit-testable
  SQLtoParquet.App/      # net8.0-windows Avalonia app (Views/ViewModels/Services)
tests/
  SQLtoParquet.Core.Tests/  # xUnit, targets Core only
```

`Directory.Build.props` at the repo root sets `net8.0`, nullable-enabled, and
`WarningsAsErrors=nullable` for every project (the App project overrides `TargetFramework` to
`net8.0-windows`). `global.json` pins the SDK to 8.0.206 with `rollForward: latestMajor` — this
matters: Avalonia 12.1.x's XAML source generator needs a newer Roslyn than SDK 8.0.206 ships, so
`latestMajor` lets a newer installed SDK (e.g. .NET 10's SDK) build the project while the app itself
still *targets* `net8.0-windows`. If XAML-backed windows mysteriously lose their generated
`InitializeComponent()` (a `CS0103` error) with no other diagnostics, this SDK/Roslyn mismatch is the
first thing to check — `dotnet --version` should report something newer than 8.0.206.

## Key classes and where logic lives

**`SQLtoParquet.Core/SqlServer/`** — `SqlServerTableCatalogService` lists user tables plus row
count / data-space / index-space via the same `sys.allocation_units` query `sp_spaceused` is built on
(index_id 0/1 = heap/clustered = the row data; index_id > 1 = nonclustered indexes).

**`SQLtoParquet.Core/Export/`** — the export engine:
- `SqlToParquetTypeMapper` — maps a column's SQL type name (from `SqlDataReader.GetColumnSchemaAsync()`)
  to a Parquet.Net `DataField`/`DecimalDataField`/`DateTimeDataField`/`TimeDataField` plus a
  `ParquetValueKind` that says how to read/buffer it. See the type table below. All fields are
  declared nullable in the Parquet schema regardless of the source column's actual nullability —
  simpler than tracking it, and harmless since a nullable schema still holds non-null data fine.
- `ColumnBuffers.cs` (internal) — buffers one column's values in memory between row-group flushes.
  Parquet.Net v6 has no generic `DataColumn` type any more; writing goes through typed
  `ParquetRowGroupWriter.WriteAsync` overloads (a nullable-value-type generic overload, plus
  dedicated non-generic overloads for `string` and `byte[]`), so there's one buffer implementation
  per CLR shape rather than a single generic one. **Note:** Parquet.Net normalizes a
  `string`/`byte[]`-typed `DataField.ClrType` to `ReadOnlyMemory<char>`/`ReadOnlyMemory<byte>`
  internally — this looks alarming in a debugger but is expected; the dedicated
  `IReadOnlyCollection<string>`/`IReadOnlyCollection<byte[]>` write/read overloads exist specifically
  to work with fields in that normalized state. `ParquetWritePipelineTests` round-trips this for real.
- `SqlValueReader` (internal) — reads one column's value out of a live `SqlDataReader` row per its
  `ParquetValueKind`.
- `TableExporter` — streams one table: opens a reader, builds the schema, buffers rows per-column,
  flushes a Parquet row group every `TableExportOptions.RowsPerRowGroup` rows (plus a final partial
  group) so the whole table is never held in memory at once. Catches per-table exceptions internally
  (except `OperationCanceledException`, which propagates) and returns a failed `ExportResult` instead
  of throwing, so a bad table doesn't take down a batch export.
- `ExportOrchestrator` — runs several `TableExportJob`s with `SemaphoreSlim`-gated concurrency
  (`TableExportOptions.MaxParallelism`, UI default 2). Each concurrent export opens its own
  `SqlConnection` — one connection can't serve multiple simultaneous readers.
- `Destinations/IExportDestination` — thin stream-in/size-out abstraction; `LocalFileExportDestination`
  is the only implementation today. A future `OneLakeExportDestination` (Fabric/ADLS Gen2) would open
  an Azure SDK write stream inside `WriteAsync` instead of a local `FileStream` — `TableExporter` and
  `ExportOrchestrator` would need no changes.

**`SQLtoParquet.Core/Analysis/`** — `DuckDbQueryService` runs ad-hoc SQL against an in-memory DuckDB
instance (`DuckDB.NET.Data.Full`, which bundles native binaries so single-file publish "just works").
`ParquetFileCatalog` lists `*.parquet` files in a folder for the Analyze tab.

**`SQLtoParquet.App/Services/`** — `AppPaths`, `ConnectionProfile`/`AuthMode`, `ConnectionProfileStore`
(recent connections at `%AppData%\SQLtoParquet\connections.json` — **never** a password field, by
design), `SqlConnectionStringFactory`. Ported near-verbatim from `SQLModelViewer.App.Services` — keep
them in sync if one gets a bugfix the other needs.

**`SQLtoParquet.App/ViewModels/`** — `ConnectionViewModel` (Connect dialog: server/auth/database
combobox/recent connections), `TableListViewModel` (Export tab: table grid, select-all, output
folder, parallelism/row-group-size, start/cancel, overall progress), `TableExportRowViewModel` (one
grid row + its live progress), `AnalysisViewModel` (Analyze/DuckDB tab), `MainWindowViewModel` (wires
them together — picking a database in the Connect dialog triggers `TableListViewModel.LoadTablesAsync`
via a `PropertyChanged` subscription, same pattern `SQLModelViewer.MainWindowViewModel` uses).

### SQL → Parquet type mapping

| SQL type | Parquet.Net field | Read via |
|---|---|---|
| `int` | `DataField<int>` | `GetInt32` |
| `tinyint` | `DataField<byte>` | `GetByte` |
| `smallint` | `DataField<short>` | `GetInt16` |
| `bigint` | `DataField<long>` | `GetInt64` |
| `bit` | `DataField<bool>` | `GetBoolean` |
| `char`/`nchar`/`varchar`/`nvarchar`/`text`/`ntext`/`xml` | `DataField<string>` | `GetString` |
| `uniqueidentifier` | `DataField<string>` | `GetGuid(i).ToString()` |
| `date` | `DateTimeDataField` (Date) | `GetDateTime` |
| `time` | `TimeDataField` (Micros, `long`) | `GetTimeSpan(i).Ticks / 10` |
| `datetime`/`datetime2`/`smalldatetime` | `DateTimeDataField` (DateAndTime) | `GetDateTime` |
| `datetimeoffset` | `DataField<string>` | formatted `"yyyy-MM-dd HH:mm:ss.fffffff zzz"` invariant |
| `money` | `DecimalDataField(19,4)` | `GetDecimal` |
| `smallmoney` | `DecimalDataField(10,4)` | `GetDecimal` |
| `float` | `DataField<double>` | `GetDouble` |
| `real` | `DataField<float>` | `GetFloat` |
| `decimal`/`numeric` | `DecimalDataField(precision, scale)` from the reader's schema | `GetDecimal` |
| `binary`/`varbinary`/`image` | `DataField<byte[]>` | `GetValue` cast to `byte[]` |
| anything else | — | throws `SqlToParquetUnsupportedTypeException` naming column + type |

The `text`/`ntext`/`xml` inclusion (as `string`) is a deliberate small superset beyond the original
PowerShell script's table, added so real-world tables with legacy text columns don't immediately fail.

## Data flow

Connect dialog (`ConnectionViewModel.ConnectAsync`) → `sys.databases` query populates the database
combobox → picking one closes the dialog → `MainWindowViewModel` reacts to `SelectedDatabase`
changing → `TableListViewModel.LoadTablesAsync` opens a connection and runs
`SqlServerTableCatalogService.ListTablesAsync` → grid populates with size info → user checks tables,
sets output folder/parallelism/row-group size, clicks Start Export → `TableListViewModel.StartExportAsync`
builds `TableExportJob`s (one `Progress<TableExportProgress>` per row) and calls
`ExportOrchestrator.RunAsync` → each job's `TableExporter.ExportAsync` streams rows into a
`LocalFileExportDestination` file → per-row and overall progress bars update live → `ExportResult`s
populate final status/Parquet file size per row. Independently, the Analyze tab's
`AnalysisViewModel.RunQueryAsync` points `DuckDbQueryService` at the output folder's `.parquet` files.

## Build / run / test

```
dotnet build SQLtoParquet.sln
dotnet run --project src\SQLtoParquet.App
dotnet test tests\SQLtoParquet.Core.Tests\SQLtoParquet.Core.Tests.csproj
```

Publish (self-contained, single-file, win-x64 — see README for why):

```
dotnet publish src\SQLtoParquet.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

## Conventions

- MVVM via CommunityToolkit.Mvvm (`[ObservableProperty]`/`[RelayCommand]`); no DI container —
  ViewModels take dependencies via constructor with a parameterless convenience constructor for
  design-time/default wiring (see `ConnectionViewModel`, `TableListViewModel`).
- `SQLtoParquet.Core` has zero Avalonia/UI references — keep it that way so the export engine stays
  independently testable.
- Async all the way down with `CancellationToken` threaded through I/O; `TableExporter` swallows
  per-table exceptions (except cancellation) into a failed `ExportResult` rather than throwing.
- Settings/connection profiles never contain a password field, on disk or in memory beyond the
  Connect dialog's own fields.
- No light theme — `App.axaml` fixes `RequestedThemeVariant="Dark"`.

## CI / release

`.github/workflows/release.yml` builds and publishes the self-contained win-x64 single-file exe and
attaches it to a GitHub Release whenever a `v*` tag is pushed. To cut a release: bump whatever version
marker you want reflected in the About dialog, `git tag vX.Y.Z`, `git push --tags`.

## Status

- [x] Core export engine (type mapping, row-group batching, orchestration) — unit + round-trip tested.
- [x] Connect dialog, table grid with sizes, export with progress/parallelism/cancellation.
- [x] Analyze (DuckDB) tab.
- [x] About dialog.
- [ ] App icon (`Assets/app.ico`) — not yet added; `AboutDialog`/`MainWindow` don't reference one.
- [ ] Azure Fabric OneLake / ADLS Gen2 export destination.

## Explicit non-goals (for now)

- Cross-platform support — this is a Windows-only tool by design.
- Writing back to SQL Server or to the exported Parquet files from the Analyze tab (read-only).
- A light theme toggle.
