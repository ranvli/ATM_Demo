import vue from '@vitejs/plugin-vue';
import { defineConfig } from 'vite';

export default defineConfig({
  plugins: [vue()],
  define: {
    'process.env.NODE_ENV': JSON.stringify('production'),
  },
  build: {
    outDir: '../wwwroot/terminal',
    emptyOutDir: true,
    lib: {
      entry: 'src/main.ts',
      formats: ['es'],
      fileName: () => 'terminal.js',
      cssFileName: 'terminal',
    },
  },
});
