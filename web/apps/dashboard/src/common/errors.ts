import type { TFunction } from '@mastera/i18n';
import { ApiError } from '@mastera/api-client';

/** Translates an API error by its problem "code"; anything unexpected gets a generic message. */
export function apiErrorMessage(t: TFunction, error: unknown, codes: Record<string, string> = {}, values: Record<string, unknown> = {}) {
  if (error instanceof ApiError && error.code && codes[error.code]) return t(codes[error.code]!, values);
  return t('errors.generic');
}
