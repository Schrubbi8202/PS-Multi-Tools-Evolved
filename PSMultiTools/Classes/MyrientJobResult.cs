using System.Collections.Generic;

namespace PSMultiTools.Classes;

public class MyrientJobResult
{
    public string EntryName { get; set; } = "";
    public string Platform { get; set; } = "";
    public bool Succeeded { get; set; }
    public string ErrorMessage { get; set; } = "";
    public string DownloadedFilePath { get; set; } = "";
    public List<string> ProcessedLocalFiles { get; set; } = [];
    public List<string> UploadedRemotePaths { get; set; } = [];
}
