namespace Audiarr.Core.Configuration;

public class MultiValuedTagsOptions
{
    public string Delimiter { get; set; } = "/";
    public bool EnableDelimiterParsing { get; set; } = true;
    public string[] PreferredDelimiters { get; set; } = { "/", ";", "," };
}
