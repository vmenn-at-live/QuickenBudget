# QuickenBudget

A small, local web application for budgeting and analyzing financial transactions exported from Quicken. It ingests a Quicken CSV (or a well-formed CSV) and
provides yearly summaries, grouped expense breakdowns and simple visualizations.

This project is not affiliated with or endorsed by Quicken. It is an independent tool created to help users analyze their financial data exported from Quicken.

---

## Highlights

- ASP.NET Core Razor Pages application targeting .NET 10 (tested with Visual Studio 2026).
- Import transactions from Quicken CSV/tab-delimited exports.
- View yearly income/expense summaries and grouped expenses.
- Configure grouping and filtering rules with regular expressions.
- Logging with Serilog
- Simple D3-based visualizations in the UI.
- Dockerfile included for containerized runs.

---

## Prerequisites

- .NET 10 SDK
- (Optional) Docker for container runs
- Recommended: Visual Studio 2026 for development

---

## Quickstart

1. Clone the repository:
   ```
   git clone https://github.com/vmenn-at-live/QuickenBudget.git
   cd QuickenBudget
   ```

2. Restore dependencies:
   ```
   dotnet restore
   ```

3. Run the application with the path to your Quicken CSV file as a command-line argument from the QuickenBudget project directory:

```
cd QuickenBudget
dotnet run -- <path-to-csv-file>
```

4. By default, the application will start a web server on `http://0.0.0.0:8080`. The port can be changed using command-line arguments.
The `-p` or `--port` option can be used to specify a different port (example assumes the input transaction file is specified via environment or in a configuration file):

```
dotnet run -- -p 5000
```

### Command-line options

| Option | Alias | Description |
|---|---|---|
| `<path>` | | Positional. Path to the Quicken CSV report file. Overrides `TransactionReader:QuickenReportFile` from all other config sources. |
| `--port <port>` | `-p` | Port to listen on. Defaults to `8080`. |
| `--config <file>` | `-c` | Path to an additional JSON config file. May be specified multiple times. |
| `--logDirectory <path>` | `-ld` | Directory for log files. If the Serilog File sink is already configured in `appsettings.json`, only the directory portion of the path is replaced (original file name is preserved). If no File sink is configured, one is added automatically with file name `log-.txt` and daily rolling defaults. |

Example — run on port 5000, load an extra config file, and write logs to a custom directory:

```
dotnet run -- report.csv -p 5000 -c selectors.json --logDirectory /var/log/quickenbudget
```

5. Open a browser and visit `http://localhost:<port>`

- Click on a year to see detailed transactions for that year.
- Use the group transactions page to view transactions grouped by categories.

---

## Visual Studio

Open the `QuickenBudget.slnx` solution file with Visual Studio 2026. The project targets .NET 10 and uses C# 14.

### Launch settings

`launchSettings.json` is gitignored because it typically contains machine-specific paths. A template is provided at
`QuickenBudget/Properties/template_launchSettings.json` as a starting point.

To set up your local launch settings:

1. Copy `template_launchSettings.json` to `launchSettings.json` in the same `Properties/` directory:
   ```
   copy QuickenBudget\Properties\template_launchSettings.json QuickenBudget\Properties\launchSettings.json
   ```

2. Edit `launchSettings.json` and update any paths to match your environment. In particular, the `Container (Dockerfile)` profile's `containerRunArguments` needs a local path to a directory containing your transaction file and selectors config.

The template defines two launch profiles:

| Profile | Description |
|---|---|
| `http` | Runs locally with `dotnet run`. Passes `config\AllTransactions.txt` and `config\PrivateSelectors.json` from the project's `config/` directory, and writes logs to a `logs/` subdirectory. |
| `Container (Dockerfile)` | Builds and runs the Docker image. Mounts a local config directory into `/config` inside the container, which must contain the transaction file and selectors config. |

---

## Configuration

Configuration is read from various sources in the following order:
1. `appsettings.json` in local directory
2. `appsettings.<EnvironmentName>.json`, where the `EnvironmentName` is defined by `ASPNETCORE_ENVIRONMENT` environment variable
3. Environment variables
4. Command line

The order is defined in ASP.NET documentation. In addition the program parses command line and loads any additional JSON config files specified by
the `-c`/`--config` option(s) and overrides settings from the previous sources with the settings from the command line config files. The files specified
by `-c`/`--config` are loaded in the order they appear on the command line, so the last one has the highest precedence.
Finally, command line parameters (the positional parameter for the CSV file path and the log files directory) have the highest precedence and will override any previous setting.

```
dotnet run -- <path-to-csv-file> -c <path-to-additional-config-file>
```

This load order is important as later configuration sources may overwrite settings specified by a previous source.

JSON configuration files added by the app use `reloadOnChange: true`, so updates to those files are picked up at runtime. The transaction pipeline now reloads automatically when `TransactionReader` or `TransactionSelectors` values change, and it also watches the configured Quicken report file for changes on disk.

### Important settings:

- `Port` — Port the web server listens on. Defaults to `8080` when absent from all configuration sources.
The resolution priority is: command-line (`--port`) > `appsettings.json` (and other configuration sources) > built-in default (`8080`).

- `TransactionReader` - Rules for reading the report file and the path to the report file. It has the following two properties:
	- `QuickenReportFile` — Path to the Quicken report transactions file. [See full description](./TRANSACTIONS_FILE.md).
	- `CSVSettings` - parameters used when reading report file
		- `LinesToSkip` - an integer indicating the number of lines to be skipped to the header line of the CSV file. The parse must start with a header line
		- `Delimiter` - the character that delimits headers and fields in the CSV file. The program uses a tab (\t) as default. Comma (,) is another common delimiter.
		- `TrimFields` - Boolean value (`true` or `false`) indicating if the fields should be trimmed of whitespace
		- `HandleQuotedFields` - Boolean value (`true` or `false`) indicating that some fields may be quoted using a double quote character according to standard CSV rules.
		- `FieldMappings` - a set of field mapping descriptors.
			- Example: `"Date": { "MapTo": "Date", "Required": true, "Skip": false }`. Entry name ("Date") is a field name in the original report file.

				The value is an object with the following properties: `MapTo` - renames the field, `Required` - Boolean indicating if the field is required, `Skip` - Boolean
that the field must be ignored.
- `TransactionSelectors` - Set of group selectors and filters to be applied to the incoming rows from the report file (transactions).
[See full description](./SELECTORS_AND_FILTERS.md).
	- `ExpensesGroupSelectors` - mappings of transactions into expenses groups.
	- `IncomeGroupSelectors` - mappings of transactions into income groups.
	- `TransactionFilters` - set of filters that help ignore some transactions.

Selector and filter arrays are processed in reverse declaration order, so the last named array in JSON has the highest precedence. Rules inside each individual array are then evaluated top-to-bottom.

When any of those JSON-backed settings change, the app keeps serving the last good in-memory snapshot until a reload succeeds. Likewise, edits to the configured Quicken report file trigger a refresh of the transaction data.

### Example JSON configuration file snippet
The below snippet comes from a Windows system as evident from the file path format. It specifies a comma-separated fields that are to be trimmed,
possibly quoted. The header line is the second line in the file (one line is skipped). No fields are renamed.

The first selector indicates that all transaction that have Walmart, Whole Foods, or Safeway in the memo field should be assigned to the Groceries group.
All transactions with category that contains Electric, Gas, or Water should be assigned to Utilities group. Category Dining should become group Dining. Finally,
all transactions where Category starts with word Vacation should be moved to group Vacation.

```json
{
  "TransactionReader": {
    "QuickenReportFile": "C:\\data\\transaction-report.csv",
    "CSVSettings": {
      "Delimiter": ",",
      "TrimFields": true,
      "HandleQuotedFields": true,
      "LinesToSkip": 1,
      "FieldMappings": {}
    }
  },
  "TransactionSelectors": {
    "ExpensesGroupSelectors": {
      "Generic": [
        { "Field": "Memo", "Filter": ".*(Walmart|Whole Foods|Safeway).*", "TargetGroup": "Groceries" },
        { "Field": "Category", "Filter": ".*(Electric|Gas|Water).*", "TargetGroup": "Utilities" },
        { "Field": "Category", "Filter": "^Dining$", "TargetGroup": "Dining" },
        { "Field": "Category", "Filter": "^Vacation.*", "TargetGroup": "Vacation" }
      ]
    }
  }
}
```

---

## Running tests

The repository contains unit tests under `QuickenBudget.Tests`.

Run all tests:

```
dotnet test
```

Run a single test project:

```
dotnet test QuickenBudget.Tests
```

Unit tests cover CSV parsing, transaction ingestion, grouping and page models.

---

## Docker

Build:

```
docker build -t quickenbudget -f QuickenBudget/Dockerfile .
```

Run (mount CSV file and set environment variable):

```
docker run -v /host/path/to/report.csv:/data/report.csv \
           -v /host/path/to/logs:/var/log/quickenbudget \
           -e TransactionReader__QuickenReportFile=/data/report.csv \
           -p 5000:8080 quickenbudget --port 8080 --logDirectory /var/log/quickenbudget
```

Then access the app at `http://localhost:5000`.

Note that you can add additional parameters at the end of the `docker run` command, such as additional config files using `--config` (or `-c`) switch(es).

---

## Observability & Logging

- Serilog is configured as the logging provider.
- Default configuration writes to the console and (when configured in `appsettings.json`) to a rolling file.
- Use the `--logDirectory` / `-ld` command-line option to redirect the file sink to a specific directory at runtime, overriding the path in `appsettings.json`. If no file sink is configured, one is added automatically with daily rolling defaults and the file name `log-.txt`.
- You can extend `appsettings.json` to add additional sinks (e.g., third-party sinks).

---

## Common troubleshooting

- App exits with message "Please provide the path to the CSV file..." — the app could not find `TransactionReader:QuickenReportFile`. Provide a CSV path via command line, environment variable or `appsettings.json`.
- Parsing warnings about header/field counts — check the skip-lines setting and delimiter. Consider using tab-delimited export from Quicken.
- Incorrect averages — note that averages are derived from transaction dates; if transactions are sporadic the code may compute averages based on months present. Review `TransactionData` grouping logic if you need a different averaging strategy (e.g., divide by 12).

---

## Project Structure
- `QuickenBudget/` — main Razor Pages app
  - `Pages/` — Razor Pages and page models
  - `Models/` — domain models (Transaction, GroupTotals, YearTotals, etc.)
  - `Tools/` — CSV parsing, transaction processing, and logging utilities (`LoggingHelpers`)
  - `Interfaces/` — service interfaces for DI
  - `Properties/template_launchSettings.json` — template for Visual Studio launch settings (`launchSettings.json` is gitignored)
  - `wwwroot/` — static assets (css, js)
  - `config/` — sample and private selector configuration files
- `QuickenBudget.Tests/` — unit tests

---


## Contributing

- Open an issue or submit a pull request.
- Add unit tests for functional changes.
- Keep changes small and focused.

---

## Security & Privacy

- This app processes local CSV files only. Do not upload sensitive files to public places.
- No data is transmitted externally by default. If you add telemetry or sinks, ensure you comply with your privacy requirements.

---

## License

This project is licensed under the MIT License — see the `LICENSE` file in the repository for details.
