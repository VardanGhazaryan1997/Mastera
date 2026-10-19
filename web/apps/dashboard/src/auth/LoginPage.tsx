import { useState, type FormEvent } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { ApiError, type ApiClient } from '@mastera/api-client';
import { useTranslation } from '@mastera/i18n';
import { Button, TextField } from '@mastera/ui';
import { LanguageSwitcher } from '../LanguageSwitcher';

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

interface FieldErrors {
  email?: string;
  password?: string;
}

export function LoginPage({ api }: { api: ApiClient }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<FieldErrors>({});

  const login = useMutation({
    mutationFn: () => api.login(email.trim(), password),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['me'] }),
  });

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();
    const next: FieldErrors = {};
    if (!email.trim()) next.email = t('auth.required');
    else if (!EMAIL.test(email.trim())) next.email = t('auth.emailInvalid');
    if (!password) next.password = t('auth.required');
    setErrors(next);
    if (!next.email && !next.password) login.mutate();
  };

  const serverError = (() => {
    if (!login.error) return null;
    if (login.error instanceof ApiError && login.error.code === 'auth.locked_out') return t('auth.lockedOut');
    if (login.error instanceof ApiError && login.error.status === 401) return t('auth.invalidCredentials');
    return t('auth.genericError');
  })();

  return (
    <div className="min-h-screen bg-neutral-50 text-neutral-900">
      <header className="flex flex-wrap items-center justify-between gap-4 border-b border-neutral-200 bg-white px-4 py-3 sm:px-6">
        <span className="text-lg font-semibold tracking-tight">{t('common.appName')}</span>
        <LanguageSwitcher />
      </header>
      <main className="mx-auto max-w-sm px-4 py-16">
        <form noValidate onSubmit={onSubmit} className="flex flex-col gap-4 rounded-lg border border-neutral-200 bg-white p-6">
          <h1 className="text-2xl font-semibold">{t('auth.title')}</h1>
          <TextField
            id="login-email"
            label={t('auth.email')}
            type="email"
            autoComplete="username"
            value={email}
            onChange={setEmail}
            error={errors.email}
          />
          <TextField
            id="login-password"
            label={t('auth.password')}
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={setPassword}
            error={errors.password}
          />
          {serverError && (
            <p role="alert" className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-800">
              {serverError}
            </p>
          )}
          <Button type="submit" disabled={login.isPending}>
            {login.isPending ? t('auth.signingIn') : t('auth.submit')}
          </Button>
        </form>
      </main>
    </div>
  );
}
