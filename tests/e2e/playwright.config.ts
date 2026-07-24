import {
  defineConfig,
  devices,
} from '@playwright/test';

const baseUrl =
  process.env.E2E_BASE_URL
  ?? 'http://localhost:5173';

export default defineConfig({
  testDir: './specs',

  outputDir:
    '../../artifacts/e2e/test-results',

  fullyParallel: false,

  forbidOnly:
    Boolean(process.env.CI),

  retries:
    process.env.CI ? 1 : 0,

  workers:
    process.env.CI ? 1 : undefined,

  timeout:
    60_000,

  expect: {
    timeout:
      15_000,
  },

  reporter: [
    [
      'list',
    ],
    [
      'html',
      {
        outputFolder:
          '../../artifacts/e2e/playwright-report',

        open:
          'never',
      },
    ],
  ],

  use: {
    baseURL:
      baseUrl,

    trace:
      'on-first-retry',

    screenshot:
      'only-on-failure',

    video:
      'retain-on-failure',
  },

  projects: [
    {
      name:
        'chromium',

      use: {
        ...devices['Desktop Chrome'],
      },
    },
  ],
});