import { Box, CssBaseline } from "@mui/material";
import type { PropsWithChildren } from "react";

export function AppShell({ children }: PropsWithChildren) {
  return (
    <>
      <CssBaseline />
      <Box
        sx={{
          minHeight: "100vh",
          color: "text.primary",
          background:
            "radial-gradient(1200px 700px at 12% 18%, rgba(37,112,255,0.25), rgba(4,6,12,0)), radial-gradient(900px 500px at 85% 80%, rgba(0,188,212,0.14), rgba(4,6,12,0)), linear-gradient(145deg, #04060c, #070d1e 55%, #02040a)",
          px: { xs: 2, md: 4 },
          py: { xs: 2, md: 3 }
        }}
      >
        {children}
      </Box>
    </>
  );
}
