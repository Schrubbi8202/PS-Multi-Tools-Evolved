import { Box, List, ListItemButton, ListItemText, Paper, Stack, Typography } from "@mui/material";
import { useMemo } from "react";
import { Link, useParams } from "react-router-dom";
import type { PlatformSummaryDto } from "../types";

type Props = {
  platforms: PlatformSummaryDto[];
};

const categories = ["Library", "Tools", "Downloads", "Firmwares", "Remote"];

export function PlatformPage({ platforms }: Props) {
  const { platformId } = useParams();
  const platform = useMemo(() => platforms.find((p) => p.id === platformId), [platformId, platforms]);

  if (!platform) {
    return <Typography>Unknown platform.</Typography>;
  }

  return (
    <Stack spacing={2}>
      <Box>
        <Typography variant="h4">{platform.name}</Typography>
        <Typography color="text.secondary">{platform.description}</Typography>
      </Box>

      <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
        <Paper sx={{ width: { md: 260 }, p: 1 }}>
          <List>
            {categories.map((category) => (
              <ListItemButton key={category}>
                <ListItemText primary={category} secondary={category === "Library" ? "Primary workflow" : undefined} />
              </ListItemButton>
            ))}
          </List>
        </Paper>

        <Paper sx={{ flex: 1, p: 2 }}>
          {platform.id === "ps3" ? (
            <Stack spacing={1}>
              <Typography variant="h6">PS3 Workspace</Typography>
              <Typography color="text.secondary">
                Full parity module is available through the redesigned PS3 page.
              </Typography>
              <Typography component={Link} to="/platform/ps3/library" sx={{ color: "primary.main", mt: 1 }}>
                Open PS3 Library
              </Typography>
            </Stack>
          ) : (
            <Typography color="text.secondary">This platform will be migrated in the next phases.</Typography>
          )}
        </Paper>
      </Stack>
    </Stack>
  );
}
