import { expect, test } from '@playwright/test';

test('dashboard asks to sign in, in Georgian by default, and switches to English', async ({ page }) => {
  await page.goto('/');

  await expect(page.getByRole('heading', { level: 1, name: 'შესვლა Mastera-ში' })).toBeVisible();

  await page.getByLabel('ენა').selectOption('en');

  await expect(page.getByRole('heading', { level: 1, name: 'Sign in to Mastera' })).toBeVisible();
  await expect(page.getByLabel('Email')).toBeVisible();
});
