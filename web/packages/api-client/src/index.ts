/** RFC 9457 problem details, as returned by the Mastera API for every error. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  /** Stable machine-readable error code, e.g. "auth.invalid_credentials". */
  code?: string;
  [extension: string]: unknown;
}

export class ApiError extends Error {
  readonly status: number;
  readonly title: string | undefined;
  readonly code: string | undefined;
  readonly problem: ProblemDetails | undefined;

  constructor(status: number, problem?: ProblemDetails) {
    super(problem?.title ?? `Request failed with status ${status}`);
    this.name = 'ApiError';
    this.status = status;
    this.title = problem?.title;
    this.code = typeof problem?.code === 'string' ? problem.code : undefined;
    this.problem = problem;
  }
}

export type HealthStatus = 'Healthy' | 'Degraded' | 'Unhealthy';

export type Role = 'Owner' | 'Admin' | 'Receptionist' | 'Master';

export interface Membership {
  tenantId: string;
  slug: string;
  name: string;
  role: Role;
}

export interface Me {
  userId: string;
  email: string;
  displayName: string;
  memberships: Membership[];
}

export interface Member {
  userId: string;
  email: string;
  displayName: string;
  role: Role;
}

export const TENANT_HEADER = 'X-Mastera-Tenant';

export interface ApiClientOptions {
  baseUrl: string;
  fetch?: typeof fetch;
  /** Returns the slug of the salon the dashboard is working in; sent as X-Mastera-Tenant. */
  getTenant?: () => string | null;
}

/**
 * Typed client for the Mastera API. Uses the BFF session cookie (credentials: 'include'); the app never
 * handles tokens. Endpoints will be generated from the API's OpenAPI document (orval) as the API grows.
 */
export function createApiClient({ baseUrl, fetch: fetchImpl = fetch, getTenant }: ApiClientOptions) {
  const request = async (path: string, init: RequestInit = {}): Promise<Response> => {
    const headers = new Headers(init.headers);
    const tenant = getTenant?.();
    if (tenant) headers.set(TENANT_HEADER, tenant);

    const response = await fetchImpl(`${baseUrl}${path}`, { credentials: 'include', ...init, headers });
    if (!response.ok) {
      const isJson = response.headers.get('content-type')?.includes('json') ?? false;
      const problem = isJson ? ((await response.json()) as ProblemDetails) : undefined;
      throw new ApiError(response.status, problem);
    }
    return response;
  };

  const sendJson = (method: string, path: string, body?: unknown) =>
    request(path, {
      method,
      headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
    });

  return {
    async getHealth(): Promise<HealthStatus> {
      return (await (await request('/health')).text()) as HealthStatus;
    },

    async login(email: string, password: string): Promise<void> {
      await sendJson('POST', '/api/auth/login', { email, password });
    },

    async logout(): Promise<void> {
      await sendJson('POST', '/api/auth/logout');
    },

    /** The signed-in user and their salons, or null when nobody is signed in. */
    async me(): Promise<Me | null> {
      try {
        return (await (await request('/api/auth/me')).json()) as Me;
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) return null;
        throw error;
      }
    },

    /** People working in the active salon (owners and admins only). */
    async listMembers(): Promise<Member[]> {
      return (await (await request('/api/members')).json()) as Member[];
    },
  };
}

export type ApiClient = ReturnType<typeof createApiClient>;
