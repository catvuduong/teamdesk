namespace Api.Tests.Unit;

public class CsvTests
{
    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("Billing, invoices", "\"Billing, invoices\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("line one\nline two", "\"line one\nline two\"")]
    [InlineData("carriage\rreturn", "\"carriage\rreturn\"")]
    [InlineData("a,\"b\"\nc", "\"a,\"\"b\"\"\nc\"")]
    public void Fields_are_quoted_only_when_they_need_it(string? value, string expected)
    {
        Assert.Equal(expected, Csv.Field(value));
    }

    [Fact]
    public void Line_joins_escaped_fields_with_commas()
    {
        Assert.Equal("1001,\"Printer, jams\",,open", Csv.Line(["1001", "Printer, jams", null, "open"]));
    }
}
