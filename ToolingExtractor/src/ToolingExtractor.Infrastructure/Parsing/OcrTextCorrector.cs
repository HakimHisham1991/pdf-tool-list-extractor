using System.Text.RegularExpressions;
using ToolingExtractor.Core.Models;

namespace ToolingExtractor.Infrastructure.Parsing;

public class OcrTextCorrector
{
    private const char Diameter = '\u00D8';
    private const char Degree = '\u00B0';

    public void CorrectRecord(ToolingRecord record)
    {
        NormalizeEngineeringSymbols(record);
        record.ToolNo = CorrectToolNumber(record.ToolNo);
        record.ToolDiameterD1 = CorrectNumericField(record.ToolDiameterD1);
        record.FluteLengthL1 = CorrectNumericField(record.FluteLengthL1);
        record.ToolExtLengthL2 = CorrectNumericField(record.ToolExtLengthL2);
        record.ToolCornerRadius = CorrectNumericField(record.ToolCornerRadius);
        record.ToolPathTimeMinutes = CorrectNumericField(record.ToolPathTimeMinutes);
        record.PartNumber = CorrectPartNumber(record.PartNumber);
        record.Operation = CorrectOperationCode(record.Operation);
        record.Revision = CorrectRevisionCode(record.Revision);
    }

    public static void NormalizeDecimalSeparators(ToolingRecord record)
    {
        var c = new OcrTextCorrector();
        record.ToolDiameterD1 = c.CorrectNumericField(record.ToolDiameterD1);
        record.FluteLengthL1 = c.CorrectNumericField(record.FluteLengthL1);
        record.ToolExtLengthL2 = c.CorrectNumericField(record.ToolExtLengthL2);
        record.ToolCornerRadius = c.CorrectNumericField(record.ToolCornerRadius);
        record.ToolPathTimeMinutes = c.CorrectNumericField(record.ToolPathTimeMinutes);
    }

    public string CorrectToolNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var m = Regex.Match(value, @"^(T#?)(\s*)(.*)$", RegexOptions.IgnoreCase);
        if (!m.Success) return value;
        var numeric = CorrectDigitsInSegment(m.Groups[3].Value);
        return m.Groups[1].Value + m.Groups[2].Value + numeric;
    }

    public string CorrectNumericField(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        value = value.Replace(',', '.');
        var chars = value.Select(c =>
        {
            return c switch
            {
                'O' => '0',
                'o' => '0',
                'I' => '1',
                'l' => '1',
                'B' => '8',
                'S' => '5',
                'Z' => '2',
                _ => c
            };
        }).ToArray();
        var corrected = new string(chars);
        return Regex.Replace(corrected, @"[^0-9.\-mm\s]", string.Empty).Trim();
    }

    public string CorrectPartNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] is not ('O' or 'o')) continue;
            var prevDigit = i > 0 && char.IsDigit(chars[i - 1]);
            var nextDigit = i < chars.Length - 1 && char.IsDigit(chars[i + 1]);
            if (prevDigit || nextDigit) chars[i] = '0';
            else chars[i] = 'O';
        }
        return new string(chars);
    }

    public string CorrectOperationCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var m = Regex.Match(value, @"^(OP\s*)(.*)$", RegexOptions.IgnoreCase);
        if (!m.Success) return value;
        return m.Groups[1].Value + CorrectDigitsInSegment(m.Groups[2].Value);
    }

    public string CorrectRevisionCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var m = Regex.Match(value, @"^(REV\s*)(.*)$", RegexOptions.IgnoreCase);
        if (!m.Success) return value;
        return m.Groups[1].Value + CorrectDigitsInSegment(m.Groups[2].Value);
    }

    private static string CorrectDigitsInSegment(string segment) =>
        segment.Replace('O', '0').Replace('o', '0').Replace('I', '1').Replace('l', '1').Replace('B', '8');

    public void NormalizeEngineeringSymbols(ToolingRecord record)
    {
        record.ToolName = NormalizeEngineeringSymbols(record.ToolName);
        record.ConsumableToolDescription = NormalizeEngineeringSymbols(record.ConsumableToolDescription);
        record.ToolSupplier = NormalizeEngineeringSymbols(record.ToolSupplier);
        record.ToolHolder = NormalizeEngineeringSymbols(record.ToolHolder);
        record.ArborDescription = NormalizeEngineeringSymbols(record.ArborDescription);
        record.Remarks = NormalizeEngineeringSymbols(record.Remarks);
        record.PartDescription = NormalizeEngineeringSymbols(record.PartDescription);
        record.MachineModel = NormalizeEngineeringSymbols(record.MachineModel);
    }

    /// <summary>
    /// Normalises diameter (Ø) and degree (°) symbols from PDF/OCR text and fixes common UTF-8 mojibake.
    /// </summary>
    public static string NormalizeEngineeringSymbols(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        value = value
            .Replace("Ã˜", "Ø", StringComparison.Ordinal)
            .Replace("Ã¸", "ø", StringComparison.Ordinal)
            .Replace("Â°", "°", StringComparison.Ordinal)
            .Replace("âˆ…", "Ø", StringComparison.Ordinal)
            .Replace("Î¦", "Ø", StringComparison.Ordinal);

        value = value
            .Replace('\u2205', Diameter)
            .Replace('\u2300', Diameter)
            .Replace('\u03A6', Diameter)
            .Replace('\u03C6', Diameter)
            .Replace('\u00F8', Diameter);

        value = Regex.Replace(value, @"(\d)º", $"$1{Degree}");

        // Replacement char (lost encoding) — infer from tooling naming patterns.
        value = Regex.Replace(value, @"\uFFFD(?=\d)", Diameter.ToString());
        value = Regex.Replace(value, @"(?<=\d)\uFFFD(?=\s|$|[^\d])", Degree.ToString());
        value = Regex.Replace(value, @"(\d)x\uFFFD", $"$1x{Degree}");

        return value;
    }
}
