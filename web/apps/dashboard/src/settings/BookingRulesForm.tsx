import { useEffect, useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { ApiClient } from '@mastera/api-client';
import { useTranslation } from '@mastera/i18n';
import { Button, TextField } from '@mastera/ui';
import { Checkbox } from '../common/Checkbox';
import { ErrorNotice, SuccessNotice } from '../common/Notice';

/** Tasks 5.6/5.8: how late clients may change bookings online, and whether masters see phone numbers. */
export function BookingRulesForm({ api, salon }: { api: ApiClient; salon: string }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const settings = useQuery({ queryKey: ['booking-settings', salon], queryFn: () => api.getBookingSettings() });
  const [hidePhones, setHidePhones] = useState(false);
  const [hours, setHours] = useState('2');
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (settings.data) {
      setHidePhones(settings.data.hideClientPhoneFromMasters);
      setHours(String(settings.data.cancellationWindowHours));
    }
  }, [settings.data]);

  const save = useMutation({
    mutationFn: () => api.updateBookingSettings({ hideClientPhoneFromMasters: hidePhones, cancellationWindowHours: Number(hours) }),
    onSuccess: (saved) => queryClient.setQueryData(['booking-settings', salon], saved),
  });

  const submit = (e: FormEvent) => {
    e.preventDefault();
    const value = Number(hours);
    if (!/^\d+$/.test(hours) || value > 168) {
      setError(t('bookingRules.windowInvalid'));
      return;
    }
    setError(null);
    save.mutate();
  };

  if (settings.isPending) return <p className="text-neutral-500">{t('common.loading')}</p>;
  if (settings.isError) return <ErrorNotice>{t('errors.generic')}</ErrorNotice>;

  return (
    <form onSubmit={submit} noValidate className="flex flex-col gap-3" aria-labelledby="booking-rules-heading">
      <h2 id="booking-rules-heading" className="text-lg font-semibold">
        {t('bookingRules.title')}
      </h2>
      <TextField
        id="cancellation-window"
        label={t('bookingRules.window')}
        inputMode="numeric"
        value={hours}
        onChange={setHours}
        error={error ?? undefined}
        className="max-w-xs"
      />
      <Checkbox label={t('bookingRules.hidePhones')} checked={hidePhones} onChange={setHidePhones} />
      {save.isError && <ErrorNotice>{t('errors.generic')}</ErrorNotice>}
      {save.isSuccess && <SuccessNotice>{t('common.saved')}</SuccessNotice>}
      <div>
        <Button type="submit" disabled={save.isPending}>
          {t('common.save')}
        </Button>
      </div>
    </form>
  );
}
