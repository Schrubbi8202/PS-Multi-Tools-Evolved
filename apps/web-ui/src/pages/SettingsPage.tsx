import { Alert, Button, Paper, Stack, Switch, TextField, Typography } from "@mui/material";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { api } from "../api";
import type { ConfigDto } from "../types";

export function SettingsPage() {
  const queryClient = useQueryClient();
  const configQuery = useQuery({
    queryKey: ["config"],
    queryFn: api.getConfig
  });
  const [form, setForm] = useState<ConfigDto | null>(null);

  useEffect(() => {
    if (configQuery.data) {
      setForm(configQuery.data);
    }
  }, [configQuery.data]);

  const saveMutation = useMutation({
    mutationFn: api.putConfig,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["config"] });
    }
  });

  if (!form) {
    return <Typography>Loading settings...</Typography>;
  }

  return (
    <Paper sx={{ p: 2 }}>
      <Typography variant="h5">Settings</Typography>
      <Typography color="text.secondary" sx={{ mb: 2 }}>
        Backed by <code>psmt-config.ini</code>
      </Typography>

      <Stack spacing={1.5}>
        <TextField
          label="PS3 IP"
          value={form.ps3Ip ?? ""}
          onChange={(e) => setForm({ ...form, ps3Ip: e.target.value })}
        />
        <TextField
          label="PS5 IP"
          value={form.ps5Ip ?? ""}
          onChange={(e) => setForm({ ...form, ps5Ip: e.target.value })}
        />
        <TextField
          type="number"
          label="PS5 FTP Port"
          value={form.ps5FtpPort ?? ""}
          onChange={(e) => setForm({ ...form, ps5FtpPort: Number(e.target.value) || null })}
        />
        <TextField
          type="number"
          label="PS5 Payload Port"
          value={form.ps5PayloadPort ?? ""}
          onChange={(e) => setForm({ ...form, ps5PayloadPort: Number(e.target.value) || null })}
        />
        <Stack direction="row" spacing={1} alignItems="center">
          <Switch
            checked={form.autoLibraryMusic}
            onChange={(_, checked) => setForm({ ...form, autoLibraryMusic: checked })}
          />
          <Typography>Auto library music</Typography>
        </Stack>
        <Button variant="contained" onClick={() => saveMutation.mutate(form)} disabled={saveMutation.isPending}>
          Save Settings
        </Button>
      </Stack>

      {saveMutation.isSuccess && (
        <Alert severity="success" sx={{ mt: 2 }}>
          Settings saved.
        </Alert>
      )}
    </Paper>
  );
}
