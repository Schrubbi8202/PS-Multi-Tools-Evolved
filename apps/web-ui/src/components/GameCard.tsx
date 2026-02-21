import { Box, Paper, Skeleton, Typography } from "@mui/material";
import { useState } from "react";
import { coverImageUrl } from "../api";
import type { LibraryItemDto, LibraryItemType } from "../types";

type Props = {
    item: LibraryItemDto;
    selected: boolean;
    onClick: () => void;
};

function normalizeItemType(item: LibraryItemDto): LibraryItemType {
    if (typeof item.itemType === "string") return item.itemType;
    switch (item.itemType as unknown as number) {
        case 0: return "backup";
        case 1: return "pkg";
        case 2: return "ps3Iso";
        case 3: return "ps2Iso";
        case 4: return "psxIso";
        case 5: return "pspIso";
        default: return "unknown";
    }
}

const typeBadgeColors: Record<string, string> = {
    ps3Iso: "#2f87ff",
    backup: "#2f87ff",
    ps2Iso: "#7c4dff",
    psxIso: "#ff6d00",
    pspIso: "#00bfa5",
    pkg: "#ff4081",
    unknown: "#546e7a"
};

function assetPath(relativePath: string): string {
    if (typeof window !== "undefined" && window.location.protocol === "file:") {
        return `./${relativePath.replace(/^\/+/, "")}`;
    }
    return `/${relativePath.replace(/^\/+/, "")}`;
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

function getTypeLabel(item: LibraryItemDto): string {
    switch (normalizeItemType(item)) {
        case "ps3Iso": return "PS3";
        case "backup": return "PS3";
        case "ps2Iso": return "PS2";
        case "psxIso": return "PSX";
        case "pspIso": return "PSP";
        case "pkg": return "PKG";
        default: return "?";
    }
}

export { normalizeItemType, getFallbackCover, getTypeLabel };

export function GameCard({ item, selected, onClick }: Props) {
    const [imgLoaded, setImgLoaded] = useState(false);
    const [imgError, setImgError] = useState(false);
    const itemType = normalizeItemType(item);
    const badgeColor = typeBadgeColors[itemType] ?? typeBadgeColors.unknown;
    const shouldTryRemoteCover = !!item.titleId || !!item.folderPath || !!item.filePath;
    const coverSrc = imgError || !shouldTryRemoteCover ? getFallbackCover(item) : coverImageUrl(item.id);

    return (
        <Paper
            onClick={onClick}
            elevation={0}
            sx={{
                cursor: "pointer",
                p: 0,
                overflow: "hidden",
                position: "relative",
                border: selected
                    ? "2px solid rgba(47, 135, 255, 0.9)"
                    : "1px solid rgba(60, 90, 160, 0.15)",
                background: selected
                    ? "linear-gradient(135deg, rgba(28, 55, 110, 0.7), rgba(12, 25, 55, 0.6))"
                    : "linear-gradient(135deg, rgba(10, 20, 45, 0.65), rgba(6, 14, 32, 0.5))",
                backdropFilter: "blur(20px)",
                transition: "all 220ms cubic-bezier(0.4, 0, 0.2, 1)",
                "&:hover": {
                    transform: "translateY(-4px) scale(1.02)",
                    borderColor: "rgba(47, 135, 255, 0.55)",
                    boxShadow: "0 8px 32px rgba(47, 135, 255, 0.15), 0 0 0 1px rgba(47, 135, 255, 0.1)"
                },
                ...(selected && {
                    boxShadow: "0 0 24px rgba(47, 135, 255, 0.3), 0 0 0 1px rgba(47, 135, 255, 0.4)"
                })
            }}
        >
            {/* Type badge */}
            <Box
                sx={{
                    position: "absolute",
                    top: 8,
                    right: 8,
                    zIndex: 2,
                    px: 1,
                    py: 0.2,
                    borderRadius: 1.5,
                    fontSize: "0.65rem",
                    fontWeight: 700,
                    letterSpacing: "0.05em",
                    color: "#fff",
                    backgroundColor: badgeColor,
                    boxShadow: `0 2px 8px ${badgeColor}66`
                }}
            >
                {getTypeLabel(item)}
            </Box>

            {/* Cover image */}
            <Box sx={{ position: "relative", width: "100%", aspectRatio: "3 / 4", overflow: "hidden" }}>
                {!imgLoaded && (
                    <Skeleton
                        variant="rectangular"
                        animation="wave"
                        sx={{
                            position: "absolute",
                            inset: 0,
                            width: "100%",
                            height: "100%",
                            bgcolor: "rgba(20, 40, 80, 0.5)"
                        }}
                    />
                )}
                <Box
                    component="img"
                    src={coverSrc}
                    alt={item.title}
                    loading="lazy"
                    onLoad={() => setImgLoaded(true)}
                    onError={() => {
                        setImgError(true);
                        setImgLoaded(true);
                    }}
                    sx={{
                        width: "100%",
                        height: "100%",
                        objectFit: "cover",
                        opacity: imgLoaded ? 1 : 0,
                        transition: "opacity 350ms ease"
                    }}
                />
            </Box>

            {/* Title */}
            <Box sx={{ p: 1.2, pt: 0.8 }}>
                <Typography
                    variant="body2"
                    sx={{
                        fontWeight: 600,
                        lineHeight: 1.3,
                        height: 34,
                        overflow: "hidden",
                        display: "-webkit-box",
                        WebkitLineClamp: 2,
                        WebkitBoxOrient: "vertical",
                        fontSize: "0.78rem"
                    }}
                >
                    {item.title}
                </Typography>
                {item.size && (
                    <Typography variant="caption" color="text.secondary" sx={{ fontSize: "0.68rem" }}>
                        {item.size}
                    </Typography>
                )}
            </Box>
        </Paper>
    );
}
