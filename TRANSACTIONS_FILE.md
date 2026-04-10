# Transactions File
The transactions file path is expected to be the first positional parameter on the command line:
```
dotnet run -- <path-to-csv-file>
```

It can also be set via the configuration file or an environment variable (the latter being the common way to provide the path to the program running in a container).
Simply set `TransactionReader__QuickenReportFile` environment variable to the path of the file.

  |OS|Environment Variable Syntax|
  |:---:|---|
  | Linux|`TransactionReader__QuickenReportFile=/path/to/file.csv`|
  |PowerShell|`$env:TransactionReader__QuickenReportFile = "C:\path\to\file.csv"`|
  |cmd.exe|`set TransactionReader__QuickenReportFile=C:\path\to\file.csv`|

Alternatively, add the path to a JSON configuration file as `TransactionReader:QuickenReportFile`.

While the app is running, it watches the currently configured report file for changes. When the file is created, edited, renamed, or deleted, the transaction data layer attempts a reload and preserves the previous in-memory snapshot if the refreshed file cannot be ingested successfully.

---

# CSV format guidance

Quicken's export can be inconsistent as often it does not create well-formed CSV/tab-delimited files. In particular, it does not handle quoting correctly,
so fields that contain double quotes can break the CSV parsing.

Recommended steps:
- Prefer UTF-8 encoding for the exported file; date formats should be ISO-like or match the locale used by your system.
- Prefer the "tab delimited (Excel compatible) disk file" option from Quicken when possible — it tends to be better formed.
- If you must use CSV, avoid fields that contain double quotes as Quicken's CSV quoting is unreliable.
- The reader supports a configurable `CSVSettings` block to control delimiter, trimming and quoted field handling.
- Use `FieldMappings` in CSV settings to map headers to canonical field names or to skip extra columns.

If parsing fails, check the app logs (Serilog) to see validation warnings/errors (duplicate header, missing required fields, or wrong field counts).

Steps to export from Quicken:
- Create a report that includes all transactions from the accounts you want to analyze.
- Use the "Print" option in Quicken report and select "CSV file" or "tab delimited (Excel compatible) disk file" as the output format.
- Save the file and provide its path to this application.
