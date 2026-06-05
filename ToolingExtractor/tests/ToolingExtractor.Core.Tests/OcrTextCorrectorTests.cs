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

    [Fact]
    public void NormalizeEngineeringSymbols_PreservesPdfDiameterAndDegree() =>
        Assert.Equal("Chamfer Ø8 x 60°", OcrTextCorrector.NormalizeEngineeringSymbols("Chamfer Ø8 x 60°"));

    [Fact]
    public void NormalizeEngineeringSymbols_FixesReplacementCharBeforeDiameter() =>
        Assert.Equal("Chamfer Ø8 x 60°", OcrTextCorrector.NormalizeEngineeringSymbols("Chamfer \uFFFD8 x 60\uFFFD"));

    [Fact]
    public void NormalizeEngineeringSymbols_FixesMojibake() =>
        Assert.Equal("Drill Ø8 x 140°", OcrTextCorrector.NormalizeEngineeringSymbols("Drill Ã˜8 x 140Â°"));

    [Fact]
    public void NormalizeEngineeringSymbols_FixesDegreeInConsumableCode() =>
        Assert.Equal("S8xR0.762x30°x65x8xG6", OcrTextCorrector.NormalizeEngineeringSymbols("S8xR0.762x30\uFFFDx65x8xG6"));
}
