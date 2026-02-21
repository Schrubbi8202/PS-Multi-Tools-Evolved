import AutorenewIcon from "@mui/icons-material/Autorenew";
import SportsEsportsIcon from "@mui/icons-material/SportsEsports";
import { Box, Button, Chip, Paper, Skeleton, Stack, TextField, Typography } from "@mui/material";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { api } from "../api";
import { ConsoleConnector } from "../components/ConsoleConnector";
import { GameCard, normalizeItemType } from "../components/GameCard";
import { GameDetailDrawer } from "../components/GameDetailDrawer";
import { JobToastContainer, useJobToast } from "../components/JobToast";
import { ToolDrawer } from "../components/ToolDrawer";
import { ToolsPanel } from "../components/ToolsPanel";
import type { ActionDescriptorDto, LibraryItemDto } from "../types";

type GalleryFilter = "all" | "ps3" | "ps2" | "psx" | "psp" | "pkg";

function assetPath(relativePath: string): string {
  if (typeof window !== "undefined" && window.location.protocol === "file:") {
    return `./${relativePath.replace(/^\/+/, "")}`;
  }
  return `/${relativePath.replace(/^\/+/, "")}`;
}

const logoFilters: Array<{ value: GalleryFilter; label: string; logo?: string }> = [
  { value: "all", label: "All" },
  { value: "ps3", label: "PS3", logo: "logos/ps3.png" },
  { value: "ps2", label: "PS2", logo: "logos/ps2.png" },
  { value: "psx", label: "PSX", logo: "logos/psx.png" },
  { value: "psp", label: "PSP", logo: "logos/psp.png" },
  { value: "pkg", label: "PKG" }
];

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

export function Ps3LibraryPage() {
  const queryClient = useQueryClient();
  const [selectedId, setSelectedId] = useState<string>("");
  const [galleryFilter, setGalleryFilter] = useState<GalleryFilter>("all");
  const [search, setSearch] = useState("");
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [toolDrawerAction, setToolDrawerAction] = useState<ActionDescriptorDto | null>(null);
  const jobToast = useJobToast();

  const libraryQuery = useQuery({
    queryKey: ["ps3-library-items"],
    queryFn: api.getPs3Items,
    refetchInterval: 5000
  });

  const actionsQuery = useQuery({
    queryKey: ["ps3-actions", selectedId],
    queryFn: () => api.getPs3Actions(selectedId || undefined)
  });

  const effectiveActions = useMemo(() => {
    const actions = actionsQuery.data ?? [];
    if (actions.some((action) => action.actionId === "download.myrient")) {
      return actions;
    }

    return [
      ...actions,
      {
        actionId: "download.myrient",
        label: "Myrient Downloader",
        category: "Downloads",
        requiresSelection: false
      }
    ];
  }, [actionsQuery.data]);

  const scanLocalMutation = useMutation({
    mutationFn: api.scanPs3Local,
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: ["ps3-library-items"] });
      jobToast.fireToast(data.jobId, data.name);
    }
  });

  const scanFtpMutation = useMutation({
    mutationFn: api.scanPs3Ftp,
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: ["ps3-library-items"] });
      queryClient.invalidateQueries({ queryKey: ["saved-consoles"] });
      jobToast.fireToast(data.jobId, data.name);
    }
  });

  const actionMutation = useMutation({
    mutationFn: (actionId: string) => api.executePs3Action(actionId, selectedId || undefined),
    onSuccess: (data) => jobToast.fireToast(data.jobId, data.name)
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
    () => (libraryQuery.data ?? []).find((item) => item.id === selectedId) ?? null,
    [libraryQuery.data, selectedId]
  );

  const isLoading = libraryQuery.isLoading;
  const totalCount = libraryQuery.data?.length ?? 0;

  return (
    <>
      <Stack spacing={2}>
        {/* Hero header */}
        <Paper
          sx={{
            p: 3,
            background: "linear-gradient(135deg, rgba(20,45,100,0.85), rgba(8,20,50,0.7))",
            position: "relative",
            overflow: "hidden"
          }}
        >
          <Box
            sx={{
              position: "absolute",
              top: -40,
              right: -20,
              opacity: 0.06,
              fontSize: 180,
              lineHeight: 1,
              fontWeight: 900,
              color: "#fff",
              userSelect: "none"
            }}
          >
            PS3
          </Box>
          <Stack
            direction={{ xs: "column", md: "row" }}
            justifyContent="space-between"
            alignItems={{ md: "center" }}
            spacing={1.5}
          >
            <Box>
              <Stack direction="row" spacing={1.5} alignItems="center">
                <SportsEsportsIcon sx={{ fontSize: 32, color: "primary.main" }} />
                <Typography variant="h4">PS3 Library</Typography>
              </Stack>
              <Typography color="text.secondary" sx={{ mt: 0.5 }}>
                Browse, manage, and interact with your PlayStation 3 game collection
              </Typography>
            </Box>
            <Stack direction="row" spacing={1} alignItems="center">
              <Chip label={`${filteredItems.length} visible`} color="primary" size="small" />
              <Chip label={`${totalCount} total`} variant="outlined" size="small" />
            </Stack>
          </Stack>
        </Paper>

        {/* Main content area */}
        <Stack direction={{ xs: "column", lg: "row" }} spacing={2}>
          {/* Console connector sidebar */}
          <ConsoleConnector
            onScanLocal={(path) => scanLocalMutation.mutate(path)}
            onScanFtp={(ip) => scanFtpMutation.mutate(ip)}
            scanLocalPending={scanLocalMutation.isPending}
            scanFtpPending={scanFtpMutation.isPending}
          />

          {/* Game gallery */}
          <Paper sx={{ flex: 1, p: 2, minWidth: 0 }}>
            {/* Search + filter bar */}
            <Stack
              direction={{ xs: "column", md: "row" }}
              spacing={1}
              alignItems={{ md: "center" }}
              sx={{ mb: 1.5 }}
            >
              <TextField
                size="small"
                fullWidth
                placeholder="Search by title or title ID…"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                sx={{
                  "& .MuiOutlinedInput-root": {
                    bgcolor: "rgba(8,16,35,0.5)"
                  }
                }}
              />
              <Button
                startIcon={<AutorenewIcon />}
                variant="outlined"
                size="small"
                onClick={() => libraryQuery.refetch()}
                sx={{ whiteSpace: "nowrap" }}
              >
                Refresh
              </Button>
            </Stack>

            {/* Filter pills */}
            <Stack
              direction="row"
              spacing={0.8}
              sx={{ mb: 1.5, overflowX: "auto", pb: 0.5, flexWrap: "nowrap" }}
            >
              {logoFilters.map((filter) => (
                <Chip
                  key={filter.value}
                  label={
                    <Stack direction="row" spacing={0.5} alignItems="center">
                      {filter.logo && (
                        <Box
                          component="img"
                          src={assetPath(filter.logo)}
                          alt={filter.label}
                          sx={{ width: 18, height: 18, objectFit: "contain" }}
                        />
                      )}
                      <span>{filter.label}</span>
                    </Stack>
                  }
                  variant={galleryFilter === filter.value ? "filled" : "outlined"}
                  color={galleryFilter === filter.value ? "primary" : "default"}
                  onClick={() => setGalleryFilter(filter.value)}
                  sx={{
                    cursor: "pointer",
                    transition: "all 180ms ease",
                    ...(galleryFilter === filter.value && {
                      boxShadow: "0 2px 12px rgba(47,135,255,0.3)"
                    })
                  }}
                />
              ))}
            </Stack>

            {/* Game grid */}
            <Box sx={{ maxHeight: 520, overflowY: "auto", pr: 0.5 }}>
              {isLoading ? (
                <Box
                  sx={{
                    display: "grid",
                    gridTemplateColumns: "repeat(auto-fill, minmax(160px, 1fr))",
                    gap: 1.2
                  }}
                >
                  {Array.from({ length: 12 }).map((_, i) => (
                    <Skeleton
                      key={i}
                      variant="rounded"
                      sx={{ width: "100%", height: 260, borderRadius: 2 }}
                      animation="wave"
                    />
                  ))}
                </Box>
              ) : filteredItems.length === 0 ? (
                <Box
                  sx={{
                    py: 8,
                    textAlign: "center"
                  }}
                >
                  <SportsEsportsIcon sx={{ fontSize: 64, color: "rgba(47,135,255,0.2)", mb: 2 }} />
                  <Typography variant="h6" color="text.secondary">
                    No games found
                  </Typography>
                  <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5, mb: 2 }}>
                    {totalCount === 0
                      ? "Connect to your PS3 or scan a local folder to load your library."
                      : "Try adjusting your search or filters."}
                  </Typography>
                  {totalCount === 0 && (
                    <Typography variant="caption" color="text.secondary">
                      Use the Connect panel on the left to get started →
                    </Typography>
                  )}
                </Box>
              ) : (
                <Box
                  sx={{
                    display: "grid",
                    gridTemplateColumns: "repeat(auto-fill, minmax(160px, 1fr))",
                    gap: 1.2
                  }}
                >
                  {filteredItems.map((item) => (
                    <GameCard
                      key={item.id}
                      item={item}
                      selected={item.id === selectedId}
                      onClick={() => {
                        setSelectedId(item.id);
                        setDrawerOpen(true);
                      }}
                    />
                  ))}
                </Box>
              )}
            </Box>
          </Paper>
        </Stack>

        {/* Tools panel */}
        <ToolsPanel
          actions={effectiveActions}
          selectedId={selectedId}
          onQuickAction={(actionId) => actionMutation.mutate(actionId)}
          onOpenTool={(action) => setToolDrawerAction(action)}
        />
      </Stack>

      {/* Game detail drawer */}
      <GameDetailDrawer
        item={drawerOpen ? selectedItem : null}
        onClose={() => setDrawerOpen(false)}
        onActionFired={(jobId, name) => jobToast.fireToast(jobId, name)}
      />

      {/* Tool drawer */}
      <ToolDrawer
        action={toolDrawerAction}
        selectedItemId={selectedId}
        onClose={() => setToolDrawerAction(null)}
        onActionFired={(jobId, name) => jobToast.fireToast(jobId, name)}
      />

      {/* Toast notifications */}
      <JobToastContainer
        entries={jobToast.entries}
        onDismiss={jobToast.dismissEntry}
        onUpdateStatus={jobToast.updateStatus}
      />
    </>
  );
}
