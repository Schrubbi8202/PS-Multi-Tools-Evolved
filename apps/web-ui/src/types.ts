export type PlatformSummaryDto = {
  id: string;
  name: string;
  description: string;
  toolCount: number;
};

export type LibraryItemType =
  | "backup"
  | "pkg"
  | "ps3Iso"
  | "ps2Iso"
  | "psxIso"
  | "pspIso"
  | "unknown";

export type LibraryItemLocation = "local" | "remote";

export type LibraryItemDto = {
  id: string;
  platform: string;
  title: string;
  titleId: string | null;
  contentId: string | null;
  region: string | null;
  category: string | null;
  version: string | null;
  appVersion: string | null;
  requiredFirmware: string | null;
  size: string | null;
  filePath: string | null;
  folderPath: string | null;
  itemType: LibraryItemType;
  location: LibraryItemLocation;
  backgroundImagePath: string | null;
  coverImagePath: string | null;
};

export type ActionDescriptorDto = {
  actionId: string;
  label: string;
  category: string;
  requiresSelection: boolean;
};

export type ConfigDto = {
  ps3Ip: string | null;
  ps5Ip: string | null;
  ps5FtpPort: number | null;
  ps5PayloadPort: number | null;
  scanThreads: number | null;
  autoLibraryMusic: boolean;
  loadIcons: boolean;
  loadBackgrounds: boolean;
  skipFileChecks: boolean;
  ftpLoadIcons: boolean;
  ftpLoadBackgrounds: boolean;
  ftpScanAllUsb: boolean;
  ftpScanExt0: boolean;
};

export type JobStartResponseDto = {
  jobId: string;
  name: string;
};

export type JobStatusDto = {
  jobId: string;
  name: string;
  state: "queued" | "running" | "completed" | "failed";
  progressPercent: number;
  message: string;
  createdAt: string;
  finishedAt: string | null;
};

export type EntryRouteResultDto = {
  route: string;
  intent: string;
  sourcePath: string;
};
