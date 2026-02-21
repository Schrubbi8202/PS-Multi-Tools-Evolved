import AddIcon from "@mui/icons-material/Add";
import CircleIcon from "@mui/icons-material/Circle";
import CloudSyncIcon from "@mui/icons-material/CloudSync";
import FolderOpenIcon from "@mui/icons-material/FolderOpen";
import RadarIcon from "@mui/icons-material/Radar";
import SaveIcon from "@mui/icons-material/Save";
import {
    Alert,
    Box,
    Button,
    Chip,
    CircularProgress,
    Dialog,
    DialogActions,
    DialogContent,
    DialogContentText,
    DialogTitle,
    Divider,
    Paper,
    Stack,
    TextField,
    Typography
} from "@mui/material";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { api } from "../api";
import type { ConfigDto } from "../types";

type Props = {
    onScanLocal: (path: string) => void;
    onScanFtp: (ip: string) => void;
    scanLocalPending: boolean;
    scanFtpPending: boolean;
};

export function ConsoleConnector({ onScanLocal, onScanFtp, scanLocalPending, scanFtpPending }: Props) {
    const queryClient = useQueryClient();
    const [localPath, setLocalPath] = useState("");
    const [manualIp, setManualIp] = useState("");
    const [showDiscoveryDialog, setShowDiscoveryDialog] = useState(false);

    const configQuery = useQuery({ queryKey: ["config"], queryFn: api.getConfig });
    const savedQuery = useQuery({ queryKey: ["saved-consoles"], queryFn: api.getSavedConsoles, refetchInterval: 15000 });

    const saveConfigMutation = useMutation({
        mutationFn: (cfg: ConfigDto) => api.putConfig(cfg),
        onSuccess: () => queryClient.invalidateQueries({ queryKey: ["config"] })
    });

    const saveConsoleMutation = useMutation({
        mutationFn: (ip: string) => api.saveConsole(ip),
        onSuccess: () => queryClient.invalidateQueries({ queryKey: ["saved-consoles"] })
    });

    const discoverMutation = useMutation({
        mutationFn: () => api.discoverConsoles(),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ["saved-consoles"] });
            setShowDiscoveryDialog(false);
        }
    });

    const savedConsoles = savedQuery.data ?? [];

    return (
        <Paper sx={{ p: 2, width: { lg: 280 }, flexShrink: 0 }}>
            <Typography variant="h6" sx={{ fontSize: "1rem", mb: 1.5 }}>
                🎮 Connect
            </Typography>

            {/* Saved consoles */}
            {savedConsoles.length > 0 && (
                <Box sx={{ mb: 2 }}>
                    <Typography variant="caption" color="text.secondary" sx={{ textTransform: "uppercase", letterSpacing: "0.06em", fontSize: "0.68rem" }}>
                        Saved Consoles
                    </Typography>
                    <Stack spacing={0.8} sx={{ mt: 0.6 }}>
                        {savedConsoles.map((c) => (
                            <Paper
                                key={c.ip}
                                sx={{
                                    p: 1,
                                    cursor: "pointer",
                                    display: "flex",
                                    alignItems: "center",
                                    gap: 1,
                                    border: "1px solid rgba(60,90,160,0.15)",
                                    "&:hover": { borderColor: "primary.main", bgcolor: "rgba(47,135,255,0.06)" },
                                    transition: "all 150ms ease"
                                }}
                                onClick={() => {
                                    onScanFtp(c.ip);
                                    if (configQuery.data) {
                                        saveConfigMutation.mutate({ ...configQuery.data, ps3Ip: c.ip });
                                    }
                                }}
                            >
                                <CircleIcon sx={{ fontSize: 10, color: c.online ? "success.main" : "error.main" }} />
                                <Box sx={{ flex: 1, minWidth: 0 }}>
                                    <Typography variant="body2" sx={{ fontWeight: 600, fontSize: "0.82rem" }}>
                                        {c.label || c.ip}
                                    </Typography>
                                    {c.label && (
                                        <Typography variant="caption" color="text.secondary" sx={{ fontSize: "0.68rem" }}>
                                            {c.ip}
                                        </Typography>
                                    )}
                                </Box>
                                <Chip
                                    label={c.online ? "Online" : "Offline"}
                                    size="small"
                                    color={c.online ? "success" : "default"}
                                    variant="outlined"
                                    sx={{ fontSize: "0.65rem", height: 20 }}
                                />
                            </Paper>
                        ))}
                    </Stack>
                </Box>
            )}

            <Divider sx={{ my: 1.5, borderColor: "rgba(80,130,220,0.12)" }} />

            {/* Manual IP entry */}
            <Typography variant="caption" color="text.secondary" sx={{ textTransform: "uppercase", letterSpacing: "0.06em", fontSize: "0.68rem" }}>
                Manual Connection
            </Typography>
            <Stack spacing={1} sx={{ mt: 0.8 }}>
                <TextField
                    label="PS3 IP Address"
                    size="small"
                    value={manualIp || configQuery.data?.ps3Ip || ""}
                    onChange={(e) => setManualIp(e.target.value)}
                    placeholder="192.168.x.x"
                />
                <Stack direction="row" spacing={0.8}>
                    <Button
                        startIcon={<CloudSyncIcon />}
                        variant="contained"
                        size="small"
                        fullWidth
                        onClick={() => {
                            const ip = manualIp || configQuery.data?.ps3Ip || "";
                            onScanFtp(ip);
                            if (configQuery.data) saveConfigMutation.mutate({ ...configQuery.data, ps3Ip: ip });
                        }}
                        disabled={scanFtpPending || !(manualIp || configQuery.data?.ps3Ip)}
                    >
                        {scanFtpPending ? "Scanning…" : "Connect"}
                    </Button>
                    <Button
                        startIcon={<SaveIcon />}
                        variant="outlined"
                        size="small"
                        onClick={() => {
                            const ip = manualIp || configQuery.data?.ps3Ip || "";
                            if (ip) saveConsoleMutation.mutate(ip);
                        }}
                        disabled={!(manualIp || configQuery.data?.ps3Ip)}
                    >
                        <AddIcon sx={{ fontSize: 16 }} />
                    </Button>
                </Stack>
            </Stack>

            <Divider sx={{ my: 1.5, borderColor: "rgba(80,130,220,0.12)" }} />

            {/* Local folder scan */}
            <Typography variant="caption" color="text.secondary" sx={{ textTransform: "uppercase", letterSpacing: "0.06em", fontSize: "0.68rem" }}>
                Local Scan
            </Typography>
            <Stack spacing={1} sx={{ mt: 0.8 }}>
                <TextField
                    label="Backup folder path"
                    size="small"
                    value={localPath}
                    onChange={(e) => setLocalPath(e.target.value)}
                />
                <Button
                    startIcon={<FolderOpenIcon />}
                    variant="outlined"
                    size="small"
                    onClick={() => onScanLocal(localPath)}
                    disabled={!localPath || scanLocalPending}
                >
                    {scanLocalPending ? "Scanning…" : "Scan Folder"}
                </Button>
            </Stack>

            <Divider sx={{ my: 1.5, borderColor: "rgba(80,130,220,0.12)" }} />

            {/* Network discovery */}
            <Button
                startIcon={<RadarIcon />}
                variant="outlined"
                size="small"
                fullWidth
                onClick={() => setShowDiscoveryDialog(true)}
                color="secondary"
            >
                Scan Network
            </Button>

            {/* Discovery confirmation dialog */}
            <Dialog open={showDiscoveryDialog} onClose={() => setShowDiscoveryDialog(false)}>
                <DialogTitle>Network Discovery</DialogTitle>
                <DialogContent>
                    <DialogContentText>
                        PS Multi Tools will scan your local network subnet for devices with FTP
                        access on port 21. This only checks your local network and is used to find
                        PS3 consoles running webMAN MOD.
                    </DialogContentText>
                    {discoverMutation.isPending && (
                        <Stack direction="row" spacing={1} alignItems="center" sx={{ mt: 2 }}>
                            <CircularProgress size={20} />
                            <Typography variant="body2">Scanning network…</Typography>
                        </Stack>
                    )}
                    {discoverMutation.data && discoverMutation.data.length > 0 && (
                        <Alert severity="success" sx={{ mt: 2 }}>
                            Found {discoverMutation.data.length} device(s): {discoverMutation.data.map((d) => d.ip).join(", ")}
                        </Alert>
                    )}
                    {discoverMutation.data && discoverMutation.data.length === 0 && (
                        <Alert severity="info" sx={{ mt: 2 }}>
                            No devices with FTP access found on this subnet.
                        </Alert>
                    )}
                </DialogContent>
                <DialogActions>
                    <Button onClick={() => setShowDiscoveryDialog(false)}>Cancel</Button>
                    <Button
                        variant="contained"
                        onClick={() => discoverMutation.mutate()}
                        disabled={discoverMutation.isPending}
                    >
                        {discoverMutation.isPending ? "Scanning…" : "Start Scan"}
                    </Button>
                </DialogActions>
            </Dialog>
        </Paper>
    );
}
