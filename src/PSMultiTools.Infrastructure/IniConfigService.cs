using IniParser;
using IniParser.Model;
using PSMultiTools.Core;

namespace PSMultiTools.Infrastructure;

public sealed class IniConfigService : IConfigService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private readonly string _configPath;
    private readonly FileIniDataParser _parser = new();

    public IniConfigService()
    {
        _configPath = Path.Combine(Environment.CurrentDirectory, "psmt-config.ini");
    }

    public async Task<ConfigDto> GetConfigAsync(CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var data = await ReadOrCreateAsync(cancellationToken);
            return MapToDto(data);
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<ConfigDto> UpdateConfigAsync(ConfigDto newConfig, CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var data = await ReadOrCreateAsync(cancellationToken);
            ApplyDto(data, newConfig);
            _parser.WriteFile(_configPath, data);
            return MapToDto(data);
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task<IniData> ReadOrCreateAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_configPath))
        {
            await File.WriteAllTextAsync(_configPath, string.Empty, cancellationToken);
        }

        var data = _parser.ReadFile(_configPath);
        EnsureSections(data);
        return data;
    }

    private static void EnsureSections(IniData data)
    {
        data.Sections.AddSection("General");
        data.Sections.AddSection("PS3 Tools");
        data.Sections.AddSection("PS5 Tools");
        data.Sections.AddSection("PS5 Library");
    }

    private static ConfigDto MapToDto(IniData data)
    {
        return new ConfigDto(
            PS3Ip: data["PS3 Tools"]["IP"],
            PS5Ip: data["PS5 Tools"]["IP"],
            PS5FtpPort: ParseInt(data["PS5 Tools"]["FTPPort"]),
            PS5PayloadPort: ParseInt(data["PS5 Tools"]["PayloadPort"]),
            ScanThreads: ParseInt(data["PS5 Library"]["ScanThreads"]),
            AutoLibraryMusic: ParseBool(data["General"]["AutoLibraryMusic"], true),
            LoadIcons: ParseBool(data["PS5 Library"]["LoadIcons"], false),
            LoadBackgrounds: ParseBool(data["PS5 Library"]["LoadBackgrounds"], false),
            SkipFileChecks: ParseBool(data["PS5 Library"]["SkipFileChecks"], false),
            FtpLoadIcons: ParseBool(data["PS5 Library"]["FTPLoadIcons"], false),
            FtpLoadBackgrounds: ParseBool(data["PS5 Library"]["FTPLoadBackgrounds"], false),
            FtpScanAllUsb: ParseBool(data["PS5 Library"]["FTPScanAllUSB"], false),
            FtpScanExt0: ParseBool(data["PS5 Library"]["FTPScanext0"], false)
        );
    }

    private static void ApplyDto(IniData data, ConfigDto dto)
    {
        data["PS3 Tools"]["IP"] = dto.PS3Ip ?? string.Empty;
        data["PS5 Tools"]["IP"] = dto.PS5Ip ?? string.Empty;
        data["PS5 Tools"]["FTPPort"] = dto.PS5FtpPort?.ToString() ?? string.Empty;
        data["PS5 Tools"]["PayloadPort"] = dto.PS5PayloadPort?.ToString() ?? string.Empty;
        data["PS5 Library"]["ScanThreads"] = dto.ScanThreads?.ToString() ?? "8";
        data["General"]["AutoLibraryMusic"] = dto.AutoLibraryMusic ? "True" : "False";
        data["PS5 Library"]["LoadIcons"] = dto.LoadIcons ? "True" : "False";
        data["PS5 Library"]["LoadBackgrounds"] = dto.LoadBackgrounds ? "True" : "False";
        data["PS5 Library"]["SkipFileChecks"] = dto.SkipFileChecks ? "True" : "False";
        data["PS5 Library"]["FTPLoadIcons"] = dto.FtpLoadIcons ? "True" : "False";
        data["PS5 Library"]["FTPLoadBackgrounds"] = dto.FtpLoadBackgrounds ? "True" : "False";
        data["PS5 Library"]["FTPScanAllUSB"] = dto.FtpScanAllUsb ? "True" : "False";
        data["PS5 Library"]["FTPScanext0"] = dto.FtpScanExt0 ? "True" : "False";
    }

    private static int? ParseInt(string? value)
    {
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static bool ParseBool(string? value, bool fallback)
    {
        return bool.TryParse(value, out var parsed) ? parsed : fallback;
    }
}
