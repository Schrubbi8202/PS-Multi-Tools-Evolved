import { app, BrowserWindow } from "electron";
import path from "node:path";
import { spawn, type ChildProcessWithoutNullStreams } from "node:child_process";

const apiHostUrl = process.env.PSMT_API_URL ?? "http://127.0.0.1:5035";
const configuredWebUrl = process.env.PSMT_WEB_URL;
let apiProcess: ChildProcessWithoutNullStreams | null = null;
let mainWindow: BrowserWindow | null = null;

async function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1460,
    height: 920,
    minWidth: 1200,
    minHeight: 760,
    backgroundColor: "#050913",
    webPreferences: {
      preload: path.join(__dirname, "preload.js")
    }
  });

  const localBundle = path.resolve(__dirname, "../../web-ui/dist/index.html");
  const fallbackToLocalBundle = async () => {
    await mainWindow!.loadFile(localBundle);
  };

  if (!configuredWebUrl) {
    await fallbackToLocalBundle();
    return;
  }

  try {
    await mainWindow.loadURL(configuredWebUrl);
  } catch {
    await fallbackToLocalBundle();
  }
}

function startApiHost() {
  const repoRoot = path.resolve(__dirname, "../../..");
  apiProcess = spawn("dotnet", ["run", "--project", "src/PSMultiTools.ApiHost/PSMultiTools.ApiHost.csproj"], {
    cwd: repoRoot,
    env: { ...process.env, ASPNETCORE_URLS: apiHostUrl },
    stdio: "pipe"
  });

  apiProcess.stdout.on("data", (chunk) => {
    process.stdout.write(`[ApiHost] ${chunk}`);
  });

  apiProcess.stderr.on("data", (chunk) => {
    process.stderr.write(`[ApiHost] ${chunk}`);
  });
}

function stopApiHost() {
  if (apiProcess && !apiProcess.killed) {
    apiProcess.kill();
  }
  apiProcess = null;
}

app.on("ready", () => {
  startApiHost();
  createWindow().catch((error) => {
    console.error("Failed to create Electron window:", error);
  });
});

app.on("second-instance", (_event: Electron.Event, argv: string[]) => {
  const fileArg = argv.find((arg) => !arg.startsWith("-") && /\.(pkg|json|elf|bin)$/i.test(arg));
  if (fileArg && mainWindow) {
    mainWindow.webContents.send("open-file-intent", fileArg);
  }
});

app.on("open-file", (event: Electron.Event, filePath: string) => {
  event.preventDefault();
  if (mainWindow) {
    mainWindow.webContents.send("open-file-intent", filePath);
  }
});

app.on("window-all-closed", () => {
  if (process.platform !== "darwin") {
    app.quit();
  }
});

app.on("before-quit", () => {
  stopApiHost();
});
