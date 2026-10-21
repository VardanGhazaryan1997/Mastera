import { useEffect, useRef, useState } from 'react';
import type { LiveNotification } from '@mastera/api-client';
import { useTranslation } from '@mastera/i18n';
import type { Realtime } from './realtime';

const TOAST_MS = 6000;
const KEEP = 30;

interface Entry {
  id: number;
  notification: LiveNotification;
  read: boolean;
}

function formatTime(iso: string, language: string) {
  return new Intl.DateTimeFormat(language, { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' }).format(new Date(iso));
}

/** Task 6.7: the bell with an unread badge, its list, and short-lived toasts for new notifications. */
export function NotificationCenter({ realtime, salon }: { realtime: Realtime; salon: string }) {
  const { t, i18n } = useTranslation();
  const [entries, setEntries] = useState<Entry[]>([]);
  const [toasts, setToasts] = useState<Entry[]>([]);
  const [open, setOpen] = useState(false);
  const nextId = useRef(1);

  useEffect(() => {
    setEntries([]);
    setToasts([]);
    const timers = new Set<ReturnType<typeof setTimeout>>();
    const stop = realtime.subscribe(salon, (notification) => {
      const entry = { id: nextId.current++, notification, read: false };
      setEntries((list) => [entry, ...list].slice(0, KEEP));
      setToasts((list) => [...list, entry]);
      const timer = setTimeout(() => {
        timers.delete(timer);
        setToasts((list) => list.filter((e) => e.id !== entry.id));
      }, TOAST_MS);
      timers.add(timer);
    });
    return () => {
      stop();
      for (const timer of timers) clearTimeout(timer);
    };
  }, [realtime, salon]);

  const unread = entries.filter((e) => !e.read).length;
  const text = (n: LiveNotification) =>
    t(`notifications.kind.${n.kind}`, {
      client: n.clientName || t('notifications.walkIn'),
      service: n.serviceName,
      master: n.masterName,
      time: formatTime(n.start, i18n.language),
    });

  // Closing the list marks everything in it as read.
  const toggle = () => {
    if (open) setEntries((list) => list.map((e) => ({ ...e, read: true })));
    setOpen(!open);
  };

  return (
    <div className="relative">
      <button
        type="button"
        onClick={toggle}
        aria-expanded={open}
        aria-label={unread > 0 ? t('notifications.bellUnread', { count: unread }) : t('notifications.bell')}
        className="relative rounded-md p-2 hover:bg-neutral-100"
      >
        <svg aria-hidden="true" viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="2">
          <path d="M6 8a6 6 0 1 1 12 0c0 7 3 8 3 8H3s3-1 3-8" />
          <path d="M10 20a2 2 0 0 0 4 0" />
        </svg>
        {unread > 0 && (
          <span data-testid="bell-badge" className="absolute -right-0.5 -top-0.5 min-w-5 rounded-full bg-red-600 px-1 text-center text-xs font-semibold text-white">
            {unread > 9 ? '9+' : unread}
          </span>
        )}
      </button>

      {open && (
        <div role="dialog" aria-label={t('notifications.title')} className="absolute right-0 z-20 mt-2 w-80 max-w-[calc(100vw-2rem)] rounded-lg border border-neutral-200 bg-white shadow-lg">
          <p className="border-b border-neutral-200 px-4 py-2 text-sm font-semibold">{t('notifications.title')}</p>
          {entries.length === 0 ? (
            <p className="px-4 py-6 text-sm text-neutral-500">{t('notifications.empty')}</p>
          ) : (
            <ul className="max-h-96 divide-y divide-neutral-100 overflow-y-auto">
              {entries.map((e) => (
                <li key={e.id} className={`px-4 py-3 text-sm ${e.read ? 'text-neutral-600' : 'font-medium'}`}>
                  {text(e.notification)}
                </li>
              ))}
            </ul>
          )}
        </div>
      )}

      <div aria-live="polite" className="pointer-events-none fixed bottom-4 right-4 z-30 flex w-80 max-w-[calc(100vw-2rem)] flex-col gap-2">
        {toasts.map((e) => (
          <div key={e.id} role="status" className="pointer-events-auto rounded-lg bg-neutral-900 px-4 py-3 text-sm text-white shadow-lg">
            {text(e.notification)}
          </div>
        ))}
      </div>
    </div>
  );
}
