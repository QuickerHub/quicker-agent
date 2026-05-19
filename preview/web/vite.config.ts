import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

const apiTarget = process.env.QKAGENT_PREVIEW_API ?? "http://127.0.0.1:8765";

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5176,
    strictPort: true,
    proxy: {
      "/api": { target: apiTarget, changeOrigin: true },
    },
  },
  build: {
    outDir: "dist",
    emptyOutDir: true,
  },
});
