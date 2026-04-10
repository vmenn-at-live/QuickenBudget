/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using QuickenBudget.Interfaces;
using QuickenBudget.Models;
using QuickenBudget.Tools;

#pragma warning disable CA1873

namespace QuickenBudget.Services;

/// <summary>
/// Read data from file and transform it into an enumerable of <see cref="Transaction"/> objects.
/// </summary>
public class TransactionReader(
    ILogger<TransactionReader> logger,
    IOptionsMonitor<TransactionSelectors> groupSelectors,
    IOptionsMonitor<TransactionReaderSettings> readerSettings) : ITransactionReader
{
    private readonly IOptionsMonitor<TransactionReaderSettings> _readerSettings = readerSettings;
    private readonly IOptionsMonitor<TransactionSelectors> _groupSelectors = groupSelectors;
    private readonly ILogger<TransactionReader> _logger = logger;

    /// <summary>
    /// The main method of this class. Reads transactions from the file specified in the settings, applies grouping and filtering.
    /// The function returns a list of transactions instead of an enumerable so that all transactions are eagerly read within this method and any parsing
    /// error encountered during enumeration is immediately thrown and wrapped with line-number context. Lazy enumeration would defer errors until the
    /// caller iterates, making it harder to associate exceptions with specific line numbers.
    /// </summary>
    /// <returns>List of <see cref="Transaction"/> objects</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public List<Transaction> Ingest()
    {
        TransactionReaderSettings? readerSettings = _readerSettings.CurrentValue;
        TransactionSelectors? groupSelectors = _groupSelectors.CurrentValue;

        if (readerSettings == null || string.IsNullOrWhiteSpace(readerSettings.QuickenReportFile))
        {
            _logger.LogError("TransactionReaderSettings is not properly configured. Please provide a valid Quicken report file path.");
            throw new InvalidOperationException("TransactionReaderSettings is not properly configured.");
        }

        _logger.LogInformation("Starting to ingest transactions from file: {FilePath}", readerSettings.QuickenReportFile);
        DateOnly propagateDate = DateOnly.MinValue;

        int expensesCount = groupSelectors?.ExpensesGroupSelectors?.Select(g => g.Value.Count).Sum() ?? 0;
        int incomeCount = groupSelectors?.IncomeGroupSelectors?.Select(g => g.Value.Count).Sum() ?? 0;
        if (expensesCount == 0 && incomeCount == 0)
        {
            _logger.LogWarning("No group selectors are provided. All transactions will be grouped as 'Other'.");
        }

        int filterCount = groupSelectors?.TransactionFilters?.Select(g => g.Value.Count).Sum() ?? 0;
        if (filterCount == 0)
        {
            _logger.LogWarning("No TransactionFilters provided. No transactions will be filtered out.");
        }

        int lastReadLine = -1;
        try
        {
            // Iterate through the transactions read from the file, create Transaction objects from them, apply grouping and filtering, and yield return the valid transactions.
            return [.. ReadTransactions(readerSettings).Select(tuple =>
             {
                 lastReadLine = tuple.line;
                 Transaction? tx;
                 (propagateDate, tx) = Transaction.CreateTransaction(tuple.propertyMap, propagateDate, groupSelectors);
                 return tx;
             }).OfType<Transaction>()];
        }
        catch (ParsingException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ParsingException(ex, lastReadLine);
        }
    }

    private IEnumerable<(int line, Dictionary<string, string> propertyMap)> ReadTransactions(TransactionReaderSettings readerSettings)
    {
        string filePath = readerSettings.QuickenReportFile;
        CSVSettings settings = readerSettings.CSVSettings;

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            _logger.LogError("The specified file does not exist: {FilePath}", filePath);
            throw new FileNotFoundException("The specified file does not exist.", filePath);
        }

        long? fileSizeBytes = null;
        try
        {
            fileSizeBytes = new FileInfo(filePath).Length;
        }
        catch (IOException ex)
        {
            _logger.LogDebug(ex, "Failed to get file size for: {FilePath}. Proceeding without file size", filePath);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogDebug(ex, "Unauthorized to determine file size for {FilePath}. Proceeding without file size.", filePath);
        }

        if (fileSizeBytes.HasValue)
        {
            _logger.LogInformation("Loading transactions from file: {FilePath} ({FileSizeBytes} bytes)", filePath, fileSizeBytes);
        }
        else
        {
            _logger.LogInformation("Loading transactions from file: {FilePath} (file size unknown)", filePath);
        }

        return CSVReader.ParseFile(filePath, settings, _logger);
    }
}
