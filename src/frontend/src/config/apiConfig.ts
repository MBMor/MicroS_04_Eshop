const defaultApiBaseUrl = 'http://localhost:5080';

export const apiConfig = {
  baseUrl: import.meta.env.VITE_API_BASE_URL ?? defaultApiBaseUrl,
} as const;