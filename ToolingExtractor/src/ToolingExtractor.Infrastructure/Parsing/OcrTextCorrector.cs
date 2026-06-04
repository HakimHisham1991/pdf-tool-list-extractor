using System.Text.RegularExpressions;
using ToolingExtractor.Core.Models;

namespace ToolingExtractor.Infrastructure.Parsing;

public class OcrTextCorrector
{
    public void CorrectRecord(ToolingRecord record)
    {
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
}
