/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QuickenBudget.Models;

/// <summary>
/// Model used to import configuration data for selecting groups of transactions.
/// The <see cref="Filter"/> property specifies the regex filter to apply to that field 
/// The <see cref="TargetGroup"/> property specifies the name of the group to which transactions matching the filter should be assigned.
/// </summary>
public class GroupSelector
{
    /// <summary>
    /// Specifies the field to filter on (e.g. "Category", "Memo", etc.).
    /// </summary>
    public string? Field {get; set; }

    /// <summary>
    /// Specifies the regex filter to apply to that field value. The string value provided in the configuration will be compiled
    /// into a Regex object for efficient matching. If the value is null or empty, no filtering will be applied for this selector.
    /// </summary>
    public string? Filter { get => _filter?.ToString(); set => _filter = TransactionRegex.CreateOrNull(value); }

    /// <summary>
    /// Specifies the name of the group to which transactions matching the filter should be assigned.
    /// </summary>
    public string? TargetGroup { get; set; }

    /// <summary>
    /// The compiled regex object created from the Filter string. This is used for efficient matching when selecting groups for transactions.
    /// It will be null if the Filter string is null or empty.
    /// </summary>
    private Regex? _filter;

    /// <summary>
    /// Evaluates the specified fields against the configured filter and returns the associated target group if the
    /// filter criteria are met.
    /// </summary>
    /// <remarks>Both the Field and TargetGroup properties must be set to non-empty, non-whitespace strings
    /// for this method to operate. The filter is applied to the trimmed value of the field. If the required field is
    /// missing or its value is invalid, the method returns null.</remarks>
    /// <param name="fields">A read-only dictionary containing field names and their corresponding values to be evaluated. The dictionary
    /// must contain the field specified by the Field property, and its value must not be null, empty, or whitespace.</param>
    /// <returns>The target group name if the filter matches the trimmed value of the specified field; otherwise, null.</returns>
    public string? SelectGroup(IReadOnlyDictionary<string, string> fields) =>
        _filter != null &&
        !string.IsNullOrWhiteSpace(Field) &&
        !string.IsNullOrWhiteSpace(TargetGroup) &&
        fields.TryGetValue(Field, out string? fieldValue) &&
        !string.IsNullOrWhiteSpace(fieldValue) &&
        _filter.IsMatch(fieldValue.Trim())
            ? TargetGroup
            : null;
}
