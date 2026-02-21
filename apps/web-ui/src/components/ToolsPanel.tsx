import BuildIcon from "@mui/icons-material/Build";
import CloudDownloadIcon from "@mui/icons-material/CloudDownload";
import DesktopWindowsIcon from "@mui/icons-material/DesktopWindows";
import LibraryBooksIcon from "@mui/icons-material/LibraryBooks";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import SettingsRemoteIcon from "@mui/icons-material/SettingsRemote";
import { Box, Paper, Stack, Tab, Tabs, Typography } from "@mui/material";
import { useMemo, useState } from "react";
import type { ActionDescriptorDto } from "../types";

type Props = {
    actions: ActionDescriptorDto[];
    selectedId: string;
    onQuickAction: (actionId: string) => void;
    onOpenTool: (action: ActionDescriptorDto) => void;
};

const categoryConfig: Record<string, { icon: React.ReactNode; color: string; description: string }> = {
    Library: { icon: <LibraryBooksIcon />, color: "#2f87ff", description: "Load and manage your game library" },
    Tools: { icon: <BuildIcon />, color: "#7c4dff", description: "File conversion, extraction, and editing tools" },
    Downloads: { icon: <CloudDownloadIcon />, color: "#00e676", description: "Browse and download packages" },
    Remote: { icon: <SettingsRemoteIcon />, color: "#00e5ff", description: "webMAN remote control commands" }
};

const complexToolIds = new Set([
    "tool.ftp.browser", "tool.param.editor", "tool.iso.tools", "tool.pkg.extract",
    "tool.pup.unpacker", "tool.rco.dumper", "tool.self.reader", "tool.batch.rename",
    "tool.coreos", "tool.fix.tar", "download.pkg.browser", "download.myrient",
    "library.load.local", "library.load.ftp"
]);

const quickFireToolIcons: Record<string, React.ReactNode> = {};

const toolDescriptions: Record<string, string> = {
    "library.load.local": "Scan a local backup folder for PS3 games",
    "library.load.ftp": "Load games from the PS3 via FTP",
    "tool.batch.rename": "Batch rename files with patterns",
    "tool.coreos": "Core_OS utilities for system files",
    "tool.fix.tar": "Fix corrupted tar archives",
    "tool.iso.tools": "Convert, patch, split, and encrypt ISOs",
    "tool.pkg.extract": "Extract PKG package contents",
    "tool.pup.unpacker": "Unpack PS3 firmware PUP files",
    "tool.rco.dumper": "Dump and extract RCO resource files",
    "tool.self.reader": "Read and analyze SELF executables",
    "tool.ftp.browser": "Browse files on your PS3 via FTP",
    "tool.param.editor": "Edit PARAM.SFO metadata files",
    "download.pkg.browser": "Browse and download PKG packages",
    "download.myrient": "Browse Redump and No-Intro sources for PS1/PS2/PS3/PSP content",
    "webman.open": "Open the webMAN MOD interface in browser",
    "webman.open.temp.c": "View PS3 temperature in Celsius",
    "webman.open.temp.f": "View PS3 temperature in Fahrenheit",
    "webman.refresh": "Tell PS3 to rescan all game content",
    "webman.reload.game": "Reload the currently mounted game",
    "webman.eject.disc": "Eject the disc from the PS3",
    "webman.insert.disc": "Insert/mount the disc on PS3",
    "webman.play.disc": "Start playing the inserted disc",
    "webman.exit.to.xmb": "Exit current game back to XMB menu",
    "webman.toggle.recording": "Toggle on/off video recording",
    "webman.toggle.bgm": "Toggle in-game background music",
    "webman.shutdown": "Safely shut down the PS3",
    "webman.restart": "Restart the PS3 system",
    "webman.restart.scan": "Restart and rescan content",
    "webman.restart.min": "Restart showing min firmware version",
    "webman.reboot.hard": "Force hard reboot the PS3",
    "webman.reboot.soft": "Perform a soft reboot",
    "webman.reboot.quick": "Quick reboot the PS3",
    "webman.reboot.vsh": "Reboot via VSH module",
    "webman.open.url": "Open a URL in the PS3 browser",
    "webman.popup": "Show system info popup on PS3"
};

export function ToolsPanel({ actions, selectedId, onQuickAction, onOpenTool }: Props) {
    const [activeTab, setActiveTab] = useState(0);

    const categories = useMemo(() => {
        const cats = Array.from(new Set(actions.map((a) => a.category)));
        return cats.map((cat) => ({
            name: cat,
            actions: actions.filter((a) => a.category === cat),
            ...(categoryConfig[cat] ?? { icon: <DesktopWindowsIcon />, color: "#78909c", description: "" })
        }));
    }, [actions]);

    const currentCategory = categories[activeTab];

    return (
        <Paper sx={{ p: 0, overflow: "hidden" }}>
            {/* Header with tabs */}
            <Box sx={{ borderBottom: "1px solid rgba(80,130,220,0.12)", px: 1 }}>
                <Tabs
                    value={activeTab}
                    onChange={(_, v) => setActiveTab(v)}
                    variant="scrollable"
                    scrollButtons="auto"
                    sx={{ minHeight: 48 }}
                >
                    {categories.map((cat) => (
                        <Tab
                            key={cat.name}
                            icon={cat.icon as React.ReactElement}
                            iconPosition="start"
                            label={
                                <Stack direction="row" spacing={0.5} alignItems="center">
                                    <span>{cat.name}</span>
                                    <Box
                                        component="span"
                                        sx={{
                                            fontSize: "0.65rem",
                                            bgcolor: "rgba(47,135,255,0.15)",
                                            px: 0.7,
                                            py: 0.1,
                                            borderRadius: 1,
                                            fontWeight: 700
                                        }}
                                    >
                                        {cat.actions.length}
                                    </Box>
                                </Stack>
                            }
                            sx={{ minHeight: 48, py: 0.5 }}
                        />
                    ))}
                </Tabs>
            </Box>

            {/* Tool cards grid */}
            {currentCategory && (
                <Box sx={{ p: 2 }}>
                    {currentCategory.description && (
                        <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5, fontSize: "0.82rem" }}>
                            {currentCategory.description}
                        </Typography>
                    )}
                    <Box
                        sx={{
                            display: "grid",
                            gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)", md: "repeat(3, 1fr)", lg: "repeat(4, 1fr)" },
                            gap: 1.2
                        }}
                    >
                        {currentCategory.actions.map((action) => {
                            const isComplex = complexToolIds.has(action.actionId);
                            const isDisabled = action.requiresSelection && !selectedId;
                            const desc = toolDescriptions[action.actionId] ?? "";

                            return (
                                <Paper
                                    key={action.actionId}
                                    onClick={() => {
                                        if (isDisabled) return;
                                        if (isComplex) {
                                            onOpenTool(action);
                                        } else {
                                            onQuickAction(action.actionId);
                                        }
                                    }}
                                    sx={{
                                        p: 1.5,
                                        cursor: isDisabled ? "not-allowed" : "pointer",
                                        opacity: isDisabled ? 0.4 : 1,
                                        border: "1px solid rgba(60,90,160,0.12)",
                                        background: isComplex
                                            ? "linear-gradient(135deg, rgba(20,35,70,0.5), rgba(10,20,45,0.4))"
                                            : "rgba(8,16,35,0.4)",
                                        transition: "all 180ms ease",
                                        "&:hover": isDisabled
                                            ? {}
                                            : {
                                                borderColor: `${currentCategory.color}88`,
                                                transform: "translateY(-2px)",
                                                boxShadow: `0 4px 16px ${currentCategory.color}22`
                                            }
                                    }}
                                >
                                    <Stack direction="row" spacing={1} alignItems="flex-start">
                                        <Box
                                            sx={{
                                                mt: 0.2,
                                                color: isComplex ? currentCategory.color : "text.secondary",
                                                fontSize: "1.1rem"
                                            }}
                                        >
                                            {isComplex ? (currentCategory.icon) : <PlayArrowIcon sx={{ fontSize: "1.1rem" }} />}
                                        </Box>
                                        <Box sx={{ flex: 1, minWidth: 0 }}>
                                            <Typography
                                                variant="body2"
                                                sx={{
                                                    fontWeight: 600,
                                                    fontSize: "0.8rem",
                                                    whiteSpace: "nowrap",
                                                    overflow: "hidden",
                                                    textOverflow: "ellipsis"
                                                }}
                                            >
                                                {action.label}
                                            </Typography>
                                            {desc && (
                                                <Typography
                                                    variant="caption"
                                                    color="text.secondary"
                                                    sx={{
                                                        fontSize: "0.68rem",
                                                        display: "-webkit-box",
                                                        WebkitLineClamp: 2,
                                                        WebkitBoxOrient: "vertical",
                                                        overflow: "hidden"
                                                    }}
                                                >
                                                    {desc}
                                                </Typography>
                                            )}
                                        </Box>
                                    </Stack>
                                    {isComplex && (
                                        <Box
                                            sx={{
                                                mt: 0.8,
                                                fontSize: "0.6rem",
                                                color: currentCategory.color,
                                                fontWeight: 700,
                                                textTransform: "uppercase",
                                                letterSpacing: "0.08em"
                                            }}
                                        >
                                            Opens panel →
                                        </Box>
                                    )}
                                </Paper>
                            );
                        })}
                    </Box>
                </Box>
            )}
        </Paper>
    );
}

