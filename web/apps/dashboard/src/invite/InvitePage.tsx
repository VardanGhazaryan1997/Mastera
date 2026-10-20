import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ApiError, type ApiClient } from '@mastera/api-client';
import { useTranslation } from '@mastera/i18n';
import { Button, TextField } from '@mastera/ui';
import { LanguageSwitcher } from '../LanguageSwitcher';
import { LoginForm } from '../auth/LoginPage';
import { ErrorNotice } from '../common/Notice';
import { apiErrorMessage } from '../common/errors';

interface Props {
  api: ApiClient;
  token: string;
  /** Called after the invitation is accepted; the app then opens the dashboard. */
  onDone: () => void;
}

/** Task 2.2: the page behind the link in an invitation email. */
export function InvitePage({ api, token, onDone }: Props) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const details = useQuery({ queryKey: ['invitation', token], queryFn: () => api.getInvitation(token) });
  const me = useQuery({ queryKey: ['me'], queryFn: () => api.me() });

  const [displayName, setDisplayName] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<{ name?: string; password?: string }>({});

  const accept = useMutation({
    mutationFn: (input: { displayName?: string; password?: string }) => api.acceptInvitation({ token, ...input }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['me'] });
      onDone();
    },
  });

  const onCreateAccount = (event: FormEvent) => {
    event.preventDefault();
    const next: { name?: string; password?: string } = {};
    const length = displayName.trim().length;
    if (length < 2 || length > 100) next.name = t('invite.nameError');
    if (password.length < 8 || !/\d/.test(password)) next.password = t('invite.passwordHint');
    setErrors(next);
    if (!next.name && !next.password) accept.mutate({ displayName: displayName.trim(), password });
  };

  const body = (() => {
    if (details.isPending || me.isPending) return <p className="text-neutral-500">{t('common.loading')}</p>;
    if (details.isError) {
      return <ErrorNotice>{details.error instanceof ApiError && details.error.status === 404 ? t('invite.notFound') : t('errors.generic')}</ErrorNotice>;
    }

    const invitation = details.data;
    const acceptError = accept.isError && (
      <ErrorNotice>
        {apiErrorMessage(
          t,
          accept.error,
          {
            'invitation.expired': 'invite.expired',
            'invitation.used': 'invite.used',
            'invitation.email_mismatch': 'invite.mismatch',
            'auth.password.weak': 'invite.passwordHint',
          },
          { email: invitation.email },
        )}
      </ErrorNotice>
    );

    return (
      <div className="flex flex-col gap-4">
        <h1 className="text-2xl font-semibold">{t('invite.title', { salon: invitation.salonName })}</h1>
        <p className="text-neutral-700">{t('invite.as', { email: invitation.email, role: t(`roles.${invitation.role}`) })}</p>

        {invitation.expired ? (
          <ErrorNotice>{t('invite.expired')}</ErrorNotice>
        ) : invitation.accepted ? (
          <ErrorNotice>{t('invite.used')}</ErrorNotice>
        ) : me.data ? (
          <>
            {acceptError}
            <div>
              <Button onClick={() => accept.mutate({})} disabled={accept.isPending}>
                {t('invite.accept')}
              </Button>
            </div>
          </>
        ) : invitation.hasAccount ? (
          <>
            <p className="text-neutral-700">{t('invite.signInFirst', { email: invitation.email })}</p>
            <LoginForm api={api} headingLevel={2} />
          </>
        ) : (
          <form noValidate onSubmit={onCreateAccount} className="flex flex-col gap-4 rounded-lg border border-neutral-200 bg-white p-6">
            <TextField id="invite-name" label={t('invite.yourName')} autoComplete="name" value={displayName} onChange={setDisplayName} error={errors.name} />
            <TextField
              id="invite-password"
              label={t('invite.password')}
              type="password"
              autoComplete="new-password"
              value={password}
              onChange={setPassword}
              error={errors.password}
            />
            {acceptError}
            <Button type="submit" disabled={accept.isPending}>
              {t('invite.accept')}
            </Button>
          </form>
        )}
      </div>
    );
  })();

  return (
    <div className="min-h-screen bg-neutral-50 text-neutral-900">
      <header className="flex flex-wrap items-center justify-between gap-4 border-b border-neutral-200 bg-white px-4 py-3 sm:px-6">
        <span className="text-lg font-semibold tracking-tight">{t('common.appName')}</span>
        <LanguageSwitcher />
      </header>
      <main className="mx-auto max-w-md px-4 py-12">{body}</main>
    </div>
  );
}
