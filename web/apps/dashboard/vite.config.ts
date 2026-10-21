import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

export default defineConfig({
  plugins: [react(), tailwindcss()],
  // /api and the live-notification hub go to the .NET API, so the auth cookie is same-origin.
  server: { port: 5173, strictPort: true, proxy: { '/api': 'http://localhost:5080', '/hubs': { target: 'http://localhost:5080', ws: true } } },
  preview: { port: 5173, strictPort: true },
  test: {
    environment: 'jsdom',
    setupFiles: ['../../test/setup.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
  },
});
