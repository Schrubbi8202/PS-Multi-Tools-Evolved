import { createTheme } from "@mui/material/styles";

export const appTheme = createTheme({
  palette: {
    mode: "dark",
    primary: { main: "#2f87ff" },
    secondary: { main: "#00bcd4" },
    background: { default: "#04060c", paper: "rgba(14, 22, 42, 0.7)" },
    text: { primary: "#e6efff", secondary: "#9cb5df" }
  },
  shape: { borderRadius: 14 },
  typography: {
    fontFamily: "'Segoe UI', 'Inter', sans-serif",
    h3: { fontWeight: 700, letterSpacing: "0.02em" },
    h6: { fontWeight: 600 },
    button: { textTransform: "none", fontWeight: 600 }
  }
});
