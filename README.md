# SQLtoParquet

A Windows desktop app for exporting SQL Server tables to local Parquet files, with a built-in DuckDB
tab for querying the results — no separate tooling required.

## Features

- **Connect dialog** — Windows Integrated or SQL Login authentication, server + database picker,
  remembers your recent connections (never your password).
- **Table browser** — lists every user table in the chosen database with row count, data size, index
  size, and total size (the same numbers `sp_spaceused` reports), with a select-all checkbox.
- **Parallel export** — exports the checked tables to local `.snappy.parquet` files with configurable
  parallelism (default 2 concurrent tables) and row-group size, live per-table and overall progress
  bars, and cancellation.
- **Size comparison** — once a table's export finishes, its Parquet file size shows right next to its
  original SQL Server size.
- **Analyze (DuckDB) tab** — run ad-hoc SQL against the exported Parquet files via an embedded DuckDB
  instance (`read_parquet('folder/*.parquet')`), with results in a grid.
- **Dark mode** — fixed dark theme, no light-mode toggle.

Azure Fabric OneLake / ADLS Gen2 upload (for Power BI) is planned but not implemented yet — see
`CLAUDE.md` for the extension point.

## Download

Grab the latest self-contained `SQLtoParquet.exe` from the [Releases](../../releases) page — no .NET
runtime install required. Windows 10/11 x64 only.

## Quick start

1. Launch `SQLtoParquet.exe`.
2. Click **Connect…**, enter your server name, pick Windows or SQL authentication, click **Connect**,
   then pick a database from the dropdown.
3. On the **Export** tab, check the tables you want, set an output folder, parallelism, and row-group
   size if you want to change the defaults, then click **Start Export**.
4. Switch to the **Analyze (DuckDB)** tab to run SQL against the exported files.

## Building from source

Requires the .NET 8 SDK (or newer — `global.json` rolls forward; see `CLAUDE.md` for why).

```
dotnet build SQLtoParquet.sln
dotnet run --project src\SQLtoParquet.App
```

## Running tests

```
dotnet test tests\SQLtoParquet.Core.Tests\SQLtoParquet.Core.Tests.csproj
```

## Publishing

```
dotnet publish src\SQLtoParquet.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Produces a single self-contained `.exe` under `src\SQLtoParquet.App\bin\Release\net8.0-windows\win-x64\publish\`.
Pushing a `vX.Y.Z` tag also builds and attaches this automatically to a GitHub Release — see
`.github/workflows/release.yml`.

## Architecture

Two-project split: `SQLtoParquet.Core` (SQL Server access, the export engine, DuckDB querying — no UI
dependency, unit-tested) and `SQLtoParquet.App` (Avalonia UI, MVVM via CommunityToolkit.Mvvm). Full
details, including the SQL→Parquet type-mapping table, are in `CLAUDE.md`.

## Known limitations

- Windows-only.
- SQL `decimal`/`numeric` columns with more significant digits than .NET's `decimal` supports (~28-29)
  will throw during export — genuinely very high-precision columns aren't common but aren't handled.
- No app icon yet.

## License

MIT — see [LICENSE](LICENSE).
