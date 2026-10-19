import { describe, expect, it } from 'vitest';
import { createI18n, defaultLanguage, resources, supportedLanguages } from './index';

const keysOf = (obj: object, prefix = ''): string[] =>
  Object.entries(obj).flatMap(([k, v]) =>
    v !== null && typeof v === 'object' ? keysOf(v as object, `${prefix}${k}.`) : [`${prefix}${k}`],
  );

describe('i18n', () => {
  it('supports Georgian, Russian and English, with Georgian as default', () => {
    expect(supportedLanguages).toEqual(['ka', 'ru', 'en']);
    expect(defaultLanguage).toBe('ka');
  });

  it.each(supportedLanguages)('"%s" has exactly the same keys as English', (lng) => {
    expect(keysOf(resources[lng].translation).sort()).toEqual(keysOf(resources.en.translation).sort());
  });

  it.each([
    ['ka', 'სალონის პანელი'],
    ['ru', 'Панель салона'],
    ['en', 'Salon dashboard'],
  ] as const)('translates the dashboard title in "%s"', (lng, expected) => {
    expect(createI18n(lng).t('dashboard.title')).toBe(expected);
  });

  it('keeps the brand name untranslated', () => {
    for (const lng of supportedLanguages) {
      expect(createI18n(lng).t('common.appName')).toBe('Mastera');
    }
  });

  it('falls back to the default language for an unsupported code', () => {
    const i18n = createI18n('de');
    expect(i18n.language).toBe('ka');
    expect(i18n.t('dashboard.title')).toBe('სალონის პანელი');
  });
});
