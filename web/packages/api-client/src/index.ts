/** RFC 9457 problem details, as returned by the Mastera API for every error. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  [extension: string]: unknown;
}

export class ApiError extends Error {
  readonly status: number;
  readonly title: string | undefined;
  readonly problem: ProblemDetails | undefined;

  constructor(status: number, problem?: ProblemDetails) {
    super(problem?.title ?? `Request failed with status ${status}`);
    this.name = 'ApiError';
    this.status = status;
    this.title = problem?.title;
    this.problem = problem;
  }
}

export type HealthStatus = 'Healthy' | 'Degraded' | 'Unhealthy';

export interface ApiClientOptions {
  baseUrl: string;
  fetch?: typeof fetch;
}

/**
 * Thin hand-written client. Endpoints added from Epic 1 onwards will be generated
 * from the API's OpenAPI document (orval) and exported from here.
 */
export function createApiClient({ baseUrl, fetch: fetchImpl = fetch }: ApiClientOptions) {
  const request = async (path: string, init?: RequestInit): Promise<Response> => {
    const response = await fetchImpl(`${baseUrl}${path}`, { credentials: 'include', ...init });
    if (!response.ok) {
      const isProblem = response.headers.get('content-type')?.includes('json') ?? false;
      const problem = isProblem ? ((await response.json()) as ProblemDetails) : undefined;
      throw new ApiError(response.status, problem);
    }
    return response;
  };

  return {
    async getHealth(): Promise<HealthStatus> {
      return (await (await request('/health')).text()) as HealthStatus;
    },
  };
}

export type ApiClient = ReturnType<typeof createApiClient>;
