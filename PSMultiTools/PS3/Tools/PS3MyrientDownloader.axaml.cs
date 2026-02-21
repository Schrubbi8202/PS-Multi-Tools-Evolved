using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using DiscUtils.Iso9660;
using DiscUtils.Streams;
using FluentFTP;
using HtmlAgilityPack;
using IniParser;
using IniParser.Model;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using Newtonsoft.Json;
using PSMultiTools.Classes;
using PSMultiTools.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PSMultiTools.PS3.Tools;

public partial class PS3MyrientDownloader : Window
{
    private sealed class SourceOption
    {
        public string Name { get; set; } = "";
        public string SourceKey { get; set; } = "";
        public string Platform { get; set; } = "";
        public string Category { get; set; } = "";
        public List<string> Roots { get; set; } = [];
    }

    private sealed class CachedIndex
    {
        public DateTime CachedAtUtc { get; set; }
        public List<MyrientCatalogEntry> Entries { get; set; } = [];
    }

    private static readonly Regex TitleIdRegex = new(@"([A-Z]{4}[-_ ]?\d{3}\.?\d{2})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BracketSegmentRegex = new(@"\[[^\]]*\]|\([^)]*\)", RegexOptions.Compiled);
    private static readonly Regex MultiWhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private readonly HttpClient IndexClient = new();
    private readonly ObservableCollection<MyrientCatalogEntry> CatalogEntries = [];
    private readonly ObservableCollection<MyrientCatalogEntry> QueueEntries = [];
    private readonly List<SourceOption> SourceOptions = [];

    private string ConsoleIP = "";

    public PS3MyrientDownloader()
    {
        InitializeComponent();

        Loaded += PS3MyrientDownloader_Loaded;
        Closing += PS3MyrientDownloader_Closing;
    }

    private async void PS3MyrientDownloader_Loaded(object? sender, RoutedEventArgs e)
    {
        CatalogListBox.ItemsSource = CatalogEntries;
        QueueListBox.ItemsSource = QueueEntries;

        BuildSourceOptions();
        SourceComboBox.ItemsSource = SourceOptions.Select(s => s.Name).ToList();
        SourceComboBox.SelectionChanged += SourceComboBox_SelectionChanged;

        LoadConfig();
        if (string.IsNullOrWhiteSpace(ExdataRemotePathTextBox.Text))
        {
            ExdataRemotePathTextBox.Text = "/dev_hdd0/exdata";
        }
        if (SourceComboBox.SelectedIndex < 0)
        {
            SourceComboBox.SelectedIndex = 0;
        }

        await LoadCatalogAsync(false);
    }

    private void PS3MyrientDownloader_Closing(object? sender, WindowClosingEventArgs e)
    {
        SaveConfig();
    }

    private void BuildSourceOptions()
    {
        SourceOptions.Clear();

        SourceOptions.Add(new SourceOption()
        {
            Name = "Redump - PlayStation (PS1)",
            SourceKey = "redump_ps1",
            Platform = "PS1",
            Category = "ISO",
            Roots =
            [
                "https://myrient.erista.me/files/Redump/Sony - PlayStation/",
            ]
        });

        SourceOptions.Add(new SourceOption()
        {
            Name = "Redump - PlayStation 2 (PS2)",
            SourceKey = "redump_ps2",
            Platform = "PS2",
            Category = "ISO",
            Roots =
            [
                "https://myrient.erista.me/files/Redump/Sony - PlayStation 2/",
            ]
        });

        SourceOptions.Add(new SourceOption()
        {
            Name = "Redump - PlayStation 3 (PS3)",
            SourceKey = "redump_ps3",
            Platform = "PS3",
            Category = "ISO",
            Roots =
            [
                "https://myrient.erista.me/files/Redump/Sony - PlayStation 3/",
            ]
        });

        SourceOptions.Add(new SourceOption()
        {
            Name = "No-Intro - PS3 PSN (Games/Updates/Themes)",
            SourceKey = "nointro_ps3_psn",
            Platform = "PS3",
            Category = "PSN",
            Roots =
            [
                "https://myrient.erista.me/files/No-Intro/Sony%20-%20PlayStation%203%20(PSN)/",
                "https://myrient.erista.me/files/No-Intro/Sony - PlayStation 3 (PSN)/",
            ]
        });

        SourceOptions.Add(new SourceOption()
        {
            Name = "No-Intro - PSP PSN",
            SourceKey = "nointro_psp_psn",
            Platform = "PSP",
            Category = "PSN",
            Roots =
            [
                "https://myrient.erista.me/files/No-Intro/Sony%20-%20PlayStation%20Portable%20(PSN)/",
                "https://myrient.erista.me/files/No-Intro/Sony - PlayStation Portable (PSN)/",
            ]
        });
    }

    private void LoadConfig()
    {
        string configPath = Path.Combine(Environment.CurrentDirectory, "psmt-config.ini");
        if (!File.Exists(configPath))
        {
            return;
        }

        try
        {
            var parser = new FileIniDataParser();
            IniData data = parser.ReadFile(configPath);
            ConsoleIP = data["PS3 Tools"]["IP"] ?? "";
            DKeyFolderTextBox.Text = data["PS3 Tools"]["DKeyFolder"] ?? "";
            RAPFolderTextBox.Text = data["PS3 Tools"]["RAPFolder"] ?? "";
            RAPDatabaseTextBox.Text = data["PS3 Tools"]["RAPDatabasePath"] ?? "";
            ExdataRemotePathTextBox.Text = data["PS3 Tools"]["RAPRemoteExdataPath"] ?? "/dev_hdd0/exdata";

            if (int.TryParse(data["PS3 Tools"]["MyrientIndexCacheAgeHours"], out int cacheHours))
            {
                CacheHoursNumeric.Value = Math.Clamp(cacheHours, 1, 720);
            }
            if (int.TryParse(data["PS3 Tools"]["LastSelectedMyrientSource"], out int selectedSource))
            {
                SourceComboBox.SelectedIndex = Math.Clamp(selectedSource, 0, SourceOptions.Count - 1);
            }
        }
        catch (Exception)
        {
        }
    }

    private void SaveConfig()
    {
        string configPath = Path.Combine(Environment.CurrentDirectory, "psmt-config.ini");
        try
        {
            var parser = new FileIniDataParser();
            IniData data = File.Exists(configPath) ? parser.ReadFile(configPath) : new IniData();

            data["PS3 Tools"]["DKeyFolder"] = DKeyFolderTextBox.Text ?? "";
            data["PS3 Tools"]["RAPFolder"] = RAPFolderTextBox.Text ?? "";
            data["PS3 Tools"]["RAPDatabasePath"] = RAPDatabaseTextBox.Text ?? "";
            data["PS3 Tools"]["RAPRemoteExdataPath"] = string.IsNullOrWhiteSpace(ExdataRemotePathTextBox.Text) ? "/dev_hdd0/exdata" : ExdataRemotePathTextBox.Text.Trim();
            data["PS3 Tools"]["MyrientIndexCacheAgeHours"] = Convert.ToInt32(CacheHoursNumeric.Value).ToString(CultureInfo.InvariantCulture);
            data["PS3 Tools"]["LastSelectedMyrientSource"] = SourceComboBox.SelectedIndex.ToString(CultureInfo.InvariantCulture);
            parser.WriteFile(configPath, data);
        }
        catch (Exception)
        {
        }
    }

    private async void SourceComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        await LoadCatalogAsync(false);
    }

    private async void RefreshIndexButton_Click(object? sender, RoutedEventArgs e)
    {
        await LoadCatalogAsync(true);
    }

    private void SearchTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private async Task LoadCatalogAsync(bool forceRefresh)
    {
        CatalogEntries.Clear();
        StatusTextBlock.Text = "Loading index ...";

        SourceOption? selectedSource = GetSelectedSource();
        if (selectedSource is null)
        {
            StatusTextBlock.Text = "No source selected";
            return;
        }

        try
        {
            List<MyrientCatalogEntry> entries = await GetCatalogForSourceAsync(selectedSource, forceRefresh);
            foreach (var entry in entries.OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                CatalogEntries.Add(entry);
            }
            ApplyFilter();
            int variants = entries.Sum(x => x.Aliases is { Count: > 0 } ? x.Aliases.Count : 1);
            StatusTextBlock.Text = $"Indexed {CatalogEntries.Count} titles ({variants} variants)";
        }
        catch (Exception ex)
        {
            await AppendLogAsync($"Index loading failed: {ex.Message}");
            StatusTextBlock.Text = "Index loading failed";
        }
    }

    private void ApplyFilter()
    {
        string search = (SearchTextBox.Text ?? "").Trim();
        if (string.IsNullOrEmpty(search))
        {
            CatalogListBox.ItemsSource = CatalogEntries;
            return;
        }

        var filtered = CatalogEntries.Where(x =>
            (x.SearchIndex ?? "").Contains(search, StringComparison.OrdinalIgnoreCase) ||
            (x.DisplayName ?? "").Contains(search, StringComparison.OrdinalIgnoreCase) ||
            (x.RelativePath ?? "").Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        CatalogListBox.ItemsSource = filtered;
    }

    private async Task<List<MyrientCatalogEntry>> GetCatalogForSourceAsync(SourceOption source, bool forceRefresh)
    {
        string cachePath = Path.Combine(Environment.CurrentDirectory, "Cache", "myrient-index-cache", $"{source.SourceKey}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);

        int cacheHours = Convert.ToInt32(CacheHoursNumeric.Value);
        if (!forceRefresh && File.Exists(cachePath))
        {
            try
            {
                var cached = JsonConvert.DeserializeObject<CachedIndex>(await File.ReadAllTextAsync(cachePath));
                if (cached is not null && cached.CachedAtUtc > DateTime.UtcNow.AddHours(-cacheHours))
                {
                    return GroupLocalizedCatalogEntries(cached.Entries ?? []);
                }
            }
            catch (Exception)
            {
            }
        }

        List<MyrientCatalogEntry> allEntries = [];
        foreach (string root in source.Roots)
        {
            try
            {
                allEntries.AddRange(await CrawlSourceRootAsync(root, source));
            }
            catch (Exception ex)
            {
                await AppendLogAsync($"Source root failed: {root} ({ex.Message})");
            }
        }

        var deduped = allEntries
            .GroupBy(e => e.Url, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        var grouped = GroupLocalizedCatalogEntries(deduped);
        var payload = new CachedIndex() { CachedAtUtc = DateTime.UtcNow, Entries = grouped };
        await File.WriteAllTextAsync(cachePath, JsonConvert.SerializeObject(payload, Formatting.Indented));
        return grouped;
    }

    private async Task<List<MyrientCatalogEntry>> CrawlSourceRootAsync(string rootUrl, SourceOption source)
    {
        List<MyrientCatalogEntry> entries = [];
        Queue<string> pending = new();
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
        pending.Enqueue(rootUrl);

        while (pending.Count > 0)
        {
            string current = pending.Dequeue();
            if (!visited.Add(current))
            {
                continue;
            }

            string html;
            try
            {
                html = await IndexClient.GetStringAsync(current);
            }
            catch
            {
                continue;
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var anchorNodes = doc.DocumentNode.SelectNodes("//a[@href]");
            if (anchorNodes is null)
            {
                continue;
            }

            foreach (var anchor in anchorNodes)
            {
                string href = anchor.GetAttributeValue("href", "");
                if (string.IsNullOrWhiteSpace(href) || href == "../")
                {
                    continue;
                }

                Uri absoluteUri = new(new Uri(current), href);
                string absolute = absoluteUri.ToString();
                if (href.EndsWith("/"))
                {
                    pending.Enqueue(absolute);
                    continue;
                }

                string ext = Path.GetExtension(absolute).ToLowerInvariant();
                if (ext is ".zip" or ".7z" or ".rar" or ".pkg" or ".iso")
                {
                    string relativePath = absolute.Replace(rootUrl, "", StringComparison.OrdinalIgnoreCase);
                    entries.Add(new MyrientCatalogEntry()
                    {
                        Source = source.SourceKey.StartsWith("redump", StringComparison.OrdinalIgnoreCase) ? "Redump" : "No-Intro",
                        Platform = source.Platform,
                        Category = source.Category,
                        DisplayName = Uri.UnescapeDataString(Path.GetFileNameWithoutExtension(absolute)),
                        Url = absolute,
                        RelativePath = Uri.UnescapeDataString(relativePath),
                        FileExtension = ext
                    });
                }
            }
        }

        return entries;
    }

    private SourceOption? GetSelectedSource()
    {
        if (SourceComboBox.SelectedIndex < 0 || SourceComboBox.SelectedIndex >= SourceOptions.Count)
        {
            return null;
        }
        return SourceOptions[SourceComboBox.SelectedIndex];
    }

    private async void BrowseDKeyFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        var fbd = new OpenFolderDialog() { Title = "Select DKEY folder" };
        var selected = await fbd.ShowAsync(this);
        if (!string.IsNullOrEmpty(selected))
        {
            DKeyFolderTextBox.Text = selected;
        }
    }

    private async void BrowseRAPFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        var fbd = new OpenFolderDialog() { Title = "Select RAP folder" };
        var selected = await fbd.ShowAsync(this);
        if (!string.IsNullOrEmpty(selected))
        {
            RAPFolderTextBox.Text = selected;
        }
    }

    private async void BrowseRAPDatabaseButton_Click(object? sender, RoutedEventArgs e)
    {
        var tsvFilter = new FileDialogFilter() { Name = "TSV file", Extensions = ["tsv"] };
        var ofd = new OpenFileDialog() { Filters = { tsvFilter }, AllowMultiple = false, Title = "Select RAP TSV database" };
        var result = await ofd.ShowAsync(this);
        if (result is not null && result.Length > 0)
        {
            RAPDatabaseTextBox.Text = result[0];
        }
    }

    private async void AddToQueueButton_Click(object? sender, RoutedEventArgs e)
    {
        if (CatalogListBox.SelectedItem is MyrientCatalogEntry selected)
        {
            if (!QueueEntries.Any(x => x.Url.Equals(selected.Url, StringComparison.OrdinalIgnoreCase)))
            {
                QueueEntries.Add(selected);
                await AppendLogAsync($"Queued: {selected.DisplayName}");
            }
        }
    }

    private void RemoveFromQueueButton_Click(object? sender, RoutedEventArgs e)
    {
        if (QueueListBox.SelectedItem is MyrientCatalogEntry selected)
        {
            QueueEntries.Remove(selected);
        }
    }

    private void ClearQueueButton_Click(object? sender, RoutedEventArgs e)
    {
        QueueEntries.Clear();
    }

    private async void ProcessAndUploadButton_Click(object? sender, RoutedEventArgs e)
    {
        if (QueueEntries.Count == 0)
        {
            var box = MessageBoxManager.GetMessageBoxStandard("No queue", "Please add at least one entry to the queue.", ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Info);
            await box.ShowWindowAsync();
            return;
        }
        if (string.IsNullOrEmpty(ConsoleIP))
        {
            var box = MessageBoxManager.GetMessageBoxStandard("Missing PS3 IP", "Please set your PS3 IP in Settings > PS3 Tools first.", ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Error);
            await box.ShowWindowAsync();
            return;
        }

        IsEnabled = false;
        JobProgressBar.Value = 0;
        StatusTextBlock.Text = "Processing queue ...";
        List<MyrientJobResult> results = [];

        try
        {
            int done = 0;
            foreach (var entry in QueueEntries.ToList())
            {
                await AppendLogAsync($"Starting: {entry.DisplayName}");
                var result = await ProcessQueueEntryAsync(entry);
                results.Add(result);
                done++;
                JobProgressBar.Value = (done * 100d) / QueueEntries.Count;
            }
        }
        finally
        {
            IsEnabled = true;
        }

        int success = results.Count(r => r.Succeeded);
        int failed = results.Count - success;
        StatusTextBlock.Text = $"Completed. Success: {success}, Failed: {failed}";

        StringBuilder summary = new();
        summary.AppendLine($"Queue completed. Success: {success}, Failed: {failed}");
        foreach (var result in results)
        {
            if (result.Succeeded)
            {
                summary.AppendLine($"OK: {result.EntryName}");
            }
            else
            {
                summary.AppendLine($"FAIL: {result.EntryName} ({result.ErrorMessage})");
            }
        }

        var finalBox = MessageBoxManager.GetMessageBoxStandard("Myrient Queue Summary", summary.ToString(), ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Info);
        await finalBox.ShowWindowAsync();
    }

    private async Task<MyrientJobResult> ProcessQueueEntryAsync(MyrientCatalogEntry entry)
    {
        var result = new MyrientJobResult() { EntryName = entry.DisplayName, Platform = entry.Platform };
        string downloadsRoot = Utils.GetDownloadsFolderPath();
        if (string.IsNullOrEmpty(downloadsRoot))
        {
            result.ErrorMessage = "Downloads folder not found.";
            return result;
        }

        string stagingRoot = Path.Combine(downloadsRoot, "PSMT-Myrient-Staging", DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + "_" + MakeSafeFileName(entry.DisplayName));
        string extractRoot = Path.Combine(stagingRoot, "extracted");
        string processedRoot = Path.Combine(stagingRoot, "processed");
        Directory.CreateDirectory(stagingRoot);
        Directory.CreateDirectory(extractRoot);
        Directory.CreateDirectory(processedRoot);

        string fileName = Uri.UnescapeDataString(Path.GetFileName(new Uri(entry.Url).AbsolutePath));
        string downloadPath = Path.Combine(stagingRoot, fileName);
        result.DownloadedFilePath = downloadPath;

        try
        {
            await DownloadToFileAsync(entry.Url, downloadPath);
            await AppendLogAsync($"Downloaded: {downloadPath}");
        }
        catch (Exception ex)
        {
            result.ErrorMessage = "Download failed: " + ex.Message;
            return result;
        }

        List<string> materializedFiles = [];
        try
        {
            if (IsArchive(downloadPath))
            {
                await ExtractArchiveAsync(downloadPath, extractRoot);
                materializedFiles = Directory.EnumerateFiles(extractRoot, "*", SearchOption.AllDirectories).ToList();
            }
            else
            {
                materializedFiles.Add(downloadPath);
            }
        }
        catch (Exception ex)
        {
            result.ErrorMessage = "Extraction failed: " + ex.Message;
            return result;
        }

        if (materializedFiles.Count == 0)
        {
            result.ErrorMessage = "No files found after extraction.";
            return result;
        }

        try
        {
            switch (entry.Category)
            {
                case "ISO" when entry.Platform == "PS1":
                    await ProcessPS1Async(entry, materializedFiles, processedRoot, result);
                    break;
                case "ISO" when entry.Platform == "PS2":
                    await ProcessPS2Async(entry, materializedFiles, processedRoot, result);
                    break;
                case "ISO" when entry.Platform == "PS3":
                    await ProcessPS3ISOAsync(entry, materializedFiles, processedRoot, result);
                    break;
                case "PSN":
                    await ProcessPSNAsync(entry, materializedFiles, processedRoot, result);
                    break;
                default:
                    result.ErrorMessage = "Unsupported entry category.";
                    return result;
            }
        }
        catch (Exception ex)
        {
            result.ErrorMessage = "Processing failed: " + ex.Message;
            return result;
        }

        try
        {
            await UploadProcessedFilesAsync(entry, result.ProcessedLocalFiles, result);
            result.Succeeded = true;
            return result;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = "Upload failed: " + ex.Message;
            return result;
        }
    }

    private async Task ProcessPS1Async(MyrientCatalogEntry entry, List<string> files, string processedRoot, MyrientJobResult result)
    {
        string? cueFile = files.FirstOrDefault(f => Path.GetExtension(f).Equals(".cue", StringComparison.OrdinalIgnoreCase));
        if (cueFile is null)
        {
            throw new InvalidOperationException("No .cue file found for PS1 entry.");
        }

        List<string> cueBins = GetCueReferencedBins(cueFile);
        string outputCue = cueFile;
        string outputBin = cueBins.FirstOrDefault() ?? "";

        if (cueBins.Count > 1)
        {
            string mergeBaseName = MakeSafeFileName(Path.GetFileNameWithoutExtension(cueFile) + "_merged");
            await RunBinMergeAsync(cueFile, mergeBaseName);
            string mergedCue = Path.Combine(Path.GetDirectoryName(cueFile)!, mergeBaseName + ".cue");
            string mergedBin = Path.Combine(Path.GetDirectoryName(cueFile)!, mergeBaseName + ".bin");
            if (!File.Exists(mergedCue) || !File.Exists(mergedBin))
            {
                throw new InvalidOperationException("binmerge did not produce expected merged files.");
            }
            outputCue = mergedCue;
            outputBin = mergedBin;
        }
        else if (cueBins.Count == 1)
        {
            outputBin = cueBins[0];
        }

        if (!File.Exists(outputCue) || !File.Exists(outputBin))
        {
            throw new InvalidOperationException("PS1 CUE/BIN output is missing.");
        }

        string titleId = TryExtractTitleId(Path.GetFileNameWithoutExtension(outputCue));
        string displayName = BuildDisplayName(entry.DisplayName, titleId);
        string gameFolder = Path.Combine(processedRoot, MakeSafeFileName(entry.DisplayName));
        Directory.CreateDirectory(gameFolder);

        string finalCue = Path.Combine(gameFolder, MakeSafeFileName(displayName) + ".cue");
        string finalBin = Path.Combine(gameFolder, MakeSafeFileName(displayName) + ".bin");
        File.Copy(outputCue, finalCue, true);
        File.Copy(outputBin, finalBin, true);

        result.ProcessedLocalFiles.Add(finalCue);
        result.ProcessedLocalFiles.Add(finalBin);
    }

    private async Task ProcessPS2Async(MyrientCatalogEntry entry, List<string> files, string processedRoot, MyrientJobResult result)
    {
        string? isoFile = files.FirstOrDefault(f => Path.GetExtension(f).Equals(".iso", StringComparison.OrdinalIgnoreCase));
        if (isoFile is null)
        {
            throw new InvalidOperationException("No .iso file found for PS2 entry.");
        }

        string titleId = TryExtractTitleId(Path.GetFileNameWithoutExtension(isoFile));
        string displayName = BuildDisplayName(entry.DisplayName, titleId);
        string finalIso = Path.Combine(processedRoot, MakeSafeFileName(displayName) + ".iso");
        File.Copy(isoFile, finalIso, true);
        result.ProcessedLocalFiles.Add(finalIso);
        await AppendLogAsync($"Prepared PS2 ISO: {finalIso}");
    }

    private async Task ProcessPS3ISOAsync(MyrientCatalogEntry entry, List<string> files, string processedRoot, MyrientJobResult result)
    {
        string? isoFile = files.FirstOrDefault(f => Path.GetExtension(f).Equals(".iso", StringComparison.OrdinalIgnoreCase));
        if (isoFile is null)
        {
            throw new InvalidOperationException("No .iso file found for PS3 entry.");
        }

        string titleId = TryExtractPs3TitleIdFromIso(isoFile);
        string dkey = await ResolveDKeyAsync(titleId);
        if (string.IsNullOrEmpty(dkey))
        {
            throw new InvalidOperationException("No decryption key available.");
        }

        await RunPs3DecAsync(isoFile, dkey);
        string outputIsoName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(isoFile)) + ".iso_decrypted.iso";
        string outputIsoPath = Path.Combine(Path.GetDirectoryName(isoFile)!, outputIsoName);
        if (!File.Exists(outputIsoPath))
        {
            throw new InvalidOperationException("Decrypted ISO output file was not produced.");
        }

        string displayName = BuildDisplayName(entry.DisplayName, titleId);
        string finalIso = Path.Combine(processedRoot, MakeSafeFileName(displayName) + ".iso");
        File.Copy(outputIsoPath, finalIso, true);
        result.ProcessedLocalFiles.Add(finalIso);
        await AppendLogAsync($"Prepared PS3 ISO: {finalIso}");
    }

    private async Task ProcessPSNAsync(MyrientCatalogEntry entry, List<string> files, string processedRoot, MyrientJobResult result)
    {
        var pkgFiles = files.Where(f => Path.GetExtension(f).Equals(".pkg", StringComparison.OrdinalIgnoreCase)).ToList();
        if (pkgFiles.Count == 0)
        {
            throw new InvalidOperationException("No .pkg files found for PSN entry.");
        }

        foreach (string pkg in pkgFiles)
        {
            string finalPkg = Path.Combine(processedRoot, Path.GetFileName(pkg));
            File.Copy(pkg, finalPkg, true);
            result.ProcessedLocalFiles.Add(finalPkg);

            string contentId = GetContentIdFromPkg(finalPkg);
            if (!string.IsNullOrEmpty(contentId))
            {
                string rapPath = await ResolveRapPathForContentIdAsync(contentId, processedRoot);
                if (string.IsNullOrEmpty(rapPath) || !File.Exists(rapPath))
                {
                    throw new InvalidOperationException($"Missing RAP for ContentID {contentId}.");
                }

                if (!result.ProcessedLocalFiles.Any(x => x.Equals(rapPath, StringComparison.OrdinalIgnoreCase)))
                {
                    result.ProcessedLocalFiles.Add(rapPath);
                }
            }
        }
        await AppendLogAsync($"Prepared {pkgFiles.Count} PKG file(s).");
    }

    private static string GetContentIdFromPkg(string pkgPath)
    {
        try
        {
            PKGDecryptor decryptor = new();
            decryptor.ProcessPKGFile(pkgPath);
            return decryptor.ContentID?.Trim() ?? "";
        }
        catch
        {
            return "";
        }
    }

    private async Task<string> ResolveRapPathForContentIdAsync(string contentId, string outputRoot)
    {
        string rapFolder = RAPFolderTextBox.Text ?? "";
        if (!string.IsNullOrEmpty(rapFolder) && Directory.Exists(rapFolder))
        {
            string directRap = Path.Combine(rapFolder, contentId + ".rap");
            if (File.Exists(directRap))
            {
                string copied = Path.Combine(outputRoot, contentId + ".rap");
                File.Copy(directRap, copied, true);
                await AppendLogAsync($"RAP from folder: {contentId}.rap");
                return copied;
            }
        }

        string rapHex = "";
        string rapDb = RAPDatabaseTextBox.Text ?? "";
        if (!string.IsNullOrEmpty(rapDb) && File.Exists(rapDb))
        {
            rapHex = TryGetRapHexFromTsv(rapDb, contentId);
            if (!string.IsNullOrEmpty(rapHex))
            {
                string generated = CreateRapFromHex(contentId, rapHex, outputRoot);
                await AppendLogAsync($"RAP created from DB: {contentId}.rap");
                return generated;
            }
        }

        var box = MessageBoxManager.GetMessageBoxStandard("RAP not found", $"No RAP found for {contentId}. Select a local .rap file?", ButtonEnum.YesNo, MsBox.Avalonia.Enums.Icon.Question);
        var decision = await box.ShowWindowDialogAsync(this);
        if (decision == ButtonResult.Yes)
        {
            var rapFilter = new FileDialogFilter() { Name = "RAP file", Extensions = ["rap"] };
            var ofd = new OpenFileDialog() { Filters = { rapFilter }, AllowMultiple = false, Title = $"Select RAP for {contentId}" };
            var result = await ofd.ShowAsync(this);
            if (result is not null && result.Length > 0 && File.Exists(result[0]))
            {
                string copied = Path.Combine(outputRoot, contentId + ".rap");
                File.Copy(result[0], copied, true);
                return copied;
            }
        }

        var input = new InputDialog() { Title = $"RAP hex for {contentId}" };
        input.NewValueTextBox.Text = "";
        input.InputDialogTitleTextBlock.Text = $"Enter RAP hex for {contentId} (32 hex chars):";
        input.ConfirmButton.Content = "Create RAP";
        string rapManualHex = (await input.ShowDialog<string>(this) ?? "").Trim();
        if (!string.IsNullOrEmpty(rapManualHex))
        {
            return CreateRapFromHex(contentId, rapManualHex, outputRoot);
        }

        return "";
    }

    private static string TryGetRapHexFromTsv(string tsvPath, string contentId)
    {
        try
        {
            foreach (string line in File.ReadLines(tsvPath).Skip(1))
            {
                string[] cols = line.Split('\t');
                if (cols.Length < 6)
                {
                    continue;
                }

                string lineContentId = cols[5].Trim();
                if (!lineContentId.Equals(contentId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string rapHex = cols[4].Trim();
                if (!string.IsNullOrEmpty(rapHex) && !rapHex.Equals("MISSING", StringComparison.OrdinalIgnoreCase))
                {
                    return rapHex;
                }
            }
        }
        catch
        {
        }

        return "";
    }

    private static string CreateRapFromHex(string contentId, string rapHex, string outputRoot)
    {
        string clean = rapHex.Replace(" ", "", StringComparison.Ordinal).Trim();
        if (clean.Length % 2 != 0)
        {
            throw new InvalidOperationException("RAP hex length is invalid.");
        }

        byte[] bytes = new byte[clean.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(clean.Substring(i * 2, 2), 16);
        }

        string rapPath = Path.Combine(outputRoot, contentId + ".rap");
        File.WriteAllBytes(rapPath, bytes);
        return rapPath;
    }

    private async Task UploadProcessedFilesAsync(MyrientCatalogEntry entry, List<string> localFiles, MyrientJobResult result)
    {
        if (localFiles.Count == 0)
        {
            throw new InvalidOperationException("No processed files to upload.");
        }

        using var ftp = new AsyncFtpClient(ConsoleIP, "anonymous", "anonymous", 21, new FtpConfig()
        {
            EncryptionMode = FtpEncryptionMode.None,
            DataConnectionEncryption = false,
            ValidateAnyCertificate = true
        });

        await ftp.Connect();

        foreach (string file in localFiles)
        {
            string remoteDir = GetRemoteDirectory(entry, file);
            await ftp.CreateDirectory(remoteDir, true);

            string fileName = Path.GetFileName(file);
            string remotePath = await GetUniqueRemotePathAsync(ftp, remoteDir, fileName);

            var progress = new Progress<FtpProgress>(p =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (p.Progress >= 0)
                    {
                        StatusTextBlock.Text = $"Uploading {fileName} - {p.Progress:F0}%";
                    }
                });
            });

            await ftp.UploadFile(file, remotePath, FtpRemoteExists.NoCheck, false, FtpVerify.None, progress);
            result.UploadedRemotePaths.Add(remotePath);
            await AppendLogAsync($"Uploaded: {remotePath}");
        }

        await ftp.Disconnect();
    }

    private string GetRemoteDirectory(MyrientCatalogEntry entry, string localFile)
    {
        if (Path.GetExtension(localFile).Equals(".rap", StringComparison.OrdinalIgnoreCase))
        {
            string configured = ExdataRemotePathTextBox.Text ?? "";
            return string.IsNullOrWhiteSpace(configured) ? "/dev_hdd0/exdata" : configured.Trim();
        }
        if (entry.Category == "PSN")
        {
            return "/dev_hdd0/packages";
        }
        if (entry.Platform == "PS3")
        {
            return "/dev_hdd0/PS3ISO";
        }
        if (entry.Platform == "PS2")
        {
            return "/dev_hdd0/PS2ISO";
        }
        if (entry.Platform == "PS1")
        {
            string folder = new DirectoryInfo(Path.GetDirectoryName(localFile)!).Name;
            return "/dev_hdd0/PSXISO/" + folder;
        }
        return "/dev_hdd0/packages";
    }

    private static async Task<string> GetUniqueRemotePathAsync(AsyncFtpClient ftp, string remoteDir, string fileName)
    {
        string stem = Path.GetFileNameWithoutExtension(fileName);
        string ext = Path.GetExtension(fileName);
        string candidate = remoteDir.TrimEnd('/') + "/" + fileName;
        int i = 1;

        while (await ftp.GetObjectInfo(candidate) is not null)
        {
            candidate = remoteDir.TrimEnd('/') + "/" + $"{stem} ({i}){ext}";
            i++;
        }

        return candidate;
    }

    private static bool IsArchive(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext is ".zip" or ".7z" or ".rar";
    }

    private async Task DownloadToFileAsync(string url, string destinationPath)
    {
        using var response = await IndexClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        await using var sourceStream = await response.Content.ReadAsStreamAsync();
        await using var targetStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
        await sourceStream.CopyToAsync(targetStream);
    }

    private async Task ExtractArchiveAsync(string archivePath, string outputDirectory)
    {
        string toolPath = ResolveToolPath("7z.exe", "7zz");
        Process extract = new()
        {
            StartInfo = new ProcessStartInfo()
            {
                FileName = toolPath,
                Arguments = $"x \"{archivePath}\" -o\"{Utils.EnsureTrailingSeparator(outputDirectory)}\" -y",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            }
        };

        extract.Start();
        string stdErr = await extract.StandardError.ReadToEndAsync();
        await extract.WaitForExitAsync();
        if (extract.ExitCode != 0)
        {
            throw new InvalidOperationException($"Archive extraction failed (exit code {extract.ExitCode}). {stdErr}");
        }
    }

    private async Task RunBinMergeAsync(string cuePath, string outputBaseName)
    {
        string toolPath = ResolveToolPath("binmerge.exe", "binmerge");
        Process merge = new()
        {
            StartInfo = new ProcessStartInfo()
            {
                FileName = toolPath,
                WorkingDirectory = Path.GetDirectoryName(cuePath),
                Arguments = $"\"{cuePath}\" \"{outputBaseName}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            }
        };

        merge.Start();
        string stdErr = await merge.StandardError.ReadToEndAsync();
        await merge.WaitForExitAsync();
        if (merge.ExitCode != 0)
        {
            throw new InvalidOperationException($"binmerge failed (exit code {merge.ExitCode}). {stdErr}");
        }
    }

    private async Task RunPs3DecAsync(string isoPath, string dkey)
    {
        string toolPath = ResolveToolPath("ps3dec.exe", "ps3dec");
        Process decrypt = new()
        {
            StartInfo = new ProcessStartInfo()
            {
                FileName = toolPath,
                Arguments = $"--iso \"{isoPath}\" --dk {dkey} --skip",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            }
        };

        decrypt.Start();
        string stdErr = await decrypt.StandardError.ReadToEndAsync();
        await decrypt.WaitForExitAsync();
        if (decrypt.ExitCode != 0)
        {
            throw new InvalidOperationException($"ps3dec failed (exit code {decrypt.ExitCode}). {stdErr}");
        }
    }

    private static string ResolveToolPath(string windowsToolName, string unixToolName)
    {
        if (OperatingSystem.IsWindows())
        {
            string[] candidates =
            [
                Path.Combine(Environment.CurrentDirectory, "Tools", windowsToolName),
                Path.Combine(Environment.CurrentDirectory, "Tools", "Windows", windowsToolName),
                Path.Combine(Environment.CurrentDirectory, "Tools", "Windows", windowsToolName.Replace("ps3dec", "PS3Dec", StringComparison.OrdinalIgnoreCase)),
            ];

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            string[] candidates =
            [
                Path.Combine(Environment.CurrentDirectory, "Tools", unixToolName),
                Path.Combine(Environment.CurrentDirectory, "Tools", "Linux", unixToolName),
            ];
            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            string[] candidates =
            [
                Path.Combine(Environment.CurrentDirectory, "Tools", unixToolName),
                Path.Combine(Environment.CurrentDirectory, "Tools", "macOS", unixToolName),
            ];
            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        throw new FileNotFoundException("Required tool was not found.");
    }

    private static string MakeSafeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            sb.Append(invalidChars.Contains(c) ? '_' : c);
        }
        return sb.ToString().Trim().Trim('.');
    }

    private static string BuildDisplayName(string baseName, string titleId)
    {
        if (string.IsNullOrEmpty(titleId))
        {
            return baseName.Trim();
        }
        return $"{baseName.Trim()} [{titleId}]";
    }

    private static string TryExtractTitleId(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return "";
        }
        var match = TitleIdRegex.Match(input);
        if (!match.Success)
        {
            return "";
        }
        return match.Groups[1].Value.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
    }

    private static List<string> GetCueReferencedBins(string cueFilePath)
    {
        List<string> bins = [];
        string cueDir = Path.GetDirectoryName(cueFilePath)!;
        foreach (string line in File.ReadAllLines(cueFilePath))
        {
            if (!line.TrimStart().StartsWith("FILE ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int firstQuote = line.IndexOf('"');
            int lastQuote = line.LastIndexOf('"');
            if (firstQuote >= 0 && lastQuote > firstQuote)
            {
                string fileName = line[(firstQuote + 1)..lastQuote];
                string fullPath = Path.Combine(cueDir, fileName);
                if (Path.GetExtension(fullPath).Equals(".bin", StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath))
                {
                    bins.Add(fullPath);
                }
            }
        }
        return bins.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string TryExtractPs3TitleIdFromIso(string isoPath)
    {
        using var isoStream = File.Open(isoPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var cd = new CDReader(isoStream, true);
        try
        {
            using SparseStream paramStream = cd.OpenFile(@"PS3_GAME\PARAM.SFO", FileMode.Open);
            var sfo = SFONew.ReadSfo(paramStream);
            if (sfo is not null && sfo.TryGetValue("TITLE_ID", out object? titleObj) && titleObj is not null)
            {
                return titleObj.ToString() ?? "";
            }
        }
        catch (Exception)
        {
        }
        finally
        {
            cd.Dispose();
        }
        return "";
    }

    private async Task<string> ResolveDKeyAsync(string titleId)
    {
        if (string.IsNullOrEmpty(titleId))
        {
            return await PromptForDKeyAsync("Unknown");
        }

        string configuredFolder = DKeyFolderTextBox.Text ?? "";
        if (!string.IsNullOrEmpty(configuredFolder) && Directory.Exists(configuredFolder))
        {
            string directPath = Path.Combine(configuredFolder, titleId + ".dkey");
            if (File.Exists(directPath))
            {
                return ReadDKeyFile(directPath);
            }

            string? discovered = Directory.EnumerateFiles(configuredFolder, "*.dkey", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(x => Path.GetFileNameWithoutExtension(x).Equals(titleId, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(discovered))
            {
                return ReadDKeyFile(discovered);
            }
        }

        string dkeyDbPath = Path.Combine(Environment.CurrentDirectory, "Tools", "dkeydb.html");
        if (File.Exists(dkeyDbPath))
        {
            string dkey = Utils.GetDKeyFromGameID(dkeyDbPath, titleId);
            if (!string.IsNullOrEmpty(dkey))
            {
                return dkey;
            }
        }

        return await PromptForDKeyAsync(titleId);
    }

    private static string ReadDKeyFile(string dkeyPath)
    {
        foreach (string line in File.ReadAllLines(dkeyPath))
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                return line.Trim();
            }
        }
        return "";
    }

    private async Task<string> PromptForDKeyAsync(string titleId)
    {
        var input = new InputDialog() { Title = $"DKey required ({titleId})" };
        input.NewValueTextBox.Text = "";
        input.InputDialogTitleTextBlock.Text = $"Enter decryption key for {titleId}:";
        input.ConfirmButton.Content = "Use Key";
        string key = await input.ShowDialog<string>(this);
        return key?.Trim() ?? "";
    }

    private async Task AppendLogAsync(string message)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            LogTextBox.Text += $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
            LogTextBox.CaretIndex = LogTextBox.Text?.Length ?? 0;
        });
    }

    private static List<MyrientCatalogEntry> GroupLocalizedCatalogEntries(IEnumerable<MyrientCatalogEntry> entries)
    {
        Dictionary<string, List<MyrientCatalogEntry>> grouped = new(StringComparer.Ordinal);
        foreach (MyrientCatalogEntry entry in entries)
        {
            PrepareEntrySearchFields(entry);
            string key = BuildLocalizationGroupKey(entry);
            if (!grouped.TryGetValue(key, out List<MyrientCatalogEntry>? bucket))
            {
                bucket = [];
                grouped[key] = bucket;
            }
            bucket.Add(entry);
        }

        List<MyrientCatalogEntry> result = [];
        foreach (List<MyrientCatalogEntry> bucket in grouped.Values)
        {
            List<MyrientCatalogEntry> ordered = bucket
                .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            MyrientCatalogEntry primary = CloneEntry(ordered[0]);
            List<string> aliases = ordered
                .SelectMany(x => x.Aliases is { Count: > 0 } ? x.Aliases : [x.DisplayName])
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (aliases.Count == 0)
            {
                aliases.Add(primary.DisplayName);
            }

            primary.Aliases = aliases;
            primary.AliasTooltip = aliases.Count > 1 ? string.Join(Environment.NewLine, aliases) : "";
            primary.SearchIndex = BuildSearchIndex(primary, aliases);
            result.Add(primary);
        }

        return result;
    }

    private static string BuildLocalizationGroupKey(MyrientCatalogEntry entry)
    {
        string titleId = TryExtractTitleId((entry.DisplayName ?? "") + " " + (entry.RelativePath ?? ""));
        if (!string.IsNullOrEmpty(titleId))
        {
            return $"id|{entry.Source}|{entry.Platform}|{entry.Category}|{entry.FileExtension}|{titleId}";
        }

        string metadataKey = BuildMetadataSignature(entry);
        if (TryGetSeriesPrefix(entry.DisplayName, out string seriesPrefix) && LooksLikeStableSeriesPrefix(seriesPrefix))
        {
            return $"series|{entry.Source}|{entry.Platform}|{entry.Category}|{entry.FileExtension}|{NormalizeForKey(seriesPrefix)}|{metadataKey}";
        }

        return $"title|{entry.Source}|{entry.Platform}|{entry.Category}|{entry.FileExtension}|{NormalizeForKey(entry.DisplayName)}|{metadataKey}";
    }

    private static string BuildMetadataSignature(MyrientCatalogEntry entry)
    {
        string candidate = !string.IsNullOrWhiteSpace(entry.RelativePath)
            ? Path.GetFileNameWithoutExtension(Uri.UnescapeDataString(entry.RelativePath))
            : entry.DisplayName;

        List<string> segments = BracketSegmentRegex.Matches(candidate)
            .Cast<Match>()
            .Select(x => x.Value.Trim('[', ']', '(', ')', ' '))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(NormalizeForKey)
            .Where(x => !string.IsNullOrEmpty(x))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        return string.Join("|", segments);
    }

    private static bool TryGetSeriesPrefix(string displayName, out string prefix)
    {
        prefix = "";
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return false;
        }

        int separatorIndex = displayName.IndexOf(" - ", StringComparison.Ordinal);
        if (separatorIndex <= 0)
        {
            return false;
        }

        prefix = displayName[..separatorIndex].Trim();
        return !string.IsNullOrEmpty(prefix);
    }

    private static bool LooksLikeStableSeriesPrefix(string prefix)
    {
        string normalized = NormalizeForKey(prefix);
        if (string.IsNullOrEmpty(normalized))
        {
            return false;
        }

        if (normalized.Any(char.IsDigit))
        {
            return true;
        }

        return normalized.Length <= 5 && normalized.IndexOf(' ') < 0;
    }

    private static string NormalizeForKey(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "";
        }

        string lower = input.Trim().ToLowerInvariant();
        string collapsedWhitespace = MultiWhitespaceRegex.Replace(lower, " ");
        return collapsedWhitespace;
    }

    private static void PrepareEntrySearchFields(MyrientCatalogEntry entry)
    {
        entry.DisplayName = (entry.DisplayName ?? "").Trim();
        entry.RelativePath = (entry.RelativePath ?? "").Trim();

        List<string> aliases = (entry.Aliases ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (aliases.Count == 0 && !string.IsNullOrWhiteSpace(entry.DisplayName))
        {
            aliases.Add(entry.DisplayName);
        }

        entry.Aliases = aliases;
        entry.AliasTooltip = aliases.Count > 1 ? string.Join(Environment.NewLine, aliases) : "";
        entry.SearchIndex = BuildSearchIndex(entry, aliases);
    }

    private static string BuildSearchIndex(MyrientCatalogEntry entry, List<string> aliases)
    {
        StringBuilder sb = new();
        sb.AppendLine(entry.DisplayName ?? "");
        sb.AppendLine(entry.RelativePath ?? "");

        foreach (string alias in aliases)
        {
            sb.AppendLine(alias);
        }

        string titleId = TryExtractTitleId((entry.DisplayName ?? "") + " " + (entry.RelativePath ?? ""));
        if (!string.IsNullOrEmpty(titleId))
        {
            sb.AppendLine(titleId);
        }

        return sb.ToString();
    }

    private static MyrientCatalogEntry CloneEntry(MyrientCatalogEntry entry)
    {
        return new MyrientCatalogEntry()
        {
            Source = entry.Source,
            Platform = entry.Platform,
            Category = entry.Category,
            DisplayName = entry.DisplayName,
            Aliases = (entry.Aliases ?? []).ToList(),
            AliasTooltip = entry.AliasTooltip,
            SearchIndex = entry.SearchIndex,
            Url = entry.Url,
            RelativePath = entry.RelativePath,
            FileExtension = entry.FileExtension,
            SizeBytes = entry.SizeBytes,
            LastModified = entry.LastModified
        };
    }
}
