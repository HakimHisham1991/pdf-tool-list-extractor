using ToolingExtractor.Infrastructure.Parsing;
using Xunit;

namespace ToolingExtractor.Core.Tests;

public class OcrTextCorrectorTests
{
    private readonly OcrTextCorrector _sut = new();

    [Fact]
    public void CorrectNumericField_OAsZero_Corrected() =>
        Assert.Equal("8.0mm", _sut.CorrectNumericField("8.Omm"));

    [Fact]
    public void CorrectNumericField_CommaDecimal_Normalised() =>
        Assert.Equal("8.5", _sut.CorrectNumericField("8,5"));

    [Fact]
    public void CorrectToolNumber_IAsOne_Corrected() =>
        Assert.Equal("T01", _sut.CorrectToolNumber("T0I"));

    [Fact]
    public void CorrectOperationCode_Corrected() =>
        Assert.Equal("OP10", _sut.CorrectOperationCode("OP1O"));

    [Fact]
    public void CorrectNumericField_CleanInput_Unchanged() =>
        Assert.Equal("12.5", _sut.CorrectNumericField("12.5"));
}
