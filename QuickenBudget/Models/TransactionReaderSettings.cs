/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
namespace QuickenBudget.Models;

/// <summary>
/// Collect configuration settings for the transaction reader. This is used to specify the file path of the Quicken report 
/// to read transactions from, as well as any other settings that may be needed in the future.
/// </summary>
public class TransactionReaderSettings
{
    /// <summary>
    /// File name to read transactions from.
    /// </summary>
    public string QuickenReportFile { get; set; } = string.Empty;

    /// <summary>
    /// Settings for the CSV reader.
    /// </summary>
    public CSVSettings CSVSettings { get; set; } = new();
}
