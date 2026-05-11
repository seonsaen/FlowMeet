import { useEffect, useMemo, useRef, useState } from 'react';
import { Battery, Bell, BellRing, CalendarClock, Check, Clock, MoonStar, Sparkles, TrendingUp, Users, Waves, X } from '../../components/icons/hugeIcons';
import type {
    BaseScheduleEntry,
    BaseScheduleOccurrenceException,
    DashboardInsights,
    EventResponse,
    GroupInvite,
    IncomingFriendRequest,
    ManagedMeeting,
    MeetingInvite,
    NotificationDto,
    ResourceResponse,
    User
} from '../../types';
import { scheduleApi } from '../../api/schedule';
import { userStateApi } from '../../api/userState';
import { groupsApi } from '../../api/groups';
import { notificationsApi } from '../../api/notifications';
import { profileApi } from '../../api/profile';
import { socialApi } from '../../api/social';

interface DashboardPageProps {
    user: User;
}

const formatDateTime = (value: string) =>
    new Intl.DateTimeFormat('ru-RU', {
        day: '2-digit',
        month: 'short',
        hour: '2-digit',
        minute: '2-digit'
    }).format(new Date(value));

const normalizeTime = (value: string) => value.length >= 5 ? value.slice(0, 5) : value;

const toLocalDateInputValue = (date: Date) => {
    const year = date.getFullYear();
    const month = `${date.getMonth() + 1}`.padStart(2, '0');
    const day = `${date.getDate()}`.padStart(2, '0');
    return `${year}-${month}-${day}`;
};

const combineDateAndTime = (date: Date, time: string) => {
    const [hours, minutes] = normalizeTime(time).split(':').map(Number);
    const next = new Date(date);
    next.setHours(hours, minutes, 0, 0);
    return next.toISOString();
};

const sameLocalDay = (value: string, date: Date) => {
    const current = new Date(value);
    return current.getFullYear() === date.getFullYear()
        && current.getMonth() === date.getMonth()
        && current.getDate() === date.getDate();
};

type AgendaItem = {
    id: string;
    title: string;
    description?: string;
    startTime: string;
    endTime: string;
    source: 'event' | 'meeting' | 'base';
};

const typeMeta: Record<string, { label: string; className: string }> = {
    Info: { label: 'Инфо', className: 'bg-slate-100 text-slate-700' },
    FriendRequest: { label: 'Друзья', className: 'bg-emerald-100 text-emerald-700' },
    GroupInvite: { label: 'Группа', className: 'bg-cyan-100 text-cyan-700' },
    MeetingInvite: { label: 'Встреча', className: 'bg-indigo-100 text-indigo-700' }
};

const sleepOptions = [
    { value: 0, label: 'Плохо' },
    { value: 1, label: 'Нормально' },
    { value: 2, label: 'Хорошо' }
];

const backgroundOptions = [
    { value: 0, label: 'Спокойно' },
    { value: 1, label: 'Напряженно' },
    { value: 2, label: 'Тяжело' }
];

const meetingStatusMeta: Record<number, { label: string; className: string }> = {
    0: { label: 'Ожидает', className: 'bg-amber-50 text-amber-700' },
    1: { label: 'Подтверждена', className: 'bg-emerald-50 text-emerald-700' },
    2: { label: 'Отменена', className: 'bg-slate-100 text-slate-600' }
};

const normalizeNotificationType = (type: NotificationDto['type']) => {
    if (typeof type === 'string') {
        return type;
    }

    return ({
        0: 'Info',
        1: 'FriendRequest',
        2: 'GroupInvite',
        3: 'MeetingInvite',
        4: 'Info',
        5: 'Info'
    } as Record<number, string>)[type] ?? 'Info';
};

const SegmentGroup = ({
    value,
    options,
    onChange
}: {
    value: number;
    options: Array<{ value: number; label: string }>;
    onChange: (value: number) => void;
}) => (
    <div className="grid grid-cols-3 gap-2">
        {options.map(option => (
            <button
                key={option.value}
                type="button"
                onClick={() => onChange(option.value)}
                className={`rounded-xl px-3 py-2 text-sm font-bold transition ${
                    value === option.value
                        ? 'border border-indigo-200 bg-indigo-50 text-indigo-700 shadow-sm'
                        : 'bg-white text-gray-500 hover:bg-indigo-50'
                }`}
            >
                {option.label}
            </button>
        ))}
    </div>
);

export const DashboardPage = ({ user }: DashboardPageProps) => {
    const [events, setEvents] = useState<EventResponse[]>([]);
    const [baseSchedule, setBaseSchedule] = useState<BaseScheduleEntry[]>([]);
    const [baseExceptions, setBaseExceptions] = useState<BaseScheduleOccurrenceException[]>([]);
    const [meetingInvites, setMeetingInvites] = useState<MeetingInvite[]>([]);
    const [groupInvites, setGroupInvites] = useState<GroupInvite[]>([]);
    const [friendRequests, setFriendRequests] = useState<IncomingFriendRequest[]>([]);
    const [myMeetings, setMyMeetings] = useState<ManagedMeeting[]>([]);
    const [notifications, setNotifications] = useState<NotificationDto[]>([]);
    const [resource, setResource] = useState<ResourceResponse | null>(null);
    const [insights, setInsights] = useState<DashboardInsights | null>(null);
    const [mood, setMood] = useState(3);
    const [sleepQuality, setSleepQuality] = useState(1);
    const [backgroundLoadLevel, setBackgroundLoadLevel] = useState(0);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');
    const [success, setSuccess] = useState('');
    const lastSavedStateRef = useRef('');
    const stateReadyRef = useRef(false);
    const saveSequenceRef = useRef(0);
    const loadDashboard = async () => {
        setLoading(true);
        setError('');
        try {
            const [schedule, baseData, baseExceptionData, resourceStatus, insightData, meetingData, groupData, friendRequestData, myMeetingsData, notificationData] = await Promise.all([
                scheduleApi.getUserSchedule(),
                profileApi.getBaseSchedule(),
                profileApi.getBaseScheduleExceptions(),
                userStateApi.getResource(),
                userStateApi.getDashboardInsights().catch(() => null),
                scheduleApi.getMeetingInvites(),
                groupsApi.getIncomingInvites().catch(() => []),
                socialApi.getIncomingRequests().catch(() => []),
                scheduleApi.getMyMeetings().catch(() => []),
                notificationsApi.getMyNotifications(true)
            ]);

            setEvents(schedule);
            setBaseSchedule(baseData);
            setBaseExceptions(baseExceptionData);
            setResource(resourceStatus);
            setInsights(insightData);
            setMood(resourceStatus.moodLevel);
            setSleepQuality(resourceStatus.sleepQuality);
            setBackgroundLoadLevel(resourceStatus.backgroundLoadLevel);
            setMeetingInvites(meetingData);
            setGroupInvites(groupData);
            setFriendRequests(friendRequestData);
            setMyMeetings(myMeetingsData);
            setNotifications(notificationData);
            lastSavedStateRef.current = JSON.stringify({
                moodLevel: resourceStatus.moodLevel,
                sleepQuality: resourceStatus.sleepQuality,
                backgroundLoadLevel: resourceStatus.backgroundLoadLevel
            });
            stateReadyRef.current = true;
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Не удалось загрузить главную');
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        void loadDashboard();
    }, []);

    useEffect(() => {
        const intervalId = window.setInterval(() => {
            void loadDashboard();
        }, 60000);

        return () => window.clearInterval(intervalId);
    }, []);

    const todayItems = useMemo<AgendaItem[]>(() => {
        const now = new Date();
        const todayKey = toLocalDateInputValue(now);
        const eventItems = events
            .filter(event => sameLocalDay(event.startTime, now))
            .map(event => ({
                id: event.id,
                title: event.title,
                description: event.description,
                startTime: event.startTime,
                endTime: event.endTime,
                source: event.source === 'meeting' ? 'meeting' : 'event'
            } satisfies AgendaItem));

        const baseItems = baseSchedule
            .filter(entry => entry.dayOfWeek === now.getDay())
            .filter(entry => !baseExceptions.some(exception => exception.baseScheduleEntryId === entry.id && exception.date === todayKey))
            .map((entry, index) => ({
                id: entry.id ?? `base-${entry.dayOfWeek}-${entry.startTime}-${index}`,
                title: entry.title,
                description: entry.description,
                startTime: combineDateAndTime(now, entry.startTime),
                endTime: combineDateAndTime(now, entry.endTime),
                source: 'base' as const
            }));

        return [...eventItems, ...baseItems]
            .sort((a, b) => new Date(a.startTime).getTime() - new Date(b.startTime).getTime());
    }, [baseExceptions, baseSchedule, events]);

    const upcomingMeetings = useMemo(() => {
        const now = new Date();
        return myMeetings
            .filter(meeting => meeting.status !== 2 && new Date(meeting.endTime) > now)
            .sort((a, b) => new Date(a.startTime).getTime() - new Date(b.startTime).getTime())
            .slice(0, 5);
    }, [myMeetings]);

    const getSourceLabel = (source: AgendaItem['source']) => {
        if (source === 'base') {
            return 'База';
        }

        if (source === 'meeting') {
            return 'Встреча';
        }

        return 'Событие';
    };

    useEffect(() => {
        if (!stateReadyRef.current) {
            return;
        }

        const payload = {
            moodLevel: mood,
            sleepQuality,
            backgroundLoadLevel
        };
        const signature = JSON.stringify(payload);

        if (signature === lastSavedStateRef.current) {
            return;
        }

        const requestId = saveSequenceRef.current + 1;
        saveSequenceRef.current = requestId;

        const timeoutId = window.setTimeout(() => {
            void (async () => {
                try {
                    await userStateApi.setMood(payload);
                    const updatedResource = await userStateApi.getResource();
                    if (requestId !== saveSequenceRef.current) {
                        return;
                    }

                    lastSavedStateRef.current = signature;
                    setResource(updatedResource);
                } catch (err) {
                    if (requestId === saveSequenceRef.current) {
                        setError(err instanceof Error ? err.message : 'Не удалось обновить ресурс');
                    }
                }
            })();
        }, 250);

        return () => window.clearTimeout(timeoutId);
    }, [backgroundLoadLevel, mood, sleepQuality]);

    const respondToMeeting = async (meetingId: string, isAccepted: boolean) => {
        await scheduleApi.respondToMeetingInvite({ meetingId, isAccepted });
        setSuccess(isAccepted ? 'Приглашение на встречу принято' : 'Приглашение на встречу отклонено');
        await loadDashboard();
    };

    const respondToGroup = async (groupId: string, isAccepted: boolean) => {
        await groupsApi.respondToInvite({ groupId, isAccepted });
        setSuccess(isAccepted ? 'Приглашение в группу принято' : 'Приглашение в группу отклонено');
        await loadDashboard();
    };

    const respondToFriendRequest = async (requesterId: string, isAccepted: boolean) => {
        if (isAccepted) {
            await socialApi.acceptFriendRequest(requesterId);
        } else {
            await socialApi.declineFriendRequest(requesterId);
        }

        setSuccess(isAccepted ? 'Заявка в друзья принята' : 'Заявка в друзья отклонена');
        await loadDashboard();
    };

    const handleMarkAsRead = async (notificationId: string) => {
        await notificationsApi.markAsRead(notificationId);
        await loadDashboard();
    };

    return (
        <div className="p-4 md:p-8 max-w-6xl mx-auto space-y-6">
            <div>
                <h1 className="text-3xl md:text-4xl font-bold text-gray-900 tracking-tight">
                    Привет, {user.firstName}
                </h1>
            </div>

            {error && <div className="bg-red-50 text-red-600 border border-red-100 rounded-2xl p-4 text-sm">{error}</div>}
            {success && <div className="bg-emerald-50 text-emerald-700 border border-emerald-100 rounded-2xl p-4 text-sm">{success}</div>}

            <section className="grid gap-4 lg:grid-cols-[1.1fr_0.9fr]">
                <div className="rounded-[28px] border border-gray-100 bg-white p-6 shadow-sm">
                    <div className="mb-6 flex items-center justify-between">
                        <div>
                            <div className="flex items-center gap-2 text-xs font-black uppercase tracking-wider text-indigo-500">
                                <Battery size={16} /> Ресурс
                            </div>
                            <h2 className="mt-2 text-2xl font-black text-gray-900">Текущий запас энергии</h2>
                        </div>
                        <span className="text-4xl font-black text-gray-900">{resource?.resourceLevel ?? 0}%</span>
                    </div>

                    <div className="h-4 overflow-hidden rounded-full bg-gray-100">
                        <div
                            className="h-full rounded-full bg-gradient-to-r from-indigo-600 to-cyan-500 transition-all"
                            style={{ width: `${Math.max(0, Math.min(resource?.resourceLevel ?? 0, 100))}%` }}
                        />
                    </div>
                    <div className="mt-5">
                        <div className="rounded-2xl bg-gray-50 p-4">
                            <div className="text-xs font-bold uppercase text-gray-400">Статус</div>
                            <div className="mt-1 text-sm font-bold text-gray-800">{resource?.statusMessage ?? 'Загружается...'}</div>
                        </div>
                    </div>

                    <div className="mt-6 border-t border-gray-100 pt-6">
                        <div className="space-y-4">
                            <div>
                                <div className="mb-2 text-xs font-black uppercase tracking-wide text-gray-400">Настроение</div>
                                <div className="grid grid-cols-5 gap-2">
                                    {[1, 2, 3, 4, 5].map(value => (
                                        <button
                                            key={value}
                                            type="button"
                                            onClick={() => setMood(value)}
                                            className={`rounded-2xl border py-3 font-black transition-all ${
                                                mood === value
                                                    ? 'border-indigo-200 bg-indigo-50 text-indigo-700 shadow-sm'
                                                    : 'border-transparent bg-gray-50 text-gray-500 hover:bg-indigo-50'
                                            }`}
                                        >
                                            {value}
                                        </button>
                                    ))}
                                </div>
                            </div>

                            <div>
                                <div className="mb-2 flex items-center gap-2 text-xs font-black uppercase tracking-wide text-gray-400">
                                    <MoonStar size={14} /> Сон
                                </div>
                                <SegmentGroup value={sleepQuality} options={sleepOptions} onChange={setSleepQuality} />
                            </div>

                            <div>
                                <div className="mb-2 flex items-center gap-2 text-xs font-black uppercase tracking-wide text-gray-400">
                                    <Waves size={14} /> Фон дня
                                </div>
                                <SegmentGroup value={backgroundLoadLevel} options={backgroundOptions} onChange={setBackgroundLoadLevel} />
                            </div>
                        </div>
                    </div>
                </div>

                <section className="rounded-[28px] border border-gray-100 bg-white p-6 shadow-sm">
                    <div className="mb-4 flex items-center justify-between">
                        <div>
                            <div className="flex items-center gap-2 text-xs font-black uppercase tracking-wider text-indigo-500">
                                <CalendarClock size={16} /> Встречи
                            </div>
                            <h2 className="mt-2 text-2xl font-black text-gray-900">Ближайшие встречи</h2>
                        </div>
                    </div>

                    {loading ? (
                        <p className="text-sm text-gray-400">Загрузка...</p>
                    ) : upcomingMeetings.length === 0 ? (
                        <div className="rounded-2xl border-2 border-dashed border-gray-200 bg-gray-50 p-6 text-center text-sm text-gray-400">
                            Пока нет встреч в ближайшем плане.
                        </div>
                    ) : (
                        <div className="space-y-3">
                            {upcomingMeetings.map(meeting => {
                                const meta = meetingStatusMeta[meeting.status] ?? meetingStatusMeta[0];

                                return (
                                    <article key={meeting.meetingId} className="rounded-2xl bg-gray-50 p-4">
                                        <div className="flex items-start justify-between gap-3">
                                            <div className="min-w-0">
                                                <div className="font-black text-gray-900">{meeting.title}</div>
                                                <div className="mt-1 text-xs text-gray-500">{formatDateTime(meeting.startTime)} - {formatDateTime(meeting.endTime)}</div>
                                            </div>
                                            <span className={`rounded-full px-2.5 py-1 text-[10px] font-black uppercase tracking-wide ${meta.className}`}>
                                                {meta.label}
                                            </span>
                                        </div>
                                        {meeting.relatedGroupName && (
                                            <div className="mt-3 text-xs font-bold text-gray-500">Группа: {meeting.relatedGroupName}</div>
                                        )}
                                        <div className="mt-3 text-xs text-gray-400">
                                            Участники: {meeting.participantNames.join(', ') || '—'}
                                        </div>
                                    </article>
                                );
                            })}
                        </div>
                    )}
                </section>
            </section>

            <section className="grid gap-4 xl:grid-cols-[0.9fr_1.1fr]">
                <div className="rounded-[28px] border border-gray-100 bg-white p-6 shadow-sm">
                    <div className="mb-5 flex items-center justify-between">
                        <div>
                            <div className="flex items-center gap-2 text-xs font-black uppercase tracking-wider text-indigo-500">
                                <TrendingUp size={16} /> Статистика
                            </div>
                            <h2 className="mt-2 text-2xl font-black text-gray-900">Статистика встреч</h2>
                        </div>
                    </div>

                    <div className="grid gap-3 sm:grid-cols-3">
                        <div className="rounded-2xl bg-gray-50 p-4">
                            <div className="text-xs font-black uppercase text-gray-400">За 30 дней</div>
                            <div className="mt-2 text-2xl font-black text-gray-900">{insights?.meetingsLast30Days ?? 0}</div>
                        </div>
                        <div className="rounded-2xl bg-gray-50 p-4">
                            <div className="text-xs font-black uppercase text-gray-400">Впереди</div>
                            <div className="mt-2 text-2xl font-black text-gray-900">{insights?.confirmedFutureMeetings ?? upcomingMeetings.length}</div>
                        </div>
                        <div className="rounded-2xl bg-gray-50 p-4">
                            <div className="text-xs font-black uppercase text-gray-400">Настроение</div>
                            <div className="mt-2 text-2xl font-black text-gray-900">{insights?.averageMoodLast14Days ?? '—'}</div>
                        </div>
                    </div>

                    {insights?.overloadWarning && (
                        <div className="mt-4 rounded-2xl border border-amber-100 bg-amber-50 p-4 text-sm font-bold text-amber-800">
                            {insights.overloadWarning}
                        </div>
                    )}

                    {insights?.frequentParticipants?.length ? (
                        <div className="mt-5">
                            <div className="mb-3 flex items-center gap-2 text-xs font-black uppercase tracking-wide text-gray-400">
                                <Users size={14} /> Частые участники
                            </div>
                            <div className="space-y-2">
                                {insights.frequentParticipants.map(participant => (
                                    <div key={participant.userId} className="flex items-center justify-between gap-3 rounded-2xl bg-gray-50 px-4 py-3">
                                        <div className="min-w-0">
                                            <div className="break-words text-sm font-black text-gray-900">{participant.name}</div>
                                            {participant.lastMeetingAt && (
                                                <div className="mt-1 text-xs text-gray-400">Последняя встреча: {formatDateTime(participant.lastMeetingAt)}</div>
                                            )}
                                        </div>
                                        <span className="shrink-0 rounded-full bg-white px-3 py-1 text-xs font-black text-indigo-600">
                                            {participant.meetingsCount}
                                        </span>
                                    </div>
                                ))}
                            </div>
                        </div>
                    ) : null}
                </div>

                <div className="rounded-[28px] border border-gray-100 bg-white p-6 shadow-sm">
                    <div className="mb-5 flex items-center justify-between">
                        <div>
                            <div className="flex items-center gap-2 text-xs font-black uppercase tracking-wider text-indigo-500">
                                <Sparkles size={16} /> Рекомендации
                            </div>
                            <h2 className="mt-2 text-2xl font-black text-gray-900">Кого стоит позвать</h2>
                        </div>
                    </div>

                    {!insights?.recommendations?.length ? (
                        <p className="rounded-2xl border-2 border-dashed border-gray-200 bg-gray-50 p-8 text-center text-sm text-gray-400">
                            Рекомендации появятся, когда накопится история встреч и отметок настроения.
                        </p>
                    ) : (
                        <div className="space-y-3">
                            {insights.recommendations.map((recommendation, index) => (
                                <article key={`${recommendation.type}-${recommendation.userId ?? index}`} className="rounded-3xl border border-indigo-100 bg-indigo-50/40 p-5">
                                    <div className="text-xs font-black uppercase tracking-wide text-indigo-500">{recommendation.title}</div>
                                    <p className="mt-2 text-sm font-bold leading-6 text-gray-800">{recommendation.message}</p>
                                    {recommendation.suggestedStartTime && recommendation.suggestedEndTime && (
                                        <div className="mt-3 rounded-2xl bg-white/80 px-4 py-3 text-sm font-black text-indigo-700">
                                            {formatDateTime(recommendation.suggestedStartTime)} - {formatDateTime(recommendation.suggestedEndTime)}
                                        </div>
                                    )}
                                </article>
                            ))}
                        </div>
                    )}
                </div>
            </section>

            <section className="grid gap-4 lg:grid-cols-2">
                <div className="bg-white rounded-[28px] border border-gray-100 shadow-sm p-6">
                    <div className="flex items-center justify-between mb-4">
                        <h2 className="font-black text-gray-900">Сегодня</h2>
                        <Clock size={18} className="text-indigo-500" />
                    </div>
                    {loading ? (
                        <p className="text-sm text-gray-400">Загрузка...</p>
                    ) : todayItems.length === 0 ? (
                        <p className="text-sm text-gray-400">На сегодня событий нет.</p>
                    ) : (
                        <div className="space-y-3">
                            {todayItems.map(item => (
                                <div key={`${item.source}-${item.id}`} className="rounded-2xl bg-gray-50 px-4 py-3">
                                    <div className="flex items-center justify-between gap-3">
                                        <div className="font-bold text-gray-800">{item.title}</div>
                                        <span className="rounded-full bg-white px-2.5 py-1 text-[10px] font-black uppercase tracking-wide text-gray-500">
                                            {getSourceLabel(item.source)}
                                        </span>
                                    </div>
                                    <div className="text-xs text-gray-500">{formatDateTime(item.startTime)} - {formatDateTime(item.endTime)}</div>
                                    {item.description && <div className="mt-1 text-xs text-gray-400">{item.description}</div>}
                                </div>
                            ))}
                        </div>
                    )}
                </div>

                <div className="bg-white rounded-[28px] border border-gray-100 shadow-sm p-6">
                    <div className="flex items-center justify-between mb-4">
                        <h2 className="font-black text-gray-900">Входящие решения</h2>
                        <Bell size={18} className="text-indigo-500" />
                    </div>
                    {[...meetingInvites, ...groupInvites, ...friendRequests].length === 0 ? (
                        <p className="text-sm text-gray-400">Нет входящих решений, требующих ответа.</p>
                    ) : (
                        <div className="space-y-3">
                            {meetingInvites.map(invite => (
                                <div key={invite.meetingId} className="rounded-2xl border border-indigo-100 bg-indigo-50/40 p-4">
                                    <div className="text-xs font-bold uppercase text-indigo-500">Встреча от {invite.organizerName}</div>
                                    <div className="font-bold text-gray-900">{invite.title}</div>
                                    <div className="text-xs text-gray-500 mb-3">{formatDateTime(invite.startTime)}</div>
                                    <div className="flex gap-2">
                                        <button onClick={() => void respondToMeeting(invite.meetingId, true)} className="rounded-xl bg-green-600 px-3 py-2 text-xs font-bold text-white"><Check size={14} /></button>
                                        <button onClick={() => void respondToMeeting(invite.meetingId, false)} className="rounded-xl bg-red-500 px-3 py-2 text-xs font-bold text-white"><X size={14} /></button>
                                    </div>
                                </div>
                            ))}
                            {groupInvites.map(invite => (
                                <div key={invite.groupId} className="rounded-2xl border border-cyan-100 bg-cyan-50/40 p-4">
                                    <div className="text-xs font-bold uppercase text-cyan-600">Группа от {invite.inviterName}</div>
                                    <div className="font-bold text-gray-900">{invite.groupName}</div>
                                    <div className="text-xs text-gray-500 mb-3">{formatDateTime(invite.createdDate)}</div>
                                    <div className="flex gap-2">
                                        <button onClick={() => void respondToGroup(invite.groupId, true)} className="rounded-xl bg-green-600 px-3 py-2 text-xs font-bold text-white"><Check size={14} /></button>
                                        <button onClick={() => void respondToGroup(invite.groupId, false)} className="rounded-xl bg-red-500 px-3 py-2 text-xs font-bold text-white"><X size={14} /></button>
                                    </div>
                                </div>
                            ))}
                            {friendRequests.map(request => (
                                <div key={request.requesterId} className="rounded-2xl border border-emerald-100 bg-emerald-50/40 p-4">
                                    <div className="text-xs font-bold uppercase text-emerald-600">Заявка в друзья</div>
                                    <div className="font-bold text-gray-900">{request.requesterName}</div>
                                    <div className="text-xs text-gray-500 mb-3">{request.requesterEmail}</div>
                                    <div className="flex gap-2">
                                        <button onClick={() => void respondToFriendRequest(request.requesterId, true)} className="rounded-xl bg-green-600 px-3 py-2 text-xs font-bold text-white"><Check size={14} /></button>
                                        <button onClick={() => void respondToFriendRequest(request.requesterId, false)} className="rounded-xl bg-red-500 px-3 py-2 text-xs font-bold text-white"><X size={14} /></button>
                                    </div>
                                </div>
                            ))}
                        </div>
                    )}
                </div>
            </section>

            <section className="bg-white rounded-[28px] border border-gray-100 shadow-sm p-6">
                <div className="mb-5 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                    <div>
                        <div className="flex items-center gap-2 text-xs font-black uppercase tracking-wider text-indigo-500">
                            <BellRing size={16} /> Уведомления
                        </div>
                        <h2 className="mt-2 text-2xl font-black text-gray-900">Что требует внимания</h2>
                    </div>
                </div>

                {loading ? (
                    <p className="text-sm text-gray-400">Загрузка...</p>
                ) : notifications.length === 0 ? (
                    <p className="rounded-2xl border-2 border-dashed border-gray-200 bg-gray-50 p-8 text-center text-sm text-gray-400">
                        Уведомлений пока нет.
                    </p>
                ) : (
                    <div className="grid gap-3 lg:grid-cols-2">
                        {notifications.slice(0, 6).map(notification => {
                            const typeName = normalizeNotificationType(notification.type);
                            const meta = typeMeta[typeName] ?? typeMeta.Info;

                            return (
                                <article key={notification.id} className="rounded-3xl border border-indigo-100 bg-white p-5">
                                    <div className="flex items-start justify-between gap-3">
                                        <div className="min-w-0">
                                            <span className={`inline-flex rounded-full px-3 py-1 text-[11px] font-black uppercase tracking-wide ${meta.className}`}>
                                                {meta.label}
                                            </span>
                                            <h3 className="mt-3 font-black text-gray-900">{notification.title}</h3>
                                            <p className="mt-2 text-sm text-gray-500">{notification.message}</p>
                                        </div>
                                        <div className="flex shrink-0 gap-2">
                                            <button
                                                onClick={() => void handleMarkAsRead(notification.id)}
                                                className="rounded-xl bg-emerald-50 p-2 text-emerald-600 hover:bg-emerald-100"
                                                title="Отметить как просмотренное"
                                            >
                                                <Check size={16} />
                                            </button>
                                        </div>
                                    </div>
                                    <div className="mt-4 flex flex-wrap gap-2 text-xs text-gray-400">
                                        <span>Создано: {formatDateTime(notification.createdAt)}</span>
                                        {notification.scheduledFor && <span>Запланировано: {formatDateTime(notification.scheduledFor)}</span>}
                                    </div>
                                </article>
                            );
                        })}
                    </div>
                )}
            </section>
        </div>
    );
};
