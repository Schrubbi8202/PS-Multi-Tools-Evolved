import type {
  ActionDescriptorDto,
  ArchiveCatalogDto,
  ConfigDto,
  EntryRouteResultDto,
  JobStartResponseDto,
  JobStatusDto,
  LibraryItemDto,
  NetworkScanResultDto,
  PlatformSummaryDto,
  SavedConsoleDto
} from "./types";

const API_BASE =
  import.meta.env.VITE_API_BASE ??
  (typeof window !== "undefined" && (window.location.protocol === "http:" || window.location.protocol === "https:")
    ? window.location.origin
    : "http://127.0.0.1:5035");

async function json<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...(init?.headers ?? {})
    }
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(text || `Request failed: ${response.status}`);
  }

  return (await response.json()) as T;
}

export function coverImageUrl(itemId: string): string {
  return `${API_BASE}/api/ps3/library/cover/${encodeURIComponent(itemId)}`;
}

export function titleIdCoverUrl(titleId: string): string {
  return `${API_BASE}/api/ps3/covers/by-title-id/${encodeURIComponent(titleId)}`;
}

export const api = {
  health: () => json<{ status: string; service: string; utc: string }>("/api/system/health"),
  getPlatforms: () => json<PlatformSummaryDto[]>("/api/platforms"),
  getConfig: () => json<ConfigDto>("/api/config"),
  putConfig: (config: ConfigDto) => json<ConfigDto>("/api/config", { method: "PUT", body: JSON.stringify(config) }),
  routeFile: (path: string) => json<EntryRouteResultDto>("/api/entry/open-file", { method: "POST", body: JSON.stringify({ path }) }),

  getPs3Items: () => json<LibraryItemDto[]>("/api/ps3/library/items"),
  scanPs3Local: (folderPath: string) =>
    json<JobStartResponseDto>("/api/ps3/library/scan/local", { method: "POST", body: JSON.stringify({ folderPath }) }),
  scanPs3Ftp: (consoleIp: string) =>
    json<JobStartResponseDto>("/api/ps3/library/scan/ftp", { method: "POST", body: JSON.stringify({ consoleIp }) }),
  filterPs3: (filter: Record<string, unknown>) =>
    json<LibraryItemDto[]>("/api/ps3/library/filter", { method: "POST", body: JSON.stringify(filter) }),
  getPs3Actions: (itemId?: string) => json<ActionDescriptorDto[]>(`/api/ps3/actions${itemId ? `?itemId=${encodeURIComponent(itemId)}` : ""}`),
  executePs3Action: (actionId: string, itemId?: string, parameters?: Record<string, string>) =>
    json<JobStartResponseDto>(`/api/ps3/actions/${encodeURIComponent(actionId)}`, {
      method: "POST",
      body: JSON.stringify({ itemId, parameters: parameters ?? {} })
    }),
  getJob: (jobId: string) => json<JobStatusDto>(`/api/jobs/${encodeURIComponent(jobId)}`),

  getSavedConsoles: () => json<SavedConsoleDto[]>("/api/ps3/consoles/saved"),
  saveConsole: (ip: string, label?: string) =>
    json<void>("/api/ps3/consoles/save", { method: "POST", body: JSON.stringify({ ip, label: label ?? null }) }),
  discoverConsoles: (subnetPrefix?: string) =>
    json<NetworkScanResultDto[]>("/api/ps3/consoles/discover", { method: "POST", body: JSON.stringify({ subnetPrefix: subnetPrefix ?? null }) }),
  getMyrientCatalog: (args?: { search?: string; platform?: string; contentType?: string; refresh?: boolean }) => {
    const qs = new URLSearchParams();
    if (args?.search) qs.set("search", args.search);
    if (args?.platform) qs.set("platform", args.platform);
    if (args?.contentType) qs.set("contentType", args.contentType);
    if (args?.refresh) qs.set("refresh", "true");
    return json<ArchiveCatalogDto>(`/api/ps3/myrient/catalog${qs.size > 0 ? `?${qs.toString()}` : ""}`);
  },
  startMyrientRcloneDownload: (request: {
    sourceUrl: string;
    destinationFolder: string;
    remoteName?: string;
    isDirectory?: boolean;
    installToPs3?: boolean;
    ps3Ip?: string;
    platform?: string;
    contentType?: string;
  }) =>
    json<JobStartResponseDto>("/api/ps3/myrient/download-rclone", {
      method: "POST",
      body: JSON.stringify(request)
    })
};
