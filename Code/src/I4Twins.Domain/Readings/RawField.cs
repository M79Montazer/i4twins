namespace I4Twins.Domain.Readings;

public enum RawFieldType
{
    Missing,
    Null,
    Text,
    Number,
    Integer,
    Boolean,
    Other,
}


public readonly record struct RawField(RawFieldType Type, double Number = 0,
    long Integer = 0, string? Text = null, bool Boolean = false)
{
    public static RawField Of(string value) => new(RawFieldType.Text, Text: value);
    public static RawField Of(double value) => new(RawFieldType.Number, Number: value);
    public static RawField Of(long value) => new(RawFieldType.Integer, Integer: value);
    public static RawField Of(bool value) => new(RawFieldType.Boolean, Boolean: value);
}
