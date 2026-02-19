namespace PSMultiTools.Core;

public record PlatformSummaryDto(
    string Id,
    string Name,
    string Description,
    int ToolCount
);

public enum LibraryItemType
{
    Backup,
    Pkg,
    Ps3Iso,
    Ps2Iso,
    PsxIso,
    PspIso,
    Unknown
}

public enum LibraryItemLocation
{
    Local,
    Remote
}

public record LibraryItemDto(
    string Id,
    string Platform,
    string Title,
    string? TitleId,
    string? ContentId,
    string? Region,
    string? Category,
    string? Version,
    string? AppVersion,
    string? RequiredFirmware,
    string? Size,
    string? FilePath,
    string? FolderPath,
    LibraryItemType ItemType,
    LibraryItemLocation Location,
    string? BackgroundImagePath,
    string? CoverImagePath
);

public record ActionDescriptorDto(
    string ActionId,
    string Label,
    string Category,
    bool RequiresSelection
);

public enum JobState
{
    Queued,
    Running,
    Completed,
    Failed
}

public record JobStartResponseDto(
    Guid JobId,
    string Name
);

public record JobStatusDto(
    Guid JobId,
    string Name,
    JobState State,
    int ProgressPercent,
    string Message,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FinishedAt
);

public record JobProgressEventDto(
    Guid JobId,
    JobState State,
    int ProgressPercent,
    string Message,
    DateTimeOffset Timestamp
);

public record ConfigDto(
    string? PS3Ip,
    string? PS5Ip,
    int? PS5FtpPort,
    int? PS5PayloadPort,
    int? ScanThreads,
    bool AutoLibraryMusic,
    bool LoadIcons,
    bool LoadBackgrounds,
    bool SkipFileChecks,
    bool FtpLoadIcons,
    bool FtpLoadBackgrounds,
    bool FtpScanAllUsb,
    bool FtpScanExt0
);

public record EntryRouteRequestDto(
    string Path
);

public record EntryRouteResultDto(
    string Route,
    string Intent,
    string SourcePath
);

public record LocalScanRequestDto(
    string FolderPath
);

public record FtpScanRequestDto(
    string ConsoleIp,
    int Port = 21
);

public record LibraryFilterRequestDto(
    LibraryItemType? ItemType,
    LibraryItemLocation? Location,
    string? Search
);

public record ExecuteActionRequestDto(
    string? ItemId,
    Dictionary<string, string>? Parameters
);

public interface IConfigService
{
    Task<ConfigDto> GetConfigAsync(CancellationToken cancellationToken);
    Task<ConfigDto> UpdateConfigAsync(ConfigDto newConfig, CancellationToken cancellationToken);
}

public interface IEntryRoutingService
{
    EntryRouteResultDto Resolve(string path);
}

public interface IJobService
{
    Guid Create(string name);
    JobStatusDto Get(Guid jobId);
    IReadOnlyCollection<JobStatusDto> GetAll();
    IAsyncEnumerable<JobProgressEventDto> Stream(Guid jobId, CancellationToken cancellationToken);
    void Update(Guid jobId, JobState state, int progressPercent, string message);
}

public interface IPS3LibraryService
{
    IReadOnlyCollection<LibraryItemDto> GetItems();
    Task<JobStartResponseDto> ScanLocalAsync(LocalScanRequestDto request, CancellationToken cancellationToken);
    Task<JobStartResponseDto> ScanFtpAsync(FtpScanRequestDto request, CancellationToken cancellationToken);
    IReadOnlyCollection<LibraryItemDto> Filter(LibraryFilterRequestDto filter);
    IReadOnlyCollection<ActionDescriptorDto> GetActions(string? itemId);
    Task<JobStartResponseDto> ExecuteActionAsync(string actionId, ExecuteActionRequestDto request, CancellationToken cancellationToken);
}

public interface IToolProcessRunner
{
    Task<int> RunAsync(string toolPath, string arguments, CancellationToken cancellationToken);
}
