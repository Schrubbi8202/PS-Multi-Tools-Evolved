export {};

declare global {
  interface Window {
    psmtDesktop?: {
      onOpenFileIntent: (callback: (path: string) => void) => () => void;
    };
  }
}
