import CloseIcon from "@mui/icons-material/Close";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import { Box, Button, Chip, Drawer, IconButton, Stack, Typography } from "@mui/material";
import { useMutation, useQuery } from "@tanstack/react-query";
import { api, coverImageUrl } from "../api";
import { normalizeItemType, getFallbackCover, getTypeLabel } from "./GameCard";
import type { LibraryItemDto } from "../types";
import { useState } from "react";

type Props = {
    item: LibraryItemDto | null;
    onClose: () => void;
    onActionFired?: (jobId: string, name: string) => void;
};

export function GameDetailDrawer({ item, onClose, onActionFired }: Props) {
    const [coverError, setCoverError] = useState(false);

    const actionsQuery = useQuery({
        queryKey: ["ps3-actions", item?.id],
        queryFn: () => api.getPs3Actions(item?.id ?? undefined),
        enabled: !!item
    });

    const actionMutation = useMutation({
        mutationFn: (actionId: string) => api.executePs3Action(actionId, item?.id ?? undefined),
        onSuccess: (data) => onActionFired?.(data.jobId, data.name)
    });

    const itemActions = (actionsQuery.data ?? []).filter((a) => a.category === "Item");

    return (
        <Drawer
            anchor="right"
            open={!!item}
            onClose={onClose}
            PaperProps={{
                sx: {
                    width: { xs: "100%", sm: 420 },
                    p: 0,
                    overflow: "hidden"
                }
            }}
        >
            {item && (
                <Box sx={{ height: "100%", display: "flex", flexDirection: "column" }}>
                    {/* Background blur cover */}
                    <Box
                        sx={{
                            position: "relative",
                            width: "100%",
                            height: 280,
                            overflow: "hidden",
                            flexShrink: 0
                        }}
                    >
                        <Box
                            component="img"
                            src={coverError ? getFallbackCover(item) : coverImageUrl(item.id)}
                            onError={() => setCoverError(true)}
                            alt=""
                            sx={{
                                position: "absolute",
                                inset: 0,
                                width: "100%",
                                height: "100%",
                                objectFit: "cover",
                                filter: "blur(28px) brightness(0.4) saturate(1.4)",
                                transform: "scale(1.2)"
                            }}
                        />
                        <Box
                            component="img"
                            src={coverError ? getFallbackCover(item) : coverImageUrl(item.id)}
                            onError={() => setCoverError(true)}
                            alt={item.title}
                            sx={{
                                position: "relative",
                                display: "block",
                                mx: "auto",
                                mt: 3,
                                height: 220,
                                borderRadius: 2.5,
                                objectFit: "cover",
                                boxShadow: "0 8px 40px rgba(0,0,0,0.6)",
                                zIndex: 1
                            }}
                        />
                        <IconButton
                            onClick={onClose}
                            sx={{
                                position: "absolute",
                                top: 10,
                                right: 10,
                                zIndex: 2,
                                color: "#fff",
                                bgcolor: "rgba(0,0,0,0.4)",
                                "&:hover": { bgcolor: "rgba(0,0,0,0.6)" }
                            }}
                        >
                            <CloseIcon />
                        </IconButton>
                    </Box>

                    {/* Content */}
                    <Box sx={{ flex: 1, overflow: "auto", px: 2.5, py: 2 }}>
                        <Typography variant="h5" sx={{ mb: 0.5 }}>
                            {item.title}
                        </Typography>
                        <Stack direction="row" spacing={1} sx={{ mb: 2 }}>
                            <Chip
                                label={getTypeLabel(item)}
                                size="small"
                                sx={{
                                    fontWeight: 700,
                                    bgcolor: "primary.main",
                                    color: "#fff"
                                }}
                            />
                            {item.region && (
                                <Chip label={item.region} size="small" variant="outlined" />
                            )}
                            {item.location === "remote" && (
                                <Chip label="Remote" size="small" color="secondary" variant="outlined" />
                            )}
                        </Stack>

                        <Stack spacing={1.2}>
                            <DetailRow label="Title ID" value={item.titleId} />
                            <DetailRow label="Content ID" value={item.contentId} />
                            <DetailRow label="Version" value={item.version} />
                            <DetailRow label="App Version" value={item.appVersion} />
                            <DetailRow label="Required FW" value={item.requiredFirmware} />
                            <DetailRow label="Size" value={item.size} />
                            <DetailRow label="Type" value={normalizeItemType(item).toUpperCase()} />
                            <DetailRow
                                label="Source"
                                value={item.filePath ?? item.folderPath}
                                mono
                            />
                        </Stack>

                        {itemActions.length > 0 && (
                            <Box sx={{ mt: 3 }}>
                                <Typography variant="subtitle2" color="text.secondary" sx={{ mb: 1 }}>
                                    Actions
                                </Typography>
                                <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
                                    {itemActions.map((action) => (
                                        <Button
                                            key={action.actionId}
                                            size="small"
                                            variant="contained"
                                            startIcon={<PlayArrowIcon />}
                                            onClick={() => actionMutation.mutate(action.actionId)}
                                            disabled={actionMutation.isPending}
                                        >
                                            {action.label}
                                        </Button>
                                    ))}
                                </Stack>
                            </Box>
                        )}
                    </Box>
                </Box>
            )}
        </Drawer>
    );
}

function DetailRow({ label, value, mono }: { label: string; value: string | null | undefined; mono?: boolean }) {
    if (!value) return null;
    return (
        <Box>
            <Typography variant="caption" color="text.secondary" sx={{ fontSize: "0.7rem", textTransform: "uppercase", letterSpacing: "0.06em" }}>
                {label}
            </Typography>
            <Typography
                variant="body2"
                sx={{
                    wordBreak: "break-all",
                    ...(mono && { fontFamily: "'Consolas', 'Courier New', monospace", fontSize: "0.78rem" })
                }}
            >
                {value}
            </Typography>
        </Box>
    );
}
