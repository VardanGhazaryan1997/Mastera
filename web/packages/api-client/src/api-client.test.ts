import { http, HttpResponse } from 'msw';
import { setupServer } from 'msw/node';
import { afterAll, afterEach, beforeAll, describe, expect, it } from 'vitest';
import { ApiError, createApiClient } from './index';

const baseUrl = 'http://api.test';
const server = setupServer();

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

describe('api client', () => {
  it('reads the health status', async () => {
    server.use(http.get(`${baseUrl}/health`, () => HttpResponse.text('Healthy')));

    await expect(createApiClient({ baseUrl }).getHealth()).resolves.toBe('Healthy');
  });

  it('turns a ProblemDetails response into an ApiError', async () => {
    server.use(
      http.get(`${baseUrl}/health`, () =>
        HttpResponse.json(
          { type: 'about:blank', title: 'Not Found', status: 404 },
          { status: 404, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );

    const error = await createApiClient({ baseUrl }).getHealth().catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({ status: 404, title: 'Not Found' });
  });

  it('sends cookies for the BFF session', async () => {
    let credentials: RequestCredentials | undefined;
    server.use(
      http.get(`${baseUrl}/health`, ({ request }) => {
        credentials = request.credentials;
        return HttpResponse.text('Healthy');
      }),
    );

    await createApiClient({ baseUrl }).getHealth();

    expect(credentials).toBe('include');
  });
});
