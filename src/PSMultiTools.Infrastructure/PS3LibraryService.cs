using FluentFTP;
using PSMultiTools.Core;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Authentication;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;

namespace PSMultiTools.Infrastructure;

public sealed class PS3LibraryService : IPS3LibraryService
{
    private sealed record ArchiveDirectoryEntry(string Href, string? Size, DateTimeOffset? ModifiedAt);
    private sealed record ArchivePreGroup(
        string Platform,
        string Bucket,
        string Title,
        string TitleKey,
        ArchiveVariantDto[] Variants
    );
    private static readonly string[] Ps3IsoExtensions = [".iso"];
    private static readonly string[] PkgExtensions = [".pkg"];
    private readonly List<LibraryItemDto> _items = [];
    private readonly object _syncRoot = new();
    private readonly IJobService _jobService;
    private readonly IConfigService _configService;
    private readonly IToolProcessRunner _toolProcessRunner;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private static readonly HttpClient ArchiveHttp = new() { Timeout = TimeSpan.FromSeconds(60) };
    private string? _lastFtpIp;
    private readonly SemaphoreSlim _archiveIndexGate = new(1, 1);
    private readonly object _archiveSyncRoot = new();
    private DateTimeOffset _archiveIndexedAt = DateTimeOffset.MinValue;
    private List<ArchiveVariantDto> _archiveIndex = [];

    // Point this to your private archive host.
    private const string PrivateArchiveBaseUrl = "https://myrient.erista.me";
    private static readonly string[] PrivateArchiveRoots =
    [
        "/files/Redump/Sony%20-%20PlayStation/",
        "/files/Redump/Sony%20-%20PlayStation%202/",
        "/files/Redump/Sony%20-%20PlayStation%203/",
        "/files/No-Intro/Sony%20-%20PlayStation%203%20%28PSN%29%20%28Content%29/",
        "/files/No-Intro/Sony%20-%20PlayStation%203%20%28PSN%29%20%28Updates%29/",
        "/files/No-Intro/Sony%20-%20PlayStation%203%20%28PSN%29%20%28Themes%29/",
        "/files/No-Intro/Sony%20-%20PlayStation%203%20%28PSN%29%20%28Avatars%29/",
        "/files/No-Intro/Sony%20-%20PlayStation%203%20%28PSN%29/",
        "/files/No-Intro/Sony%20-%20PlayStation%20Portable%20%28PSN%29/"
    ];
    private const string DefaultRcloneRemoteName = "PrivateLibraryProvider";

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
        new("download.myrient", "Myrient Downloader", "Downloads", false),
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

    public PS3LibraryService(IJobService jobService, IConfigService configService, IToolProcessRunner toolProcessRunner)
    {
        _jobService = jobService;
        _configService = configService;
        _toolProcessRunner = toolProcessRunner;
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

                _lastFtpIp = request.ConsoleIp;
                try { await SaveConsoleAsync(request.ConsoleIp, null, cancellationToken); } catch { }

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
            case "download.myrient":
                _jobService.Update(jobId, JobState.Running, 60, "Myrient downloader workspace requested.");
                await Task.Delay(120, cancellationToken);
                break;
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

        // Initialize collections
        
        // Use sequential scan because AsyncFtpClient might not support concurrent operations on a single connection instance
        foreach (var remoteFolder in remoteFolders)
        {
            if (!await conn.DirectoryExists(remoteFolder, cancellationToken)) continue;

            var listing = await conn.GetListing(remoteFolder, cancellationToken);
            foreach (var item in listing)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _jobService.Update(jobId, JobState.Running, Math.Min(95, results.Count % 100), $"Scanning {remoteFolder}");

                if (item.Type == FtpObjectType.Directory && remoteFolder.EndsWith("/GAMES", StringComparison.OrdinalIgnoreCase))
                {
                    string? titleId = null;
                    string? title = item.Name;
                    string? version = null;
                    string? appVer = null;

                    try
                    {
                        // Try root first (extracted/flat format), then PS3_GAME/ (standard disc backup format)
                        string[] sfoPaths = [$"{item.FullName}/PARAM.SFO", $"{item.FullName}/PS3_GAME/PARAM.SFO"];
                        foreach (var sfoPath in sfoPaths)
                        {
                            if (!await conn.FileExists(sfoPath, cancellationToken)) continue;
                            using var ms = new MemoryStream();
                            if (await conn.DownloadStream(ms, sfoPath, token: cancellationToken))
                            {
                                var sfo = ParamSfo.Parse(ms.ToArray());
                                if (sfo != null)
                                {
                                    titleId = sfo.TitleId;
                                    title = sfo.Title ?? item.Name;
                                    version = sfo.Version;
                                    appVer = sfo.AppVersion;
                                    break;
                                }
                            }
                        }
                    }
                    catch { }

                    results.Add(new LibraryItemDto(
                        Id: Guid.NewGuid().ToString("N"),
                        Platform: "PS3",
                        Title: title ?? item.Name,
                        TitleId: titleId,
                        ContentId: null,
                        Region: null,
                        Category: null,
                        Version: version,
                        AppVersion: appVer,
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
                    if (type == LibraryItemType.Unknown) continue;

                    string? titleId = null;
                    // Try to match [BLES12345] pattern
                    var match = System.Text.RegularExpressions.Regex.Match(item.Name, @"\[([BNC][LPE][EJUSA]\d{5})\]");
                    if (match.Success) titleId = match.Groups[1].Value;

                    results.Add(new LibraryItemDto(
                        Id: Guid.NewGuid().ToString("N"),
                        Platform: "PS3",
                        Title: Path.GetFileNameWithoutExtension(item.Name),
                        TitleId: titleId,
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
        
        // Scan /dev_hdd0/game/ for PKG-installed content: PS1 Classics, PS2 Classics, PSN PS3 games, Minis, PSP titles
        const string gameFolder = "/dev_hdd0/game";
        if (await conn.DirectoryExists(gameFolder, cancellationToken))
        {
            var gameListing = await conn.GetListing(gameFolder, cancellationToken);
            foreach (var gameDir in gameListing)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (gameDir.Type != FtpObjectType.Directory) continue;

                _jobService.Update(jobId, JobState.Running, Math.Min(95, results.Count % 100), $"Scanning {gameFolder}");

                string? titleId = null;
                string? title = gameDir.Name;
                string? version = null;
                string? appVer = null;
                string? category = null;
                string? contentId = null;

                try
                {
                    // PKG-installed games always have PARAM.SFO at the root of the title folder
                    var sfoPath = $"{gameDir.FullName}/PARAM.SFO";
                    if (await conn.FileExists(sfoPath, cancellationToken))
                    {
                        using var ms = new MemoryStream();
                        if (await conn.DownloadStream(ms, sfoPath, token: cancellationToken))
                        {
                            var sfo = ParamSfo.Parse(ms.ToArray());
                            if (sfo != null)
                            {
                                titleId = sfo.TitleId;
                                title = sfo.Title ?? gameDir.Name;
                                version = sfo.Version;
                                appVer = sfo.AppVersion;
                                category = sfo.Category;
                                contentId = sfo.ContentId;
                            }
                        }
                    }
                }
                catch { }

                // Skip non-game content (DLC, themes, avatars, etc.)
                // Only include playable titles with a known game category
                var itemType = category switch
                {
                    "ME" => LibraryItemType.PsxIso,   // PS1 Classic (PSone)
                    "2P" => LibraryItemType.Ps2Iso,   // PS2 Classic
                    "MN" => LibraryItemType.PspIso,   // Mini (PSP/PS3 Mini)
                    "EG" => LibraryItemType.PspIso,   // PSP game via PSN
                    "HG" => LibraryItemType.Backup,   // PS3 downloadable game
                    "GD" => LibraryItemType.Backup,   // PS3 game data
                    "DG" => LibraryItemType.Backup,   // PS3 disc game
                    _ => LibraryItemType.Unknown
                };

                if (itemType == LibraryItemType.Unknown) continue;

                // Skip if it's a PS3 game already found in /dev_hdd0/GAMES (avoid duplicates by titleId)
                if (itemType == LibraryItemType.Backup && titleId is not null &&
                    results.Any(r => r.TitleId == titleId && r.ItemType == LibraryItemType.Backup))
                    continue;

                var platform = itemType switch
                {
                    LibraryItemType.PsxIso => "PS1",
                    LibraryItemType.Ps2Iso => "PS2",
                    LibraryItemType.PspIso => "PSP",
                    _ => "PS3"
                };

                results.Add(new LibraryItemDto(
                    Id: Guid.NewGuid().ToString("N"),
                    Platform: platform,
                    Title: title ?? gameDir.Name,
                    TitleId: titleId,
                    ContentId: contentId,
                    Region: null,
                    Category: category,
                    Version: version,
                    AppVersion: appVer,
                    RequiredFirmware: null,
                    Size: null,
                    FilePath: null,
                    FolderPath: gameDir.FullName,
                    ItemType: itemType,
                    Location: LibraryItemLocation.Remote,
                    BackgroundImagePath: null,
                    CoverImagePath: null
                ));
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

    public async Task<byte[]?> GetCoverImageAsync(string itemId, CancellationToken cancellationToken)
    {
        LibraryItemDto? item;
        lock (_syncRoot)
        {
            item = _items.FirstOrDefault(i => i.Id == itemId);
        }

        if (item is null) return null;

        // 0. For local items, read ICON0.PNG directly from disk (fastest, no network)
        if (item.Location == LibraryItemLocation.Local && item.FolderPath is not null)
        {
            var icon0 = Path.Combine(item.FolderPath, "ICON0.PNG");
            if (File.Exists(icon0))
            {
                return await File.ReadAllBytesAsync(icon0, cancellationToken);
            }
        }

        if (item.Location == LibraryItemLocation.Remote && _lastFtpIp is not null)
        {
            try
            {
                var ftpConfig = new FtpConfig
                {
                    ValidateAnyCertificate = true,
                    SslProtocols = SslProtocols.None,
                    DataConnectionEncryption = false,
                    DataConnectionType = FtpDataConnectionType.PASV,
                    ConnectTimeout = 3000,
                    ReadTimeout = 3000
                };
                using var conn = new AsyncFtpClient(_lastFtpIp, "anonymous", "anonymous", 21, ftpConfig);
                await conn.Connect(cancellationToken);

                // 1. Try ICON0.PNG if it's a folder game (root first, then PS3_GAME/ for disc backups)
                if (item.FolderPath is not null)
                {
                    string[] iconPaths = [$"{item.FolderPath}/ICON0.PNG", $"{item.FolderPath}/PS3_GAME/ICON0.PNG"];
                    foreach (var iconPath in iconPaths)
                    {
                        if (!await conn.FileExists(iconPath, cancellationToken)) continue;
                        using var ms = new MemoryStream();
                        if (await conn.DownloadStream(ms, iconPath, token: cancellationToken))
                        {
                            await conn.Disconnect(cancellationToken);
                            return ms.ToArray();
                        }
                    }
                }

                // 2. Try webMAN cache paths if TitleId is known
                if (item.TitleId is not null)
                {
                    var titleId = item.TitleId.ToUpperInvariant();
                    // Some common paths where webMAN/multiMAN stores covers
                    var wmtPath = $"/dev_hdd0/tmp/wmt/{titleId}.PNG";
                    var mmPath = $"/dev_hdd0/game/BLES80608/USRDIR/covers/{titleId}.JPG"; // Standard multiMAN path

                    // Try wmt/ first (PNG)
                    if (await conn.FileExists(wmtPath, cancellationToken))
                    {
                         using var ms = new MemoryStream();
                         if (await conn.DownloadStream(ms, wmtPath, token: cancellationToken))
                         {
                             await conn.Disconnect(cancellationToken);
                             return ms.ToArray();
                         }
                    }

                    // Try multiMAN/covers (JPG)
                    if (await conn.FileExists(mmPath, cancellationToken))
                    {
                         using var ms = new MemoryStream();
                         if (await conn.DownloadStream(ms, mmPath, token: cancellationToken))
                         {
                             await conn.Disconnect(cancellationToken);
                             return ms.ToArray();
                         }
                    }
                }

                await conn.Disconnect(cancellationToken);
            }
            catch { }
        }

        // 3. webMAN HTTP icon endpoint: http://{ip}/icon.ps3/{TITLEID}
        if (item.TitleId is not null)
        {
            var config = await _configService.GetConfigAsync(cancellationToken);
            var ps3Ip = _lastFtpIp ?? config.PS3Ip;
            if (ps3Ip is not null)
            {
                try
                {
                    var wmUrl = $"http://{ps3Ip}/icon.ps3/{Uri.EscapeDataString(item.TitleId.ToUpperInvariant())}";
                    var bytes = await Http.GetByteArrayAsync(wmUrl, cancellationToken);
                    if (bytes.Length > 100) return bytes;
                }
                catch { }
            }
        }

        // 4. Fallback to online DB by TitleID
        if (item.TitleId is not null)
        {
            try
            {
                var url = $"https://rpcs3.net/compat/api/covers/{Uri.EscapeDataString(item.TitleId)}";
                var bytes = await Http.GetByteArrayAsync(url, cancellationToken);
                if (bytes.Length > 100) return bytes;
            }
            catch { }
        }

        // 5. Fallback by title lookup on rpcs3.net (PS3 items only — rpcs3.net only covers PS3)
        var isPs3Item = item.ItemType is LibraryItemType.Backup or LibraryItemType.Ps3Iso or LibraryItemType.Pkg;
        if (isPs3Item && !string.IsNullOrWhiteSpace(item.Title))
        {
            try
            {
                var lookupUrl = $"https://rpcs3.net/compatibility?g={Uri.EscapeDataString(item.Title)}";
                var html = await Http.GetStringAsync(lookupUrl, cancellationToken);
                // Try to locate a likely PS3 serial in the page and then fetch cover by serial.
                var serialMatch = Regex.Match(html, @"\b([BNC][LPEJUSA][A-Z]\d{5})\b", RegexOptions.IgnoreCase);
                if (serialMatch.Success)
                {
                    var serial = serialMatch.Groups[1].Value.ToUpperInvariant();
                    var coverUrl = $"https://rpcs3.net/compat/api/covers/{Uri.EscapeDataString(serial)}";
                    var bytes = await Http.GetByteArrayAsync(coverUrl, cancellationToken);
                    if (bytes.Length > 100) return bytes;
                }
            }
            catch { }
        }

        // 6. Libretro thumbnail by title (all platforms including PS3)
        var libretroSystem = item.ItemType switch
        {
            LibraryItemType.Ps2Iso => "Sony_-_PlayStation_2",
            LibraryItemType.PsxIso => "Sony_-_PlayStation",
            LibraryItemType.PspIso => "Sony_-_PlayStation_Portable",
            LibraryItemType.Ps3Iso or LibraryItemType.Backup => "Sony_-_PlayStation_3",
            _ => null
        };
        if (libretroSystem is not null && !string.IsNullOrWhiteSpace(item.Title))
        {
            try
            {
                var libretroUrl = $"https://raw.githubusercontent.com/libretro-thumbnails/{libretroSystem}/master/Named_Boxarts/{Uri.EscapeDataString(item.Title)}.png";
                var bytes = await Http.GetByteArrayAsync(libretroUrl, cancellationToken);
                if (bytes.Length > 100) return bytes;
            }
            catch { }
        }

        return null;
    }

    public async Task<IReadOnlyCollection<SavedConsoleDto>> GetSavedConsolesAsync(CancellationToken cancellationToken)
    {
        var config = await _configService.GetConfigAsync(cancellationToken);
        var savedIps = config.SavedPs3Ips ?? [];
        var results = new List<SavedConsoleDto>();

        foreach (var ip in savedIps)
        {
            var online = await IsPortOpenAsync(ip, 21, 500);
            results.Add(new SavedConsoleDto(ip, null, DateTimeOffset.UtcNow, online));
        }

        return results;
    }

    public async Task SaveConsoleAsync(string ip, string? label, CancellationToken cancellationToken)
    {
        var config = await _configService.GetConfigAsync(cancellationToken);
        var existing = config.SavedPs3Ips?.ToList() ?? [];
        if (!existing.Contains(ip, StringComparer.OrdinalIgnoreCase))
        {
            existing.Add(ip);
        }

        await _configService.UpdateConfigAsync(config with { SavedPs3Ips = existing.ToArray() }, cancellationToken);
    }

    public async Task<IReadOnlyCollection<NetworkScanResultDto>> DiscoverConsolesAsync(string? subnetPrefix, CancellationToken cancellationToken)
    {
        var prefix = subnetPrefix;
        if (string.IsNullOrWhiteSpace(prefix))
        {
            try
            {
                var addrs = System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName());
                var local = addrs.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                if (local is not null)
                {
                    var parts = local.ToString().Split('.');
                    prefix = $"{parts[0]}.{parts[1]}.{parts[2]}";
                }
            }
            catch { }
        }

        if (string.IsNullOrWhiteSpace(prefix)) return [];

        var results = new List<NetworkScanResultDto>();
        var tasks = new List<Task>();
        var semaphore = new SemaphoreSlim(50);

        for (var i = 1; i <= 254; i++)
        {
            var ip = $"{prefix}.{i}";
            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var open = await IsPortOpenAsync(ip, 21, 300);
                    sw.Stop();
                    if (open)
                    {
                        lock (results)
                        {
                            results.Add(new NetworkScanResultDto(ip, true, (int)sw.ElapsedMilliseconds));
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken));
        }

        await Task.WhenAll(tasks);
        return results;
    }

    public Task<JobStartResponseDto> StartPrivateArchiveRcloneDownloadAsync(RcloneDownloadRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SourceUrl))
        {
            throw new ArgumentException("SourceUrl is required.");
        }

        if (string.IsNullOrWhiteSpace(request.DestinationFolder))
        {
            throw new ArgumentException("DestinationFolder is required.");
        }

        var jobId = _jobService.Create("Private Archive Download (rclone)");
        _ = Task.Run(async () =>
        {
            try
            {
                _jobService.Update(jobId, JobState.Running, 5, "Preparing rclone command");

                if (request.InstallToPs3)
                {
                    var installIp = await ResolveInstallIpAsync(request, cancellationToken);
                    var reachable = await IsPortOpenAsync(installIp, 21, 2000);
                    if (!reachable)
                    {
                        throw new InvalidOperationException($"PS3 {installIp} is not reachable on FTP port 21.");
                    }
                }

                var source = BuildRcloneSourceFromUrl(request.SourceUrl, request.RemoteName);
                var destination = request.DestinationFolder;
                Directory.CreateDirectory(destination);
                var args = $"copy {Quote(source)} {Quote(destination)} --multi-thread-streams 0 -vP";

                _jobService.Update(jobId, JobState.Running, 20, $"Running: rclone {args}");
                var exitCode = await _toolProcessRunner.RunAsync("rclone", args, cancellationToken);
                if (exitCode != 0)
                {
                    throw new InvalidOperationException($"rclone failed with exit code {exitCode}.");
                }

                if (request.InstallToPs3)
                {
                    _jobService.Update(jobId, JobState.Running, 70, "Download done. Uploading to PS3.");
                    var installIp = await ResolveInstallIpAsync(request, cancellationToken);
                    await UploadDownloadedToPs3Async(
                        destination,
                        request.SourceUrl,
                        request.Platform,
                        request.ContentType,
                        installIp,
                        cancellationToken);
                }

                _jobService.Update(jobId, JobState.Completed, 100, request.InstallToPs3 ? "Download + PS3 upload completed." : "Download completed.");
            }
            catch (Exception ex)
            {
                _jobService.Update(jobId, JobState.Failed, 100, $"Download failed: {ex.Message}");
            }
        }, cancellationToken);

        return Task.FromResult(new JobStartResponseDto(jobId, "Private Archive Download (rclone)"));
    }

    public async Task<ArchiveCatalogDto> GetPrivateArchiveCatalogAsync(string? search, string? platform, string? contentType, bool refresh, CancellationToken cancellationToken)
    {
        await EnsurePrivateArchiveIndexAsync(refresh, cancellationToken);

        List<ArchiveVariantDto> variants;
        DateTimeOffset indexedAt;
        lock (_archiveSyncRoot)
        {
            variants = [.. _archiveIndex];
            indexedAt = _archiveIndexedAt;
        }

        IEnumerable<ArchiveVariantDto> query = variants;
        if (!string.IsNullOrWhiteSpace(platform))
        {
            query = query.Where(v => v.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(contentType))
        {
            query = query.Where(v => v.ContentType.Equals(contentType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(v =>
                v.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                BuildBaseTitle(v.DisplayName).Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var preGroups = query
            .GroupBy(v => $"{BuildGroupingTitleKey(v.DisplayName)}|{v.Platform}|{GetGroupingBucket(v.Platform, v.ContentType)}", StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var first = g.First();
                var title = BuildBaseTitle(first.DisplayName);
                var key = BuildGroupingTitleKey(title);
                return new ArchivePreGroup(
                    Platform: first.Platform,
                    Bucket: GetGroupingBucket(first.Platform, first.ContentType),
                    Title: title,
                    TitleKey: key,
                    Variants: g.OrderBy(v => v.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray()
                );
            })
            .ToArray();

        var mergedPreGroups = MergeLocalizedPreGroups(preGroups);

        var grouped = mergedPreGroups
            .Select(g =>
            {
                var first = g.Variants[0];
                var sortedVariants = g.Variants.OrderBy(v => v.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
                var distinctTitles = sortedVariants
                    .Select(v => BuildBaseTitle(v.DisplayName))
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var title = ChooseGroupTitle(distinctTitles);
                var alternateTitles = distinctTitles
                    .Where(t => !t.Equals(title, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var titleId = sortedVariants
                    .Select(v => TryExtractTitleId(v.DisplayName))
                    .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
                var distinctContentTypes = sortedVariants
                    .Select(v => v.ContentType)
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var groupContentType = distinctContentTypes.Length == 1 ? distinctContentTypes[0] : "Mixed";
                var libretroSystem = first.Platform switch
                {
                    "PS1" => "Sony_-_PlayStation",
                    "PS2" => "Sony_-_PlayStation_2",
                    "PSP" => "Sony_-_PlayStation_Portable",
                    _ => null
                };
                var coverUrl = libretroSystem is not null
                    ? $"https://raw.githubusercontent.com/libretro-thumbnails/{libretroSystem}/master/Named_Boxarts/{Uri.EscapeDataString(Path.GetFileNameWithoutExtension(first.DisplayName))}.png"
                    : null;
                return new ArchiveTitleGroupDto(
                    Id: ComputeStableId(first.Platform + "|" + groupContentType + "|" + BuildGroupingTitleKey(title)),
                    Title: title,
                    Platform: first.Platform,
                    ContentType: groupContentType,
                    TitleId: titleId,
                    CoverUrl: coverUrl,
                    AlternateTitles: alternateTitles,
                    Variants: sortedVariants
                );
            })
            .OrderBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ArchiveCatalogDto(
            IndexedAt: indexedAt,
            TotalGroups: grouped.Length,
            TotalVariants: grouped.Sum(g => g.Variants.Count),
            Groups: grouped
        );
    }

    private async Task EnsurePrivateArchiveIndexAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        var shouldRefresh = forceRefresh;
        lock (_archiveSyncRoot)
        {
            if (!shouldRefresh)
            {
                shouldRefresh = _archiveIndex.Count == 0 || DateTimeOffset.UtcNow - _archiveIndexedAt > TimeSpan.FromHours(24);
            }
        }

        if (!shouldRefresh)
        {
            return;
        }

        await _archiveIndexGate.WaitAsync(cancellationToken);
        try
        {
            lock (_archiveSyncRoot)
            {
                if (!forceRefresh && _archiveIndex.Count > 0 && DateTimeOffset.UtcNow - _archiveIndexedAt <= TimeSpan.FromHours(24))
                {
                    return;
                }
            }

            var results = new List<ArchiveVariantDto>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var root in PrivateArchiveRoots)
            {
                await CrawlPrivateArchiveDirectoryAsync(root, results, visited, 0, cancellationToken);
            }

            lock (_archiveSyncRoot)
            {
                _archiveIndex = results;
                _archiveIndexedAt = DateTimeOffset.UtcNow;
            }
        }
        finally
        {
            _archiveIndexGate.Release();
        }
    }

    private async Task CrawlPrivateArchiveDirectoryAsync(string relativePath, List<ArchiveVariantDto> output, HashSet<string> visited, int depth, CancellationToken cancellationToken)
    {
        if (depth > 8 || !visited.Add(relativePath))
        {
            return;
        }

        var uri = BuildArchiveUri(relativePath);
        string html;
        try
        {
            html = await ArchiveHttp.GetStringAsync(uri, cancellationToken);
        }
        catch
        {
            return;
        }

        foreach (var entry in ExtractDirectoryEntries(html))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var href = entry.Href;
            if (href.StartsWith("../", StringComparison.Ordinal) || href.StartsWith("?", StringComparison.Ordinal) || href.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            Uri childUri;
            try
            {
                childUri = new Uri(uri, WebUtility.HtmlDecode(href));
            }
            catch
            {
                continue;
            }

            if (!childUri.Host.Equals(uri.Host, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var childPath = childUri.AbsolutePath;
            if (href.EndsWith("/", StringComparison.Ordinal))
            {
                await CrawlPrivateArchiveDirectoryAsync(childPath, output, visited, depth + 1, cancellationToken);
                continue;
            }

            if (!IsArchiveCandidate(childPath))
            {
                continue;
            }

            var fileName = Uri.UnescapeDataString(Path.GetFileName(childPath));
            var (region, versionTag) = ExtractRegionAndVersion(fileName);
            var platform = InferPlatform(childPath);
            var kind = InferContentType(childPath, fileName, platform);
            output.Add(new ArchiveVariantDto(
                Id: ComputeStableId(childUri.ToString()),
                DisplayName: fileName,
                Url: childUri.ToString(),
                Platform: platform,
                ContentType: kind,
                Region: region,
                VersionTag: versionTag,
                Size: entry.Size,
                ModifiedAt: entry.ModifiedAt
            ));
        }
    }

    private static Uri BuildArchiveUri(string relativePath)
    {
        var baseUri = new Uri(PrivateArchiveBaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
        return new Uri(baseUri, relativePath.TrimStart('/'));
    }

    private static IReadOnlyCollection<ArchiveDirectoryEntry> ExtractDirectoryEntries(string html)
    {
        var rowRegex = new Regex(
            "<tr>\\s*<td\\s+class=\"link\">\\s*<a\\s+href=\"([^\"]+)\"[^>]*>.*?</a>\\s*</td>\\s*<td\\s+class=\"size\">\\s*([^<]*)\\s*</td>\\s*<td\\s+class=\"date\">\\s*([^<]*)\\s*</td>\\s*</tr>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var matches = rowRegex.Matches(html);
        var entries = new List<ArchiveDirectoryEntry>(matches.Count);
        foreach (Match match in matches)
        {
            if (!match.Success || match.Groups.Count < 4)
            {
                continue;
            }

            var href = WebUtility.HtmlDecode(match.Groups[1].Value);
            var sizeRaw = WebUtility.HtmlDecode(match.Groups[2].Value).Trim();
            var dateRaw = WebUtility.HtmlDecode(match.Groups[3].Value).Trim();

            if (href is "../" or "./")
            {
                continue;
            }

            DateTimeOffset? parsedDate = null;
            if (!string.IsNullOrWhiteSpace(dateRaw) &&
                !dateRaw.Equals("-", StringComparison.Ordinal) &&
                DateTime.TryParseExact(dateRaw, "dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
            {
                parsedDate = new DateTimeOffset(dt);
            }

            var size = string.IsNullOrWhiteSpace(sizeRaw) || sizeRaw == "-" ? null : sizeRaw;
            entries.Add(new ArchiveDirectoryEntry(href, size, parsedDate));
        }

        return entries;
    }

    private static bool IsArchiveCandidate(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".zip" or ".pkg" or ".iso" or ".7z" or ".rar";
    }

    private static string BuildBaseTitle(string displayName)
    {
        var title = Path.GetFileNameWithoutExtension(displayName);
        title = Regex.Replace(title, @"\s*\([^)]*\)", " ");
        title = title.Replace('_', ' ');
        title = Regex.Replace(title, @"\s*[-–—]\s*", " - ");
        title = Regex.Replace(title, @"\s+", " ").Trim();
        title = Regex.Replace(title, @"\s*-\s*A Criterion Game$", string.Empty, RegexOptions.IgnoreCase).Trim();
        return title;
    }

    private static string BuildGroupingTitleKey(string displayNameOrTitle)
    {
        var title = BuildBaseTitle(displayNameOrTitle);
        title = title.ToLowerInvariant();
        title = Regex.Replace(title, @"[^\p{L}\p{Nd}]+", " ");
        title = Regex.Replace(title, @"\s+", " ").Trim();
        return title;
    }

    private static string ChooseGroupTitle(IReadOnlyCollection<string> titles)
    {
        var ordered = titles
            .OrderByDescending(GetEnglishTitleScore)
            .ThenBy(v => v.Length)
            .ThenBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return ordered.FirstOrDefault() ?? "Unknown Title";
    }

    private static ArchivePreGroup[] MergeLocalizedPreGroups(ArchivePreGroup[] input)
    {
        var groups = input.ToList();
        var changed = true;
        while (changed)
        {
            changed = false;
            for (var i = 0; i < groups.Count && !changed; i++)
            {
                for (var j = i + 1; j < groups.Count && !changed; j++)
                {
                    var a = groups[i];
                    var b = groups[j];
                    if (!ShouldMergeLocalizedGroups(a, b))
                    {
                        continue;
                    }

                    var mergedVariants = a.Variants.Concat(b.Variants)
                        .GroupBy(v => v.Id, StringComparer.OrdinalIgnoreCase)
                        .Select(g => g.First())
                        .OrderBy(v => v.DisplayName, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    var mergedTitle = ChooseGroupTitle([a.Title, b.Title]);
                    groups[i] = new ArchivePreGroup(
                        Platform: a.Platform,
                        Bucket: a.Bucket,
                        Title: mergedTitle,
                        TitleKey: BuildGroupingTitleKey(mergedTitle),
                        Variants: mergedVariants);
                    groups.RemoveAt(j);
                    changed = true;
                }
            }
        }

        return [.. groups];
    }

    private static bool ShouldMergeLocalizedGroups(ArchivePreGroup a, ArchivePreGroup b)
    {
        if (!a.Platform.Equals(b.Platform, StringComparison.OrdinalIgnoreCase) ||
            !a.Bucket.Equals(b.Bucket, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (a.TitleKey.Equals(b.TitleKey, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var prefixA = GetTitlePrefix(a.Title);
        var prefixB = GetTitlePrefix(b.Title);
        if (string.IsNullOrWhiteSpace(prefixA) ||
            !prefixA.Equals(prefixB, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var languageA = DetectLanguageBucket(GetTitleSuffix(a.Title));
        var languageB = DetectLanguageBucket(GetTitleSuffix(b.Title));
        if (languageA == languageB)
        {
            return false;
        }

        var sizeA = EstimateMedianBytes(a.Variants);
        var sizeB = EstimateMedianBytes(b.Variants);
        if (sizeA <= 0 || sizeB <= 0)
        {
            return false;
        }

        var diff = Math.Abs(sizeA - sizeB) / (double)Math.Max(sizeA, sizeB);
        return diff <= 0.03d;
    }

    private static string GetTitlePrefix(string title)
    {
        var parts = title.Split(" - ", 2, StringSplitOptions.TrimEntries);
        return parts.Length > 1 ? parts[0] : string.Empty;
    }

    private static string GetTitleSuffix(string title)
    {
        var parts = title.Split(" - ", 2, StringSplitOptions.TrimEntries);
        return parts.Length > 1 ? parts[1] : parts[0];
    }

    private static long EstimateMedianBytes(IReadOnlyCollection<ArchiveVariantDto> variants)
    {
        var values = variants
            .Select(v => ParseApproxBytes(v.Size))
            .Where(v => v > 0)
            .OrderBy(v => v)
            .ToArray();
        if (values.Length == 0) return 0;
        return values[values.Length / 2];
    }

    private static long ParseApproxBytes(string? size)
    {
        if (string.IsNullOrWhiteSpace(size))
        {
            return 0;
        }

        var match = Regex.Match(size, @"^\s*([0-9]+(?:\.[0-9]+)?)\s*([KMGTP])i?B\s*$", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return 0;
        }

        if (!double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return 0;
        }

        var unit = match.Groups[2].Value.ToUpperInvariant();
        var factor = unit switch
        {
            "K" => Math.Pow(1024d, 1),
            "M" => Math.Pow(1024d, 2),
            "G" => Math.Pow(1024d, 3),
            "T" => Math.Pow(1024d, 4),
            "P" => Math.Pow(1024d, 5),
            _ => 0d
        };
        if (factor <= 0d)
        {
            return 0;
        }

        return (long)(value * factor);
    }

    private static int GetEnglishTitleScore(string title)
    {
        var text = title.ToLowerInvariant();
        var score = 0;
        if (Regex.IsMatch(text, @"\b(the|and|of|for|with|is|not|world)\b"))
        {
            score += 2;
        }

        if (Regex.IsMatch(text, @"[a-z]"))
        {
            score += 1;
        }

        if (Regex.IsMatch(text, @"\b(le|la|les|el|los|las|die|der|das)\b"))
        {
            score -= 2;
        }

        return score;
    }

    private static string DetectLanguageBucket(string text)
    {
        var t = text.ToLowerInvariant();
        if (Regex.IsMatch(t, @"\b(the|and|of|for|with|is|not|world)\b"))
        {
            return "en";
        }

        if (Regex.IsMatch(t, @"\b(le|la|les|de|des|du|ne|pas)\b"))
        {
            return "fr";
        }

        if (Regex.IsMatch(t, @"\b(el|la|los|las|es|de|del)\b"))
        {
            return "es";
        }

        if (Regex.IsMatch(t, @"\b(die|der|das|und|ist|nicht)\b"))
        {
            return "de";
        }

        return "unknown";
    }

    private static string InferPlatform(string path)
    {
        var normalized = Uri.UnescapeDataString(path).ToLowerInvariant();
        if (normalized.Contains("playstation 3"))
        {
            return "PS3";
        }

        if (normalized.Contains("playstation 2"))
        {
            return "PS2";
        }

        if (normalized.Contains("playstation portable"))
        {
            return "PSP";
        }

        if (normalized.Contains("sony - playstation/"))
        {
            return "PS1";
        }

        return "Unknown";
    }

    private static string InferContentType(string path, string fileName, string platform)
    {
        var normalized = Uri.UnescapeDataString(path).ToLowerInvariant();
        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        if (platform == "PS3")
        {
            if (normalized.Contains("(themes)"))
            {
                return "Themes";
            }

            if (normalized.Contains("(avatars)"))
            {
                return "Avatars";
            }

            if (normalized.Contains("(updates)"))
            {
                return "Updates";
            }

            if (normalized.Contains("(psn)") || ext == ".pkg")
            {
                return "Games (PKG)";
            }

            return "Games (ISO)";
        }

        if (normalized.Contains("(themes)"))
        {
            return "Themes";
        }

        if (normalized.Contains("(psn)"))
        {
            return "Games (PKG)";
        }

        return "Games (ISO)";
    }

    private static string GetGroupingBucket(string platform, string contentType)
    {
        if (!platform.Equals("PS3", StringComparison.OrdinalIgnoreCase))
        {
            return contentType;
        }

        if (contentType.Equals("Games (ISO)", StringComparison.OrdinalIgnoreCase) ||
            contentType.Equals("Games (PKG)", StringComparison.OrdinalIgnoreCase) ||
            contentType.Equals("Updates", StringComparison.OrdinalIgnoreCase))
        {
            return "PS3-GAME";
        }

        return contentType;
    }

    private static (string? Region, string? VersionTag) ExtractRegionAndVersion(string displayName)
    {
        var noExt = Path.GetFileNameWithoutExtension(displayName);
        var tags = Regex.Matches(noExt, "\\(([^)]*)\\)")
            .Select(m => m.Groups[1].Value.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToArray();

        if (tags.Length == 0)
        {
            return (null, null);
        }

        var regionKeywords = new[]
        {
            "Europe", "USA", "Japan", "Asia", "Australia", "UK", "World", "Germany", "France", "Spain", "Italy"
        };
        var region = tags.FirstOrDefault(t => regionKeywords.Any(k => t.Contains(k, StringComparison.OrdinalIgnoreCase)));
        var version = string.Join(" | ", tags.Where(t => !string.Equals(t, region, StringComparison.OrdinalIgnoreCase)));
        return (region, string.IsNullOrWhiteSpace(version) ? null : version);
    }

    private static string ComputeStableId(string input)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string? TryExtractTitleId(string displayName)
    {
        var match = Regex.Match(displayName, @"\b([A-Z]{4}\d{5})\b");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string BuildRcloneSourceFromUrl(string sourceUrl, string? remoteName)
    {
        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("SourceUrl is invalid.");
        }

        var path = Uri.UnescapeDataString(uri.AbsolutePath);
        const string filesPrefix = "/files/";
        if (!path.StartsWith(filesPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("SourceUrl must point to a /files/ path.");
        }

        var remotePath = path[filesPrefix.Length..];
        var resolvedRemote = string.IsNullOrWhiteSpace(remoteName) ? DefaultRcloneRemoteName : remoteName.Trim();
        return $"{resolvedRemote}:{remotePath}";
    }

    private async Task<string> ResolveInstallIpAsync(RcloneDownloadRequestDto request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Ps3Ip))
        {
            return request.Ps3Ip.Trim();
        }

        var config = await _configService.GetConfigAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(config.PS3Ip))
        {
            return config.PS3Ip;
        }

        throw new InvalidOperationException("Direct install selected but no PS3 IP is set.");
    }

    private async Task UploadDownloadedToPs3Async(
        string destinationFolder,
        string sourceUrl,
        string? platform,
        string? contentType,
        string ps3Ip,
        CancellationToken cancellationToken)
    {
        var sourceName = Uri.UnescapeDataString(Path.GetFileName(new Uri(sourceUrl).AbsolutePath));
        var localPath = Path.Combine(destinationFolder, sourceName);
        if (!File.Exists(localPath))
        {
            throw new FileNotFoundException($"Downloaded file not found: {localPath}");
        }

        var ext = Path.GetExtension(localPath).ToLowerInvariant();
        var remoteFolder = ResolvePs3DestinationFolder(ext, platform, contentType);
        if (remoteFolder is null)
        {
            throw new InvalidOperationException("Direct install currently supports only .pkg and .iso downloads.");
        }

        var remotePath = $"{remoteFolder}/{Path.GetFileName(localPath)}";
        var ftpConfig = new FtpConfig
        {
            ValidateAnyCertificate = true,
            SslProtocols = SslProtocols.None,
            DataConnectionEncryption = false,
            DataConnectionType = FtpDataConnectionType.PASV
        };

        using var conn = new AsyncFtpClient(ps3Ip, "anonymous", "anonymous", 21, ftpConfig);
        await conn.Connect(cancellationToken);
        await conn.CreateDirectory(remoteFolder, cancellationToken);
        await conn.UploadFile(localPath, remotePath, FtpRemoteExists.Overwrite, createRemoteDir: true, token: cancellationToken);
        await conn.Disconnect(cancellationToken);
    }

    private static string? ResolvePs3DestinationFolder(string ext, string? platform, string? contentType)
    {
        if (ext == ".pkg")
        {
            return "/dev_hdd0/packages";
        }

        if (ext != ".iso")
        {
            return null;
        }

        var normalizedPlatform = (platform ?? string.Empty).Trim().ToUpperInvariant();
        if (normalizedPlatform == "PS2")
        {
            return "/dev_hdd0/PS2ISO";
        }

        if (normalizedPlatform == "PS1")
        {
            return "/dev_hdd0/PSXISO";
        }

        if (normalizedPlatform == "PS3")
        {
            if (!string.IsNullOrWhiteSpace(contentType) && contentType.Contains("PKG", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return "/dev_hdd0/PS3ISO";
        }

        return "/dev_hdd0/PS3ISO";
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private static async Task<bool> IsPortOpenAsync(string host, int port, int timeoutMs)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            var completed = await Task.WhenAny(connectTask, Task.Delay(timeoutMs));
            return completed == connectTask && client.Connected;
        }
        catch
        {
            return false;
        }
    }
}
