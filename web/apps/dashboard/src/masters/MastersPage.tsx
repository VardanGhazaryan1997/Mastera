import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { ApiClient, Master, Service } from '@mastera/api-client';
import { useTranslation } from '@mastera/i18n';
import { Button, TextField } from '@mastera/ui';
import { Checkbox } from '../common/Checkbox';
import { ErrorNotice, SuccessNotice } from '../common/Notice';
import { apiErrorMessage } from '../common/errors';
import { InviteForm } from '../invite/InviteForm';
import { ScheduleEditor } from '../schedule/ScheduleEditor';

interface Props {
  api: ApiClient;
  salon: string;
}

/** Tasks 2.2–2.4: masters, the services each offers (with own price/duration), and inviting them. */
export function MastersPage({ api, salon }: Props) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [name, setName] = useState('');
  const [nameError, setNameError] = useState<string>();

  const masters = useQuery({ queryKey: ['masters', salon], queryFn: () => api.listMasters() });
  const services = useQuery({ queryKey: ['services', salon, false], queryFn: () => api.listServices(false) });

  const create = useMutation({
    mutationFn: () => api.createMaster({ displayName: name.trim() }),
    onSuccess: async (master) => {
      setName('');
      await queryClient.invalidateQueries({ queryKey: ['masters', salon] });
      setSelectedId(master.id);
    },
  });

  const onAdd = (event: FormEvent) => {
    event.preventDefault();
    const length = name.trim().length;
    const message = length < 2 || length > 60 ? t('masters.nameError') : undefined;
    setNameError(message);
    if (!message) create.mutate();
  };

  const selected = masters.data?.find((m) => m.id === selectedId) ?? null;

  return (
    <div className="grid gap-6 lg:grid-cols-[18rem_1fr]">
      <div className="flex flex-col gap-4">
        {masters.isPending ? (
          <p className="text-neutral-500">{t('common.loading')}</p>
        ) : masters.isError ? (
          <ErrorNotice>{t('errors.generic')}</ErrorNotice>
        ) : (
          <ul className="flex flex-col gap-1">
            {masters.data.map((m) => (
              <li key={m.id}>
                <button
                  type="button"
                  aria-current={m.id === selectedId ? 'true' : undefined}
                  onClick={() => setSelectedId(m.id)}
                  className={`flex w-full items-center justify-between gap-2 rounded-md border px-3 py-2 text-left ${
                    m.id === selectedId ? 'border-neutral-900 bg-white' : 'border-neutral-200 bg-white hover:border-neutral-400'
                  }`}
                >
                  <span className="font-medium">{m.displayName}</span>
                  <span className={`text-xs ${m.isActive ? 'text-green-700' : 'text-neutral-500'}`}>
                    {t(m.isActive ? 'common.active' : 'common.inactive')}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        )}
        <form noValidate onSubmit={onAdd} className="flex flex-col gap-2">
          <TextField id="new-master" label={t('masters.new')} value={name} onChange={setName} error={nameError} />
          <Button type="submit" variant="secondary" disabled={create.isPending}>
            {t('masters.add')}
          </Button>
          {create.isError && <ErrorNotice>{apiErrorMessage(t, create.error)}</ErrorNotice>}
        </form>
      </div>

      <div className="min-w-0">
        {selected ? (
          <MasterEditor key={selected.id} api={api} salon={salon} master={selected} services={services.data ?? []} />
        ) : (
          <p className="text-neutral-600">{t('masters.select')}</p>
        )}
      </div>
    </div>
  );
}

interface OfferDraft {
  price: string;
  duration: string;
}

function MasterEditor({ api, salon, master, services }: Props & { master: Master; services: Service[] }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['masters', salon] });

  const [displayName, setDisplayName] = useState(master.displayName);
  const [bio, setBio] = useState(master.bio ?? '');
  const [isActive, setIsActive] = useState(master.isActive);
  const [nameError, setNameError] = useState<string>();

  const [offers, setOffers] = useState<Record<string, OfferDraft>>(() =>
    Object.fromEntries(
      master.services.map((s) => [s.serviceId, { price: s.price?.toString() ?? '', duration: s.durationMinutes?.toString() ?? '' }]),
    ),
  );

  const saveProfile = useMutation({
    mutationFn: () => api.updateMaster(master.id, { displayName: displayName.trim(), bio: bio.trim() || null, isActive }),
    onSuccess: refresh,
  });

  const saveServices = useMutation({
    mutationFn: () =>
      api.setMasterServices(
        master.id,
        services
          .filter((s) => offers[s.id])
          .map((s) => ({
            serviceId: s.id,
            price: offers[s.id]!.price.trim() === '' ? null : Number(offers[s.id]!.price),
            durationMinutes: offers[s.id]!.duration.trim() === '' ? null : Number(offers[s.id]!.duration),
          })),
      ),
    onSuccess: refresh,
  });

  const onSaveProfile = (event: FormEvent) => {
    event.preventDefault();
    const length = displayName.trim().length;
    const message = length < 2 || length > 60 ? t('masters.nameError') : undefined;
    setNameError(message);
    if (!message) saveProfile.mutate();
  };

  const toggle = (serviceId: string, on: boolean) =>
    setOffers((current) => {
      const next = { ...current };
      if (on) next[serviceId] = { price: '', duration: '' };
      else delete next[serviceId];
      return next;
    });

  const change = (serviceId: string, field: keyof OfferDraft, value: string) =>
    setOffers((current) => ({ ...current, [serviceId]: { ...current[serviceId]!, [field]: value } }));

  return (
    <div className="flex flex-col gap-6">
      <form noValidate onSubmit={onSaveProfile} className="flex flex-col gap-3 rounded-lg border border-neutral-200 bg-white p-4">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2 className="text-lg font-semibold">{master.displayName}</h2>
          <span className="text-xs text-neutral-600">{t(master.userId ? 'masters.linked' : 'masters.notLinked')}</span>
        </div>
        <TextField id="master-name" label={t('masters.name')} value={displayName} onChange={setDisplayName} error={nameError} />
        <TextField id="master-bio" label={t('masters.bio')} value={bio} onChange={setBio} />
        <Checkbox label={t('masters.active')} checked={isActive} onChange={setIsActive} />
        {saveProfile.isError && <ErrorNotice>{apiErrorMessage(t, saveProfile.error)}</ErrorNotice>}
        {saveProfile.isSuccess && <SuccessNotice>{t('common.saved')}</SuccessNotice>}
        <div>
          <Button type="submit" disabled={saveProfile.isPending}>
            {t('masters.saveProfile')}
          </Button>
        </div>
      </form>

      <fieldset className="flex flex-col gap-3 rounded-lg border border-neutral-200 bg-white p-4">
        <legend className="px-1 text-lg font-semibold">{t('masters.services')}</legend>
        {services.length === 0 && <p className="text-sm text-neutral-600">{t('masters.noServices')}</p>}
        {services.map((s) => {
          const offer = offers[s.id];
          return (
            <div key={s.id} className="flex flex-col gap-2 border-b border-neutral-100 pb-3 last:border-0">
              <Checkbox label={s.name} checked={!!offer} onChange={(on) => toggle(s.id, on)} />
              {offer && (
                <div className="flex flex-wrap gap-3 pl-6">
                  <input
                    aria-label={t('masters.ownPrice', { name: s.name })}
                    placeholder={s.price.toFixed(2)}
                    type="number"
                    inputMode="decimal"
                    min={0}
                    step="0.01"
                    value={offer.price}
                    onChange={(e) => change(s.id, 'price', e.target.value)}
                    className="w-28 rounded-md border border-neutral-300 px-2 py-1 text-sm"
                  />
                  <input
                    aria-label={t('masters.ownDuration', { name: s.name })}
                    placeholder={String(s.durationMinutes)}
                    type="number"
                    inputMode="numeric"
                    min={5}
                    step={5}
                    value={offer.duration}
                    onChange={(e) => change(s.id, 'duration', e.target.value)}
                    className="w-28 rounded-md border border-neutral-300 px-2 py-1 text-sm"
                  />
                  <span className="self-center text-xs text-neutral-500">
                    {t('masters.defaultHint', { price: s.price.toFixed(2), minutes: s.durationMinutes })}
                  </span>
                </div>
              )}
            </div>
          );
        })}
        {saveServices.isError && <ErrorNotice>{apiErrorMessage(t, saveServices.error)}</ErrorNotice>}
        {saveServices.isSuccess && <SuccessNotice>{t('common.saved')}</SuccessNotice>}
        <div>
          <Button onClick={() => saveServices.mutate()} disabled={saveServices.isPending || services.length === 0}>
            {t('masters.saveServices')}
          </Button>
        </div>
      </fieldset>

      <ScheduleEditor api={api} masterId={master.id} canEditPattern />

      {!master.userId && (
        <div className="flex flex-col gap-2 rounded-lg border border-neutral-200 bg-white p-4">
          <p className="text-sm text-neutral-600">{t('invite.masterHint')}</p>
          <InviteForm api={api} role="Master" masterId={master.id} />
        </div>
      )}
    </div>
  );
}
