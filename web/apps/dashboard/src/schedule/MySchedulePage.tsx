import { useQuery } from '@tanstack/react-query';
import { ApiError, type ApiClient } from '@mastera/api-client';
import { useTranslation } from '@mastera/i18n';
import { ErrorNotice } from '../common/Notice';
import { ScheduleEditor } from './ScheduleEditor';
import { TelegramConnect } from './TelegramConnect';

/** For masters: their own schedule. They manage days off; the salon sets regular hours. */
export function MySchedulePage({ api, salon }: { api: ApiClient; salon: string }) {
  const { t } = useTranslation();
  const me = useQuery({ queryKey: ['my-master', salon], queryFn: () => api.getMyMaster() });

  if (me.isPending) return <p className="text-neutral-500">{t('common.loading')}</p>;
  if (me.isError) {
    return me.error instanceof ApiError && me.error.status === 404 ? (
      <p className="text-neutral-700">{t('schedule.noProfile')}</p>
    ) : (
      <ErrorNotice>{t('errors.generic')}</ErrorNotice>
    );
  }

  return (
    <div className="flex flex-col gap-6">
      <TelegramConnect api={api} />
      <ScheduleEditor api={api} masterId={me.data.id} canEditPattern={false} />
    </div>
  );
}
