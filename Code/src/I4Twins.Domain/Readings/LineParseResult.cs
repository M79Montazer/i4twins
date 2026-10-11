namespace I4Twins.Domain.Readings;

public abstract record LineParseResult;

public sealed record LineParsed(RawReading Reading) : LineParseResult;

public sealed record LineMalformed(string Reason) : LineParseResult;
