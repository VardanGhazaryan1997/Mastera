import { expect, test } from '@playwright/test';

test('dashboard opens in Georgian and switches to English', async ({ page }) => {
  await page.goto('/');

  await expect(page.getByRole('heading', { level: 1, name: 'სალონის პანელი' })).toBeVisible();

  await page.getByLabel('ენა').selectOption('en');

  await expect(page.getByRole('heading', { level: 1, name: 'Salon dashboard' })).toBeVisible();
});
