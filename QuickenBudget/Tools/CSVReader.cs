/* https://github.com/vmenn-at-live/QuickenBudget
 * Copyright (c) 2026 by Valerian Menn - MIT License
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.Extensions.Logging;

using QuickenBudget.Models;

using FieldRecord = QuickenBudget.Models.CSVSettings.FieldRecord;

namespace QuickenBudget.Tools;

/// <summary>
/// A static class that provides functionality to read and parse CSV files based on specified settings, including handling of field mappings, required fields, and logging of any issues encountered during parsing.
/// </summary>
public static class CSVReader
{
    /// <summary>
    /// Parse a collection of lines from a CSV file into an enumerable of dictionaries, where each dictionary represents a record
    /// with key-value pairs corresponding to header-field mappings.
    /// 
    /// The method handles skipping initial lines (per settings), trimming, field validation, and logging of any issues encountered during parsing.
    /// </summary>
    /// <param name="lines">Lines to parse</param>
    /// <param name="settings">CSV settings</param>
    /// <param name="logger">Logger instance</param>
    /// <returns>Enumerable of tuples that contain the line number and a dictionary representing CSV records</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static IEnumerable<(int line, Dictionary<string, string> propertyMap)> ParseLines(IEnumerable<string> lines, CSVSettings settings, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(settings, nameof(settings));
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));
        ValidateFileMappings(settings.FieldMappings, logger);
        // If the separator is a whitespace character, we cannot trim lines, so disable line trimming
        bool trimLines = !char.IsWhiteSpace(settings.Delimiter);

        // Skip initial lines
        lines = (lines ?? throw new ArgumentNullException(nameof(lines))).Skip(settings.LinesToSkip);
        int lineNumber = settings.LinesToSkip;
        string[]? headers = null;
        int headerCount = 0;

        // Now we can iterate.
        foreach (var l in lines)
        {
            lineNumber++;
            // Ignore empty lines
            if (l == null || string.IsNullOrWhiteSpace(l))
            {
                continue;
            }

            // Trim the line if needed, then split into fields
            var line = trimLines ? l.Trim() : l;
            var fields = line.SplitLine(settings.Delimiter, settings.TrimFields, settings.HandleQuotedFields).ToArray();
            int fieldCount = fields.Length;

            // If we haven't processed the header yet, do so now and validate it. Otherwise, validate the number of fields and generate/yield return the record.
            if (headers == null)
            {
                headers = ValidateHeader(fields, settings.FieldMappings, logger);
                headerCount = headers.Length;
            }
            else if (headerCount != fieldCount)
            {
                logger.LogWarning("Skipped line {lineNumber} as it has wrong number of fields, {headerFields} was expected, {actualFields} was found",
                    lineNumber, headerCount, fieldCount);
                continue;
            }
            else
            {
                yield return (lineNumber, headers
                    .Zip(fields, (header, field) => (header, field))
                    .Where(hf => !string.IsNullOrWhiteSpace(hf.header))
                    .ToDictionary(hf => hf.header, hf => hf.field));
            }
        }
    }

    /// <summary>
    /// Splits the specified string into a sequence of substrings based on the given delimiter, with options to trim
    /// whitespace and handle quoted fields.
    /// </summary>
    /// <remarks>When handleQuoting is set to true, fields enclosed in quotes are treated as single fields
    /// even if they contain delimiter characters. This is useful for parsing CSV or similar formats where fields may be
    /// quoted.</remarks>
    /// <param name="line">The input string to split. May be null or consist only of white-space characters; in such cases, an empty collection is returned..</param>
    /// <param name="delimiter">The character that delimits the fields in the input string.</param>
    /// <param name="trimFields">true to trim leading and trailing white-space from each resulting field; otherwise, false.</param>
    /// <param name="handleQuoting">true to treat quoted fields as single values, allowing delimiters within quotes; otherwise, false.</param>
    /// <returns>An enumerable collection of substrings resulting from the split operation. Returns an empty collection if the
    /// input string is null or white-space.</returns>
    private static IEnumerable<string> SplitLine(this string line, char delimiter, bool trimFields, bool handleQuoting)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return [];
        }


        IEnumerable<string> result = handleQuoting ? line.SplitLineWithQuoting(delimiter) : line.Split(delimiter);

        if (trimFields)
        {
            result = result.Select(field => field.Trim());
        }

        return result;
    }

    /// <summary>
    /// Splits a delimited string into fields, preserving quoted sections as single fields and handling escaped quotes.
    /// </summary>
    /// <remarks>Fields enclosed in double quotes are parsed as single fields, even if they contain delimiter
    /// characters. Double quotes within quoted fields must be escaped by doubling them (e.g., "" becomes "). The method
    /// yields empty fields for consecutive delimiters and always includes the last field, even if it is
    /// empty.</remarks>
    /// <param name="line">The input string to parse. May contain quoted sections and escaped quotes.</param>
    /// <param name="delimiter">The character used to separate fields in the input string.</param>
    /// <returns>An enumerable collection of strings representing the parsed fields. Quoted sections are treated as single
    /// fields, and escaped quotes within quoted fields are unescaped.</returns>
    private static IEnumerable<string> SplitLineWithQuoting(this string line, char delimiter)
    {
        bool inQuotes = false;
        StringBuilder currentField = new();
        for (int i = 0; i < line.Length; i++)
        {
            // Examine the next character.
            char c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // If this is an escaped quote (i.e., two consecutive quotes), and if so, add a single quote to the field and skip the next character.
                        currentField.Append('"');
                        i++;
                    }
                    // This is the closing quote for this field, so we should exit quote mode. We don't add this quote to the field value.
                    else
                    {
                        inQuotes = false;
                    }
                }
                // Not a quote, just add the character to the current field
                else
                {
                    currentField.Append(c);
                }
            }
            else
            {
                // Start a quoted field if we encounter a quote at the beginning of a field. We don't add this quote to the field value.
                if (c == '"' && currentField.Length == 0)
                {
                    inQuotes = true;
                }
                else if (c == delimiter)
                {
                    yield return currentField.ToString();
                    currentField.Clear();
                }
                // Regular character outside of quotes, just add it to the current field. A quote here would be treated as a normal character since we're not in quote mode.
                else
                {
                    currentField.Append(c);
                }
            }
        }

        // Add the last field, even if empty, to match string.Split behavior
        yield return currentField.ToString();
    }

    /// <summary>
    /// Validate that the configuration of field mappings provided in the settings is consistent and does not contain any issues that would prevent proper parsing of the CSV file.
    /// </summary>
    /// <param name="fieldInfo">Dictionary of field mappings from the CSV settings.</param>
    /// <param name="logger">Logger instance</param>
    /// <exception cref="InvalidDataException">We have duplicate MapTo values in the field mappings.</exception>
    private static void ValidateFileMappings(Dictionary<string, FieldRecord> fieldInfo, ILogger logger)
    {
        if (fieldInfo == null || fieldInfo.Count == 0)
        {
            logger.LogWarning("No field mappings provided in settings. All headers will be used as-is.");
        }
        else
        {
            // Check for duplicate MapTo values among the field mappings, which would cause conflicts when translating headers.
            if (fieldInfo.Values
                .Where(p => p != null && !p.Skip && !string.IsNullOrWhiteSpace(p.MapTo)) // Safety checks, ignore null, skipped, or unmapped fields
                .GroupBy(p => p.MapTo)
                .Any(g => g.Count() > 1))
            {
                logger.LogError("Duplicate MapTo values found in field mappings.");
                throw new InvalidDataException("Duplicate MapTo values found in field mappings.");
            }
        }
    }

    /// <summary>
    /// Processes the header line of the CSV file, validating it against the expected field mappings provided in the settings.
    /// </summary>
    /// <param name="headers">The headers from the CSV file.</param>
    /// <param name="fieldInfo">The field mappings from the CSV settings.</param>
    /// <param name="logger">Logger instance</param>
    /// <returns>Array of validated and translated headers.</returns>
    /// <exception cref="InvalidDataException">Thrown when duplicate headers are found or required fields are missing.</exception>
    private static string[] ValidateHeader(IEnumerable<string> headers, Dictionary<string, FieldRecord> fieldInfo, ILogger logger)
    {
        // We use a HashSet to track seen headers for efficient lookup, an integer to count required fields, and a list to build the translated header.
        HashSet<string> seen = [];
        List<string> translated = [];
        int required = 0;
        foreach (string header in headers)
        {
            string headerValue = header;

            if (!string.IsNullOrWhiteSpace(header))
            {
                // Check for duplicate headers
                if (seen.Contains(header))
                {
                    logger.LogError("Duplicate header found: {Header}", header);
                    throw new InvalidDataException($"Duplicate header found: {header}");
                }

                // Mark this header as seen
                seen.Add(header);

                // See if we have special instructions about this one...
                if (fieldInfo != null && fieldInfo.TryGetValue(header, out var info))
                {
                    if (info.Skip)
                    {
                        headerValue = string.Empty;
                    }
                    else
                    {
                        if (info.Required)
                        {
                            required++;
                        }

                        if (!string.IsNullOrWhiteSpace(info.MapTo))
                        {
                            headerValue = info.MapTo;
                        }

                    }
                }
            }

            // We keep all headers, even empty, since we'll need to match them to fields.
            translated.Add(headerValue);
        }
        
        if (fieldInfo != null && required != fieldInfo.Count(f => !f.Value.Skip && f.Value.Required))
        {
            string missingFields = string.Join(", ", fieldInfo.Where(f => !f.Value.Skip && f.Value.Required && !translated.Contains(f.Value.MapTo)).Select(f => f.Key));
            logger.LogError("Missing required fields: {MissingFields}", missingFields);
            throw new InvalidDataException($"Missing required fields: {missingFields}");
        }
        return [.. translated];
    }

    /// <summary>
    /// Read a file. See <see cref="ParseLines(IEnumerable{string}, CSVSettings, ILogger)"/> for details.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <param name="settings">CSV settings</param>
    /// <param name="logger">Logger instance</param>
    /// <returns>Enumerable of tuples that contain the line number and a dictionary representing CSV records</returns>
    public static IEnumerable<(int line, Dictionary<string, string> propertyMap)> ParseFile(string filePath, CSVSettings settings, ILogger logger) =>
        ParseLines(File.ReadLines(filePath), settings, logger);
}
