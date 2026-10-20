import type { Role } from '@mastera/api-client';

export type PageKey = 'calendar' | 'bookings' | 'schedule' | 'masters' | 'services' | 'settings';

const everyone: readonly Role[] = ['Owner', 'Admin', 'Receptionist', 'Master'];
const managers: readonly Role[] = ['Owner', 'Admin'];

/** Menu items and the roles that see them. The API enforces the same rules; this only hides what can't be used. */
export const pages: readonly { key: PageKey; roles: readonly Role[] }[] = [
  { key: 'calendar', roles: everyone },
  { key: 'bookings', roles: everyone },
  { key: 'schedule', roles: ['Master'] },
  { key: 'masters', roles: managers },
  { key: 'services', roles: managers },
  { key: 'settings', roles: managers },
];

export const pagesFor = (role: Role | undefined) => (role ? pages.filter((p) => p.roles.includes(role)) : []);
