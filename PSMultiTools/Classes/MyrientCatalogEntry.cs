using System.Collections.Generic;

namespace PSMultiTools.Classes;

public class MyrientCatalogEntry
{
    public string Source { get; set; } = "";
    public string Platform { get; set; } = "";
    public string Category { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public List<string> Aliases { get; set; } = [];
    public string AliasTooltip { get; set; } = "";
    public string SearchIndex { get; set; } = "";
    public string Url { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public string FileExtension { get; set; } = "";
    public long? SizeBytes { get; set; }
    public string LastModified { get; set; } = "";
}
