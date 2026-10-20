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

export interface ServiceCategory {
  id: string;
  name: string;
  sortOrder: number;
}

export interface Service {
  id: string;
  categoryId: string | null;
  name: string;
  description: string | null;
  durationMinutes: number;
  price: number;
  isArchived: boolean;
}

export interface ServiceInput {
  name: string;
  durationMinutes: number;
  price: number;
  categoryId?: string | null;
  description?: string | null;
}

/** A service a master offers; null price/duration means the service's own. */
export interface MasterServiceOffer {
  serviceId: string;
  price: number | null;
  durationMinutes: number | null;
}

export interface Master {
  id: string;
  displayName: string;
  bio: string | null;
  isActive: boolean;
  userId: string | null;
  services: MasterServiceOffer[];
}

export interface MasterInput {
  displayName: string;
  bio?: string | null;
  isActive?: boolean;
}

export type InvitableRole = Exclude<Role, 'Owner'>;

export interface InvitationDetails {
  salonName: string;
  salonSlug: string;
  email: string;
  role: Role;
  expired: boolean;
  accepted: boolean;
  hasAccount: boolean;
}

export type Weekday = 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday' | 'Sunday';

export const WEEKDAYS: readonly Weekday[] = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];

/** Times are "HH:mm" in the salon's time zone. */
export interface TimeRange {
  start: string;
  end: string;
}

export interface DaySchedule extends TimeRange {
  breaks?: TimeRange[];
}

export type WorkPattern =
  | { kind: 'weekly'; days: Partial<Record<Weekday, DaySchedule>> }
  | { kind: 'rotating'; anchor: string; cycle: (DaySchedule | null)[] };

export interface ScheduleException {
  id: string;
  from: string;
  to: string;
  /** null = day off. */
  hours: DaySchedule | null;
  note: string | null;
}

export interface Schedule {
  masterId: string;
  pattern: WorkPattern | null;
  exceptions: ScheduleException[];
}

export interface ScheduleExceptionInput {
  from: string;
  to?: string | null;
  hours?: DaySchedule | null;
  note?: string | null;
}

export interface AvailableSlot {
  time: string;
  start: string;
  masters: { masterId: string; masterName: string; price: number; durationMinutes: number; end: string }[];
}

export interface Availability {
  date: string;
  timeZone: string;
  serviceId: string;
  slots: AvailableSlot[];
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
  /** withTenant: false for calls that are not about the active salon (e.g. accepting an invitation to another one). */
  const request = async (path: string, init: RequestInit = {}, withTenant = true): Promise<Response> => {
    const headers = new Headers(init.headers);
    const tenant = withTenant ? getTenant?.() : null;
    if (tenant) headers.set(TENANT_HEADER, tenant);

    const response = await fetchImpl(`${baseUrl}${path}`, { credentials: 'include', ...init, headers });
    if (!response.ok) {
      const isJson = response.headers.get('content-type')?.includes('json') ?? false;
      const problem = isJson ? ((await response.json()) as ProblemDetails) : undefined;
      throw new ApiError(response.status, problem);
    }
    return response;
  };

  const sendJson = (method: string, path: string, body?: unknown, withTenant = true) =>
    request(
      path,
      {
        method,
        headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
        body: body === undefined ? undefined : JSON.stringify(body),
      },
      withTenant,
    );

  const json = async <T>(response: Promise<Response>): Promise<T> => (await (await response).json()) as T;

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

    listCategories: () => json<ServiceCategory[]>(request('/api/service-categories')),
    createCategory: (name: string, sortOrder = 0) =>
      json<ServiceCategory>(sendJson('POST', '/api/service-categories', { name, sortOrder })),

    listServices: (includeArchived = false) =>
      json<Service[]>(request(`/api/services${includeArchived ? '?includeArchived=true' : ''}`)),
    createService: (input: ServiceInput) => json<Service>(sendJson('POST', '/api/services', input)),
    updateService: (id: string, input: ServiceInput) => json<Service>(sendJson('PUT', `/api/services/${id}`, input)),
    archiveService: (id: string) => json<Service>(sendJson('POST', `/api/services/${id}/archive`)),
    restoreService: (id: string) => json<Service>(sendJson('POST', `/api/services/${id}/restore`)),

    listMasters: () => json<Master[]>(request('/api/masters')),
    createMaster: (input: MasterInput) => json<Master>(sendJson('POST', '/api/masters', input)),
    updateMaster: (id: string, input: MasterInput) => json<Master>(sendJson('PUT', `/api/masters/${id}`, input)),
    setMasterServices: (id: string, services: MasterServiceOffer[]) =>
      json<Master>(sendJson('PUT', `/api/masters/${id}/services`, { services })),

    getMyMaster: () => json<Master>(request('/api/masters/me')),
    getSchedule: (masterId: string) => json<Schedule>(request(`/api/masters/${masterId}/schedule`)),
    setPattern: (masterId: string, pattern: WorkPattern | null) =>
      json<Schedule>(sendJson('PUT', `/api/masters/${masterId}/schedule/pattern`, { pattern })),
    addScheduleException: (masterId: string, input: ScheduleExceptionInput) =>
      json<ScheduleException>(sendJson('POST', `/api/masters/${masterId}/schedule/exceptions`, input)),
    async removeScheduleException(masterId: string, exceptionId: string): Promise<void> {
      await sendJson('DELETE', `/api/masters/${masterId}/schedule/exceptions/${exceptionId}`);
    },

    getAvailability: (serviceId: string, date: string, masterId?: string | null) => {
      const query = new URLSearchParams({ serviceId, date });
      if (masterId) query.set('masterId', masterId);
      return json<Availability>(request(`/api/availability?${query.toString()}`));
    },

    async invite(input: { email: string; role: InvitableRole; masterId?: string | null }): Promise<void> {
      await sendJson('POST', '/api/invitations', input);
    },
    getInvitation: (token: string) =>
      json<InvitationDetails>(request(`/api/invitations/${encodeURIComponent(token)}`, {}, false)),
    async acceptInvitation(input: { token: string; displayName?: string; password?: string }): Promise<void> {
      await sendJson('POST', '/api/invitations/accept', input, false);
    },
  };
}

export type ApiClient = ReturnType<typeof createApiClient>;
