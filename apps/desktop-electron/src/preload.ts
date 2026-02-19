import { contextBridge, ipcRenderer } from "electron";

contextBridge.exposeInMainWorld("psmtDesktop", {
  onOpenFileIntent: (callback: (path: string) => void) => {
    const listener = (_: unknown, path: string) => callback(path);
    ipcRenderer.on("open-file-intent", listener);
    return () => ipcRenderer.removeListener("open-file-intent", listener);
  }
});
