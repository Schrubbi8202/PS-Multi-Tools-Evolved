import { Box, CssBaseline } from "@mui/material";
import type { PropsWithChildren } from "react";

export function AppShell({ children }: PropsWithChildren) {
  return (
    <>
      <CssBaseline />
      <style>{`
        @import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&display=swap');

        @keyframes bgShift {
          0% { background-position: 0% 50%; }
          50% { background-position: 100% 50%; }
          100% { background-position: 0% 50%; }
        }

        ::-webkit-scrollbar { width: 6px; }
        ::-webkit-scrollbar-track { background: transparent; }
        ::-webkit-scrollbar-thumb { background: rgba(47,135,255,0.25); border-radius: 3px; }
        ::-webkit-scrollbar-thumb:hover { background: rgba(47,135,255,0.45); }

        * { scrollbar-width: thin; scrollbar-color: rgba(47,135,255,0.25) transparent; }
      `}</style>
      <Box
        sx={{
          minHeight: "100vh",
          color: "text.primary",
          background:
            "radial-gradient(ellipse 1400px 800px at 10% 15%, rgba(47,135,255,0.18), transparent), " +
            "radial-gradient(ellipse 1000px 600px at 90% 85%, rgba(0,229,255,0.1), transparent), " +
            "radial-gradient(ellipse 600px 600px at 50% 50%, rgba(100,60,220,0.06), transparent), " +
            "linear-gradient(155deg, #04060c 0%, #0a1225 40%, #060e1e 70%, #02040a 100%)",
          backgroundSize: "200% 200%",
          animation: "bgShift 30s ease infinite",
          px: { xs: 1.5, sm: 2.5, md: 4 },
          py: { xs: 1.5, sm: 2, md: 3 }
        }}
      >
        {children}
      </Box>
    </>
  );
}
