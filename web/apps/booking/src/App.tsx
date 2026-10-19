import { useEffect } from 'react';
import { I18nextProvider, supportedLanguages, useTranslation, type i18n } from '@mastera/i18n';
import { Button, LanguageSelect } from '@mastera/ui';

function Shell() {
  const { t, i18n } = useTranslation();

  useEffect(() => {
    document.documentElement.lang = i18n.language;
  }, [i18n.language]);

  const languages = supportedLanguages.map((lng) => ({ value: lng, label: t(`language.${lng}`) }));

  return (
    <div className="min-h-screen bg-neutral-50 text-neutral-900">
      <header className="flex flex-wrap items-center justify-between gap-4 border-b border-neutral-200 bg-white px-4 py-3 sm:px-6">
        <span className="text-lg font-semibold tracking-tight">{t('common.appName')}</span>
        <LanguageSelect
          label={t('language.label')}
          value={i18n.language}
          options={languages}
          onChange={(lng) => void i18n.changeLanguage(lng)}
        />
      </header>
      <main className="mx-auto max-w-5xl px-4 py-10 sm:px-6">
        <h1 className="text-3xl font-semibold">{t('booking.title')}</h1>
        <p className="mt-2 text-neutral-600">{t('booking.intro')}</p>
        <Button className="mt-6">{t('booking.cta')}</Button>
      </main>
    </div>
  );
}

export function App({ i18n }: { i18n: i18n }) {
  return (
    <I18nextProvider i18n={i18n}>
      <Shell />
    </I18nextProvider>
  );
}
