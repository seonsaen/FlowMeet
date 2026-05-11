import { type FormEvent, useEffect, useState } from 'react';
import type {
    BaseScheduleEntry,
    ManagedMeeting,
    MeetingTimePreset,
    NotificationSettings,
    ProfileResponse,
    User,
    UserSettings
} from '../../types';
import { BellRing, CalendarClock, CalendarPlus, Clock3, KeyRound, LogOut, Pencil, Plus, Save, Trash2, X } from '../../components/icons/hugeIcons';
import { profileApi } from '../../api/profile';
import { useAuth } from '../../hooks/useAuth';
import { scheduleApi } from '../../api/schedule';

interface ProfilePageProps {
    user: User;
    onLogout: () => void;
}

const weekDays = [
    { value: 1, label: 'Понедельник', short: 'Пн' },
    { value: 2, label: 'Вторник', short: 'Вт' },
    { value: 3, label: 'Среда', short: 'Ср' },
    { value: 4, label: 'Четверг', short: 'Чт' },
    { value: 5, label: 'Пятница', short: 'Пт' },
    { value: 6, label: 'Суббота', short: 'Сб' },
    { value: 0, label: 'Воскресенье', short: 'Вс' }
];

const typeLabels = [
    { value: 0, label: 'Обязательное', className: 'bg-red-50 text-red-700 border-red-100' },
    { value: 1, label: 'Гибкое', className: 'bg-blue-50 text-blue-700 border-blue-100' },
    { value: 2, label: 'Желательное', className: 'bg-amber-50 text-amber-700 border-amber-100' }
];

const notificationOptions: Array<{
    key: keyof NotificationSettings;
    label: string;
    description: string;
}> = [
    {
        key: 'meetingInvites',
        label: 'Встречи',
        description: 'Приглашения и обновления по встречам'
    },
    {
        key: 'groupInvites',
        label: 'Группы',
        description: 'Приглашения в группы и изменения состава'
    },
    {
        key: 'emailNotifications',
        label: 'Email-копия',
        description: 'Дублировать важные уведомления на почту'
    }
];

const meetingPresets: Array<{ value: MeetingTimePreset; label: string; description: string }> = [
    { value: 'morning', label: 'Утром', description: 'Хорошо для ранних встреч' },
    { value: 'day', label: 'Днем', description: 'Нейтральное окно' },
    { value: 'evening', label: 'Вечером', description: 'Подходит, если днем обычно занят' },
    { value: 'any', label: 'В любое', description: 'Готов встречаться в разное время' }
];

const defaultSettings: UserSettings = {
    emailNotifications: false,
    meetingInvites: true,
    groupInvites: true,
    reminders: true,
    meetingReminderLeadMinutes: 60,
    meetingPreferences: {
        preset: 'day',
        earliestTime: '10:00',
        latestTime: '20:00'
    }
};

const normalizeTime = (value: string) => value.length >= 5 ? value.slice(0, 5) : value;

const formatDateTime = (value: string) =>
    new Intl.DateTimeFormat('ru-RU', {
        day: '2-digit',
        month: 'short',
        hour: '2-digit',
        minute: '2-digit'
    }).format(new Date(value));

const emptyEntry = (): BaseScheduleEntry => ({
    dayOfWeek: 1,
    title: '',
    description: '',
    startTime: '09:00',
    endTime: '10:00',
    type: 0
});

const normalizeEntry = (entry: BaseScheduleEntry): BaseScheduleEntry => ({
    ...entry,
    startTime: normalizeTime(entry.startTime),
    endTime: normalizeTime(entry.endTime)
});

const getTypeMeta = (type: number) => typeLabels.find(item => item.value === Number(type)) ?? typeLabels[1];

const parseSettings = (settingsJson?: string): UserSettings => {
    if (!settingsJson) {
        return defaultSettings;
    }

    try {
        const parsed = JSON.parse(settingsJson) as Partial<UserSettings>;
        return {
            ...defaultSettings,
            ...parsed,
            meetingPreferences: {
                ...defaultSettings.meetingPreferences,
                ...parsed.meetingPreferences
            }
        };
    } catch {
        return defaultSettings;
    }
};

export const ProfilePage = ({ user, onLogout }: ProfilePageProps) => {
    const { updateUser } = useAuth();
    const [profile, setProfile] = useState<ProfileResponse>({
        id: user.id,
        email: user.email,
        firstName: user.firstName,
        lastName: user.lastName ?? '',
        settingsJson: '{}'
    });
    const [settings, setSettings] = useState<UserSettings>(defaultSettings);
    const [schedule, setSchedule] = useState<BaseScheduleEntry[]>([]);
    const [meetingHistory, setMeetingHistory] = useState<ManagedMeeting[]>([]);
    const [draftEntry, setDraftEntry] = useState<BaseScheduleEntry | null>(null);
    const [editingIndex, setEditingIndex] = useState<number | null>(null);
    const [isHistoryOpen, setIsHistoryOpen] = useState(false);
    const [isVerticalScheduleLayout, setIsVerticalScheduleLayout] = useState(false);
    const [pendingEmail, setPendingEmail] = useState(user.email);
    const [emailCode, setEmailCode] = useState('');
    const [isEmailCodeRequested, setIsEmailCodeRequested] = useState(false);
    const [sendingEmailCode, setSendingEmailCode] = useState(false);
    const [confirmingEmailCode, setConfirmingEmailCode] = useState(false);
    const [currentPassword, setCurrentPassword] = useState('');
    const [newPassword, setNewPassword] = useState('');
    const [confirmPassword, setConfirmPassword] = useState('');
    const [changingPassword, setChangingPassword] = useState(false);
    const [loading, setLoading] = useState(true);
    const [savingProfile, setSavingProfile] = useState(false);
    const [savingEntry, setSavingEntry] = useState(false);
    const [error, setError] = useState('');
    const [success, setSuccess] = useState('');

    useEffect(() => {
        const loadProfile = async () => {
            setLoading(true);
            setError('');
            try {
                const [profileData, scheduleData, historyData] = await Promise.all([
                    profileApi.getProfile(),
                    profileApi.getBaseSchedule(),
                    scheduleApi.getMeetingHistory()
                ]);
                setProfile(profileData);
                setSettings(parseSettings(profileData.settingsJson));
                setSchedule(scheduleData);
                setMeetingHistory(historyData);
                setPendingEmail(profileData.email);
            } catch (err) {
                setError(err instanceof Error ? err.message : 'Не удалось загрузить профиль');
            } finally {
                setLoading(false);
            }
        };

        void loadProfile();
    }, []);

    useEffect(() => {
        const updateLayout = () => {
            setIsVerticalScheduleLayout(window.innerHeight > window.innerWidth && window.innerWidth < 1280);
        };

        updateLayout();
        window.addEventListener('resize', updateLayout);
        return () => window.removeEventListener('resize', updateLayout);
    }, []);

    const showSuccess = (message: string) => {
        setSuccess(message);
        setError('');
    };

    const persistSchedule = async (nextSchedule: BaseScheduleEntry[]) => {
        const updated = await profileApi.updateBaseSchedule(nextSchedule.map(normalizeEntry));
        setSchedule(updated);
    };

    const openCreateEntry = () => {
        setDraftEntry(emptyEntry());
        setEditingIndex(null);
        setError('');
    };

    const openEditEntry = (index: number) => {
        setDraftEntry(normalizeEntry(schedule[index]));
        setEditingIndex(index);
        setError('');
    };

    const closeEntryModal = () => {
        setDraftEntry(null);
        setEditingIndex(null);
    };

    const entryDurationMinutes = draftEntry
        ? Math.max(
            0,
            (Number(normalizeTime(draftEntry.endTime).split(':')[0]) * 60 + Number(normalizeTime(draftEntry.endTime).split(':')[1]))
            - (Number(normalizeTime(draftEntry.startTime).split(':')[0]) * 60 + Number(normalizeTime(draftEntry.startTime).split(':')[1]))
        )
        : 0;

    const applyEntryDuration = (minutes: number) => {
        if (!draftEntry) {
            return;
        }

        const [hours, mins] = normalizeTime(draftEntry.startTime).split(':').map(Number);
        const date = new Date();
        date.setHours(hours, mins, 0, 0);
        date.setMinutes(date.getMinutes() + minutes);

        setDraftEntry(current => current ? {
            ...current,
            endTime: `${date.getHours().toString().padStart(2, '0')}:${date.getMinutes().toString().padStart(2, '0')}`
        } : current);
    };

    const syncEntryEndTimeWithDuration = (nextStartTime: string) => {
        if (!draftEntry) {
            return;
        }

        if (entryDurationMinutes <= 0) {
            setDraftEntry(current => current ? { ...current, startTime: nextStartTime } : current);
            return;
        }

        const [hours, mins] = normalizeTime(nextStartTime).split(':').map(Number);
        const date = new Date();
        date.setHours(hours, mins, 0, 0);
        date.setMinutes(date.getMinutes() + entryDurationMinutes);

        setDraftEntry(current => current ? {
            ...current,
            startTime: nextStartTime,
            endTime: `${date.getHours().toString().padStart(2, '0')}:${date.getMinutes().toString().padStart(2, '0')}`
        } : current);
    };

    const handleProfileSave = async (event: FormEvent) => {
        event.preventDefault();
        setSavingProfile(true);
        setError('');

        if (settings.meetingPreferences.earliestTime >= settings.meetingPreferences.latestTime) {
            setSavingProfile(false);
            setError('Окно встреч задано неверно: время "не раньше" должно быть раньше времени "не позже"');
            return;
        }

        try {
            const updated = await profileApi.updateProfile({
                firstName: profile.firstName,
                lastName: profile.lastName,
                settingsJson: JSON.stringify(settings)
            });

            setProfile(updated);
            setSettings(parseSettings(updated.settingsJson));
            updateUser({
                email: updated.email,
                firstName: updated.firstName,
                lastName: updated.lastName
            });
            showSuccess('Профиль сохранен');
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Не удалось сохранить профиль');
        } finally {
            setSavingProfile(false);
        }
    };

    const handleRequestEmailCode = async () => {
        if (!pendingEmail.trim()) {
            setError('Укажите новый email');
            return;
        }

        if (pendingEmail.trim().toLowerCase() === profile.email.toLowerCase()) {
            setError('Укажите новый email, отличный от текущего');
            return;
        }

        setSendingEmailCode(true);
        setError('');

        try {
            const response = await profileApi.requestEmailChange(pendingEmail.trim());
            setIsEmailCodeRequested(true);
            setEmailCode('');
            showSuccess(response.message);
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Не удалось отправить код подтверждения');
        } finally {
            setSendingEmailCode(false);
        }
    };

    const handleConfirmEmailCode = async () => {
        if (!emailCode.trim()) {
            setError('Введите код подтверждения');
            return;
        }

        setConfirmingEmailCode(true);
        setError('');

        try {
            const updated = await profileApi.confirmEmailChange(pendingEmail.trim(), emailCode.trim());
            setProfile(updated);
            setPendingEmail(updated.email);
            setIsEmailCodeRequested(false);
            setEmailCode('');
            updateUser({ email: updated.email });
            showSuccess('Email подтвержден и обновлен');
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Не удалось подтвердить email');
        } finally {
            setConfirmingEmailCode(false);
        }
    };

    const handleChangePassword = async () => {
        if (!currentPassword || !newPassword) {
            setError('Заполните текущий и новый пароль');
            return;
        }

        if (newPassword !== confirmPassword) {
            setError('Подтверждение пароля не совпадает');
            return;
        }

        setChangingPassword(true);
        setError('');

        try {
            const response = await profileApi.changePassword(currentPassword, newPassword);
            setCurrentPassword('');
            setNewPassword('');
            setConfirmPassword('');
            showSuccess(response.message);
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Не удалось обновить пароль');
        } finally {
            setChangingPassword(false);
        }
    };

    const handleEntrySave = async (event: FormEvent) => {
        event.preventDefault();
        if (!draftEntry) {
            return;
        }

        if (draftEntry.endTime <= draftEntry.startTime) {
            setError('Время окончания базового блока должно быть позже времени начала');
            return;
        }

        setSavingEntry(true);
        try {
            const normalizedDraft = normalizeEntry(draftEntry);
            const nextSchedule = editingIndex === null
                ? [...schedule, normalizedDraft]
                : schedule.map((entry, index) => index === editingIndex ? normalizedDraft : entry);

            await persistSchedule(nextSchedule);
            closeEntryModal();
            showSuccess(editingIndex === null ? 'Базовый блок добавлен' : 'Базовый блок обновлен');
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Не удалось сохранить базовый блок');
        } finally {
            setSavingEntry(false);
        }
    };

    const handleEntryDelete = async (index: number) => {
        try {
            await persistSchedule(schedule.filter((_, currentIndex) => currentIndex !== index));
            showSuccess('Базовый блок удален');
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Не удалось удалить базовый блок');
        }
    };

    const latestHistoryMeeting = meetingHistory[0];

    return (
        <div className="w-full max-w-[1500px] mx-auto space-y-6 p-4 md:p-8">
            <div className="rounded-[32px] border border-gray-100 bg-white p-6 shadow-sm">
                <div className="flex flex-col gap-5 sm:flex-row sm:items-center sm:justify-between">
                    <div>
                        <h1 className="text-3xl font-black text-gray-900">{profile.firstName} {profile.lastName}</h1>
                        <p className="text-sm text-gray-500">{profile.email}</p>
                    </div>
                    <button onClick={onLogout} className="flex items-center justify-center gap-2 rounded-2xl bg-gray-100 px-5 py-3 text-sm font-bold text-gray-700 transition hover:bg-gray-200">
                        <LogOut size={18} /> Выйти
                    </button>
                </div>
            </div>

            {error && <div className="rounded-2xl border border-red-100 bg-red-50 p-4 text-sm text-red-600">{error}</div>}
            {success && <div className="rounded-2xl border border-emerald-100 bg-emerald-50 p-4 text-sm text-emerald-700">{success}</div>}

            <form onSubmit={handleProfileSave} className="rounded-[28px] border border-gray-100 bg-white p-6 shadow-sm">
                <div className="mb-6 flex flex-col gap-2 lg:flex-row lg:items-end lg:justify-between">
                    <div>
                        <h2 className="text-xl font-black text-gray-900">Настройки профиля</h2>
                    </div>
                    <button disabled={savingProfile || loading} className="flex items-center justify-center gap-2 rounded-xl bg-indigo-600 px-4 py-3 text-sm font-bold text-white disabled:opacity-50">
                        <Save size={18} /> Сохранить изменения
                    </button>
                </div>

                <div className="grid gap-6 xl:grid-cols-[1.1fr_0.9fr]">
                    <div className="space-y-6">
                        <section className="rounded-3xl border border-gray-100 bg-gray-50/70 p-5">
                            <h3 className="text-lg font-black text-gray-900">Основные данные</h3>
                            <div className="mt-4 grid gap-3 lg:grid-cols-2">
                                <input value={profile.firstName} onChange={event => setProfile(current => ({ ...current, firstName: event.target.value }))} required placeholder="Имя" className="w-full rounded-xl border border-gray-200 bg-white px-4 py-3 text-sm text-gray-900 outline-none focus:ring-2 focus:ring-indigo-500" />
                                <input value={profile.lastName} onChange={event => setProfile(current => ({ ...current, lastName: event.target.value }))} placeholder="Фамилия" className="w-full rounded-xl border border-gray-200 bg-white px-4 py-3 text-sm text-gray-900 outline-none focus:ring-2 focus:ring-indigo-500" />
                                <div className="rounded-2xl border border-gray-100 bg-white p-4 lg:col-span-2">
                                    <div className="text-xs font-black uppercase tracking-wide text-gray-400">Текущий email</div>
                                    <div className="mt-1 font-bold text-gray-900">{profile.email}</div>
                                    <div className="mt-4 grid gap-3 sm:grid-cols-[1fr_auto]">
                                        <input
                                            type="email"
                                            value={pendingEmail}
                                            onChange={event => setPendingEmail(event.target.value)}
                                            placeholder="Новый email"
                                            className="w-full rounded-xl border border-gray-200 bg-white px-4 py-3 text-sm text-gray-900 outline-none focus:ring-2 focus:ring-indigo-500"
                                        />
                                        <button
                                            type="button"
                                            onClick={() => void handleRequestEmailCode()}
                                            disabled={sendingEmailCode}
                                            className="rounded-xl bg-indigo-600 px-4 py-3 text-sm font-bold text-white disabled:opacity-50"
                                        >
                                            {sendingEmailCode ? 'Отправляю...' : 'Код'}
                                        </button>
                                    </div>
                                    {isEmailCodeRequested && (
                                        <div className="mt-3 flex flex-col gap-3 sm:flex-row">
                                            <input
                                                value={emailCode}
                                                onChange={event => setEmailCode(event.target.value.replace(/\D/g, '').slice(0, 6))}
                                                placeholder="Код из письма"
                                                className="w-full rounded-xl border border-gray-200 bg-white px-4 py-3 text-sm text-gray-900 outline-none focus:ring-2 focus:ring-indigo-500"
                                            />
                                            <button
                                                type="button"
                                                onClick={() => void handleConfirmEmailCode()}
                                                disabled={confirmingEmailCode}
                                                className="rounded-xl bg-indigo-50 px-4 py-3 text-sm font-bold text-indigo-700 disabled:opacity-50"
                                            >
                                                {confirmingEmailCode ? 'Проверяю...' : 'Подтвердить'}
                                            </button>
                                        </div>
                                    )}
                                </div>
                            </div>
                        </section>

                        <section className="rounded-3xl border border-gray-100 bg-gray-50/70 p-5">
                            <div className="flex items-center gap-2 text-gray-900">
                                <KeyRound size={18} className="text-indigo-500" />
                                <h3 className="text-lg font-black">Безопасность</h3>
                            </div>
                            <p className="mt-1 text-sm text-gray-500">Смените пароль прямо здесь, если знаете текущий.</p>
                            <div className="mt-4 rounded-2xl border border-gray-100 bg-white p-4">
                                <div className="grid gap-3">
                                    <input
                                        type="password"
                                        value={currentPassword}
                                        onChange={event => setCurrentPassword(event.target.value)}
                                        placeholder="Текущий пароль"
                                        className="w-full rounded-xl border border-gray-200 bg-white px-4 py-3 text-sm text-gray-900 outline-none focus:ring-2 focus:ring-indigo-500"
                                    />
                                    <input
                                        type="password"
                                        value={newPassword}
                                        onChange={event => setNewPassword(event.target.value)}
                                        minLength={6}
                                        placeholder="Новый пароль"
                                        className="w-full rounded-xl border border-gray-200 bg-white px-4 py-3 text-sm text-gray-900 outline-none focus:ring-2 focus:ring-indigo-500"
                                    />
                                    <input
                                        type="password"
                                        value={confirmPassword}
                                        onChange={event => setConfirmPassword(event.target.value)}
                                        minLength={6}
                                        placeholder="Повторите новый пароль"
                                        className="w-full rounded-xl border border-gray-200 bg-white px-4 py-3 text-sm text-gray-900 outline-none focus:ring-2 focus:ring-indigo-500"
                                    />
                                </div>
                                <button
                                    type="button"
                                    onClick={() => void handleChangePassword()}
                                    disabled={changingPassword}
                                    className="mt-4 rounded-xl bg-indigo-600 px-4 py-3 text-sm font-bold text-white disabled:opacity-50"
                                >
                                    {changingPassword ? 'Обновляю...' : 'Обновить пароль'}
                                </button>
                            </div>
                        </section>
                    </div>

                    <div className="space-y-6">
                        <section className="rounded-3xl border border-gray-100 bg-gray-50/70 p-5">
                            <div className="flex items-center gap-2 text-gray-900">
                                <BellRing size={18} className="text-indigo-500" />
                                <h3 className="text-lg font-black">Уведомления</h3>
                            </div>

                            <div className="mt-4 space-y-3">
                                {notificationOptions.map(option => (
                                    <label key={option.key} className="flex cursor-pointer items-start justify-between gap-4 rounded-2xl border border-gray-100 bg-white px-4 py-4">
                                        <div className="min-w-0">
                                            <div className="font-black text-gray-900">{option.label}</div>
                                            <div className="mt-1 text-sm text-gray-500">{option.description}</div>
                                        </div>
                                        <div className="relative mt-1">
                                            <input
                                                type="checkbox"
                                                checked={settings[option.key]}
                                                onChange={() => setSettings(current => ({
                                                    ...current,
                                                    [option.key]: !current[option.key]
                                                }))}
                                                className="peer sr-only"
                                            />
                                            <div className="h-7 w-12 rounded-full bg-gray-200 transition peer-checked:bg-indigo-600" />
                                            <div className="absolute left-1 top-1 h-5 w-5 rounded-full bg-white transition peer-checked:translate-x-5" />
                                        </div>
                                    </label>
                                ))}
                            </div>
                        </section>

                        <section className="rounded-3xl border border-gray-100 bg-gray-50/70 p-5">
                            <div className="flex items-center gap-2 text-gray-900">
                                <Clock3 size={18} className="text-indigo-500" />
                                <h3 className="text-lg font-black">Время для встреч</h3>
                            </div>
                            <p className="mt-1 text-sm text-gray-500">Планировщик будет избегать слишком ранних и неудобных слотов.</p>

                            <div className="mt-4 grid gap-3 sm:grid-cols-2">
                                {meetingPresets.map(preset => (
                                    <button
                                        key={preset.value}
                                        type="button"
                                        onClick={() => setSettings(current => ({
                                            ...current,
                                            meetingPreferences: {
                                                ...current.meetingPreferences,
                                                preset: preset.value
                                            }
                                        }))}
                                        className={`rounded-2xl border px-4 py-4 text-left transition ${
                                            settings.meetingPreferences.preset === preset.value
                                                ? 'border-indigo-200 bg-indigo-50 text-indigo-700'
                                                : 'border-gray-100 bg-white text-gray-700'
                                        }`}
                                    >
                                        <div className="font-black">{preset.label}</div>
                                        <div className="mt-1 text-sm text-gray-500">{preset.description}</div>
                                    </button>
                                ))}
                            </div>

                            <div className="mt-4 grid gap-3 sm:grid-cols-2">
                                <label className="block">
                                    <div className="mb-1 text-xs font-black uppercase tracking-wide text-gray-400">Не раньше</div>
                                    <input
                                        type="time"
                                        value={settings.meetingPreferences.earliestTime}
                                        onChange={event => setSettings(current => ({
                                            ...current,
                                            meetingPreferences: {
                                                ...current.meetingPreferences,
                                                earliestTime: event.target.value
                                            }
                                        }))}
                                        className="w-full rounded-xl border border-gray-200 bg-white px-4 py-3 text-sm text-gray-900 outline-none focus:ring-2 focus:ring-indigo-500"
                                    />
                                </label>
                                <label className="block">
                                    <div className="mb-1 text-xs font-black uppercase tracking-wide text-gray-400">Не позже</div>
                                    <input
                                        type="time"
                                        value={settings.meetingPreferences.latestTime}
                                        onChange={event => setSettings(current => ({
                                            ...current,
                                            meetingPreferences: {
                                                ...current.meetingPreferences,
                                                latestTime: event.target.value
                                            }
                                        }))}
                                        className="w-full rounded-xl border border-gray-200 bg-white px-4 py-3 text-sm text-gray-900 outline-none focus:ring-2 focus:ring-indigo-500"
                                    />
                                </label>
                            </div>

                            <label className="mt-4 block">
                                <div className="mb-1 text-xs font-black uppercase tracking-wide text-gray-400">Предупреждать о встрече за сколько минут</div>
                                <input
                                    type="number"
                                    min={5}
                                    max={1440}
                                    value={settings.meetingReminderLeadMinutes}
                                    onChange={event => setSettings(current => ({
                                        ...current,
                                        meetingReminderLeadMinutes: Math.min(1440, Math.max(5, Number(event.target.value) || 60))
                                    }))}
                                        className="w-full rounded-xl border border-gray-200 bg-white px-4 py-3 text-sm text-gray-900 outline-none focus:ring-2 focus:ring-indigo-500"
                                />
                            </label>
                        </section>
                    </div>
                </div>
            </form>

            <div className="rounded-[28px] border border-gray-100 bg-white p-6 shadow-sm">
                <div className="mb-5 flex items-center justify-between gap-3">
                    <div>
                        <h2 className="text-xl font-black text-gray-900">История встреч</h2>
                    </div>
                    <CalendarClock size={22} className="text-indigo-500" />
                </div>

                {loading ? (
                    <p className="text-sm text-gray-400">Загрузка...</p>
                ) : meetingHistory.length === 0 ? (
                    <p className="rounded-2xl border-2 border-dashed border-gray-200 bg-gray-50 p-6 text-center text-sm text-gray-400">
                        Прошедших встреч пока нет
                    </p>
                ) : (
                    <div className="grid gap-3 md:grid-cols-[1fr_auto] md:items-center">
                        <div className="rounded-2xl border border-gray-100 bg-gray-50/70 p-4">
                            <div className="text-xs font-black uppercase tracking-wide text-gray-400">Всего прошедших встреч</div>
                            <div className="mt-2 text-3xl font-black text-gray-900">{meetingHistory.length}</div>
                            {latestHistoryMeeting && (
                                <div className="mt-3 text-sm text-gray-500">
                                    Последняя: <span className="font-bold text-gray-800">{latestHistoryMeeting.title}</span>, {formatDateTime(latestHistoryMeeting.startTime)}
                                </div>
                            )}
                        </div>
                        <button
                            type="button"
                            onClick={() => setIsHistoryOpen(true)}
                            className="rounded-xl bg-indigo-600 px-5 py-3 text-sm font-bold text-white shadow-lg shadow-indigo-100"
                        >
                            Посмотреть историю
                        </button>
                    </div>
                )}
            </div>

            <div className="rounded-[28px] border border-gray-100 bg-white p-6 shadow-sm">
                <div className="mb-5 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                    <div>
                        <h2 className="text-xl font-black text-gray-900">Базовое расписание</h2>
                        <p className="text-sm text-gray-500">Повторяющиеся блоки автоматически появляются в разделе «План»</p>
                    </div>
                    <button onClick={openCreateEntry} className="rounded-xl bg-indigo-600 px-4 py-2 text-sm font-bold text-white shadow-lg shadow-indigo-100">
                        <Plus size={18} className="inline" /> Блок
                    </button>
                </div>

                {loading ? (
                    <p className="text-sm text-gray-400">Загрузка...</p>
                ) : schedule.length === 0 ? (
                    <div className="rounded-2xl border-2 border-dashed border-gray-200 bg-gray-50 p-6 text-center">
                        <CalendarPlus className="mx-auto mb-3 text-gray-300" size={28} />
                        <p className="text-sm font-bold text-gray-500">Базовое расписание пустое</p>
                        <p className="text-xs text-gray-400">Добавьте регулярные пары, работу или дорогу, чтобы они появились в плане.</p>
                    </div>
                ) : isVerticalScheduleLayout ? (
                    <div className="space-y-4">
                        {weekDays.map(day => {
                            const dayEntries = schedule
                                .map((entry, index) => ({ entry, index }))
                                .filter(item => item.entry.dayOfWeek === day.value)
                                .sort((a, b) => normalizeTime(a.entry.startTime).localeCompare(normalizeTime(b.entry.startTime)));

                            return (
                                <div key={day.value} className="rounded-2xl border border-gray-100 bg-gray-50/70 p-4">
                                    <div className="mb-3">
                                        <div className="text-xs font-black uppercase text-gray-400">{day.short}</div>
                                        <div className="text-lg font-black text-gray-900">{day.label}</div>
                                    </div>
                                    <div className="space-y-3">
                                        {dayEntries.length === 0 ? (
                                            <p className="rounded-xl border border-dashed border-gray-200 bg-white/70 px-3 py-4 text-center text-sm text-gray-400">Свободно</p>
                                        ) : dayEntries.map(({ entry, index }) => {
                                            const meta = getTypeMeta(entry.type);
                                            return (
                                                <div key={entry.id ?? `${day.value}-${index}`} className={`rounded-xl border px-4 py-4 ${meta.className}`}>
                                                    <div className="mb-2 flex items-start justify-between gap-2">
                                                        <div className="min-w-0">
                                                            <div className="font-black text-gray-900">{entry.title}</div>
                                                            <div className="mt-1 text-sm font-bold">{normalizeTime(entry.startTime)} - {normalizeTime(entry.endTime)}</div>
                                                        </div>
                                                        <div className="flex shrink-0 gap-1">
                                                            <button onClick={() => openEditEntry(index)} className="rounded-lg bg-white/70 p-2 text-gray-500 hover:text-indigo-600">
                                                                <Pencil size={14} />
                                                            </button>
                                                            <button onClick={() => void handleEntryDelete(index)} className="rounded-lg bg-white/70 p-2 text-gray-500 hover:text-red-600">
                                                                <Trash2 size={14} />
                                                            </button>
                                                        </div>
                                                    </div>
                                                    <span className="rounded-full bg-white/70 px-2 py-0.5 text-[10px] font-black uppercase tracking-wide">{meta.label}</span>
                                                    {entry.description && <p className="mt-2 text-sm text-gray-500">{entry.description}</p>}
                                                </div>
                                            );
                                        })}
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                ) : (
                    <div className="overflow-x-auto pb-2">
                        <div className="grid min-w-[1120px] grid-cols-7 gap-4">
                            {weekDays.map(day => {
                                const dayEntries = schedule
                                    .map((entry, index) => ({ entry, index }))
                                    .filter(item => item.entry.dayOfWeek === day.value)
                                    .sort((a, b) => normalizeTime(a.entry.startTime).localeCompare(normalizeTime(b.entry.startTime)));

                                return (
                                    <div key={day.value} className="min-h-[330px] rounded-2xl border border-gray-100 bg-gray-50/70 p-3">
                                        <div className="mb-3 flex items-center justify-between">
                                            <div className="min-w-0">
                                                <div className="text-xs font-black uppercase text-gray-400">{day.short}</div>
                                                <div className="break-words font-black leading-tight text-gray-900">{day.label}</div>
                                            </div>
                                        </div>

                                        <div className="space-y-2">
                                            {dayEntries.length === 0 ? (
                                                <p className="rounded-xl border border-dashed border-gray-200 bg-white/70 px-3 py-4 text-center text-xs text-gray-400">Свободно</p>
                                            ) : dayEntries.map(({ entry, index }) => {
                                                const meta = getTypeMeta(entry.type);
                                                return (
                                                    <div key={entry.id ?? `${day.value}-${index}`} className={`rounded-xl border px-3 py-3 text-xs ${meta.className}`}>
                                                        <div className="mb-2 flex items-start justify-between gap-2">
                                                            <div className="min-w-0 flex-1">
                                                                <div className="break-words font-black leading-tight text-gray-900">{entry.title}</div>
                                                                <div className="mt-1 font-bold">{normalizeTime(entry.startTime)} - {normalizeTime(entry.endTime)}</div>
                                                            </div>
                                                            <div className="flex shrink-0 gap-1">
                                                                <button onClick={() => openEditEntry(index)} className="rounded-lg bg-white/70 p-1.5 text-gray-500 hover:text-indigo-600">
                                                                    <Pencil size={13} />
                                                                </button>
                                                                <button onClick={() => void handleEntryDelete(index)} className="rounded-lg bg-white/70 p-1.5 text-gray-500 hover:text-red-600">
                                                                    <Trash2 size={13} />
                                                                </button>
                                                            </div>
                                                        </div>
                                                        <span className="rounded-full bg-white/70 px-2 py-0.5 text-[10px] font-black uppercase tracking-wide">{meta.label}</span>
                                                        {entry.description && <p className="mt-2 break-words text-gray-500">{entry.description}</p>}
                                                    </div>
                                                );
                                            })}
                                        </div>
                                    </div>
                                );
                            })}
                        </div>
                    </div>
                )}
            </div>

            {isHistoryOpen && (
                <div className="fixed inset-0 z-50 overflow-y-auto bg-slate-950/50 p-3 sm:p-4">
                    <div className="flex min-h-full items-start justify-center py-2 sm:items-center sm:py-6">
                        <div className="max-h-[calc(100vh-1.5rem)] w-full max-w-3xl overflow-y-auto rounded-[28px] bg-white p-5 shadow-2xl sm:p-6">
                            <div className="mb-5 flex items-start justify-between gap-4">
                                <div>
                                    <h2 className="text-2xl font-black text-gray-900">История встреч</h2>
                                    <p className="mt-1 text-sm text-gray-500">Прошедшие подтверждённые встречи в обратном хронологическом порядке.</p>
                                </div>
                                <button
                                    type="button"
                                    onClick={() => setIsHistoryOpen(false)}
                                    className="rounded-xl bg-gray-100 p-2 text-gray-500 hover:text-gray-900"
                                    aria-label="Закрыть историю встреч"
                                >
                                    <X size={18} />
                                </button>
                            </div>

                            <div className="space-y-3">
                                {meetingHistory.map(meeting => (
                                    <article key={meeting.meetingId} className="rounded-2xl border border-gray-100 bg-gray-50/70 p-4">
                                        <div className="flex items-start justify-between gap-3">
                                            <div className="min-w-0">
                                                <h3 className="break-words font-black text-gray-900">{meeting.title}</h3>
                                                {meeting.description && <p className="mt-1 text-sm text-gray-500">{meeting.description}</p>}
                                            </div>
                                            {meeting.relatedGroupName && (
                                                <span className="shrink-0 rounded-full bg-white px-2.5 py-1 text-[10px] font-black uppercase tracking-wide text-gray-500">
                                                    {meeting.relatedGroupName}
                                                </span>
                                            )}
                                        </div>
                                        <div className="mt-3 text-sm font-bold text-gray-800">
                                            {formatDateTime(meeting.startTime)} - {formatDateTime(meeting.endTime)}
                                        </div>
                                        <div className="mt-2 text-xs text-gray-500">
                                            Участники: {meeting.participantNames.join(', ') || '—'}
                                        </div>
                                    </article>
                                ))}
                            </div>
                        </div>
                    </div>
                </div>
            )}

            {draftEntry && (
                <div className="fixed inset-0 z-50 overflow-y-auto bg-slate-950/50 p-3 sm:p-4">
                    <div className="flex min-h-full items-start justify-center py-2 sm:items-center sm:py-6">
                        <form onSubmit={handleEntrySave} className="max-h-[calc(100vh-1.5rem)] w-full max-w-md overflow-y-auto rounded-[28px] bg-white p-5 shadow-2xl sm:max-w-lg sm:p-6">
                        <div className="mb-5">
                            <h2 className="mb-1 text-2xl font-black text-gray-900">
                                {editingIndex === null ? 'Новый базовый блок' : 'Редактировать базовый блок'}
                            </h2>
                            <p className="text-sm text-gray-500">Это повторяющийся еженедельный шаблон, который потом появляется в плане.</p>
                        </div>
                        <div className="space-y-4">
                            <div>
                                <div className="mb-1 text-xs font-black uppercase tracking-wide text-gray-400">Название</div>
                                <input value={draftEntry.title} onChange={event => setDraftEntry(current => current ? { ...current, title: event.target.value } : current)} required placeholder="Например, Учебная пара" className="w-full rounded-xl border border-gray-200 px-4 py-3 text-sm text-gray-900 outline-none focus:ring-2 focus:ring-indigo-500" />
                            </div>
                            <div>
                                <div className="mb-1 text-xs font-black uppercase tracking-wide text-gray-400">Описание</div>
                                <textarea value={draftEntry.description ?? ''} onChange={event => setDraftEntry(current => current ? { ...current, description: event.target.value } : current)} placeholder="Что это за повторяющийся блок" className="w-full rounded-xl border border-gray-200 px-4 py-3 text-sm text-gray-900 outline-none focus:ring-2 focus:ring-indigo-500" />
                            </div>
                            <div className="grid gap-3 sm:grid-cols-3">
                                <label className="block">
                                    <div className="mb-1 text-xs font-black uppercase tracking-wide text-gray-400">День</div>
                                    <select value={draftEntry.dayOfWeek} onChange={event => setDraftEntry(current => current ? { ...current, dayOfWeek: Number(event.target.value) } : current)} className="w-full rounded-xl border border-gray-200 px-4 py-3 text-sm text-gray-900">
                                        {weekDays.map(day => <option key={day.value} value={day.value}>{day.label}</option>)}
                                    </select>
                                </label>
                                <label className="block">
                                    <div className="mb-1 text-xs font-black uppercase tracking-wide text-gray-400">Начало</div>
                                    <input
                                        type="time"
                                        value={normalizeTime(draftEntry.startTime)}
                                        onChange={event => syncEntryEndTimeWithDuration(event.target.value)}
                                        className="w-full rounded-xl border border-gray-200 px-4 py-3 text-sm text-gray-900"
                                    />
                                </label>
                                <label className="block">
                                    <div className="mb-1 text-xs font-black uppercase tracking-wide text-gray-400">Конец</div>
                                    <input type="time" value={normalizeTime(draftEntry.endTime)} onChange={event => setDraftEntry(current => current ? { ...current, endTime: event.target.value } : current)} className="w-full rounded-xl border border-gray-200 px-4 py-3 text-sm text-gray-900" />
                                </label>
                            </div>

                            <div className="rounded-2xl border border-gray-100 bg-gray-50 p-4">
                                <div className="mb-2 text-xs font-black uppercase tracking-wide text-gray-400">Быстрая длительность</div>
                                <div className="flex flex-wrap gap-2">
                                    {[30, 60, 90, 120].map(minutes => (
                                        <button
                                            key={minutes}
                                            type="button"
                                            onClick={() => applyEntryDuration(minutes)}
                                            className={`rounded-xl border px-3 py-2 text-sm font-bold transition ${
                                                entryDurationMinutes === minutes
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
                                <div className="mb-2 text-xs font-black uppercase tracking-wide text-gray-400">Тип блока</div>
                                <div className="grid gap-2 sm:grid-cols-3">
                                    {typeLabels.map(option => (
                                        <button
                                            key={option.value}
                                            type="button"
                                            onClick={() => setDraftEntry(current => current ? { ...current, type: option.value } : current)}
                                            className={`flex min-h-[126px] flex-col justify-between rounded-2xl border px-4 py-4 text-left transition ${
                                                draftEntry.type === option.value
                                                    ? option.value === 0
                                                        ? 'border-red-300 bg-red-50 text-red-700 shadow-md ring-2 ring-red-100 ring-offset-2 ring-offset-white'
                                                        : option.value === 1
                                                            ? 'border-blue-300 bg-blue-50 text-blue-700 shadow-md ring-2 ring-blue-100 ring-offset-2 ring-offset-white'
                                                            : 'border-amber-300 bg-amber-50 text-amber-700 shadow-md ring-2 ring-amber-100 ring-offset-2 ring-offset-white'
                                                    : 'border-gray-100 bg-white text-gray-700 hover:border-gray-200'
                                            }`}
                                        >
                                            <div className="min-w-0">
                                                <div className="font-black">{option.label}</div>
                                                <div className="mt-2 text-xs leading-5 text-current/80">
                                                    {option.value === 0 ? 'Нельзя пересекать' : option.value === 1 ? 'Можно подвинуть' : 'Не критично пропустить'}
                                                </div>
                                            </div>
                                            <div className={`mt-4 h-1.5 rounded-full transition ${
                                                draftEntry.type === option.value
                                                    ? option.value === 0
                                                        ? 'bg-red-500'
                                                        : option.value === 1
                                                            ? 'bg-blue-500'
                                                            : 'bg-amber-500'
                                                    : 'bg-transparent'
                                            }`} />
                                        </button>
                                    ))}
                                </div>
                            </div>
                        </div>
                        <div className="sticky bottom-0 mt-5 flex justify-end gap-2 border-t border-gray-100 bg-white/95 pt-4 backdrop-blur">
                            <button type="button" onClick={closeEntryModal} className="rounded-xl bg-gray-100 px-4 py-2 text-sm font-bold text-gray-600">Отмена</button>
                            <button disabled={savingEntry} type="submit" className="rounded-xl bg-indigo-600 px-4 py-2 text-sm font-bold text-white disabled:opacity-50">
                                {editingIndex === null ? 'Создать' : 'Сохранить'}
                            </button>
                        </div>
                        </form>
                    </div>
                </div>
            )}
        </div>
    );
};
