import HomeIcon from "@mui/icons-material/Home";
import SettingsIcon from "@mui/icons-material/Settings";
import SportsEsportsIcon from "@mui/icons-material/SportsEsports";
import { AppBar, Box, Button, Stack, Toolbar, Typography } from "@mui/material";
import { useQuery } from "@tanstack/react-query";
import { useEffect } from "react";
import { Link, Navigate, Route, Routes, useNavigate } from "react-router-dom";
import { api } from "./api";
import { AppShell } from "./components/AppShell";
import { HomePage } from "./pages/HomePage";
import { PlatformPage } from "./pages/PlatformPage";
import { Ps3LibraryPage } from "./pages/Ps3LibraryPage";
import { SettingsPage } from "./pages/SettingsPage";

export default function App() {
  const navigate = useNavigate();
  const platformsQuery = useQuery({ queryKey: ["platforms"], queryFn: api.getPlatforms });

  useEffect(() => {
    if (!window.psmtDesktop) return;
    const unsubscribe = window.psmtDesktop.onOpenFileIntent(async (filePath) => {
      try {
        const route = await api.routeFile(filePath);
        navigate(route.route);
      } catch {
        navigate("/");
      }
    });
    return unsubscribe;
  }, [navigate]);

  return (
    <AppShell>
      <AppBar position="static" color="transparent" elevation={0} sx={{ mb: 3 }}>
        <Toolbar disableGutters sx={{ justifyContent: "space-between" }}>
          <Stack direction="row" spacing={1.5} alignItems="center">
            <SportsEsportsIcon color="primary" />
            <Typography variant="h6">PS Multi Tools 2026</Typography>
          </Stack>
          <Stack direction="row" spacing={1}>
            <Button component={Link} to="/" startIcon={<HomeIcon />} color="inherit">
              Home
            </Button>
            <Button component={Link} to="/platform/ps3/library" color="inherit">
              PS3
            </Button>
            <Button component={Link} to="/settings" startIcon={<SettingsIcon />} color="inherit">
              Settings
            </Button>
          </Stack>
        </Toolbar>
      </AppBar>

      <Box>
        <Routes>
          <Route path="/" element={<HomePage platforms={platformsQuery.data ?? []} />} />
          <Route path="/platform/ps3" element={<Navigate to="/platform/ps3/library" replace />} />
          <Route path="/platform/:platformId" element={<PlatformPage platforms={platformsQuery.data ?? []} />} />
          <Route path="/platform/ps3/library" element={<Ps3LibraryPage />} />
          <Route path="/settings" element={<SettingsPage />} />
        </Routes>
      </Box>
    </AppShell>
  );
}
