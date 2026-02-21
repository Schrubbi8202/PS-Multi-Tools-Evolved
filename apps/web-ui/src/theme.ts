import { createTheme } from "@mui/material/styles";

export const appTheme = createTheme({
  palette: {
    mode: "dark",
    primary: { main: "#2f87ff", light: "#64a8ff", dark: "#1a62cc" },
    secondary: { main: "#00e5ff", light: "#6effff", dark: "#00b2cc" },
    success: { main: "#00e676" },
    warning: { main: "#ffab40" },
    error: { main: "#ff5252" },
    background: { default: "#04060c", paper: "rgba(12, 20, 42, 0.72)" },
    text: { primary: "#e6efff", secondary: "#8ba8d4" }
  },
  shape: { borderRadius: 14 },
  typography: {
    fontFamily: "'Inter', 'Segoe UI', sans-serif",
    h3: { fontWeight: 800, letterSpacing: "-0.02em" },
    h4: { fontWeight: 700, letterSpacing: "-0.01em" },
    h5: { fontWeight: 700 },
    h6: { fontWeight: 600 },
    button: { textTransform: "none", fontWeight: 600, letterSpacing: "0.01em" }
  },
  components: {
    MuiPaper: {
      styleOverrides: {
        root: {
          backgroundImage: "none",
          backdropFilter: "blur(24px) saturate(180%)",
          border: "1px solid rgba(80, 130, 220, 0.12)",
          transition: "border-color 200ms ease, box-shadow 200ms ease"
        }
      }
    },
    MuiDrawer: {
      styleOverrides: {
        paper: {
          backgroundImage: "none",
          backgroundColor: "rgba(8, 14, 30, 0.94)",
          backdropFilter: "blur(32px) saturate(200%)",
          borderLeft: "1px solid rgba(80, 130, 220, 0.18)"
        }
      }
    },
    MuiButton: {
      styleOverrides: {
        root: {
          borderRadius: 10,
          padding: "8px 18px",
          transition: "all 180ms ease"
        },
        contained: {
          boxShadow: "0 2px 12px rgba(47, 135, 255, 0.25)",
          "&:hover": {
            boxShadow: "0 4px 20px rgba(47, 135, 255, 0.4)",
            transform: "translateY(-1px)"
          }
        }
      }
    },
    MuiChip: {
      styleOverrides: {
        root: {
          fontWeight: 600,
          borderRadius: 8
        }
      }
    },
    MuiTextField: {
      styleOverrides: {
        root: {
          "& .MuiOutlinedInput-root": {
            borderRadius: 10,
            "&.Mui-focused .MuiOutlinedInput-notchedOutline": {
              borderColor: "#2f87ff",
              boxShadow: "0 0 0 3px rgba(47, 135, 255, 0.15)"
            }
          }
        }
      }
    },
    MuiTab: {
      styleOverrides: {
        root: {
          textTransform: "none",
          fontWeight: 600,
          minHeight: 44,
          fontSize: "0.9rem"
        }
      }
    }
  }
});
