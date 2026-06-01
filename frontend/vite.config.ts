import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// The API base for the dev proxy. Override with VITE_API_TARGET if your API runs
// elsewhere (e.g. http://localhost:5000 under docker compose).
const apiTarget = process.env.VITE_API_TARGET ?? "http://localhost:5077";

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      "/api": { target: apiTarget, changeOrigin: true },
    },
  },
});
