import vue from '@vitejs/plugin-vue';
import { defineConfig } from 'vite';

export default defineConfig({
  plugins: [vue()],
  define: {
    // Replace Node's environment check at build time so the bundle runs in the browser.
    'process.env.NODE_ENV': JSON.stringify('production'),
  },
  build: {
    outDir: '../wwwroot/terminal',
    emptyOutDir: true,
    sourcemap: true,
    lib: {
      entry: 'src/main.ts',
      formats: ['es'],
      fileName: () => 'terminal.js',
      cssFileName: 'terminal',
    },
  },
});
