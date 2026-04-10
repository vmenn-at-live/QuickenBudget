/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
using System.Collections.Generic;

namespace QuickenBudget.Models;

/// <summary>
/// Configuration settings for the CSV reader used in transaction reading.
/// </summary>
public class CSVSettings
{
    /// <summary>
    /// Number of lines to skip at the beginning of the file.
    /// </summary>
    public int LinesToSkip { get; set; } = 5;

    /// <summary>
    /// Delimiter character used in the CSV file.
    /// </summary>
    public char Delimiter { get; set; } = '\t';


    /// <summary>
    /// Whether to trim whitespace from fields.
    /// </summary>
    public bool TrimFields { get; set; } = true;

    /// <summary>
    /// Flag indicating whether to handle quoted fields in the CSV file. If true, the reader will correctly parse fields that are enclosed in quotes,
    /// allowing for delimiters and quotes within those fields.
    /// </summary>
    public bool HandleQuotedFields { get; set; } = true;

    /// <summary>
    /// Record that represents the mapping of a CSV field to a Transaction property, along with whether the field is required and whether it should be skipped.
    /// </summary>
    /// <param name="MapTo">The name of the property to map the CSV field to.</param>
    /// <param name="Required">Indicates whether the field is required.</param>
    /// <param name="Skip">Indicates whether the field should be skipped.</param>
    public record FieldRecord(string MapTo, bool Required, bool Skip);

    /// <summary>
    /// Field/column mappings for the CSV file. The key is the name of the field in the CSV header, and the value is a <see cref="FieldRecord"/>
    /// that specifies how to map that field to a Transaction property, whether it's required, and whether it should be skipped.
    /// </summary>
    public Dictionary<string, FieldRecord> FieldMappings { get; set; } = [];
}