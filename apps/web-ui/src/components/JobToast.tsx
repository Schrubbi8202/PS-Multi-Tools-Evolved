import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import ErrorIcon from "@mui/icons-material/Error";
import { Alert, CircularProgress, Snackbar, Stack, Typography } from "@mui/material";
import { useEffect, useState } from "react";
import { api } from "../api";
import type { JobStatusDto } from "../types";

type JobToastEntry = {
    jobId: string;
    name: string;
    status: JobStatusDto | null;
};

export function useJobToast() {
    const [entries, setEntries] = useState<JobToastEntry[]>([]);

    function fireToast(jobId: string, name: string) {
        setEntries((prev) => [...prev, { jobId, name, status: null }]);
    }

    function dismissEntry(jobId: string) {
        setEntries((prev) => prev.filter((e) => e.jobId !== jobId));
    }

    function updateStatus(jobId: string, status: JobStatusDto) {
        setEntries((prev) =>
            prev.map((e) => (e.jobId === jobId ? { ...e, status } : e))
        );
    }

    return { entries, fireToast, dismissEntry, updateStatus };
}

type Props = {
    entries: JobToastEntry[];
    onDismiss: (jobId: string) => void;
    onUpdateStatus: (jobId: string, status: JobStatusDto) => void;
};

export function JobToastContainer({ entries, onDismiss, onUpdateStatus }: Props) {
    return (
        <>
            {entries.map((entry, idx) => (
                <JobToastItem
                    key={entry.jobId}
                    entry={entry}
                    index={idx}
                    onDismiss={() => onDismiss(entry.jobId)}
                    onStatusUpdate={(s) => onUpdateStatus(entry.jobId, s)}
                />
            ))}
        </>
    );
}

function JobToastItem({
    entry,
    index,
    onDismiss,
    onStatusUpdate
}: {
    entry: JobToastEntry;
    index: number;
    onDismiss: () => void;
    onStatusUpdate: (status: JobStatusDto) => void;
}) {
    const [open, setOpen] = useState(true);

    useEffect(() => {
        const interval = setInterval(async () => {
            try {
                const status = await api.getJob(entry.jobId);
                onStatusUpdate(status);
                if (status.state === "completed" || status.state === "failed") {
                    clearInterval(interval);
                    setTimeout(() => {
                        setOpen(false);
                        setTimeout(onDismiss, 400);
                    }, 3000);
                }
            } catch {
                clearInterval(interval);
            }
        }, 800);

        return () => clearInterval(interval);
    }, [entry.jobId]);

    const state = entry.status?.state;
    const severity = state === "completed" ? "success" : state === "failed" ? "error" : "info";

    return (
        <Snackbar
            open={open}
            onClose={() => {
                setOpen(false);
                setTimeout(onDismiss, 300);
            }}
            anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
            sx={{ bottom: { xs: 16 + index * 64, sm: 24 + index * 64 } }}
        >
            <Alert
                severity={severity}
                variant="filled"
                onClose={() => {
                    setOpen(false);
                    setTimeout(onDismiss, 300);
                }}
                icon={
                    state === "completed" ? (
                        <CheckCircleIcon />
                    ) : state === "failed" ? (
                        <ErrorIcon />
                    ) : (
                        <CircularProgress size={18} color="inherit" />
                    )
                }
                sx={{
                    minWidth: 280,
                    backdropFilter: "blur(12px)",
                    boxShadow: "0 8px 32px rgba(0,0,0,0.4)"
                }}
            >
                <Stack>
                    <Typography variant="body2" sx={{ fontWeight: 600, fontSize: "0.82rem" }}>
                        {entry.name}
                    </Typography>
                    <Typography variant="caption" sx={{ opacity: 0.85, fontSize: "0.72rem" }}>
                        {entry.status?.message ?? "Starting…"}
                    </Typography>
                </Stack>
            </Alert>
        </Snackbar>
    );
}
