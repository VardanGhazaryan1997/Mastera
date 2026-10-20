import { useId, useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { WEEKDAYS, type ApiClient, type DaySchedule, type Schedule, type ScheduleException, type Weekday, type WorkPattern } from '@mastera/api-client';
import { useTranslation } from '@mastera/i18n';
import { Button, TextField } from '@mastera/ui';
import { Checkbox } from '../common/Checkbox';
import { ErrorNotice, SuccessNotice } from '../common/Notice';
import { apiErrorMessage } from '../common/errors';

const SCHEDULE_ERRORS = {
  'schedule.exception.overlap': 'schedule.errors.overlap',
  'schedule.range.invalid': 'schedule.errors.time',
  'schedule.break.overlap': 'schedule.errors.time',
  'schedule.break.outside_hours': 'schedule.errors.time',
  'schedule.time.invalid': 'schedule.errors.time',
  'schedule.date.invalid': 'schedule.errors.date',
  'schedule.exception.range': 'schedule.errors.date',
  'schedule.cycle.length': 'schedule.errors.cycle',
  'schedule.cycle.no_working_day': 'schedule.errors.cycle',
};

interface Props {
  api: ApiClient;
  masterId: string;
  /** Owners and admins set regular hours; masters only manage their own days off. */
  canEditPattern: boolean;
}

/** Task 3.4: a master's regular hours (weekly or shifts) and their days off / changed hours. */
export function ScheduleEditor({ api, masterId, canEditPattern }: Props) {
  const { t } = useTranslation();
  const headingId = useId();
  const schedule = useQuery({ queryKey: ['schedule', masterId], queryFn: () => api.getSchedule(masterId) });

  return (
    <section aria-labelledby={headingId} className="flex flex-col gap-4 rounded-lg border border-neutral-200 bg-white p-4">
      <h2 id={headingId} className="text-lg font-semibold">
        {t('schedule.title')}
      </h2>
      {schedule.isPending ? (
        <p className="text-neutral-500">{t('common.loading')}</p>
      ) : schedule.isError ? (
        <ErrorNotice>{t('errors.generic')}</ErrorNotice>
      ) : (
        <>
          {canEditPattern ? (
            <PatternForm api={api} masterId={masterId} schedule={schedule.data} />
          ) : (
            <PatternSummary schedule={schedule.data} />
          )}
          <Exceptions api={api} masterId={masterId} exceptions={schedule.data.exceptions} />
        </>
      )}
    </section>
  );
}

interface DayDraft {
  on: boolean;
  start: string;
  end: string;
  breakStart: string;
  breakEnd: string;
}

const toDraft = (day: DaySchedule | undefined): DayDraft => ({
  on: !!day,
  start: day?.start ?? '10:00',
  end: day?.end ?? '19:00',
  breakStart: day?.breaks?.[0]?.start ?? '',
  breakEnd: day?.breaks?.[0]?.end ?? '',
});

const toDay = (start: string, end: string, breakStart = '', breakEnd = ''): DaySchedule => ({
  start,
  end,
  breaks: breakStart && breakEnd ? [{ start: breakStart, end: breakEnd }] : [],
});

function PatternForm({ api, masterId, schedule }: { api: ApiClient; masterId: string; schedule: Schedule }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const pattern = schedule.pattern;
  const [mode, setMode] = useState<'weekly' | 'rotating'>(pattern?.kind ?? 'weekly');
  const [days, setDays] = useState<Record<Weekday, DayDraft>>(() =>
    Object.fromEntries(WEEKDAYS.map((d) => [d, toDraft(pattern?.kind === 'weekly' ? pattern.days[d] : undefined)])) as Record<Weekday, DayDraft>,
  );
  const firstShift = pattern?.kind === 'rotating' ? pattern.cycle.find((d) => d) ?? undefined : undefined;
  const [anchor, setAnchor] = useState(pattern?.kind === 'rotating' ? pattern.anchor : '');
  const [daysOn, setDaysOn] = useState(pattern?.kind === 'rotating' ? String(pattern.cycle.filter((d) => d).length) : '2');
  const [daysOff, setDaysOff] = useState(pattern?.kind === 'rotating' ? String(pattern.cycle.filter((d) => !d).length) : '2');
  const [shiftStart, setShiftStart] = useState(firstShift?.start ?? '10:00');
  const [shiftEnd, setShiftEnd] = useState(firstShift?.end ?? '20:00');

  const save = useMutation({
    mutationFn: () => {
      const next: WorkPattern =
        mode === 'weekly'
          ? {
              kind: 'weekly',
              days: Object.fromEntries(
                WEEKDAYS.filter((d) => days[d].on).map((d) => [d, toDay(days[d].start, days[d].end, days[d].breakStart, days[d].breakEnd)]),
              ),
            }
          : {
              kind: 'rotating',
              anchor,
              cycle: [
                ...Array.from({ length: Number(daysOn) || 0 }, () => toDay(shiftStart, shiftEnd)),
                ...Array.from({ length: Number(daysOff) || 0 }, () => null),
              ],
            };
      return api.setPattern(masterId, next);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['schedule', masterId] }),
  });

  const update = (day: Weekday, change: Partial<DayDraft>) => setDays((current) => ({ ...current, [day]: { ...current[day], ...change } }));

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();
    save.mutate();
  };

  const time = (label: string, value: string, onChange: (v: string) => void) => (
    <input aria-label={label} type="time" value={value} onChange={(e) => onChange(e.target.value)} className="rounded-md border border-neutral-300 px-2 py-1 text-sm" />
  );

  return (
    <form noValidate onSubmit={onSubmit} className="flex flex-col gap-3">
      <h3 className="font-medium">{t('schedule.regular')}</h3>
      {!pattern && <p className="text-sm text-amber-800">{t('schedule.notSet')}</p>}
      <div role="radiogroup" aria-label={t('schedule.regular')} className="flex flex-wrap gap-4 text-sm">
        {(['weekly', 'rotating'] as const).map((value) => (
          <label key={value} className="flex items-center gap-2">
            <input type="radio" name={`mode-${masterId}`} checked={mode === value} onChange={() => setMode(value)} />
            {t(value === 'weekly' ? 'schedule.weekly' : 'schedule.shifts')}
          </label>
        ))}
      </div>

      {mode === 'weekly' ? (
        <ul className="flex flex-col gap-2">
          {WEEKDAYS.map((d) => {
            const name = t(`weekdays.${d}`);
            const draft = days[d];
            return (
              <li key={d} className="flex flex-wrap items-center gap-2">
                <div className="w-36">
                  <Checkbox label={name} checked={draft.on} onChange={(on) => update(d, { on })} />
                </div>
                {draft.on && (
                  <>
                    {time(t('schedule.startLabel', { day: name }), draft.start, (v) => update(d, { start: v }))}
                    <span aria-hidden="true">–</span>
                    {time(t('schedule.endLabel', { day: name }), draft.end, (v) => update(d, { end: v }))}
                    <span className="text-xs text-neutral-500">☕</span>
                    {time(t('schedule.breakStartLabel', { day: name }), draft.breakStart, (v) => update(d, { breakStart: v }))}
                    <span aria-hidden="true">–</span>
                    {time(t('schedule.breakEndLabel', { day: name }), draft.breakEnd, (v) => update(d, { breakEnd: v }))}
                  </>
                )}
              </li>
            );
          })}
        </ul>
      ) : (
        <div className="grid gap-3 sm:grid-cols-3">
          <TextField id={`anchor-${masterId}`} label={t('schedule.firstDay')} type="date" value={anchor} onChange={setAnchor} />
          <TextField id={`on-${masterId}`} label={t('schedule.daysOn')} type="number" min={1} value={daysOn} onChange={setDaysOn} />
          <TextField id={`off-${masterId}`} label={t('schedule.daysOff')} type="number" min={1} value={daysOff} onChange={setDaysOff} />
          <TextField id={`shift-start-${masterId}`} label={t('schedule.shiftStart')} type="time" value={shiftStart} onChange={setShiftStart} />
          <TextField id={`shift-end-${masterId}`} label={t('schedule.shiftEnd')} type="time" value={shiftEnd} onChange={setShiftEnd} />
        </div>
      )}

      {save.isError && <ErrorNotice>{apiErrorMessage(t, save.error, SCHEDULE_ERRORS)}</ErrorNotice>}
      {save.isSuccess && <SuccessNotice>{t('common.saved')}</SuccessNotice>}
      <div>
        <Button type="submit" disabled={save.isPending}>
          {t('schedule.savePattern')}
        </Button>
      </div>
    </form>
  );
}

const hoursText = (day: DaySchedule) =>
  `${day.start}–${day.end}${day.breaks?.length ? ` (☕ ${day.breaks.map((b) => `${b.start}–${b.end}`).join(', ')})` : ''}`;

function PatternSummary({ schedule }: { schedule: Schedule }) {
  const { t } = useTranslation();
  const pattern = schedule.pattern;
  return (
    <div className="flex flex-col gap-1">
      <h3 className="font-medium">{t('schedule.regular')}</h3>
      {!pattern ? (
        <p className="text-sm text-amber-800">{t('schedule.notSet')}</p>
      ) : pattern.kind === 'weekly' ? (
        <ul className="text-sm">
          {WEEKDAYS.filter((d) => pattern.days[d]).map((d) => (
            <li key={d}>{`${t(`weekdays.${d}`)}: ${hoursText(pattern.days[d]!)}`}</li>
          ))}
        </ul>
      ) : (
        <p className="text-sm">
          {t('schedule.shiftSummary', {
            on: pattern.cycle.filter((d) => d).length,
            off: pattern.cycle.filter((d) => !d).length,
            anchor: pattern.anchor,
            hours: pattern.cycle.find((d) => d) ? hoursText(pattern.cycle.find((d) => d)!) : '',
          })}
        </p>
      )}
    </div>
  );
}

const datesText = (e: { from: string; to: string }) => (e.from === e.to ? e.from : `${e.from} – ${e.to}`);

function Exceptions({ api, masterId, exceptions }: { api: ApiClient; masterId: string; exceptions: ScheduleException[] }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['schedule', masterId] });

  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [working, setWorking] = useState(false);
  const [start, setStart] = useState('10:00');
  const [end, setEnd] = useState('16:00');
  const [note, setNote] = useState('');
  const [fromError, setFromError] = useState<string>();

  const add = useMutation({
    mutationFn: () =>
      api.addScheduleException(masterId, {
        from,
        to: to || null,
        hours: working ? toDay(start, end) : null,
        note: note.trim() || null,
      }),
    onSuccess: async () => {
      setFrom('');
      setTo('');
      setNote('');
      setWorking(false);
      await refresh();
    },
  });

  const remove = useMutation({ mutationFn: (id: string) => api.removeScheduleException(masterId, id), onSuccess: refresh });

  const onAdd = (event: FormEvent) => {
    event.preventDefault();
    const message = from ? undefined : t('schedule.errors.date');
    setFromError(message);
    if (!message) add.mutate();
  };

  return (
    <div className="flex flex-col gap-3">
      <h3 className="font-medium">{t('schedule.exceptions')}</h3>
      {exceptions.length === 0 ? (
        <p className="text-sm text-neutral-600">{t('schedule.noExceptions')}</p>
      ) : (
        <ul className="flex flex-col gap-1">
          {exceptions.map((e) => (
            <li key={e.id} className="flex flex-wrap items-center justify-between gap-2 rounded-md bg-neutral-50 px-3 py-2 text-sm">
              <span>
                <span className="font-medium tabular-nums">{datesText(e)}</span>
                {': '}
                {e.hours ? hoursText(e.hours) : t('schedule.dayOff')}
                {e.note ? ` · ${e.note}` : ''}
              </span>
              <Button variant="secondary" aria-label={t('schedule.remove', { dates: datesText(e) })} onClick={() => remove.mutate(e.id)}>
                {t('schedule.removeShort')}
              </Button>
            </li>
          ))}
        </ul>
      )}

      <form noValidate onSubmit={onAdd} className="grid gap-3 sm:grid-cols-2">
        <TextField id={`exception-from-${masterId}`} label={t('schedule.exceptionFrom')} type="date" value={from} onChange={setFrom} error={fromError} />
        <TextField id={`exception-to-${masterId}`} label={t('schedule.exceptionTo')} type="date" value={to} onChange={setTo} />
        <div className="sm:col-span-2">
          <Checkbox label={t('schedule.differentHours')} checked={working} onChange={setWorking} />
        </div>
        {working && (
          <>
            <TextField id={`exception-start-${masterId}`} label={t('schedule.from')} type="time" value={start} onChange={setStart} />
            <TextField id={`exception-end-${masterId}`} label={t('schedule.to')} type="time" value={end} onChange={setEnd} />
          </>
        )}
        <TextField id={`exception-note-${masterId}`} label={t('schedule.note')} value={note} onChange={setNote} className="sm:col-span-2" />
        {(add.isError || remove.isError) && (
          <div className="sm:col-span-2">
            <ErrorNotice>{apiErrorMessage(t, add.error ?? remove.error, SCHEDULE_ERRORS)}</ErrorNotice>
          </div>
        )}
        <div>
          <Button type="submit" variant="secondary" disabled={add.isPending}>
            {t('schedule.add')}
          </Button>
        </div>
      </form>
    </div>
  );
}
