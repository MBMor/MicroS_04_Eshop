import react from '@vitejs/plugin-react';
import { loadEnv } from 'vite';
import { defineConfig } from 'vitest/config';

export default defineConfig(({ mode }) => {
    const env = loadEnv(mode, process.cwd(), '');

    const apiProxyTarget =
        env.VITE_DEV_PROXY_TARGET
        || 'http://localhost:5080';

    return {
        plugins: [
            react(),
        ],

        server: {
            proxy: {
                '/api': {
                    target: apiProxyTarget,
                    changeOrigin: true,
                },
            },
        },

        test: {
            environment: 'jsdom',
            setupFiles: [
                './src/test/setup.ts',
            ],
            clearMocks: true,
            restoreMocks: true,
        },
    };
});
