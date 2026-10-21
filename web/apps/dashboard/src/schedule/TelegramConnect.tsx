import { useMutation } from '@tanstack/react-query';
import type { ApiClient } from '@mastera/api-client';
import { useTranslation } from '@mastera/i18n';
import { Button } from '@mastera/ui';
import { ErrorNotice } from '../common/Notice';

/** Task 6.5: masters open a one-time link to the salon's Telegram bot to get booking notifications there. */
export function TelegramConnect({ api }: { api: ApiClient }) {
  const { t } = useTranslation();
  const link = useMutation({ mutationFn: () => api.createTelegramLink() });

  return (
    <section aria-labelledby="telegram-heading" className="flex flex-col gap-2 rounded-lg border border-neutral-200 bg-white p-4">
      <h2 id="telegram-heading" className="font-semibold">
        {t('telegram.title')}
      </h2>
      <p className="text-sm text-neutral-600">{t('telegram.intro')}</p>
      {link.data ? (
        <p className="text-sm">
          <a href={link.data.url} target="_blank" rel="noreferrer" className="font-medium text-blue-700 underline">
            {t('telegram.open')}
          </a>{' '}
          <span className="text-neutral-500">{t('telegram.expires')}</span>
        </p>
      ) : (
        <div>
          <Button variant="secondary" onClick={() => link.mutate()} disabled={link.isPending}>
            {t('telegram.connect')}
          </Button>
        </div>
      )}
      {link.isError && <ErrorNotice>{t('errors.generic')}</ErrorNotice>}
    </section>
  );
}
