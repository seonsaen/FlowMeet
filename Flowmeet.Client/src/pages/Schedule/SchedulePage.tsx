import { type FormEvent, useCallback, useEffect, useMemo, useState } from 'react';
import { CalendarPlus, ChevronLeft, ChevronRight, Pencil, Repeat2, Trash2 } from '../../components/icons/hugeIcons';
import type { BaseScheduleEntry, BaseScheduleOccurrenceException, EventResponse } from '../../types';
import { scheduleApi } from '../../api/schedule';
import { profileApi } from '../../api/profile';
import { useSearchParams } from 'react-router-dom';

const typeNames = ['Mandatory', 'Flexible', 'Desirable'] as const;

const typeMeta: Record<string, { label: string; className: string }> = {
    Mandatory: { label: 'Обязательное', className: 'bg-red-50 border-red-100 text-red-700' },
    Flexible: { label: 'Гибкое', className: 'bg-blue-50 border-blue-100 text-blue-700' },
    Desirable: { label: 'Желательное', className: 'bg-amber-50 border-amber-100 text-amber-700' }
};

const eventTypeOptions = [
    {
        value: 0,
        label: 'Обязательное',
        description: 'Нельзя пересекать',
        selectedClassName: 'border-red-300 bg-red-50 text-red-700 shadow-md ring-2 ring-red-100 ring-offset-2 ring-offset-white',
        barClassName: 'bg-red-500'
    },
    {
        value: 1,
        label: 'Гибкое',
        description: 'Можно подвинуть',
        selectedClassName: 'border-blue-300 bg-blue-50 text-blue-700 shadow-md ring-2 ring-blue-100 ring-offset-2 ring-offset-white',
        barClassName: 'bg-blue-500'
    },
    {
        value: 2,
        label: 'Желательное',
        description: 'Не критично пропустить',
        selectedClassName: 'border-amber-300 bg-amber-50 text-amber-700 shadow-md ring-2 ring-amber-100 ring-offset-2 ring-offset-white',
        barClassName: 'bg-amber-500'
    }
];

const dayNames = ['Вс', 'Пн', 'Вт', 'Ср', 'Чт', 'Пт', 'Сб'];

type PlannedItem = {
    id: string;
    title: string;
    description?: string;
    startLabel: string;
    endLabel: string;
    sortValue: number;
    type: string | number;
    source: 'event' | 'base' | 'meeting';
    baseScheduleEntryId?: string;
    occurrenceDate?: string;
};

const toDateInput = (date: Date) => date.toISOString().slice(0, 10);
const toLocalDateKey = (date: Date) => {
    const year = date.getFullYear();
    const month = `${date.getMonth() + 1}`.padStart(2, '0');
    const day = `${date.getDate()}`.padStart(2, '0');
    return `${year}-${month}-${day}`;
};
const toLocalDateInput = (value: string) => new Date(value).toISOString().slice(0, 10);
const toLocalTimeInput = (value: string) =>
    new Date(value).toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit', hour12: false });
const normalizeTime = (value: string) => value.length >= 5 ? value.slice(0, 5) : value;
const timeToMinutes = (value: string) => {
    const [hours, minutes] = normalizeTime(value).split(':').map(Number);
    return hours * 60 + minutes;
};
const formatTime = (value: string) => new Date(value).toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' });
const sameDay = (value: string, date: Date) => {
    const eventDate = new Date(value);
    return eventDate.getFullYear() === date.getFullYear()
        && eventDate.getMonth() === date.getMonth()
        && eventDate.getDate() === date.getDate();
};

const getTypeName = (value: string | number) => {
    if (typeof value === 'number') {
        return typeNames[value] ?? 'Flexible';
    }

    const numeric = Number(value);
    if (!Number.isNaN(numeric)) {
        return typeNames[numeric] ?? 'Flexible';
    }

    return value;
};

const getTypeValue = (value: string | number) => {
    if (typeof value === 'number') {
        return value;
    }

    const numeric = Number(value);
    if (!Number.isNaN(numeric)) {
        return numeric;
    }

    const index = typeNames.findIndex(type => type === value);
    return index >= 0 ? index : 1;
};

const eventToItem = (event: EventResponse): PlannedItem => ({
    id: event.id,
    title: event.title,
    description: event.description,
    startLabel: formatTime(event.startTime),
    endLabel: formatTime(event.endTime),
    sortValue: new Date(event.startTime).getHours() * 60 + new Date(event.startTime).getMinutes(),
    type: event.type,
    source: event.source === 'meeting' ? 'meeting' : 'event'
});

const baseToItem = (entry: BaseScheduleEntry, index: number, day: Date): PlannedItem => ({
    id: entry.id ?? `base-${entry.dayOfWeek}-${entry.startTime}-${index}`,
    title: entry.title,
    description: entry.description,
    startLabel: normalizeTime(entry.startTime),
    endLabel: normalizeTime(entry.endTime),
    sortValue: timeToMinutes(entry.startTime),
    type: entry.type,
    source: 'base',
    baseScheduleEntryId: entry.id,
    occurrenceDate: toLocalDateKey(day)
});

const isBaseEntryActiveOnDate = (entry: BaseScheduleEntry, occurrenceDate: string) => {
    if (entry.effectiveFromDate && entry.effectiveFromDate > occurrenceDate) {
        return false;
    }

    if (entry.effectiveToDate && entry.effectiveToDate <= occurrenceDate) {
        return false;
    }

    return true;
};

export const SchedulePage = () => {
    const [searchParams, setSearchParams] = useSearchParams();
    const [events, setEvents] = useState<EventResponse[]>([]);
    const [baseSchedule, setBaseSchedule] = useState<BaseScheduleEntry[]>([]);
    const [baseExceptions, setBaseExceptions] = useState<BaseScheduleOccurrenceException[]>([]);
    const [scheduleView, setScheduleView] = useState<'day' | 'week'>('day');
    const [dayOffset, setDayOffset] = useState(0);
    const [weekOffset, setWeekOffset] = useState(0);
    const [isModalOpen, setIsModalOpen] = useState(false);
    const [isVerticalWeekLayout, setIsVerticalWeekLayout] = useState(false);
    const [editingEvent, setEditingEvent] = useState<EventResponse | null>(null);
    const [editingBaseItem, setEditingBaseItem] = useState<PlannedItem | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');

    const [title, setTitle] = useState('');
    const [description, setDescription] = useState('');
    const [date, setDate] = useState(toDateInput(new Date()));
    const [startTime, setStartTime] = useState('09:00');
    const [endTime, setEndTime] = useState('10:00');
    const [type, setType] = useState(0);

    const selectedDay = useMemo(() => {
        const current = new Date();
        current.setDate(current.getDate() + dayOffset);
        current.setHours(0, 0, 0, 0);
        return current;
    }, [dayOffset]);

    const weekDays = useMemo(() => {
        const today = new Date();
        const mondayOffset = today.getDay() === 0 ? -6 : 1 - today.getDay();
        const monday = new Date(today);
        monday.setDate(today.getDate() + mondayOffset + weekOffset * 7);
        monday.setHours(0, 0, 0, 0);

        return Array.from({ length: 7 }, (_, index) => {
            const current = new Date(monday);
            current.setDate(monday.getDate() + index);
            return current;
        });
    }, [weekOffset]);

    const visibleRange = useMemo(() => {
        const timestamps = [weekDays[0], weekDays[6], selectedDay].map(item => item.getTime());
        return {
            start: toLocalDateKey(new Date(Math.min(...timestamps))),
            end: toLocalDateKey(new Date(Math.max(...timestamps)))
        };
    }, [selectedDay, weekDays]);

    const loadSchedule = useCallback(async () => {
        setLoading(true);
        setError('');
        try {
            const [eventData, baseData, exceptionData] = await Promise.all([
                scheduleApi.getUserSchedule(),
                profileApi.getBaseScheduleHistory(visibleRange.start, visibleRange.end),
                profileApi.getBaseScheduleExceptions()
            ]);

            setEvents(eventData.sort((a, b) => new Date(a.startTime).getTime() - new Date(b.startTime).getTime()));
            setBaseSchedule(baseData);
            setBaseExceptions(exceptionData);
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Не удалось загрузить расписание');
        } finally {
            setLoading(false);
        }
    }, [visibleRange.end, visibleRange.start]);

    useEffect(() => {
        void loadSchedule();
    }, [loadSchedule]);

    useEffect(() => {
        const updateLayout = () => {
            setIsVerticalWeekLayout(window.innerHeight > window.innerWidth && window.innerWidth < 1280);
        };

        updateLayout();
        window.addEventListener('resize', updateLayout);
        return () => window.removeEventListener('resize', updateLayout);
    }, []);

    useEffect(() => {
        if (searchParams.get('create') !== '1') {
            return;
        }

        resetEventForm();
        setError('');
        setIsModalOpen(true);

        const next = new URLSearchParams(searchParams);
        next.delete('create');
        setSearchParams(next, { replace: true });
    }, [searchParams, setSearchParams]);

    const getItemsForDay = (day: Date) => {
        const dayEvents = events
            .filter(event => sameDay(event.startTime, day))
            .map(eventToItem);

        const occurrenceDate = toLocalDateKey(day);

        const dayBaseEntries = baseSchedule
            .filter(entry => entry.dayOfWeek === day.getDay())
            .filter(entry => isBaseEntryActiveOnDate(entry, occurrenceDate))
            .filter(entry => !baseExceptions.some(exception => exception.baseScheduleEntryId === entry.id && exception.date === occurrenceDate))
            .map((entry, index) => baseToItem(entry, index, day));

        return [...dayEvents, ...dayBaseEntries].sort((a, b) => a.sortValue - b.sortValue);
    };

    const formatWeekRange = `${weekDays[0].toLocaleDateString('ru-RU', { day: '2-digit', month: 'short' })} - ${weekDays[6].toLocaleDateString('ru-RU', { day: '2-digit', month: 'short' })}`;

    const resetEventForm = () => {
        setTitle('');
        setDescription('');
        setDate(toDateInput(new Date()));
        setStartTime('09:00');
        setEndTime('10:00');
        setType(0);
        setEditingEvent(null);
        setEditingBaseItem(null);
    };

    const openCreateModal = () => {
        resetEventForm();
        setError('');
        setIsModalOpen(true);
    };

    const openEditModal = (event: EventResponse) => {
        setEditingEvent(event);
        setEditingBaseItem(null);
        setTitle(event.title);
        setDescription(event.description ?? '');
        setDate(toLocalDateInput(event.startTime));
        setStartTime(toLocalTimeInput(event.startTime));
        setEndTime(toLocalTimeInput(event.endTime));
        setType(getTypeValue(event.type));
        setError('');
        setIsModalOpen(true);
    };

    const openBaseOccurrenceEditModal = (item: PlannedItem) => {
        setEditingEvent(null);
        setEditingBaseItem(item);
        setTitle(item.title);
        setDescription(item.description ?? '');
        setDate(item.occurrenceDate ?? toDateInput(new Date()));
        setStartTime(item.startLabel);
        setEndTime(item.endLabel);
        setType(getTypeValue(item.type));
        setError('');
        setIsModalOpen(true);
    };

    const closeModal = () => {
        setIsModalOpen(false);
        resetEventForm();
    };

    const applyDuration = (minutes: number) => {
        const [hours, mins] = startTime.split(':').map(Number);
        const startDateTime = new Date(`${date}T${hours.toString().padStart(2, '0')}:${mins.toString().padStart(2, '0')}`);
        startDateTime.setMinutes(startDateTime.getMinutes() + minutes);
        const nextEndTime = `${startDateTime.getHours().toString().padStart(2, '0')}:${startDateTime.getMinutes().toString().padStart(2, '0')}`;
        setEndTime(nextEndTime);
    };

    const syncEndTimeWithCurrentDuration = (nextDate: string, nextStartTime: string) => {
        if (currentDurationMinutes <= 0) {
            return;
        }

        const [hours, mins] = nextStartTime.split(':').map(Number);
        const startDateTime = new Date(`${nextDate}T${hours.toString().padStart(2, '0')}:${mins.toString().padStart(2, '0')}`);
        startDateTime.setMinutes(startDateTime.getMinutes() + currentDurationMinutes);
        setEndTime(`${startDateTime.getHours().toString().padStart(2, '0')}:${startDateTime.getMinutes().toString().padStart(2, '0')}`);
    };

    const handleSubmit = async (event: FormEvent) => {
        event.preventDefault();
        setError('');

        if (endTime <= startTime) {
            setError('Время окончания должно быть позже времени начала');
            return;
        }

        try {
            const payload = {
                title,
                description: description || undefined,
                startTime: new Date(`${date}T${startTime}`).toISOString(),
                endTime: new Date(`${date}T${endTime}`).toISOString(),
                type
            };

            if (editingEvent) {
                await scheduleApi.updateEvent(editingEvent.id, payload);
            } else if (editingBaseItem?.baseScheduleEntryId && editingBaseItem.occurrenceDate) {
                await scheduleApi.overrideBaseOccurrence({
                    baseScheduleEntryId: editingBaseItem.baseScheduleEntryId,
                    occurrenceDate: editingBaseItem.occurrenceDate,
                    ...payload
                });
            } else {
                await scheduleApi.createEvent(payload);
            }

            closeModal();
            await loadSchedule();
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Не удалось сохранить событие');
        }
    };

    const handleDelete = async (eventId: string) => {
        await scheduleApi.deleteEvent(eventId);
        await loadSchedule();
    };

    const handleCancelBaseOccurrence = async (item: PlannedItem) => {
        if (!item.baseScheduleEntryId || !item.occurrenceDate) {
            return;
        }

        await scheduleApi.cancelBaseOccurrence({
            baseScheduleEntryId: item.baseScheduleEntryId,
            occurrenceDate: item.occurrenceDate
        });
        await loadSchedule();
    };

    const renderItem = (item: PlannedItem, compact = false) => {
        const typeName = getTypeName(item.type);
        const meta = typeMeta[typeName] ?? typeMeta.Flexible;
        const actionButtonClass = `${compact ? 'rounded-lg p-1.5' : 'rounded-xl p-2'} bg-white/70 text-gray-500 hover:text-indigo-600`;
        const destructiveActionButtonClass = `${compact ? 'rounded-lg p-1.5' : 'rounded-xl p-2'} bg-white/70 text-gray-500 hover:text-red-600`;
        const actions = item.source === 'event' ? (
            <div className="flex shrink-0 gap-1">
                <button
                    onClick={() => {
                        const sourceEvent = events.find(event => event.id === item.id);
                        if (sourceEvent) {
                            openEditModal(sourceEvent);
                        }
                    }}
                    className={actionButtonClass}
                >
                    <Pencil size={compact ? 13 : 16} />
                </button>
                <button onClick={() => void handleDelete(item.id)} className={destructiveActionButtonClass}>
                    <Trash2 size={compact ? 13 : 16} />
                </button>
            </div>
        ) : item.source === 'base' ? (
            <div className="flex shrink-0 gap-1">
                <button
                    onClick={() => openBaseOccurrenceEditModal(item)}
                    className={actionButtonClass}
                >
                    <Pencil size={compact ? 13 : 16} />
                </button>
                <button
                    onClick={() => void handleCancelBaseOccurrence(item)}
                    className={destructiveActionButtonClass}
                >
                    <Trash2 size={compact ? 13 : 16} />
                </button>
            </div>
        ) : null;

        return (
            <div key={`${item.source}-${item.id}`} className={`border ${compact ? 'rounded-xl px-3 py-3 text-xs' : 'rounded-2xl p-4'} ${meta.className} ${item.source === 'base' ? 'border-dashed bg-opacity-70' : ''}`}>
                <div className={`${compact ? 'mb-2' : 'mb-3'} flex items-start justify-between gap-2`}>
                    <div className="flex min-w-0 flex-wrap items-center gap-2">
                        <span className="text-xs font-black uppercase">{meta.label}</span>
                        {item.source === 'meeting' && (
                            <span className="rounded-full bg-white/70 px-2 py-0.5 text-[10px] font-black uppercase tracking-wide text-gray-500">
                                Встреча
                            </span>
                        )}
                        {item.source === 'base' && (
                            <span className="inline-flex items-center gap-1 rounded-full bg-white/70 px-2 py-0.5 text-[10px] font-black uppercase tracking-wide text-gray-500">
                                <Repeat2 size={11} /> База
                            </span>
                        )}
                    </div>
                    {!compact && actions}
                </div>
                <h3 className={`break-words font-black leading-tight text-gray-900 ${compact ? 'text-sm' : 'text-base'}`}>{item.title}</h3>
                {!compact && item.description && <p className="mt-2 text-sm text-gray-500">{item.description}</p>}
                {compact ? (
                    <div className="mt-2 flex items-end justify-between gap-2">
                        <p className="font-bold">{item.startLabel} - {item.endLabel}</p>
                        {actions}
                    </div>
                ) : (
                    <p className="mt-3 font-bold">{item.startLabel} - {item.endLabel}</p>
                )}
            </div>
        );
    };

    const currentDurationMinutes = Math.max(0, timeToMinutes(endTime) - timeToMinutes(startTime));
    const selectedDayItems = getItemsForDay(selectedDay);
    const selectedDayLabel = selectedDay.toLocaleDateString('ru-RU', { weekday: 'long', day: 'numeric', month: 'long' });

    return (
        <div className="w-full max-w-[1500px] mx-auto space-y-6 p-4 md:p-8">
            <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                <div>
                    <h1 className="text-3xl font-black text-gray-900">План</h1>
                </div>
                <div className="flex gap-2">
                    <div className="bg-gray-100 p-1 rounded-xl flex text-xs font-bold">
                        <button onClick={() => setScheduleView('day')} className={`px-4 py-2 rounded-lg ${scheduleView === 'day' ? 'bg-white shadow text-gray-900' : 'text-gray-500'}`}>День</button>
                        <button onClick={() => setScheduleView('week')} className={`px-4 py-2 rounded-lg ${scheduleView === 'week' ? 'bg-white shadow text-gray-900' : 'text-gray-500'}`}>Неделя</button>
                    </div>
                    <button onClick={openCreateModal} className="rounded-xl bg-indigo-600 px-4 py-2 text-sm font-bold text-white shadow-lg shadow-indigo-100">
                        <CalendarPlus size={18} className="inline mr-2" /> Событие
                    </button>
                </div>
            </div>

            {error && <div className="rounded-2xl border border-red-100 bg-red-50 p-4 text-sm text-red-600">{error}</div>}

            {scheduleView === 'day' ? (
                <section className="rounded-[28px] border border-gray-100 bg-white p-6 shadow-sm">
                    <div className="mb-5 flex items-center justify-between">
                        <button onClick={() => setDayOffset(value => value - 1)} className="rounded-xl p-2 text-gray-700 hover:bg-gray-100"><ChevronLeft size={20} /></button>
                        <div className="text-center">
                            <div className="font-black capitalize text-gray-900">{selectedDayLabel}</div>
                            {dayOffset !== 0 && <button onClick={() => setDayOffset(0)} className="text-xs font-bold text-indigo-600">Вернуться к сегодня</button>}
                        </div>
                        <button onClick={() => setDayOffset(value => value + 1)} className="rounded-xl p-2 text-gray-700 hover:bg-gray-100"><ChevronRight size={20} /></button>
                    </div>
                    {loading ? <p className="text-sm text-gray-400">Загрузка...</p> : selectedDayItems.length === 0 ? (
                        <p className="text-sm text-gray-400">На этот день нет событий, встреч и базовых блоков.</p>
                    ) : (
                        <div className="space-y-3">{selectedDayItems.map(item => renderItem(item))}</div>
                    )}
                </section>
            ) : (
                <section className="rounded-[28px] border border-gray-100 bg-white p-6 shadow-sm">
                    <div className="mb-5 flex items-center justify-between">
                        <button onClick={() => setWeekOffset(value => value - 1)} className="rounded-xl p-2 text-gray-700 hover:bg-gray-100"><ChevronLeft size={20} /></button>
                        <div className="text-center">
                            <div className="font-black text-gray-900">{formatWeekRange}</div>
                            {weekOffset !== 0 && <button onClick={() => setWeekOffset(0)} className="text-xs font-bold text-indigo-600">Вернуться к текущей неделе</button>}
                        </div>
                        <button onClick={() => setWeekOffset(value => value + 1)} className="rounded-xl p-2 text-gray-700 hover:bg-gray-100"><ChevronRight size={20} /></button>
                    </div>
                    {isVerticalWeekLayout ? (
                        <div className="space-y-4">
                            {weekDays.map(day => {
                                const dayItems = getItemsForDay(day);
                                return (
                                    <div key={day.toISOString()} className="rounded-2xl border border-gray-100 bg-gray-50/60 p-4">
                                        <div className="mb-3 flex items-end justify-between gap-3">
                                            <div>
                                                <div className="text-xs font-black uppercase text-gray-400">{dayNames[day.getDay()]}</div>
                                                <div className="text-lg font-black text-gray-900">
                                                    {day.getDate()} {day.toLocaleDateString('ru-RU', { month: 'long' })}
                                                </div>
                                            </div>
                                        </div>
                                        <div className="space-y-3">
                                            {dayItems.length === 0 ? <p className="text-sm text-gray-400">Свободно</p> : dayItems.map(item => renderItem(item))}
                                        </div>
                                    </div>
                                );
                            })}
                        </div>
                    ) : (
                        <div className="overflow-x-auto pb-2">
                            <div className="grid min-w-[1120px] grid-cols-7 gap-4">
                                {weekDays.map(day => {
                                    const dayItems = getItemsForDay(day);
                                    return (
                                        <div key={day.toISOString()} className="min-h-[500px] rounded-2xl border border-gray-100 bg-gray-50/60 p-3">
                                            <div className="mb-3">
                                                <div className="text-xs font-black uppercase text-gray-400">{dayNames[day.getDay()]}</div>
                                                <div className="break-words font-black leading-tight text-gray-900">
                                                    {day.getDate()} {day.toLocaleDateString('ru-RU', { month: 'short' })}
                                                </div>
                                            </div>
                                            <div className="space-y-2">
                                                {dayItems.length === 0 ? (
                                                    <p className="rounded-xl border border-dashed border-gray-200 bg-white/70 px-3 py-4 text-center text-xs text-gray-400">Свободно</p>
                                                ) : dayItems.map(item => renderItem(item, true))}
                                            </div>
                                        </div>
                                    );
                                })}
                            </div>
                        </div>
                    )}
                </section>
            )}

            {isModalOpen && (
                <div className="fixed inset-0 z-50 overflow-y-auto bg-slate-950/50 p-3 sm:p-4">
                    <div className="flex min-h-full items-start justify-center py-2 sm:items-center sm:py-6">
                        <form onSubmit={handleSubmit} className="max-h-[calc(100vh-1.5rem)] w-full max-w-md overflow-y-auto rounded-[28px] bg-white p-5 shadow-2xl sm:max-w-lg sm:p-6">
                        <div className="mb-5">
                            <h2 className="text-2xl font-black text-gray-900">
                                {editingEvent ? 'Редактировать событие' : editingBaseItem ? 'Изменить базовый блок только на этот день' : 'Новое событие'}
                            </h2>
                            {editingBaseItem && (
                                <p className="mt-1 text-sm text-gray-500">
                                    Изменение не затронет недельный шаблон: будет создано отдельное событие только для выбранной даты.
                                </p>
                            )}
                        </div>

                        <div className="space-y-4">
                            <div>
                                <div className="mb-1 text-xs font-black uppercase tracking-wide text-gray-400">Название</div>
                                <input value={title} onChange={event => setTitle(event.target.value)} required placeholder="Введите название события" className="w-full rounded-xl border border-gray-200 px-4 py-3 text-sm text-gray-900 outline-none focus:ring-2 focus:ring-indigo-500" />
                            </div>
                            <div>
                                <div className="mb-1 text-xs font-black uppercase tracking-wide text-gray-400">Описание</div>
                                <textarea value={description} onChange={event => setDescription(event.target.value)} placeholder="Введите описание события" className="w-full rounded-xl border border-gray-200 px-4 py-3 text-sm text-gray-900 outline-none focus:ring-2 focus:ring-indigo-500" />
                            </div>
                            <div className="grid gap-3 sm:grid-cols-3">
                                <label className="block">
                                    <div className="mb-1 text-xs font-black uppercase tracking-wide text-gray-400">Дата</div>
                                    <input type="date" value={date} onChange={event => setDate(event.target.value)} className="w-full rounded-xl border border-gray-200 px-4 py-3 text-sm text-gray-900" />
                                </label>
                                <label className="block">
                                    <div className="mb-1 text-xs font-black uppercase tracking-wide text-gray-400">Начало</div>
                                    <input
                                        type="time"
                                        value={startTime}
                                        onChange={event => {
                                            const nextStartTime = event.target.value;
                                            setStartTime(nextStartTime);
                                            syncEndTimeWithCurrentDuration(date, nextStartTime);
                                        }}
                                        className="w-full rounded-xl border border-gray-200 px-4 py-3 text-sm text-gray-900"
                                    />
                                </label>
                                <label className="block">
                                    <div className="mb-1 text-xs font-black uppercase tracking-wide text-gray-400">Конец</div>
                                    <input type="time" value={endTime} onChange={event => setEndTime(event.target.value)} className="w-full rounded-xl border border-gray-200 px-4 py-3 text-sm text-gray-900" />
                                </label>
                            </div>

                            <div className="rounded-2xl border border-gray-100 bg-gray-50 p-4">
                                <div className="mb-2 text-xs font-black uppercase tracking-wide text-gray-400">Быстрая длительность</div>
                                <div className="flex flex-wrap gap-2">
                                    {[30, 60, 90, 120].map(minutes => (
                                        <button
                                            key={minutes}
                                            type="button"
                                            onClick={() => applyDuration(minutes)}
                                            className={`rounded-xl border px-3 py-2 text-sm font-bold transition ${
                                                currentDurationMinutes === minutes
                                                    ? 'border-indigo-200 bg-indigo-50 text-indigo-700'
                                                    : 'border-transparent bg-white text-gray-600 hover:bg-indigo-50'
                                            }`}
                                        >
                                            {minutes} мин
                                        </button>
                                    ))}
                                </div>
                            </div>

                            <div>
                                <div className="mb-2 text-xs font-black uppercase tracking-wide text-gray-400">Тип события</div>
                                <div className="grid gap-2 sm:grid-cols-3">
                                    {eventTypeOptions.map(option => (
                                        <button
                                            key={option.value}
                                            type="button"
                                            onClick={() => setType(option.value)}
                                            className={`flex min-h-[126px] flex-col justify-between rounded-2xl border px-4 py-4 text-left transition ${
                                                type === option.value
                                                    ? option.selectedClassName
                                                    : 'border-gray-100 bg-white text-gray-700 hover:border-gray-200'
                                            }`}
                                        >
                                            <div className="min-w-0">
                                                <div className="font-black">{option.label}</div>
                                                <div className="mt-2 text-xs leading-5 text-current/80">{option.description}</div>
                                            </div>
                                            <div className={`mt-4 h-1.5 rounded-full transition ${type === option.value ? option.barClassName : 'bg-transparent'}`} />
                                        </button>
                                    ))}
                                </div>
                            </div>
                        </div>
                        <div className="sticky bottom-0 mt-5 flex justify-end gap-2 border-t border-gray-100 bg-white/95 pt-4 backdrop-blur">
                            <button type="button" onClick={closeModal} className="rounded-xl bg-gray-100 px-4 py-2 text-sm font-bold text-gray-600">Отмена</button>
                            <button type="submit" className="rounded-xl bg-indigo-600 px-4 py-2 text-sm font-bold text-white">
                                {editingEvent ? 'Сохранить' : editingBaseItem ? 'Применить на этот день' : 'Создать'}
                            </button>
                        </div>
                        </form>
                    </div>
                </div>
            )}
        </div>
    );
};
