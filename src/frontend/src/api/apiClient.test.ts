import {
    afterEach,
    beforeEach,
    describe,
    expect,
    it,
    vi,
} from 'vitest';

import { getValidAccessToken } from '../auth/keycloakClient';
import {
    apiRequest,
    ApiError,
} from './apiClient';

vi.mock('../auth/keycloakClient', () => ({
    getValidAccessToken: vi.fn(),
}));

const mockedGetValidAccessToken =
    vi.mocked(getValidAccessToken);

describe('apiRequest', () => {
    const fetchMock = vi.fn<typeof fetch>();

    beforeEach(() => {
        mockedGetValidAccessToken.mockReset();
        fetchMock.mockReset();

        vi.stubGlobal('fetch', fetchMock);
    });

    afterEach(() => {
        vi.unstubAllGlobals();
    });

    it('adds the bearer token to an authenticated request',
        async () => {
            mockedGetValidAccessToken.mockResolvedValue(
                'access-token',
            );

            fetchMock.mockResolvedValue(
                jsonResponse({
                    id: 'order-1',
                }),
            );

            const result = await apiRequest<{
                id: string;
            }>('/api/v1/orders');

            expect(result).toEqual({
                id: 'order-1',
            });

            expect(fetchMock).toHaveBeenCalledOnce();

            const [
                requestUrl,
                requestInit,
            ] = fetchMock.mock.calls[0];

            expect(requestUrl).toBe('/api/v1/orders');

            const headers = new Headers(
                requestInit?.headers,
            );

            expect(
                headers.get('Authorization'),
            ).toBe('Bearer access-token');

            expect(
                headers.get('Accept'),
            ).toBe('application/json');
        });

    it('does not add Authorization when no token exists',
        async () => {
            mockedGetValidAccessToken.mockResolvedValue(null);

            fetchMock.mockResolvedValue(
                jsonResponse({
                    status: 'ok',
                }),
            );

            await apiRequest('/api/v1/products');

            const requestInit =
                fetchMock.mock.calls[0][1];

            const headers = new Headers(
                requestInit?.headers,
            );

            expect(
                headers.has('Authorization'),
            ).toBe(false);
        });

    it('preserves caller headers', async () => {
        mockedGetValidAccessToken.mockResolvedValue(
            'access-token',
        );

        fetchMock.mockResolvedValue(
            jsonResponse({
                created: true,
            }),
        );

        await apiRequest(
            '/api/v1/orders',
            {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Correlation-Id': 'correlation-1',
                },
                body: JSON.stringify({
                    shippingAddress: 'Brno',
                }),
            },
        );

        const requestInit =
            fetchMock.mock.calls[0][1];

        const headers = new Headers(
            requestInit?.headers,
        );

        expect(
            headers.get('Content-Type'),
        ).toBe('application/json');

        expect(
            headers.get('X-Correlation-Id'),
        ).toBe('correlation-1');

        expect(
            headers.get('Authorization'),
        ).toBe('Bearer access-token');
    });

    it('returns undefined for a 204 response',
        async () => {
            mockedGetValidAccessToken.mockResolvedValue(
                'access-token',
            );

            fetchMock.mockResolvedValue(
                new Response(null, {
                    status: 204,
                }),
            );

            const result = await apiRequest<void>(
                '/api/v1/basket',
                {
                    method: 'DELETE',
                },
            );

            expect(result).toBeUndefined();
        });

    it('uses problem detail for an unauthorized response',
        async () => {
            mockedGetValidAccessToken.mockResolvedValue(null);

            fetchMock.mockResolvedValue(
                jsonResponse(
                    {
                        title: 'Unauthorized',
                        detail: 'A valid access token is required.',
                    },
                    401,
                ),
            );

            await expect(
                apiRequest('/api/v1/orders'),
            ).rejects.toMatchObject({
                name: 'ApiError',
                status: 401,
                message:
                    'A valid access token is required.',
            });
        });

    it('uses fallback message for a non-JSON forbidden response',
        async () => {
            mockedGetValidAccessToken.mockResolvedValue(
                'access-token',
            );

            fetchMock.mockResolvedValue(
                new Response('Forbidden', {
                    status: 403,
                    headers: {
                        'Content-Type': 'text/plain',
                    },
                }),
            );

            await expect(
                apiRequest('/api/v1/inventory-items'),
            ).rejects.toEqual(
                new ApiError(
                    'You do not have permission to perform this operation.',
                    403,
                ),
            );
        });
});

function jsonResponse(
    body: unknown,
    status = 200,
): Response {
    return new Response(
        JSON.stringify(body),
        {
            status,
            headers: {
                'Content-Type': 'application/json',
            },
        },
    );
}
