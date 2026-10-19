import { useEffect } from 'react';
import { supportedLanguages, useTranslation } from '@mastera/i18n';
import { LanguageSelect } from '@mastera/ui';

export function LanguageSwitcher() {
  const { t, i18n } = useTranslation();

  useEffect(() => {
    document.documentElement.lang = i18n.language;
  }, [i18n.language]);

  return (
    <LanguageSelect
      label={t('language.label')}
      value={i18n.language}
      options={supportedLanguages.map((lng) => ({ value: lng, label: t(`language.${lng}`) }))}
      onChange={(lng) => void i18n.changeLanguage(lng)}
    />
  );
}
