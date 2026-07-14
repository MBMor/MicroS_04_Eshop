import { apiConfig, buildApiUrl } from '../config/apiConfig';

type ProblemDetails = {
    title?: string;
    detail?: string;
};

type ApiRequestOptions = {
    includeDevelopmentCustomerId?: boolean;
};

export class ApiError extends Error {
    public readonly status: number;

    public constructor(message: string, status: number) {
        super(message);
        this.name = 'ApiError';
        this.status = status;
    }
}

export async function apiRequest<T>(
    path: string,
    requestInit: RequestInit = {},
    options: ApiRequestOptions = {},
): Promise<T> {
    const headers = new Headers(requestInit.headers);

    headers.set('Accept', 'application/json');

    if (
        options.includeDevelopmentCustomerId
        && apiConfig.developmentCustomerId
    ) {
        headers.set(
            'X-Customer-Id',
            apiConfig.developmentCustomerId,
        );
    }

    const response = await fetch(buildApiUrl(path), {
        ...requestInit,
        headers,
    });

    if (!response.ok) {
        throw new ApiError(
            await readErrorMessage(response),
            response.status,
        );
    }

    if (response.status === 204) {
        return undefined as T;
    }

    return await response.json() as T;
}

async function readErrorMessage(
    response: Response,
): Promise<string> {
    const fallbackMessage = `API request failed with status ${response.status}.`;

    try {
        const problem = await response.json() as ProblemDetails;

        return problem.detail
            ?? problem.title
            ?? fallbackMessage;
    } catch {
        return fallbackMessage;
    }
}
