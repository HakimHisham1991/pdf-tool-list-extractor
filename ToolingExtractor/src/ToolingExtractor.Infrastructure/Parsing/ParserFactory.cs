using ToolingExtractor.Core.Enums;
using ToolingExtractor.Core.Interfaces;

namespace ToolingExtractor.Infrastructure.Parsing;

public class ParserFactory
{
    private readonly IEnumerable<IToolingParser> _parsers;

    public ParserFactory(IEnumerable<IToolingParser> parsers) => _parsers = parsers;

    public IToolingParser GetParser(TemplateType type) =>
        _parsers.FirstOrDefault(p => p.SupportedTemplate == type)
        ?? throw new InvalidOperationException($"No parser for template {type}");
}
