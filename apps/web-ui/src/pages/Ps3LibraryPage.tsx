import AutorenewIcon from "@mui/icons-material/Autorenew";
import CloudSyncIcon from "@mui/icons-material/CloudSync";
import FolderOpenIcon from "@mui/icons-material/FolderOpen";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import SaveIcon from "@mui/icons-material/Save";
import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Alert,
  Box,
  Button,
  Chip,
  FormControlLabel,
  Paper,
  Radio,
  RadioGroup,
  Stack,
  TextField,
  Typography
} from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { api } from "../api";
import type { ConfigDto, LibraryItemDto } from "../types";

type GalleryFilter = "all" | "ps3" | "ps2" | "psx" | "psp" | "pkg";
type NormalizedItemType = "backup" | "pkg" | "ps3Iso" | "ps2Iso" | "psxIso" | "pspIso" | "unknown";

const logoFilters: Array<{ value: GalleryFilter; label: string; logo?: string }> = [
  { value: "all", label: "All" },
  { value: "ps3", label: "PS3", logo: "logos/ps3.png" },
  { value: "ps2", label: "PS2", logo: "logos/ps2.png" },
  { value: "psx", label: "PSX", logo: "logos/psx.png" },
  { value: "psp", label: "PSP", logo: "logos/psp.png" },
  { value: "pkg", label: "PKG" }
];

function assetPath(relativePath: string): string {
  if (typeof window !== "undefined" && window.location.protocol === "file:") {
    return `./${relativePath.replace(/^\/+/, "")}`;
  }
  return `/${relativePath.replace(/^\/+/, "")}`;
}

function normalizeItemType(item: LibraryItemDto): NormalizedItemType {
  if (typeof item.itemType === "string") {
    return item.itemType as NormalizedItemType;
  }

  switch (item.itemType as unknown as number) {
    case 0:
      return "backup";
    case 1:
      return "pkg";
    case 2:
      return "ps3Iso";
    case 3:
      return "ps2Iso";
    case 4:
      return "psxIso";
    case 5:
      return "pspIso";
    default:
      return "unknown";
  }
}

function toConsoleFilter(item: LibraryItemDto): GalleryFilter {
  switch (normalizeItemType(item)) {
    case "ps3Iso":
    case "backup":
      return "ps3";
    case "ps2Iso":
      return "ps2";
    case "psxIso":
      return "psx";
    case "pspIso":
      return "psp";
    case "pkg":
      return "pkg";
    default:
      return "all";
  }
}

function getFallbackCover(item: LibraryItemDto): string {
  switch (normalizeItemType(item)) {
    case "ps3Iso":
    case "backup":
      return assetPath("covers/ps3disc.png");
    case "ps2Iso":
      return assetPath("covers/ps2disc.png");
    case "psxIso":
      return assetPath("covers/ps1disc.png");
    case "pspIso":
      return assetPath("covers/umd.png");
    case "pkg":
      return assetPath("covers/pkg.png");
    default:
      return assetPath("covers/blankcover.png");
  }
}

function getDisplayCover(item: LibraryItemDto): string {
  if (
    item.coverImagePath &&
    (item.coverImagePath.startsWith("http://") ||
      item.coverImagePath.startsWith("https://") ||
      item.coverImagePath.startsWith("/"))
  ) {
    return item.coverImagePath;
  }

  return getFallbackCover(item);
}

export function Ps3LibraryPage() {
  const queryClient = useQueryClient();
  const [selectedId, setSelectedId] = useState<string>("");
  const [localPath, setLocalPath] = useState("");
  const [ps3Ip, setPs3Ip] = useState("");
  const [galleryFilter, setGalleryFilter] = useState<GalleryFilter>("all");
  const [search, setSearch] = useState("");
  const [webUrl, setWebUrl] = useState("https://www.psdevwiki.com/ps3/");
  const [expandedCategory, setExpandedCategory] = useState<string>("Library");

  const configQuery = useQuery({ queryKey: ["config"], queryFn: api.getConfig });
  const libraryQuery = useQuery({
    queryKey: ["ps3-library-items"],
    queryFn: api.getPs3Items,
    refetchInterval: 3000
  });
  const actionsQuery = useQuery({
    queryKey: ["ps3-actions", selectedId],
    queryFn: () => api.getPs3Actions(selectedId || undefined)
  });

  const scanLocalMutation = useMutation({
    mutationFn: api.scanPs3Local,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["ps3-library-items"] })
  });

  const scanFtpMutation = useMutation({
    mutationFn: api.scanPs3Ftp,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["ps3-library-items"] })
  });

  const actionMutation = useMutation({
    mutationFn: (actionId: string) => api.executePs3Action(actionId, selectedId || undefined)
  });

  const saveConfigMutation = useMutation({
    mutationFn: (cfg: ConfigDto) => api.putConfig(cfg)
  });

  const filteredItems = useMemo(() => {
    const items = libraryQuery.data ?? [];
    return items.filter((item) => {
      const generation = toConsoleFilter(item);
      const filterMatch = galleryFilter === "all" || generation === galleryFilter;
      const searchMatch =
        search.length === 0 ||
        item.title.toLowerCase().includes(search.toLowerCase()) ||
        (item.titleId ?? "").toLowerCase().includes(search.toLowerCase());
      return filterMatch && searchMatch;
    });
  }, [libraryQuery.data, galleryFilter, search]);

  const selectedItem = useMemo(
    () => filteredItems.find((item) => item.id === selectedId),
    [filteredItems, selectedId]
  );

  const groupedActions = useMemo(() => {
    const list = actionsQuery.data ?? [];
    const categories = Array.from(new Set(list.map((action) => action.category)));
    const grouped = categories.map((category) => ({
      category,
      items: list.filter((action) => action.category === category)
    }));
    const quick = list.filter((action) =>
      ["library.load.local", "library.load.ftp", "tool.iso.tools", "webman.refresh", "webman.open", "webman.shutdown"].includes(action.actionId)
    );
    return { grouped, quick };
  }, [actionsQuery.data]);

  return (
    <Stack spacing={2.2}>
      <Paper sx={{ p: 2.2, background: "linear-gradient(120deg, rgba(20,40,88,0.95), rgba(8,18,40,0.82))" }}>
        <Stack direction={{ xs: "column", md: "row" }} justifyContent="space-between" spacing={2}>
          <Box>
            <Typography variant="h4">PS3 Library</Typography>
            <Typography color="text.secondary">Gallery-first browser with cover preview and generation filters</Typography>
          </Box>
          <Stack direction="row" spacing={1}>
            <Chip label={`${filteredItems.length} visible`} color="primary" />
            <Chip label={`${libraryQuery.data?.length ?? 0} total`} variant="outlined" />
          </Stack>
        </Stack>
      </Paper>

      <Stack direction={{ xs: "column", lg: "row" }} spacing={2}>
        <Paper sx={{ p: 2, width: { lg: 300 } }}>
          <Typography variant="h6">Load Sources</Typography>
          <Stack spacing={1.2} sx={{ mt: 1.3 }}>
            <TextField
              label="Local backup folder"
              size="small"
              value={localPath}
              onChange={(e) => setLocalPath(e.target.value)}
            />
            <Button
              startIcon={<FolderOpenIcon />}
              variant="contained"
              onClick={() => scanLocalMutation.mutate(localPath)}
              disabled={!localPath || scanLocalMutation.isPending}
            >
              Scan Local
            </Button>
            <TextField
              label="PS3 IP"
              size="small"
              value={ps3Ip || configQuery.data?.ps3Ip || ""}
              onChange={(e) => setPs3Ip(e.target.value)}
            />
            <Stack direction="row" spacing={1}>
              <Button
                startIcon={<CloudSyncIcon />}
                variant="contained"
                onClick={() => scanFtpMutation.mutate(ps3Ip || configQuery.data?.ps3Ip || "")}
                disabled={scanFtpMutation.isPending || !(ps3Ip || configQuery.data?.ps3Ip)}
              >
                Scan FTP
              </Button>
              <Button
                startIcon={<SaveIcon />}
                variant="outlined"
                onClick={() => {
                  if (!configQuery.data) return;
                  saveConfigMutation.mutate({ ...configQuery.data, ps3Ip: ps3Ip || configQuery.data.ps3Ip });
                }}
              >
                Save
              </Button>
            </Stack>
          </Stack>
        </Paper>

        <Paper sx={{ p: 2, flex: 1 }}>
          <Stack direction={{ xs: "column", md: "row" }} spacing={1} alignItems={{ md: "center" }}>
            <TextField
              size="small"
              fullWidth
              placeholder="Search title or title id"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
            <Button startIcon={<AutorenewIcon />} variant="outlined" onClick={() => libraryQuery.refetch()}>
              Refresh
            </Button>
          </Stack>

          <RadioGroup
            row
            value={galleryFilter}
            onChange={(_, value) => setGalleryFilter(value as GalleryFilter)}
            sx={{ mt: 1.4, gap: 0.8, overflowX: "auto", flexWrap: "nowrap", pb: 0.5 }}
          >
            {logoFilters.map((filter) => (
              <FormControlLabel
                key={filter.value}
                value={filter.value}
                control={<Radio size="small" />}
                label={
                  <Stack direction="row" spacing={0.8} alignItems="center">
                    {filter.logo ? (
                      <Box
                        component="img"
                        src={assetPath(filter.logo)}
                        alt={filter.label}
                        sx={{ width: 26, height: 26, objectFit: "contain" }}
                      />
                    ) : null}
                    <Typography variant="body2">{filter.label}</Typography>
                  </Stack>
                }
                sx={{ m: 0, border: "1px solid rgba(80,125,210,0.35)", borderRadius: 2, px: 1, py: 0.2 }}
              />
            ))}
          </RadioGroup>

          <Box sx={{ mt: 1.2, maxHeight: 470, overflowY: "auto", pr: 0.5 }}>
            <Box
              sx={{
                display: "grid",
                gridTemplateColumns: "repeat(auto-fill, minmax(150px, 1fr))",
                gap: 1.1
              }}
            >
              {filteredItems.map((item) => (
                <Paper
                  key={item.id}
                  onClick={() => setSelectedId(item.id)}
                  sx={{
                    cursor: "pointer",
                    p: 1,
                    border:
                      item.id === selectedId
                        ? "1px solid rgba(89,155,255,0.95)"
                        : "1px solid rgba(63,88,150,0.25)",
                    background: item.id === selectedId ? "rgba(28,52,101,0.62)" : "rgba(10,21,45,0.55)",
                    transition: "transform 140ms ease",
                    "&:hover": { transform: "translateY(-2px)" }
                  }}
                >
                  <Box
                    component="img"
                    src={getDisplayCover(item)}
                    alt={item.title}
                    sx={{
                      width: "100%",
                      aspectRatio: "3 / 4",
                      objectFit: "cover",
                      borderRadius: 1.5,
                      background: "#0b1224"
                    }}
                  />
                  <Typography
                    variant="body2"
                    sx={{ mt: 0.7, fontWeight: 600, lineHeight: 1.25, height: 36, overflow: "hidden" }}
                  >
                    {item.title}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                    {normalizeItemType(item).toUpperCase()} {item.size ? `- ${item.size}` : ""}
                  </Typography>
                </Paper>
              ))}
            </Box>
          </Box>
        </Paper>

        <Paper sx={{ p: 2, width: { lg: 340 } }}>
          <Typography variant="h6">Now Selected</Typography>
          {selectedItem ? (
            <Stack spacing={1} sx={{ mt: 1 }}>
              <Box
                component="img"
                src={getDisplayCover(selectedItem)}
                alt={selectedItem.title}
                sx={{
                  width: "100%",
                  aspectRatio: "3 / 4",
                  objectFit: "cover",
                  borderRadius: 1.8,
                  background: "#0b1224"
                }}
              />
              <Typography variant="h6">{selectedItem.title}</Typography>
              <Chip label={normalizeItemType(selectedItem).toUpperCase()} sx={{ width: "fit-content" }} />
              <Typography variant="body2">Title ID: {selectedItem.titleId ?? "N/A"}</Typography>
              <Typography variant="body2">Region: {selectedItem.region ?? "N/A"}</Typography>
              <Typography variant="body2">Version: {selectedItem.version ?? "N/A"}</Typography>
              <Typography variant="body2">Required FW: {selectedItem.requiredFirmware ?? "N/A"}</Typography>
              <Typography variant="body2" sx={{ wordBreak: "break-all" }}>
                Source: {selectedItem.filePath ?? selectedItem.folderPath ?? "N/A"}
              </Typography>
            </Stack>
          ) : (
            <Typography color="text.secondary" sx={{ mt: 1 }}>
              Select a cover from the gallery to inspect details.
            </Typography>
          )}
        </Paper>
      </Stack>

      <Paper sx={{ p: 2 }}>
        <Typography variant="h6">Quick Actions</Typography>
        <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap sx={{ mt: 1 }}>
          {groupedActions.quick.map((action) => (
            <Button
              key={action.actionId}
              size="small"
              variant={action.category === "Remote" ? "outlined" : "contained"}
              startIcon={<PlayArrowIcon />}
              onClick={() => actionMutation.mutate(action.actionId)}
              disabled={action.requiresSelection && !selectedId}
            >
              {action.label}
            </Button>
          ))}
        </Stack>

        <Typography variant="h6" sx={{ mt: 2 }}>
          All Actions By Category
        </Typography>
        <Stack spacing={1} sx={{ mt: 1 }}>
          {groupedActions.grouped.map((group) => (
            <Accordion
              key={group.category}
              expanded={expandedCategory === group.category}
              onChange={(_, expanded) => setExpandedCategory(expanded ? group.category : "")}
              disableGutters
              sx={{ background: "rgba(7,16,36,0.6)" }}
            >
              <AccordionSummary expandIcon={<ExpandMoreIcon />} sx={{ minHeight: 40 }}>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  {group.category}
                </Typography>
              </AccordionSummary>
              <AccordionDetails sx={{ pt: 0 }}>
                <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
                  {group.items.map((action) => (
                    <Button
                      key={action.actionId}
                      size="small"
                      variant={group.category === "Remote" ? "outlined" : "contained"}
                      startIcon={<PlayArrowIcon />}
                      onClick={() => actionMutation.mutate(action.actionId)}
                      disabled={action.requiresSelection && !selectedId}
                    >
                      {action.label}
                    </Button>
                  ))}
                </Stack>
              </AccordionDetails>
            </Accordion>
          ))}
        </Stack>

        <Stack direction={{ xs: "column", md: "row" }} spacing={1} sx={{ mt: 1.5 }}>
          <TextField
            size="small"
            fullWidth
            label="URL for 'Open URL in PS3 Browser'"
            value={webUrl}
            onChange={(e) => setWebUrl(e.target.value)}
          />
          <Button
            variant="contained"
            onClick={async () => api.executePs3Action("webman.open.url", selectedId || undefined, { url: webUrl })}
          >
            Send URL Action
          </Button>
        </Stack>
      </Paper>

      {(scanLocalMutation.error || scanFtpMutation.error || actionMutation.error) && (
        <Alert severity="error">
          {(scanLocalMutation.error as Error)?.message ||
            (scanFtpMutation.error as Error)?.message ||
            (actionMutation.error as Error)?.message}
        </Alert>
      )}
    </Stack>
  );
}
