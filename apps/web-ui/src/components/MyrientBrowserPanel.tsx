import SearchIcon from "@mui/icons-material/Search";
import SettingsIcon from "@mui/icons-material/Settings";
import AlbumOutlinedIcon from "@mui/icons-material/AlbumOutlined";
import Inventory2OutlinedIcon from "@mui/icons-material/Inventory2Outlined";
import PaletteOutlinedIcon from "@mui/icons-material/PaletteOutlined";
import SystemUpdateAltOutlinedIcon from "@mui/icons-material/SystemUpdateAltOutlined";
import FaceOutlinedIcon from "@mui/icons-material/FaceOutlined";
import {
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  IconButton,
  MenuItem,
  Select,
  Stack,
  TextField,
  Tooltip,
  Typography
} from "@mui/material";
import { useMutation, useQuery } from "@tanstack/react-query";
import { useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { api, titleIdCoverUrl } from "../api";
import type { ArchiveTitleGroupDto, ArchiveVariantDto } from "../types";

export function MyrientBrowserPanel() {
  const searchInputRef = useRef<HTMLInputElement | null>(null);
  const [activeSearch, setActiveSearch] = useState("");
  const [platform, setPlatform] = useState("All");
  const [contentType, setContentType] = useState("All");
  const [destinationFolder, setDestinationFolder] = useState("H:\\Downloads\\PSMT-Archive");
  const [remoteName, setRemoteName] = useState("PrivateLibraryProvider");
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [settingsDraftDestination, setSettingsDraftDestination] = useState(destinationFolder);
  const [settingsDraftRemote, setSettingsDraftRemote] = useState(remoteName);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [activeGroup, setActiveGroup] = useState<ArchiveTitleGroupDto | null>(null);
  const [selectedRegion, setSelectedRegion] = useState("All");
  const [selectedVersion, setSelectedVersion] = useState("All");
  const [selectedDialogContentType, setSelectedDialogContentType] = useState("All");
  const [selectedVariantId, setSelectedVariantId] = useState("");
  const [downloadMode, setDownloadMode] = useState<"local" | "direct">("local");
  const [ps3Ip, setPs3Ip] = useState("");
  const [lastJobInfo, setLastJobInfo] = useState("");

  const queryKey = ["myrient-catalog", activeSearch, platform, contentType];
  const catalogQuery = useQuery({
    queryKey,
    queryFn: () =>
      api.getMyrientCatalog({
        search: activeSearch || undefined,
        platform: platform === "All" ? undefined : platform,
        contentType: contentType === "All" ? undefined : contentType
      }),
    retry: false
  });

  const downloadMutation = useMutation({
    mutationFn: (request: {
      sourceUrl: string;
      destinationFolder: string;
      remoteName: string;
      installToPs3?: boolean;
      ps3Ip?: string;
      platform?: string;
      contentType?: string;
    }) => api.startMyrientRcloneDownload(request),
    onSuccess: (job) => setLastJobInfo(`Started job ${job.jobId}: ${job.name}`)
  });

  const groups = catalogQuery.data?.groups ?? [];
  const ps3ContentTypes = ["All", "Games (ISO)", "Games (PKG)", "Updates", "Themes", "Avatars"];
  const isCatalogBusy = catalogQuery.isLoading || catalogQuery.isFetching;

  const regions = useMemo(() => {
    if (!activeGroup) return ["All"];
    return ["All", ...Array.from(new Set(activeGroup.variants.map((v) => v.region).filter((v): v is string => !!v))).sort()];
  }, [activeGroup]);

  const versions = useMemo(() => {
    if (!activeGroup) return ["All"];
    return ["All", ...Array.from(new Set(activeGroup.variants.map((v) => v.versionTag).filter((v): v is string => !!v))).sort()];
  }, [activeGroup]);

  const filteredVariants = useMemo(() => {
    if (!activeGroup) return [];
    return activeGroup.variants.filter((v) => {
      const typeOk = selectedDialogContentType === "All" || v.contentType === selectedDialogContentType;
      const regionOk = selectedRegion === "All" || v.region === selectedRegion;
      const versionOk = selectedVersion === "All" || v.versionTag === selectedVersion;
      return typeOk && regionOk && versionOk;
    });
  }, [activeGroup, selectedDialogContentType, selectedRegion, selectedVersion]);

  const dialogContentTypes = useMemo(() => {
    if (!activeGroup) return ["All"];
    return ["All", ...Array.from(new Set(activeGroup.variants.map((v) => v.contentType))).sort((a, b) => a.localeCompare(b))];
  }, [activeGroup]);

  const selectedVariant: ArchiveVariantDto | null = useMemo(() => {
    return filteredVariants.find((v) => v.id === selectedVariantId) ?? filteredVariants[0] ?? null;
  }, [filteredVariants, selectedVariantId]);

  useEffect(() => {
    if (platform !== "PS3" && contentType !== "All") {
      setContentType("All");
    }
  }, [platform, contentType]);

  useEffect(() => {
    setSettingsDraftDestination(destinationFolder);
    setSettingsDraftRemote(remoteName);
  }, [settingsOpen, destinationFolder, remoteName]);

  function openEditionDialog(group: ArchiveTitleGroupDto) {
    setActiveGroup(group);
    setSelectedDialogContentType("All");
    setSelectedRegion("All");
    setSelectedVersion("All");
    setSelectedVariantId("");
    setDownloadMode("local");
    setDialogOpen(true);
  }

  async function startDownloadFromDialog() {
    if (!selectedVariant) return;
    await downloadMutation.mutateAsync({
      sourceUrl: selectedVariant.url,
      destinationFolder,
      remoteName,
      installToPs3: downloadMode === "direct",
      ps3Ip: downloadMode === "direct" ? ps3Ip || undefined : undefined,
      platform: selectedVariant.platform,
      contentType: selectedVariant.contentType
    });
    setDialogOpen(false);
  }

  function triggerSearch() {
    const value = (searchInputRef.current?.value ?? "").trim();
    setActiveSearch(value);
  }

  function saveSettings() {
    setDestinationFolder(settingsDraftDestination.trim() || destinationFolder);
    setRemoteName(settingsDraftRemote.trim() || remoteName);
    setSettingsOpen(false);
  }

  const platformOptions: Array<{ id: string; label: string; icon?: string }> = [
    { id: "All", label: "All" },
    { id: "PS1", label: "PS1", icon: "https://upload.wikimedia.org/wikipedia/commons/thumb/4/4e/Playstation_logo_colour.svg/1280px-Playstation_logo_colour.svg.png" },
    { id: "PS2", label: "PS2", icon: "https://logos-world.net/wp-content/uploads/2023/03/PS2-Logo.png" },
    { id: "PS3", label: "PS3", icon: "https://i.pinimg.com/736x/37/9a/4c/379a4c6aa291dd1c012cffff93b3b772.jpg" },
    { id: "PSP", label: "PSP", icon: "https://cdn.worldvectorlogo.com/logos/psp-1.svg" }
  ];

  function typeIndicators(group: ArchiveTitleGroupDto) {
    const distinct = Array.from(new Set(group.variants.map((v) => v.contentType)));
    return distinct.sort((a, b) => a.localeCompare(b));
  }

  function typeIndicatorMeta(contentType: string): { key: string; label: string; icon: ReactNode } {
    if (contentType === "Games (ISO)") {
      return { key: "iso", label: "ISO", icon: <AlbumOutlinedIcon sx={{ fontSize: 12 }} /> };
    }
    if (contentType === "Games (PKG)") {
      return { key: "pkg", label: "PKG", icon: <Inventory2OutlinedIcon sx={{ fontSize: 12 }} /> };
    }
    if (contentType === "Updates") {
      return { key: "upd", label: "UPD", icon: <SystemUpdateAltOutlinedIcon sx={{ fontSize: 12 }} /> };
    }
    if (contentType === "Themes") {
      return { key: "thm", label: "THEME", icon: <PaletteOutlinedIcon sx={{ fontSize: 12 }} /> };
    }
    if (contentType === "Avatars") {
      return { key: "ava", label: "AVATAR", icon: <FaceOutlinedIcon sx={{ fontSize: 12 }} /> };
    }
    return { key: contentType.toLowerCase(), label: contentType, icon: <Inventory2OutlinedIcon sx={{ fontSize: 12 }} /> };
  }

  return (
    <Box sx={{ position: "relative", height: "100%" }}>
      <Stack spacing={1.5} sx={{ height: "100%" }}>
      <Stack direction="row" spacing={1}>
        <TextField
          size="small"
          fullWidth
          label="Search title"
          placeholder="Need for Speed"
          defaultValue={activeSearch}
          inputRef={searchInputRef}
          onKeyDown={(e) => {
            if (e.key === "Enter") {
              e.preventDefault();
              triggerSearch();
            }
          }}
          disabled={isCatalogBusy}
        />
        <Button variant="outlined" startIcon={<SearchIcon />} onClick={triggerSearch} disabled={isCatalogBusy}>
          Search
        </Button>
        <IconButton color="primary" onClick={() => setSettingsOpen(true)} aria-label="Downloader settings" disabled={isCatalogBusy}>
          <SettingsIcon />
        </IconButton>
      </Stack>

      <Typography variant="caption" color="text.secondary">
        Wait for loading to finish, then click a game cover to choose edition/version and download target.
      </Typography>

      <Stack spacing={1}>
        <Stack direction="row" spacing={1} flexWrap="wrap">
          {platformOptions.map((p) => (
            <Button
              key={p.id}
              size="small"
              variant={platform === p.id ? "contained" : "outlined"}
              onClick={() => setPlatform(p.id)}
              disabled={isCatalogBusy}
              sx={{ minWidth: p.icon ? 78 : 52, px: p.icon ? 0.8 : 1.4, height: 36 }}
            >
              {p.icon ? (
                <Box
                  sx={{
                    px: 0.9,
                    py: 0.2,
                    borderRadius: 0.8,
                    bgcolor: platform === p.id ? "rgba(255,255,255,0.96)" : "rgba(255,255,255,0.9)",
                    border: "1px solid rgba(30,60,110,0.22)",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    minWidth: 50
                  }}
                >
                  <Box component="img" src={p.icon} alt={p.label} sx={{ height: 18, width: "auto", objectFit: "contain", filter: "contrast(1.15) saturate(1.08)" }} />
                </Box>
              ) : (
                p.label
              )}
            </Button>
          ))}
        </Stack>
        {platform === "PS3" && (
          <Stack direction="row" spacing={1} flexWrap="wrap">
            {ps3ContentTypes.map((c) => (
              <Button key={c} size="small" variant={contentType === c ? "contained" : "outlined"} onClick={() => setContentType(c)} disabled={isCatalogBusy}>
                {c}
              </Button>
            ))}
          </Stack>
        )}
      </Stack>

      <Box sx={{ ml: "auto", alignSelf: "flex-end" }}>
        <Typography variant="caption" color="text.secondary">
          {catalogQuery.isFetching ? "Loading catalog..." : catalogQuery.data ? `${catalogQuery.data.totalGroups} groups / ${catalogQuery.data.totalVariants} files` : "Loading..."}
        </Typography>
      </Box>

      {catalogQuery.isError && (
        <Typography color="error.main" sx={{ fontSize: "0.85rem" }}>
          Catalog could not be loaded. Ensure API host is restarted and `/api/ps3/myrient/catalog` is available.
        </Typography>
      )}

      <Box sx={{ flex: 1, minHeight: 320, overflowY: "auto", pr: 0.5 }}>
        <Box sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(160px, 1fr))", gap: 1 }}>
          {groups.map((g) => (
            <Tooltip
              key={g.id}
              placement="top"
              title={g.alternateTitles.length > 0 ? `Also called: ${g.alternateTitles.join(" | ")}` : ""}
              disableHoverListener={g.alternateTitles.length === 0}
            >
              <Card onClick={() => openEditionDialog(g)} sx={{ cursor: "pointer", border: "1px solid rgba(70,110,170,0.25)" }}>
              <Box sx={{ position: "relative", height: 130, bgcolor: "rgba(10,24,50,0.8)", display: "flex", alignItems: "center", justifyContent: "center" }}>
                {(g.titleId || g.coverUrl) ? (
                  <Box component="img" src={g.titleId ? titleIdCoverUrl(g.titleId) : g.coverUrl!} alt={g.title} sx={{ width: "100%", height: "100%", objectFit: "cover" }} />
                ) : (
                  <Typography sx={{ px: 1, textAlign: "center", fontSize: "0.75rem", color: "text.secondary" }}>{g.platform}</Typography>
                )}
                <Stack
                  direction="row"
                  spacing={0.3}
                  alignItems="center"
                  sx={{
                    position: "absolute",
                    left: 6,
                    bottom: 6,
                    px: 0.5,
                    py: 0.15,
                    borderRadius: 0.6,
                    border: "1px solid rgba(120,165,255,0.38)",
                    bgcolor: "rgba(18,32,64,0.85)",
                    whiteSpace: "nowrap"
                  }}
                >
                  <Typography sx={{ fontSize: "0.58rem", fontWeight: 700, lineHeight: 1 }}>{g.platform}</Typography>
                </Stack>
                <Stack
                  direction="row"
                  spacing={0.4}
                  sx={{
                    position: "absolute",
                    right: 6,
                    bottom: 6,
                    maxWidth: "92%",
                    overflowX: "auto",
                    px: 0.4,
                    py: 0.2,
                    borderRadius: 1,
                    bgcolor: "rgba(5,12,30,0.8)"
                  }}
                >
                  {typeIndicators(g).map((t) => {
                    const meta = typeIndicatorMeta(t);
                    return (
                      <Stack
                        key={meta.key}
                        direction="row"
                        spacing={0.3}
                        alignItems="center"
                        sx={{
                          px: 0.5,
                          py: 0.15,
                          borderRadius: 0.6,
                          border: "1px solid rgba(120,165,255,0.38)",
                          bgcolor: "rgba(18,32,64,0.85)",
                          whiteSpace: "nowrap"
                        }}
                      >
                        {meta.icon}
                        <Typography sx={{ fontSize: "0.58rem", fontWeight: 700, lineHeight: 1 }}>{meta.label}</Typography>
                      </Stack>
                    );
                  })}
                </Stack>
              </Box>
              <CardContent sx={{ p: 1 }}>
                <Typography variant="body2" sx={{ fontWeight: 600, fontSize: "0.78rem" }}>{g.title}</Typography>
                <Typography variant="caption" color="text.secondary">{g.variants.length} versions</Typography>
              </CardContent>
              </Card>
            </Tooltip>
          ))}
        </Box>
      </Box>

      {lastJobInfo && (
        <Typography sx={{ fontSize: "0.75rem" }} color="text.secondary">
          {lastJobInfo}
        </Typography>
      )}

      <Dialog open={settingsOpen} onClose={() => setSettingsOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Downloader Settings</DialogTitle>
        <DialogContent>
          <Stack spacing={1.2} sx={{ mt: 0.5 }}>
            <TextField
              size="small"
              fullWidth
              label="Local Target Folder"
              value={settingsDraftDestination}
              onChange={(e) => setSettingsDraftDestination(e.target.value)}
            />
            <TextField
              size="small"
              fullWidth
              label="Rclone Remote"
              value={settingsDraftRemote}
              onChange={(e) => setSettingsDraftRemote(e.target.value)}
            />
            <Typography variant="caption" color="text.secondary">
              These values are used when starting downloads from the version picker.
            </Typography>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setSettingsOpen(false)}>Cancel</Button>
          <Button variant="contained" onClick={saveSettings}>Save</Button>
        </DialogActions>
      </Dialog>

      <Dialog open={dialogOpen} onClose={() => setDialogOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>{activeGroup?.title ?? "Select Edition"}</DialogTitle>
        <DialogContent>
          <Stack spacing={1.2} sx={{ mt: 0.5 }}>
            <Stack direction="row" spacing={0.8}>
              <Chip size="small" label={activeGroup?.platform ?? "-"} />
              <Chip size="small" label={activeGroup?.contentType ?? "-"} />
            </Stack>

            <Select size="small" value={selectedDialogContentType} onChange={(e) => setSelectedDialogContentType(String(e.target.value))}>
              {dialogContentTypes.map((t) => <MenuItem key={t} value={t}>Type: {t}</MenuItem>)}
            </Select>

            <Select size="small" value={selectedRegion} onChange={(e) => setSelectedRegion(String(e.target.value))}>
              {regions.map((r) => <MenuItem key={r} value={r}>Region: {r}</MenuItem>)}
            </Select>

            <Select size="small" value={selectedVersion} onChange={(e) => setSelectedVersion(String(e.target.value))}>
              {versions.map((v) => <MenuItem key={v} value={v}>Version: {v}</MenuItem>)}
            </Select>

            <Box sx={{ maxHeight: 180, overflowY: "auto", border: "1px solid rgba(70,110,170,0.25)", borderRadius: 1, p: 0.8 }}>
              {filteredVariants.map((v) => (
                <Card key={v.id} onClick={() => setSelectedVariantId(v.id)} sx={{ mb: 0.7, p: 0.8, cursor: "pointer", border: v.id === selectedVariant?.id ? "1px solid rgba(47,135,255,0.85)" : "1px solid rgba(70,110,170,0.25)" }}>
                  <Typography variant="caption" sx={{ display: "block" }}>{v.displayName}</Typography>
                  <Typography variant="caption" color="text.secondary">{v.contentType} | {v.region ?? "-"} | {v.versionTag ?? "Default"}</Typography>
                </Card>
              ))}
            </Box>

            <Stack direction="row" spacing={1}>
              <Button size="small" variant={downloadMode === "local" ? "contained" : "outlined"} onClick={() => setDownloadMode("local")}>
                Download To Local
              </Button>
              <Button size="small" variant={downloadMode === "direct" ? "contained" : "outlined"} onClick={() => setDownloadMode("direct")}>
                Install On PS3
              </Button>
            </Stack>

            {downloadMode === "direct" && (
              <TextField
                size="small"
                label="PS3 IP (optional)"
                placeholder="192.168.178.177"
                value={ps3Ip}
                onChange={(e) => setPs3Ip(e.target.value)}
                helperText="Reachability is verified before direct install starts."
              />
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)}>Cancel</Button>
          <Button variant="contained" onClick={startDownloadFromDialog} disabled={!selectedVariant || downloadMutation.isPending}>
            {downloadMutation.isPending ? "Starting..." : "Start Download"}
          </Button>
        </DialogActions>
      </Dialog>
      </Stack>

      {isCatalogBusy && (
        <Box
          sx={{
            position: "absolute",
            inset: 0,
            zIndex: 50,
            bgcolor: "rgba(3,10,28,0.68)",
            backdropFilter: "blur(1.5px)",
            display: "flex",
            flexDirection: "column",
            alignItems: "center",
            justifyContent: "center",
            gap: 1.2
          }}
        >
          <CircularProgress size={32} />
          <Typography variant="body2" color="text.secondary">
            Loading catalog...
          </Typography>
        </Box>
      )}
    </Box>
  );
}
