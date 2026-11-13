import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// The SPA calls /api on its own origin; Vite proxies to the ASP.NET Core API so the
// HttpOnly auth cookie and CSRF cookie stay first-party (no CORS needed).
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': { target: process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:5080', changeOrigin: false },
      '/health': { target: process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:5080' },
    },
  },
  build: {
    rollupOptions: {
      output: {
        manualChunks: {
          react: ['react', 'react-dom', 'react-router'],
          mui: ['@mui/material', '@emotion/react', '@emotion/styled'],
          data: ['@tanstack/react-query', 'axios', 'zod', 'react-hook-form'],
        },
      },
    },
  },
})
