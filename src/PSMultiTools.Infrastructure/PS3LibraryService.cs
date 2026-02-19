using FluentFTP;
using PSMultiTools.Core;
using System.Security.Authentication;

namespace PSMultiTools.Infrastructure;

public sealed class PS3LibraryService : IPS3LibraryService
{
    private static readonly string[] Ps3IsoExtensions = [".iso"];
    private static readonly string[] PkgExtensions = [".pkg"];
    private readonly List<LibraryItemDto> _items = [];
    private readonly object _syncRoot = new();
    private readonly IJobService _jobService;
    private readonly IConfigService _configService;
    private static readonly HttpClient Http = new();

    private static readonly IReadOnlyCollection<ActionDescriptorDto> BaseActions =
    [
        new("library.load.local", "Load Local Folder", "Library", false),
        new("library.load.ftp", "Load From PS3 FTP", "Library", false),
        new("tool.batch.rename", "Batch Rename Utility", "Tools", false),
        new("tool.coreos", "Core_OS Tools", "Tools", false),
        new("tool.fix.tar", "Fix Tar", "Tools", false),
        new("tool.iso.tools", "ISO Tools", "Tools", true),
        new("tool.pkg.extract", "Extract PKG", "Tools", true),
        new("tool.pup.unpacker", "PUP Unpacker", "Tools", false),
        new("tool.rco.dumper", "RCO Dumper", "Tools", false),
        new("tool.self.reader", "SELF Reader", "Tools", false),
        new("tool.ftp.browser", "FTP Browser", "Tools", false),
        new("tool.param.editor", "PARAM.SFO Editor", "Tools", false),
        new("download.pkg.browser", "PKG Browser & Downloader", "Downloads", false),
        new("webman.open", "Open webMAN MOD Web Interface", "Remote", false),
        new("webman.open.temp.c", "Open Temperature GUI (C)", "Remote", false),
        new("webman.open.temp.f", "Open Temperature GUI (F)", "Remote", false),
        new("webman.refresh", "Rescan Games and Refresh XML", "Remote", false),
        new("webman.reload.game", "Reload PS3 Game", "Remote", false),
        new("webman.eject.disc", "Eject Disc", "Remote", false),
        new("webman.insert.disc", "Insert Disc", "Remote", false),
        new("webman.play.disc", "Play Disc", "Remote", false),
        new("webman.exit.to.xmb", "Exit Game to XMB", "Remote", false),
        new("webman.toggle.recording", "Toggle Video Recording", "Remote", false),
        new("webman.toggle.bgm", "Toggle In-Game Music", "Remote", false),
        new("webman.shutdown", "Shutdown PS3", "Remote", false),
        new("webman.restart", "Restart PS3", "Remote", false),
        new("webman.restart.scan", "Restart and Scan Content", "Remote", false),
        new("webman.restart.min", "Restart Show Min Version", "Remote", false),
        new("webman.reboot.hard", "Hard Reboot", "Remote", false),
        new("webman.reboot.soft", "Soft Reboot", "Remote", false),
        new("webman.reboot.quick", "Quick Reboot", "Remote", false),
        new("webman.reboot.vsh", "Reboot Using VSH", "Remote", false),
        new("webman.open.url", "Open URL in PS3 Browser", "Remote", false),
        new("webman.popup", "Show System Info on PS3", "Remote", false)
    ];

    public PS3LibraryService(IJobService jobService, IConfigService configService)
    {
        _jobService = jobService;
        _configService = configService;
    }

    public IReadOnlyCollection<LibraryItemDto> GetItems()
    {
        lock (_syncRoot)
        {
            return _items.ToArray();
        }
    }

    public Task<JobStartResponseDto> ScanLocalAsync(LocalScanRequestDto request, CancellationToken cancellationToken)
    {
        var jobId = _jobService.Create("PS3 Local Scan");
        _ = Task.Run(async () =>
        {
            try
            {
                _jobService.Update(jobId, JobState.Running, 2, "Scanning local files");
                var scanned = await ScanLocalInternalAsync(jobId, request.FolderPath, cancellationToken);
                lock (_syncRoot)
                {
                    _items.Clear();
                    _items.AddRange(scanned);
                }

                _jobService.Update(jobId, JobState.Completed, 100, $"Completed. {scanned.Count} items loaded.");
            }
            catch (Exception ex)
            {
                _jobService.Update(jobId, JobState.Failed, 100, $"Local scan failed: {ex.Message}");
            }
        }, cancellationToken);

        return Task.FromResult(new JobStartResponseDto(jobId, "PS3 Local Scan"));
    }

    public Task<JobStartResponseDto> ScanFtpAsync(FtpScanRequestDto request, CancellationToken cancellationToken)
    {
        var jobId = _jobService.Create("PS3 FTP Scan");
        _ = Task.Run(async () =>
        {
            try
            {
                _jobService.Update(jobId, JobState.Running, 2, "Connecting to FTP");
                var scanned = await ScanFtpInternalAsync(jobId, request.ConsoleIp, request.Port, cancellationToken);
                lock (_syncRoot)
                {
                    _items.Clear();
                    _items.AddRange(scanned);
                }

                _jobService.Update(jobId, JobState.Completed, 100, $"Completed. {scanned.Count} items loaded.");
            }
            catch (Exception ex)
            {
                _jobService.Update(jobId, JobState.Failed, 100, $"FTP scan failed: {ex.Message}");
            }
        }, cancellationToken);

        return Task.FromResult(new JobStartResponseDto(jobId, "PS3 FTP Scan"));
    }

    public IReadOnlyCollection<LibraryItemDto> Filter(LibraryFilterRequestDto filter)
    {
        IEnumerable<LibraryItemDto> query = GetItems();
        if (filter.ItemType is not null)
        {
            query = query.Where(i => i.ItemType == filter.ItemType);
        }

        if (filter.Location is not null)
        {
            query = query.Where(i => i.Location == filter.Location);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(i =>
                i.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (i.TitleId?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (i.ContentId?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        return query.ToArray();
    }

    public IReadOnlyCollection<ActionDescriptorDto> GetActions(string? itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return BaseActions;
        }

        var item = GetItems().FirstOrDefault(i => i.Id == itemId);
        if (item is null)
        {
            return BaseActions;
        }

        var itemActions = new List<ActionDescriptorDto>(BaseActions);
        switch (item.ItemType)
        {
            case LibraryItemType.Pkg:
                itemActions.Add(new ActionDescriptorDto("item.pkg.info", "PKG Details", "Item", true));
                itemActions.Add(new ActionDescriptorDto("item.pkg.extract", "Extract .pkg", "Item", true));
                break;
            case LibraryItemType.Backup:
                itemActions.Add(new ActionDescriptorDto("item.backup.create.iso", "Create ISO", "Item", true));
                break;
            case LibraryItemType.Ps3Iso:
                itemActions.Add(new ActionDescriptorDto("item.iso.extract", "Extract ISO", "Item", true));
                itemActions.Add(new ActionDescriptorDto("item.iso.patch", "Patch ISO", "Item", true));
                itemActions.Add(new ActionDescriptorDto("item.iso.split", "Split ISO", "Item", true));
                break;
        }

        itemActions.Add(new ActionDescriptorDto("item.copy.to", "Copy To", "Item", true));
        itemActions.Add(new ActionDescriptorDto("item.play.rpcs3", "Play With RPCS3", "Item", true));
        return itemActions;
    }

    public Task<JobStartResponseDto> ExecuteActionAsync(string actionId, ExecuteActionRequestDto request, CancellationToken cancellationToken)
    {
        var jobId = _jobService.Create($"PS3 Action: {actionId}");
        _ = Task.Run(async () =>
        {
            try
            {
                _jobService.Update(jobId, JobState.Running, 10, "Executing action");
                await ExecuteActionInternalAsync(jobId, actionId, request, cancellationToken);
                _jobService.Update(jobId, JobState.Completed, 100, "Action completed.");
            }
            catch (Exception ex)
            {
                _jobService.Update(jobId, JobState.Failed, 100, $"Action failed: {ex.Message}");
            }
        }, cancellationToken);
        return Task.FromResult(new JobStartResponseDto(jobId, $"PS3 Action: {actionId}"));
    }

    private async Task ExecuteActionInternalAsync(Guid jobId, string actionId, ExecuteActionRequestDto request, CancellationToken cancellationToken)
    {
        switch (actionId)
        {
            case "library.load.local":
                if (request.Parameters is null || !request.Parameters.TryGetValue("folderPath", out var folderPath))
                {
                    throw new ArgumentException("folderPath parameter is required.");
                }

                await ScanLocalInternalAsync(jobId, folderPath, cancellationToken);
                break;
            case "library.load.ftp":
                {
                    var ip = await ResolvePs3IpAsync(request, cancellationToken);
                    await ScanFtpInternalAsync(jobId, ip, 21, cancellationToken);
                    break;
                }
            case "webman.open":
                await ExecuteWebManCommandAsync(jobId, await ResolvePs3IpAsync(request, cancellationToken), "/", cancellationToken);
                break;
            case "webman.open.temp.c":
                await ExecuteWebManCommandAsync(jobId, await ResolvePs3IpAsync(request, cancellationToken), "/tempc.html", cancellationToken);
                break;
            case "webman.open.temp.f":
                await ExecuteWebManCommandAsync(jobId, await ResolvePs3IpAsync(request, cancellationToken), "/tempf.html", cancellationToken);
                break;
            case "webman.refresh":
                await ExecuteWebManCommandAsync(jobId, await ResolvePs3IpAsync(request, cancellationToken), "/refresh.ps3?xmb", cancellationToken);
                break;
            case "webman.reload.game":
                await ExecuteWebManCommandAsync(jobId, await ResolvePs3IpAsync(request, cancellationToken), "/xmb.ps3$reloadgame", cancellationToken);
                break;
            case "webman.eject.disc":
                await ExecuteWebManCommandAsync(jobId, await ResolvePs3IpAsync(request, cancellationToken), "/eject.ps3", cancellationToken);
                break;
            case "webman.insert.disc":
                await ExecuteWebManCommandAsync(jobId, await ResolvePs3IpAsync(request, cancellationToken), "/insert.ps3", cancellationToken);
                break;
            case "webman.play.disc":
                await ExecuteWebManCommandAsync(jobId, await ResolvePs3IpAsync(request, cancellationToken), "/play.ps3", cancellationToken);
                break;
            case "webman.exit.to.xmb":
                await ExecuteWebManCommandAsync(jobId, await ResolvePs3IpAsync(request, cancellationToken), "/xmb.ps3$exit", cancellationToken);
                break;
            case "webman.toggle.recording":
                await ExecuteWebManCommandAsync(jobId, await ResolvePs3IpAsync(request, cancellationToken), "/videorec.ps3", cancellationToken);
                break;
            case "webman.toggle.bgm":
                await ExecuteWebManCommandAsync(jobId, await ResolvePs3IpAsync(request, cancellationToken), "/sysbgm.ps3", cancellationToken);
                break;
            case "webman.shutdown":
                await ExecuteWebManCommandAsync(jobId, await ResolvePs3IpAsync(request, cancellationToken), "/shutdown.ps3", cancellationToken);
                break;
            case "webman.restart":
                await ExecuteWebManCommandAsync(jobId, await ResolvePs3IpAsync(request, cancellationToken), "/restart.ps3", cancellationToken);
                break;
            case "webman.restart.scan":
                await ExecuteWebManCommandAsync(jobId, await ResolvePs3IpAsync(request, cancellationToken), "/restart.ps3?0", cancellationToken);
                break;
            case "webman.restart.min":
                await ExecuteWebManCommandAsync(jobId, await ResolvePs3IpAsync(request, cancellationToken), "/restart.ps3?min", cancellationToken);
                break;
            case "webman.reboot.hard":
                await ExecuteWebManCommandAsync(jobId, await ResolvePs3IpAsync(request, cancellationToken), "/reboot.ps3?hard", cancellationToken);
                break;
            case "webman.reboot.soft":
                await ExecuteWebManCommandAsync(jobId, await ResolvePs3IpAsync(request, cancellationToken), "/reboot.ps3?soft", cancellationToken);
                break;
            case "webman.reboot.quick":
                await ExecuteWebManCommandAsync(jobId, await ResolvePs3IpAsync(request, cancellationToken), "/reboot.ps3?quick", cancellationToken);
                break;
            case "webman.reboot.vsh":
                await ExecuteWebManCommandAsync(jobId, await ResolvePs3IpAsync(request, cancellationToken), "/reboot.ps3?vsh", cancellationToken);
                break;
            case "webman.open.url":
                {
                    if (request.Parameters is null || !request.Parameters.TryGetValue("url", out var url))
                    {
                        throw new ArgumentException("url parameter is required.");
                    }

                    await ExecuteWebManCommandAsync(jobId, await ResolvePs3IpAsync(request, cancellationToken), "/browser.ps3?" + Uri.EscapeDataString(url), cancellationToken);
                    break;
                }
            case "webman.popup":
                await ExecuteWebManCommandAsync(jobId, await ResolvePs3IpAsync(request, cancellationToken), "/popup.ps3", cancellationToken);
                break;
            default:
                _jobService.Update(jobId, JobState.Running, 60, $"Action '{actionId}' acknowledged by backend.");
                await Task.Delay(120, cancellationToken);
                break;
        }
    }

    private async Task<string> ResolvePs3IpAsync(ExecuteActionRequestDto request, CancellationToken cancellationToken)
    {
        if (request.Parameters is not null &&
            request.Parameters.TryGetValue("ip", out var providedIp) &&
            !string.IsNullOrWhiteSpace(providedIp))
        {
            return providedIp;
        }

        var config = await _configService.GetConfigAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(config.PS3Ip))
        {
            return config.PS3Ip;
        }

        throw new InvalidOperationException("PS3 IP is missing. Set it in config or pass it in action parameters.");
    }

    private async Task ExecuteWebManCommandAsync(Guid jobId, string ip, string path, CancellationToken cancellationToken)
    {
        var target = $"http://{ip}{path}";
        _jobService.Update(jobId, JobState.Running, 50, $"Calling {target}");
        using var response = await Http.GetAsync(target, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"webMAN returned {(int)response.StatusCode}.");
        }
    }

    private async Task<List<LibraryItemDto>> ScanLocalInternalAsync(Guid jobId, string folderPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException("The selected local folder does not exist.");
        }

        var results = new List<LibraryItemDto>();
        var files = Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories).ToArray();
        var total = Math.Max(files.Length, 1);

        for (var i = 0; i < files.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = files[i];
            var ext = Path.GetExtension(file).ToLowerInvariant();
            var type = MapByPathAndExtension(file, ext);
            if (type == LibraryItemType.Unknown)
            {
                continue;
            }

            var info = new FileInfo(file);
            results.Add(new LibraryItemDto(
                Id: Guid.NewGuid().ToString("N"),
                Platform: "PS3",
                Title: Path.GetFileNameWithoutExtension(file),
                TitleId: null,
                ContentId: null,
                Region: null,
                Category: null,
                Version: null,
                AppVersion: null,
                RequiredFirmware: null,
                Size: HumanBytes(info.Length),
                FilePath: file,
                FolderPath: Path.GetDirectoryName(file),
                ItemType: type,
                Location: LibraryItemLocation.Local,
                BackgroundImagePath: null,
                CoverImagePath: null
            ));

            if (i % 200 == 0)
            {
                var progress = (int)((i / (double)total) * 100);
                _jobService.Update(jobId, JobState.Running, progress, $"Scanned {i}/{total}");
            }
        }

        await Task.CompletedTask;
        return results;
    }

    private async Task<List<LibraryItemDto>> ScanFtpInternalAsync(Guid jobId, string consoleIp, int port, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(consoleIp))
        {
            throw new ArgumentException("Console IP is required.", nameof(consoleIp));
        }

        var config = new FtpConfig
        {
            ValidateAnyCertificate = true,
            SslProtocols = SslProtocols.None,
            DataConnectionEncryption = false,
            DataConnectionType = FtpDataConnectionType.PASV
        };

        using var conn = new AsyncFtpClient(consoleIp, "anonymous", "anonymous", port, config);
        await conn.Connect(cancellationToken);
        var results = new List<LibraryItemDto>();
        var remoteFolders = new[] { "/dev_hdd0/GAMES", "/dev_hdd0/PS3ISO", "/dev_hdd0/PS2ISO", "/dev_hdd0/PSXISO", "/dev_hdd0/PSPISO" };

        foreach (var remoteFolder in remoteFolders)
        {
            if (!await conn.DirectoryExists(remoteFolder))
            {
                continue;
            }

            var listing = await conn.GetListing(remoteFolder);
            foreach (var item in listing)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _jobService.Update(jobId, JobState.Running, Math.Min(95, results.Count % 100), $"Scanning {remoteFolder}");

                if (item.Type == FtpObjectType.Directory && remoteFolder.EndsWith("/GAMES", StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new LibraryItemDto(
                        Id: Guid.NewGuid().ToString("N"),
                        Platform: "PS3",
                        Title: item.Name,
                        TitleId: null,
                        ContentId: null,
                        Region: null,
                        Category: null,
                        Version: null,
                        AppVersion: null,
                        RequiredFirmware: null,
                        Size: null,
                        FilePath: null,
                        FolderPath: item.FullName,
                        ItemType: LibraryItemType.Backup,
                        Location: LibraryItemLocation.Remote,
                        BackgroundImagePath: null,
                        CoverImagePath: null
                    ));
                }
                else if (item.Type == FtpObjectType.File)
                {
                    var type = MapByPathAndExtension(item.FullName, Path.GetExtension(item.Name).ToLowerInvariant());
                    if (type == LibraryItemType.Unknown)
                    {
                        continue;
                    }

                    results.Add(new LibraryItemDto(
                        Id: Guid.NewGuid().ToString("N"),
                        Platform: "PS3",
                        Title: Path.GetFileNameWithoutExtension(item.Name),
                        TitleId: null,
                        ContentId: null,
                        Region: null,
                        Category: null,
                        Version: null,
                        AppVersion: null,
                        RequiredFirmware: null,
                        Size: item.Size >= 0 ? HumanBytes(item.Size) : null,
                        FilePath: item.FullName,
                        FolderPath: null,
                        ItemType: type,
                        Location: LibraryItemLocation.Remote,
                        BackgroundImagePath: null,
                        CoverImagePath: null
                    ));
                }
            }
        }

        await conn.Disconnect(cancellationToken);
        return results;
    }

    private static LibraryItemType MapByPathAndExtension(string path, string ext)
    {
        var normalized = path.Replace('\\', '/').ToLowerInvariant();

        if (normalized.Contains("/games/") || normalized.EndsWith("/games"))
        {
            return LibraryItemType.Backup;
        }

        if (PkgExtensions.Contains(ext))
        {
            return LibraryItemType.Pkg;
        }

        if (normalized.Contains("/ps3iso/") && Ps3IsoExtensions.Contains(ext))
        {
            return LibraryItemType.Ps3Iso;
        }

        if (normalized.Contains("/ps2iso/"))
        {
            return LibraryItemType.Ps2Iso;
        }

        if (normalized.Contains("/psxiso/"))
        {
            return LibraryItemType.PsxIso;
        }

        if (normalized.Contains("/pspiso/"))
        {
            return LibraryItemType.PspIso;
        }

        return LibraryItemType.Unknown;
    }

    private static string HumanBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var size = (double)bytes;
        var order = 0;
        while (size >= 1024 && order < units.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {units[order]}";
    }
}
