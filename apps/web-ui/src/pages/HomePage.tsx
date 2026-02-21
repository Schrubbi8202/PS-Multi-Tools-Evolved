import { Box, Chip, Paper, Stack, TextField, Typography } from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
import type { PlatformSummaryDto } from "../types";

type Props = {
  platforms: PlatformSummaryDto[];
};

export function HomePage({ platforms }: Props) {
  return (
    <Stack spacing={3}>
      <Box>
        <Typography variant="h3">PS Multi Tools</Typography>
        <Typography color="text.secondary">Modern local desktop workspace for PlayStation tooling</Typography>
      </Box>

      <TextField
        placeholder="Search tools, actions, platforms..."
        fullWidth
        slotProps={{
          input: {
            sx: {
              bgcolor: "rgba(12,20,38,0.6)",
              borderRadius: 2
            }
          }
        }}
      />

      <Stack direction="row" spacing={2} sx={{ overflowX: "auto", pb: 1 }}>
        {platforms.map((platform) => (
          <Paper
            key={platform.id}
            component={RouterLink}
            to={platform.id === "ps3" ? "/platform/ps3/library" : `/platform/${platform.id}`}
            sx={{
              minWidth: 190,
              px: 2,
              py: 2,
              textDecoration: "none",
              color: "inherit",
              transition: "transform 180ms ease",
              "&:hover": {
                transform: "translateY(-4px)",
                borderColor: "rgba(81,146,255,0.6)"
              }
            }}
          >
            <Typography variant="h6">{platform.name}</Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
              {platform.description}
            </Typography>
            <Chip label={`${platform.toolCount}+ tools`} size="small" sx={{ mt: 1.4 }} />
          </Paper>
        ))}
      </Stack>
    </Stack>
  );
}
