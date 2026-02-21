import CloseIcon from "@mui/icons-material/Close";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import {
  Box,
  Button,
  CircularProgress,
  Dialog,
  Drawer,
  IconButton,
  LinearProgress,
  Stack,
  TextField,
  Typography
} from "@mui/material";
import { useMutation } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { api } from "../api";
import { MyrientBrowserPanel } from "./MyrientBrowserPanel";
import type { ActionDescriptorDto, JobStatusDto } from "../types";

type Props = {
  action: ActionDescriptorDto | null;
  selectedItemId: string;
  onClose: () => void;
  onActionFired?: (jobId: string, name: string) => void;
};

export function ToolDrawer({ action, selectedItemId, onClose, onActionFired }: Props) {
  const [params, setParams] = useState<Record<string, string>>({});
  const [jobStatus, setJobStatus] = useState<JobStatusDto | null>(null);

  const executeMutation = useMutation({
    mutationFn: () => api.executePs3Action(action?.actionId ?? "", selectedItemId || undefined, params),
    onSuccess: (data) => {
      onActionFired?.(data.jobId, data.name);
      pollJob(data.jobId);
    }
  });

  function pollJob(jobId: string) {
    const interval = setInterval(async () => {
      try {
        const status = await api.getJob(jobId);
        setJobStatus(status);
        if (status.state === "completed" || status.state === "failed") {
          clearInterval(interval);
        }
      } catch {
        clearInterval(interval);
      }
    }, 1000);
  }

  useEffect(() => {
    if (!action) {
      setParams({});
      setJobStatus(null);
    }
  }, [action]);

  const paramFields = getParamFields(action?.actionId ?? "");
  const isMyrientBrowser = action?.actionId === "download.myrient";

  if (action && isMyrientBrowser) {
    return (
      <Dialog
        open
        fullScreen={false}
        maxWidth={false}
        onClose={onClose}
        PaperProps={{
          sx: {
            background: "linear-gradient(180deg, rgba(8,20,45,1), rgba(4,10,28,1))",
            width: "calc(100vw - 56px)",
            height: "calc(100vh - 56px)",
            maxWidth: "none",
            maxHeight: "none",
            margin: "28px",
            borderRadius: 2
          }
        }}
      >
        <Box
          sx={{
            px: 2.5,
            py: 2,
            borderBottom: "1px solid rgba(80,130,220,0.18)",
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            background: "linear-gradient(135deg, rgba(20,40,80,0.8), rgba(8,18,42,0.65))"
          }}
        >
          <Box>
            <Typography variant="h6" sx={{ fontSize: "1.08rem" }}>
              {action.label}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              {action.category} | {action.actionId}
            </Typography>
          </Box>
          <IconButton onClick={onClose} size="small">
            <CloseIcon />
          </IconButton>
        </Box>
        <Box sx={{ flex: 1, overflow: "hidden", px: 2.5, py: 2 }}>
          <MyrientBrowserPanel />
        </Box>
      </Dialog>
    );
  }

  return (
    <Drawer
      anchor="right"
      open={!!action}
      onClose={onClose}
      PaperProps={{
        sx: {
          width: { xs: "100%", sm: 560, md: 740 },
          display: "flex",
          flexDirection: "column"
        }
      }}
    >
      {action && (
        <>
          <Box
            sx={{
              px: 2.5,
              py: 2,
              borderBottom: "1px solid rgba(80,130,220,0.12)",
              display: "flex",
              alignItems: "center",
              justifyContent: "space-between",
              background: "linear-gradient(135deg, rgba(20,40,80,0.6), rgba(8,18,42,0.4))"
            }}
          >
            <Box>
              <Typography variant="h6" sx={{ fontSize: "1.05rem" }}>
                {action.label}
              </Typography>
              <Typography variant="caption" color="text.secondary">
                {action.category} | {action.actionId}
              </Typography>
            </Box>
            <IconButton onClick={onClose} size="small">
              <CloseIcon />
            </IconButton>
          </Box>

          <Box sx={{ flex: 1, overflow: "auto", px: 2.5, py: 2 }}>
            <>
                {paramFields.length > 0 ? (
                  <Stack spacing={1.5}>
                    <Typography variant="subtitle2" color="text.secondary" sx={{ fontSize: "0.8rem" }}>
                      Parameters
                    </Typography>
                    {paramFields.map((field) => (
                      <TextField
                        key={field.key}
                        label={field.label}
                        size="small"
                        fullWidth
                        value={params[field.key] ?? ""}
                        onChange={(e) => setParams((p) => ({ ...p, [field.key]: e.target.value }))}
                        placeholder={field.placeholder}
                      />
                    ))}
                  </Stack>
                ) : (
                  <Typography variant="body2" color="text.secondary">
                    No additional parameters required. Click Execute to run this tool.
                  </Typography>
                )}

                {action.requiresSelection && !selectedItemId && (
                  <Box
                    sx={{
                      mt: 2,
                      p: 1.5,
                      borderRadius: 2,
                      bgcolor: "rgba(255,82,82,0.1)",
                      border: "1px solid rgba(255,82,82,0.25)"
                    }}
                  >
                    <Typography variant="body2" color="error.main" sx={{ fontSize: "0.82rem" }}>
                      This tool requires a selected library item.
                    </Typography>
                  </Box>
                )}

                {jobStatus && (
                  <Box
                    sx={{
                      mt: 2,
                      p: 1.5,
                      borderRadius: 2,
                      bgcolor:
                        jobStatus.state === "completed"
                          ? "rgba(0,230,118,0.08)"
                          : jobStatus.state === "failed"
                            ? "rgba(255,82,82,0.08)"
                            : "rgba(47,135,255,0.08)",
                      border: `1px solid ${
                        jobStatus.state === "completed"
                          ? "rgba(0,230,118,0.25)"
                          : jobStatus.state === "failed"
                            ? "rgba(255,82,82,0.25)"
                            : "rgba(47,135,255,0.2)"
                      }`
                    }}
                  >
                    <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 0.8 }}>
                      {(jobStatus.state === "queued" || jobStatus.state === "running") && <CircularProgress size={16} />}
                      <Typography variant="body2" sx={{ fontWeight: 600, fontSize: "0.82rem" }}>
                        {jobStatus.state === "completed"
                          ? "Completed"
                          : jobStatus.state === "failed"
                            ? "Failed"
                            : "Running"}
                      </Typography>
                    </Stack>
                    {(jobStatus.state === "running" || jobStatus.state === "queued") && (
                      <LinearProgress variant="determinate" value={jobStatus.progressPercent} sx={{ mb: 0.8, borderRadius: 1 }} />
                    )}
                    <Typography variant="caption" color="text.secondary" sx={{ fontSize: "0.75rem" }}>
                      {jobStatus.message}
                    </Typography>
                  </Box>
                )}
            </>
          </Box>

          <Box
            sx={{
              px: 2.5,
              py: 1.5,
              borderTop: "1px solid rgba(80,130,220,0.12)",
              display: "flex",
              gap: 1
            }}
          >
            <Button variant="outlined" onClick={onClose} sx={{ flex: 1 }}>
              Cancel
            </Button>
            <Button
              variant="contained"
              startIcon={<PlayArrowIcon />}
              onClick={() => executeMutation.mutate()}
              disabled={executeMutation.isPending || (action.requiresSelection && !selectedItemId)}
              sx={{ flex: 2 }}
            >
              {executeMutation.isPending ? "Executing..." : "Execute"}
            </Button>
          </Box>
        </>
      )}
    </Drawer>
  );
}

type ParamField = { key: string; label: string; placeholder: string };

function getParamFields(actionId: string): ParamField[] {
  switch (actionId) {
    case "library.load.local":
      return [{ key: "folderPath", label: "Folder Path", placeholder: "C:\\PS3Games" }];
    case "webman.open.url":
      return [{ key: "url", label: "URL", placeholder: "https://..." }];
    default:
      return [];
  }
}
