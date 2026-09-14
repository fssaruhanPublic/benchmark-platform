import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// The production build lands directly in the API's wwwroot, so `dotnet run --project
// src/BenchmarkPlatform.Api` serves the dashboard on its own.
export default defineConfig({
  plugins: [react()],
  build: {
    outDir: '../BenchmarkPlatform.Api/wwwroot',
    emptyOutDir: true,
  },
  server: {
    port: 5173,
    proxy: {
      '/api': 'http://localhost:5080',
      '/health': 'http://localhost:5080',
    },
  },
})
