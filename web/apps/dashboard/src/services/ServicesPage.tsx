import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { ApiClient, Service, ServiceCategory } from '@mastera/api-client';
import { useTranslation } from '@mastera/i18n';
import { Button, Select, TextField } from '@mastera/ui';
import { Checkbox } from '../common/Checkbox';
import { ErrorNotice } from '../common/Notice';
import { apiErrorMessage } from '../common/errors';

interface Props {
  api: ApiClient;
  salon: string;
}

/** Task 2.4: the salon's service menu — categories and services. */
export function ServicesPage({ api, salon }: Props) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [showArchived, setShowArchived] = useState(false);
  const [editing, setEditing] = useState<Service | 'new' | null>(null);

  const categories = useQuery({ queryKey: ['categories', salon], queryFn: () => api.listCategories() });
  const services = useQuery({ queryKey: ['services', salon, showArchived], queryFn: () => api.listServices(showArchived) });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['services', salon] });
  const archive = useMutation({
    mutationFn: (service: Service) => (service.isArchived ? api.restoreService(service.id) : api.archiveService(service.id)),
    onSuccess: refresh,
  });

  const categoryName = (id: string | null) => categories.data?.find((c) => c.id === id)?.name ?? '—';

  return (
    <div className="flex flex-col gap-8">
      <CategoriesSection api={api} salon={salon} categories={categories.data ?? []} />

      <section aria-labelledby="services-heading" className="flex flex-col gap-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h2 id="services-heading" className="text-lg font-semibold">
            {t('services.title')}
          </h2>
          <div className="flex flex-wrap items-center gap-4">
            <Checkbox label={t('services.showArchived')} checked={showArchived} onChange={setShowArchived} />
            <Button onClick={() => setEditing('new')}>{t('services.add')}</Button>
          </div>
        </div>

        {editing && (
          <ServiceForm
            key={editing === 'new' ? 'new' : editing.id}
            api={api}
            salon={salon}
            categories={categories.data ?? []}
            service={editing === 'new' ? null : editing}
            onDone={() => setEditing(null)}
          />
        )}

        {services.isPending ? (
          <p className="text-neutral-500">{t('common.loading')}</p>
        ) : services.isError ? (
          <ErrorNotice>{t('errors.generic')}</ErrorNotice>
        ) : services.data.length === 0 ? (
          <p className="text-neutral-600">{t('services.empty')}</p>
        ) : (
          <div className="overflow-x-auto rounded-lg border border-neutral-200 bg-white">
            <table className="w-full text-left text-sm">
              <thead className="border-b border-neutral-200 text-neutral-600">
                <tr>
                  <th className="px-4 py-2 font-medium">{t('services.name')}</th>
                  <th className="px-4 py-2 font-medium">{t('services.category')}</th>
                  <th className="px-4 py-2 font-medium">{t('services.duration')}</th>
                  <th className="px-4 py-2 text-right font-medium">{t('services.price')}</th>
                  <th className="px-4 py-2" />
                </tr>
              </thead>
              <tbody className="divide-y divide-neutral-100">
                {services.data.map((service) => (
                  <tr key={service.id} className={service.isArchived ? 'text-neutral-400' : undefined}>
                    <td className="px-4 py-2">
                      <span className="font-medium">{service.name}</span>
                      {service.isArchived && (
                        <span className="ml-2 rounded-full bg-neutral-100 px-2 py-0.5 text-xs">{t('services.archived')}</span>
                      )}
                    </td>
                    <td className="px-4 py-2">{categoryName(service.categoryId)}</td>
                    <td className="px-4 py-2 tabular-nums">{t('common.minutes', { count: service.durationMinutes })}</td>
                    <td className="px-4 py-2 text-right tabular-nums">{service.price.toFixed(2)}</td>
                    <td className="px-4 py-2">
                      <div className="flex justify-end gap-2">
                        <Button variant="secondary" aria-label={t('services.editLabel', { name: service.name })} onClick={() => setEditing(service)}>
                          {t('common.edit')}
                        </Button>
                        <Button
                          variant="secondary"
                          aria-label={t(service.isArchived ? 'services.restoreLabel' : 'services.archiveLabel', { name: service.name })}
                          onClick={() => archive.mutate(service)}
                        >
                          {t(service.isArchived ? 'common.restore' : 'common.archive')}
                        </Button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}

function CategoriesSection({ api, salon, categories }: Props & { categories: ServiceCategory[] }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [name, setName] = useState('');
  const [error, setError] = useState<string>();

  const create = useMutation({
    mutationFn: () => api.createCategory(name.trim(), 0),
    onSuccess: () => {
      setName('');
      return queryClient.invalidateQueries({ queryKey: ['categories', salon] });
    },
  });

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();
    const length = name.trim().length;
    const message = length < 2 || length > 60 ? t('categories.name') : undefined;
    setError(message);
    if (!message) create.mutate();
  };

  return (
    <section aria-labelledby="categories-heading" className="flex flex-col gap-3">
      <h2 id="categories-heading" className="text-lg font-semibold">
        {t('categories.title')}
      </h2>
      {categories.length === 0 ? (
        <p className="text-sm text-neutral-600">{t('categories.empty')}</p>
      ) : (
        <ul className="flex flex-wrap gap-2">
          {categories.map((c) => (
            <li key={c.id} className="rounded-full border border-neutral-200 bg-white px-3 py-1 text-sm">
              {c.name}
            </li>
          ))}
        </ul>
      )}
      <form noValidate onSubmit={onSubmit} className="flex flex-wrap items-end gap-2">
        <TextField id="new-category" label={t('categories.new')} value={name} onChange={setName} error={error} />
        <Button type="submit" variant="secondary" disabled={create.isPending}>
          {t('categories.add')}
        </Button>
      </form>
      {create.isError && <ErrorNotice>{apiErrorMessage(t, create.error, { 'category.name.taken': 'categories.taken' })}</ErrorNotice>}
    </section>
  );
}

interface FormErrors {
  name?: string;
  duration?: string;
  price?: string;
}

function ServiceForm({
  api,
  salon,
  categories,
  service,
  onDone,
}: Props & { categories: ServiceCategory[]; service: Service | null; onDone: () => void }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [name, setName] = useState(service?.name ?? '');
  const [categoryId, setCategoryId] = useState(service?.categoryId ?? '');
  const [duration, setDuration] = useState(String(service?.durationMinutes ?? 30));
  const [price, setPrice] = useState(service ? String(service.price) : '');
  const [description, setDescription] = useState(service?.description ?? '');
  const [errors, setErrors] = useState<FormErrors>({});

  const save = useMutation({
    mutationFn: () => {
      const input = {
        name: name.trim(),
        categoryId: categoryId || null,
        durationMinutes: Number(duration),
        price: Number(price),
        description: description.trim() || null,
      };
      return service ? api.updateService(service.id, input) : api.createService(input);
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['services', salon] });
      onDone();
    },
  });

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();
    const next: FormErrors = {};
    const nameLength = name.trim().length;
    if (nameLength < 2 || nameLength > 100) next.name = t('services.errors.name');
    const minutes = Number(duration);
    if (!Number.isInteger(minutes) || minutes < 5 || minutes > 480 || minutes % 5 !== 0) next.duration = t('services.errors.duration');
    const amount = Number(price);
    if (price.trim() === '' || Number.isNaN(amount) || amount < 0 || amount > 100000) next.price = t('services.errors.price');
    setErrors(next);
    if (!next.name && !next.duration && !next.price) save.mutate();
  };

  return (
    <form
      noValidate
      onSubmit={onSubmit}
      aria-label={t(service ? 'services.edit' : 'services.add')}
      className="grid gap-4 rounded-lg border border-neutral-200 bg-white p-4 sm:grid-cols-2"
    >
      <TextField id="service-name" label={t('services.name')} value={name} onChange={setName} error={errors.name} />
      <div className="flex flex-col gap-1">
        <Select
          label={t('services.category')}
          value={categoryId}
          options={[{ value: '', label: t('common.none') }, ...categories.map((c) => ({ value: c.id, label: c.name }))]}
          onChange={setCategoryId}
        />
      </div>
      <TextField
        id="service-duration"
        label={t('services.duration')}
        type="number"
        inputMode="numeric"
        min={5}
        step={5}
        value={duration}
        onChange={setDuration}
        error={errors.duration}
      />
      <TextField
        id="service-price"
        label={t('services.price')}
        type="number"
        inputMode="decimal"
        min={0}
        step="0.01"
        value={price}
        onChange={setPrice}
        error={errors.price}
      />
      <TextField
        id="service-description"
        label={t('services.description')}
        value={description}
        onChange={setDescription}
        className="sm:col-span-2"
      />
      {save.isError && (
        <div className="sm:col-span-2">
          <ErrorNotice>{apiErrorMessage(t, save.error, { 'service.name.taken': 'services.errors.taken' })}</ErrorNotice>
        </div>
      )}
      <div className="flex gap-2 sm:col-span-2">
        <Button type="submit" disabled={save.isPending}>
          {t('common.save')}
        </Button>
        <Button variant="secondary" onClick={onDone}>
          {t('common.cancel')}
        </Button>
      </div>
    </form>
  );
}
