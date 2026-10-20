import { useState, type FormEvent } from 'react';
import { useMutation } from '@tanstack/react-query';
import type { ApiClient, InvitableRole } from '@mastera/api-client';
import { useTranslation } from '@mastera/i18n';
import { Button, Select, TextField } from '@mastera/ui';
import { ErrorNotice, SuccessNotice } from '../common/Notice';
import { apiErrorMessage } from '../common/errors';

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/** Sends an invitation email. With a fixed role (a master's profile) there is no role picker. */
export function InviteForm({ api, role, masterId }: { api: ApiClient; role?: InvitableRole; masterId?: string }) {
  const { t } = useTranslation();
  const [email, setEmail] = useState('');
  const [chosenRole, setChosenRole] = useState<InvitableRole>(role ?? 'Admin');
  const [error, setError] = useState<string>();
  const [sentTo, setSentTo] = useState<string>();

  const invite = useMutation({
    mutationFn: (to: string) => api.invite({ email: to, role: role ?? chosenRole, masterId: masterId ?? null }),
    onSuccess: (_, to) => {
      setSentTo(to);
      setEmail('');
    },
  });

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();
    const to = email.trim();
    const message = EMAIL.test(to) ? undefined : t('auth.emailInvalid');
    setError(message);
    setSentTo(undefined);
    if (!message) invite.mutate(to);
  };

  return (
    <form noValidate onSubmit={onSubmit} className="flex flex-col gap-3">
      <div className="flex flex-wrap items-end gap-3">
        <TextField id={`invite-email-${masterId ?? 'staff'}`} label={t('invite.email')} type="email" value={email} onChange={setEmail} error={error} />
        {!role && (
          <Select
            label={t('invite.role')}
            value={chosenRole}
            options={(['Admin', 'Receptionist', 'Master'] as const).map((r) => ({ value: r, label: t(`roles.${r}`) }))}
            onChange={(value) => setChosenRole(value as InvitableRole)}
          />
        )}
        <Button type="submit" variant="secondary" disabled={invite.isPending}>
          {t('invite.send')}
        </Button>
      </div>
      {invite.isError && <ErrorNotice>{apiErrorMessage(t, invite.error)}</ErrorNotice>}
      {sentTo && <SuccessNotice>{t('invite.sent', { email: sentTo })}</SuccessNotice>}
    </form>
  );
}
