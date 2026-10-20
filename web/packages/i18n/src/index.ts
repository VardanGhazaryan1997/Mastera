import i18next, { type i18n } from 'i18next';
import { initReactI18next } from 'react-i18next';
import en from './locales/en.json';
import ka from './locales/ka.json';
import ru from './locales/ru.json';

export const supportedLanguages = ['ka', 'ru', 'en'] as const;
export type Language = (typeof supportedLanguages)[number];
export const defaultLanguage: Language = 'ka';

export const resources = {
  ka: { translation: ka },
  ru: { translation: ru },
  en: { translation: en },
} as const;

export const isSupportedLanguage = (value: string): value is Language =>
  (supportedLanguages as readonly string[]).includes(value);

/** Creates an isolated, synchronously initialised i18next instance (one per app or per test). */
export function createI18n(language: string = defaultLanguage): i18n {
  const instance = i18next.createInstance();
  void instance.use(initReactI18next).init({
    resources,
    lng: isSupportedLanguage(language) ? language : defaultLanguage,
    fallbackLng: defaultLanguage,
    supportedLngs: [...supportedLanguages],
    interpolation: { escapeValue: false },
    initAsync: false,
  });
  return instance;
}

export { I18nextProvider, useTranslation } from 'react-i18next';
export type { i18n, TFunction } from 'i18next';
