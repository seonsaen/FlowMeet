import { type FormEvent, useEffect, useMemo, useState } from 'react';
import {
    CalendarClock,
    Check,
    MailPlus,
    MessageCircleMore,
    Pencil,
    Plus,
    Search,
    Send,
    Trash2,
    UserPlus,
    Users,
    X
} from '../../components/icons/hugeIcons';
import type { Friend, Group, GroupInvite, IncomingFriendRequest, ManagedMeeting, MeetingInvite, OutgoingMeetingInvite, TimeSlot } from '../../types';
import { groupsApi } from '../../api/groups';
import { planningApi } from '../../api/planning';
import { scheduleApi } from '../../api/schedule';
import { socialApi } from '../../api/social';
import { useAuth } from '../../hooks/useAuth';

type NetworkTab = 'friends' | 'groups' | 'invites' | 'meetings';

type MeetingTarget = {
    mode: 'create' | 'edit';
    kind: 'friend' | 'group';
    label: string;
    participantIds: string[];
    participantNames?: string[];
    titleSuggestion: string;
    descriptionSuggestion?: string;
    slotCacheKey?: string;
    groupId?: string;
    meetingId?: string;
};

type ConfirmActionState = {
    title: string;
    description: string;
    confirmLabel: string;
    tone: 'danger' | 'primary';
    onConfirm: () => Promise<void>;
};

const toLocalDateInputValue = (date: Date) => {
    const year = date.getFullYear();
    const month = `${date.getMonth() + 1}`.padStart(2, '0');
    const day = `${date.getDate()}`.padStart(2, '0');
    return `${year}-${month}-${day}`;
};

const todayInput = () => toLocalDateInputValue(new Date());

const formatDateTime = (value: string) =>
    new Intl.DateTimeFormat('ru-RU', {
        day: '2-digit',
        month: 'short',
        hour: '2-digit',
        minute: '2-digit'
    }).format(new Date(value));

const formatSlot = (slot: TimeSlot) =>
    `${formatDateTime(slot.startTime)} - ${new Date(slot.endTime).toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' })}`;

const formatTimeRange = (startTime: string, endTime: string) =>
    `${formatDateTime(startTime)} - ${new Date(endTime).toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' })}`;

const toTimeInput = (value: string) =>
    new Date(value).toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit', hour12: false });

const toDateInput = (value: string) => toLocalDateInputValue(new Date(value));

const getDurationMinutesBetween = (start: string, end: string) => {
    const [startHours, startMinutes] = start.split(':').map(Number);
    const [endHours, endMinutes] = end.split(':').map(Number);
    const diff = endHours * 60 + endMinutes - (startHours * 60 + startMinutes);
    return diff > 0 ? diff : 0;
};

const buildEndTime = (date: string, startTime: string, durationMinutes: number) => {
    const startDate = new Date(`${date}T${startTime}`);
    startDate.setMinutes(startDate.getMinutes() + durationMinutes);
    return `${startDate.getHours().toString().padStart(2, '0')}:${startDate.getMinutes().toString().padStart(2, '0')}`;
};

const suitabilityMeta: Record<string, { label: string; className: string }> = {
    Optimal: { label: 'Оптимально', className: 'bg-emerald-50 text-emerald-700 border-emerald-100' },
    RequiresMoving: { label: 'Можно, если подвинуть', className: 'bg-amber-50 text-amber-700 border-amber-100' },
    LowEnergy: { label: 'Низкий ресурс', className: 'bg-rose-50 text-rose-700 border-rose-100' },
    Compromise: { label: 'Компромисс', className: 'bg-slate-50 text-slate-700 border-slate-200' }
};

const roleLabel = (role: number) => {
    if (role === 0) return 'владелец';
    if (role === 1) return 'админ';
    return 'участник';
};

const canInviteMembers = (member?: { isOwner: boolean; role: number }) =>
    Boolean(member?.isOwner || member?.role === 0 || member?.role === 1);

const findUserMembership = (group: Group, userId?: string) =>
    userId ? group.members.find(member => member.userId === userId) : undefined;

const meetingStatusLabel = (status: number) => {
    if (status === 1) return 'Подтверждена';
    if (status === 2) return 'Отменена';
    return 'Ожидает ответов';
};

export const NetworkPage = () => {
    const { user } = useAuth();
    const [activeTab, setActiveTab] = useState<NetworkTab>('friends');
    const [friends, setFriends] = useState<Friend[]>([]);
    const [groups, setGroups] = useState<Group[]>([]);
    const [invites, setInvites] = useState<GroupInvite[]>([]);
    const [friendRequests, setFriendRequests] = useState<IncomingFriendRequest[]>([]);
    const [meetingInvites, setMeetingInvites] = useState<MeetingInvite[]>([]);
    const [outgoingMeetingInvites, setOutgoingMeetingInvites] = useState<OutgoingMeetingInvite[]>([]);
    const [myMeetings, setMyMeetings] = useState<ManagedMeeting[]>([]);
    const [nearestSlots, setNearestSlots] = useState<Record<string, TimeSlot | null | undefined>>({});
    const [meetingSlots, setMeetingSlots] = useState<TimeSlot[]>([]);
    const [meetingTarget, setMeetingTarget] = useState<MeetingTarget | null>(null);
    const [isFriendModalOpen, setIsFriendModalOpen] = useState(false);
    const [isGroupModalOpen, setIsGroupModalOpen] = useState(false);
    const [isGroupInviteModalOpen, setIsGroupInviteModalOpen] = useState(false);
    const [editingGroup, setEditingGroup] = useState<Group | null>(null);
    const [groupInviteTargetId, setGroupInviteTargetId] = useState('');
    const [friendEmail, setFriendEmail] = useState('');
    const [groupName, setGroupName] = useState('');
    const [groupDescription, setGroupDescription] = useState('');
    const [selectedNewGroupFriendIds, setSelectedNewGroupFriendIds] = useState<string[]>([]);
    const [selectedGroupInviteFriendIds, setSelectedGroupInviteFriendIds] = useState<string[]>([]);
    const [meetingTitle, setMeetingTitle] = useState('');
    const [meetingDescription, setMeetingDescription] = useState('');
    const [meetingDate, setMeetingDate] = useState(todayInput());
    const [meetingStartTime, setMeetingStartTime] = useState('09:00');
    const [meetingEndTime, setMeetingEndTime] = useState('10:00');
    const [meetingDurationInput, setMeetingDurationInput] = useState('60');
    const [search, setSearch] = useState('');
    const [loading, setLoading] = useState(true);
    const [slotLoadingId, setSlotLoadingId] = useState('');
    const [meetingSlotsLoading, setMeetingSlotsLoading] = useState(false);
    const [confirmAction, setConfirmAction] = useState<ConfirmActionState | null>(null);
    const [confirmingAction, setConfirmingAction] = useState(false);
    const [error, setError] = useState('');
    const [success, setSuccess] = useState('');

    const loadNetwork = async () => {
        setLoading(true);
        setError('');
        try {
            const [friendData, groupData] = await Promise.all([
                socialApi.getFriends(),
                groupsApi.getUserGroups()
            ]);

            const secondaryResults = await Promise.allSettled([
                groupsApi.getIncomingInvites(),
                socialApi.getIncomingRequests(),
                scheduleApi.getMeetingInvites(),
                scheduleApi.getOutgoingMeetingInvites(),
                scheduleApi.getMyMeetings()
            ]);

            const [groupInvitesResult, friendRequestsResult, meetingInvitesResult, outgoingMeetingInvitesResult, myMeetingsResult] = secondaryResults;
            const partialErrors: string[] = [];

            setFriends(friendData);
            setNearestSlots(
                Object.fromEntries(
                    friendData.map(friend => [friend.id, friend.nearestAvailableSlot ?? null])
                )
            );
            setGroups(groupData);
            setInvites(groupInvitesResult.status === 'fulfilled' ? groupInvitesResult.value : []);
            setFriendRequests(friendRequestsResult.status === 'fulfilled' ? friendRequestsResult.value : []);
            setMeetingInvites(meetingInvitesResult.status === 'fulfilled' ? meetingInvitesResult.value : []);
            setOutgoingMeetingInvites(outgoingMeetingInvitesResult.status === 'fulfilled' ? outgoingMeetingInvitesResult.value : []);
            setMyMeetings(myMeetingsResult.status === 'fulfilled' ? myMeetingsResult.value : []);
            setGroupInviteTargetId(current => current || (groupData[0]?.id ?? ''));

            if (groupInvitesResult.status === 'rejected')
                partialErrors.push('Не удалось загрузить приглашения в группы');
            if (friendRequestsResult.status === 'rejected')
                partialErrors.push('Не удалось загрузить заявки в друзья');
            if (meetingInvitesResult.status === 'rejected')
                partialErrors.push('Не удалось загрузить входящие встречи');
            if (outgoingMeetingInvitesResult.status === 'rejected')
                partialErrors.push('Не удалось загрузить ожидающие встречи');
            if (myMeetingsResult.status === 'rejected')
                partialErrors.push('Не удалось загрузить созданные встречи');

            if (partialErrors.length > 0)
                setError(partialErrors.join('. '));
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Не удалось загрузить контакты');
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        void loadNetwork();
    }, []);

    const filteredFriends = useMemo(() => {
        const query = search.trim().toLowerCase();
        if (!query) {
            return friends;
        }

        return friends.filter(friend => friend.fullName.toLowerCase().includes(query));
    }, [friends, search]);

    const filteredGroups = useMemo(() => {
        const query = search.trim().toLowerCase();
        if (!query) {
            return groups;
        }

        return groups.filter(group =>
            group.name.toLowerCase().includes(query)
            || (group.description ?? '').toLowerCase().includes(query)
        );
    }, [groups, search]);

    const filteredInvites = useMemo(() => {
        const query = search.trim().toLowerCase();
        if (!query) {
            return invites;
        }

        return invites.filter(invite =>
            invite.groupName.toLowerCase().includes(query)
            || invite.inviterName.toLowerCase().includes(query)
        );
    }, [invites, search]);

    const filteredMeetingInvites = useMemo(() => {
        const query = search.trim().toLowerCase();
        if (!query) {
            return meetingInvites;
        }

        return meetingInvites.filter(invite =>
            invite.title.toLowerCase().includes(query)
            || invite.organizerName.toLowerCase().includes(query)
            || (invite.description ?? '').toLowerCase().includes(query)
        );
    }, [meetingInvites, search]);

    const filteredOutgoingMeetingInvites = useMemo(() => {
        const query = search.trim().toLowerCase();
        if (!query) {
            return outgoingMeetingInvites;
        }

        return outgoingMeetingInvites.filter(invite =>
            invite.title.toLowerCase().includes(query)
            || (invite.description ?? '').toLowerCase().includes(query)
            || invite.pendingParticipantNames.some(name => name.toLowerCase().includes(query))
        );
    }, [outgoingMeetingInvites, search]);

    const filteredMyMeetings = useMemo(() => {
        const query = search.trim().toLowerCase();
        if (!query) {
            return myMeetings;
        }

        return myMeetings.filter(meeting =>
            meeting.title.toLowerCase().includes(query)
            || (meeting.description ?? '').toLowerCase().includes(query)
            || meeting.organizerName.toLowerCase().includes(query)
            || meeting.participantNames.some(name => name.toLowerCase().includes(query))
            || (meeting.relatedGroupName ?? '').toLowerCase().includes(query)
        );
    }, [myMeetings, search]);

    const filteredFriendRequests = useMemo(() => {
        const query = search.trim().toLowerCase();
        if (!query) {
            return friendRequests;
        }

        return friendRequests.filter(request =>
            request.requesterId.toLowerCase().includes(query)
            || request.requesterName.toLowerCase().includes(query)
            || request.requesterEmail.toLowerCase().includes(query)
        );
    }, [friendRequests, search]);

    const manageableGroups = useMemo(
        () => groups.filter(group => canInviteMembers(findUserMembership(group, user?.id))),
        [groups, user?.id]
    );

    const activeInviteGroup = useMemo(
        () => manageableGroups.find(group => group.id === groupInviteTargetId) ?? null,
        [groupInviteTargetId, manageableGroups]
    );

    const availableGroupInviteFriends = useMemo(() => {
        if (!activeInviteGroup) {
            return [];
        }

        const memberIds = new Set(activeInviteGroup.members.map(member => member.userId));
        return friends.filter(friend => !memberIds.has(friend.id));
    }, [activeInviteGroup, friends]);

    useEffect(() => {
        if (!activeInviteGroup) {
            return;
        }

        const allowedIds = new Set(availableGroupInviteFriends.map(friend => friend.id));
        setSelectedGroupInviteFriendIds(current => current.filter(id => allowedIds.has(id)));
    }, [activeInviteGroup, availableGroupInviteFriends]);

    useEffect(() => {
        if (!isGroupInviteModalOpen) {
            return;
        }

        if (manageableGroups.length === 0) {
            setGroupInviteTargetId('');
            setSelectedGroupInviteFriendIds([]);
            return;
        }

        if (!manageableGroups.some(group => group.id === groupInviteTargetId)) {
            setGroupInviteTargetId(manageableGroups[0].id);
        }
    }, [groupInviteTargetId, isGroupInviteModalOpen, manageableGroups]);

    const searchPlaceholder = {
        friends: 'Найти друга',
        groups: 'Найти группу',
        invites: 'Найти приглашение',
        meetings: 'Найти встречу'
    }[activeTab];

    const getMeetingDurationValue = () => {
        const digits = meetingDurationInput.replace(/\D/g, '').slice(0, 3);
        if (!digits) {
            return null;
        }

        const value = Number(digits);
        return Number.isFinite(value) ? value : null;
    };

    const showMessage = (message: string) => {
        setSuccess(message);
        setError('');
    };

    const toggleSelection = (items: string[], value: string) =>
        items.includes(value)
            ? items.filter(item => item !== value)
            : [...items, value];

    const syncMeetingEndTimeWithDuration = (nextDate: string, nextStartTime: string, rawDuration?: string) => {
        const duration = Number((rawDuration ?? meetingDurationInput).replace(/\D/g, '').slice(0, 3));
        if (!Number.isFinite(duration) || duration <= 0) {
            return;
        }

        setMeetingEndTime(buildEndTime(nextDate, nextStartTime, Math.min(duration, 720)));
    };

    const handleMeetingDurationInputChange = (value: string) => {
        const digits = value.replace(/\D/g, '').slice(0, 3);
        setMeetingDurationInput(digits);

        if (digits) {
            syncMeetingEndTimeWithDuration(meetingDate, meetingStartTime, digits);
        }
    };

    const normalizeMeetingDurationInput = () => {
        const parsed = getMeetingDurationValue();
        if (!parsed) {
            const fallbackDuration = getDurationMinutesBetween(meetingStartTime, meetingEndTime) || 60;
            setMeetingDurationInput(String(fallbackDuration));
            syncMeetingEndTimeWithDuration(meetingDate, meetingStartTime, String(fallbackDuration));
            return;
        }

        const normalized = Math.min(720, Math.max(15, parsed));
        setMeetingDurationInput(String(normalized));
        syncMeetingEndTimeWithDuration(meetingDate, meetingStartTime, String(normalized));
    };

    const applyMeetingDurationPreset = (minutes: number) => {
        setMeetingDurationInput(String(minutes));
        syncMeetingEndTimeWithDuration(meetingDate, meetingStartTime, String(minutes));
    };

    const openConfirmAction = (action: ConfirmActionState) => {
        setConfirmAction(action);
        setError('');
    };

    const handleConfirmAction = async () => {
        if (!confirmAction) {
            return;
        }

        setConfirmingAction(true);
        try {
            await confirmAction.onConfirm();
            setConfirmAction(null);
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Операция не выполнена');
        } finally {
            setConfirmingAction(false);
        }
    };

    const safeSubmit = (handler: (event: FormEvent) => Promise<void>) => (event: FormEvent) => {
        handler(event).catch(err => setError(err instanceof Error ? err.message : 'Операция не выполнена'));
    };

    const calculateSlots = async (participantIds: string[], duration = 60, startDate = todayInput()) => {
        const slots = await planningApi.findSlots({
            participantIds,
            startDate,
            durationMinutes: duration
        });

        return slots;
    };

    const loadNearestSlot = async (friend: Friend) => {
        setSlotLoadingId(friend.id);
        setError('');
        try {
            const slots = await calculateSlots([friend.id]);
            setNearestSlots(current => ({ ...current, [friend.id]: slots[0] ?? null }));
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Не удалось подобрать ближайшее время');
        } finally {
            setSlotLoadingId('');
        }
    };

    const handleFriendRequest = async (event: FormEvent) => {
        event.preventDefault();
        await socialApi.sendFriendRequest(friendEmail);
        setFriendEmail('');
        setIsFriendModalOpen(false);
        showMessage('Заявка в друзья отправлена');
    };

    const openCreateGroupModal = () => {
        setEditingGroup(null);
        setGroupName('');
        setGroupDescription('');
        setSelectedNewGroupFriendIds([]);
        setIsGroupModalOpen(true);
        setError('');
    };

    const openEditGroupModal = (group: Group) => {
        setEditingGroup(group);
        setGroupName(group.name);
        setGroupDescription(group.description ?? '');
        setSelectedNewGroupFriendIds([]);
        setIsGroupModalOpen(true);
        setError('');
    };

    const closeGroupModal = () => {
        setIsGroupModalOpen(false);
        setEditingGroup(null);
        setGroupName('');
        setGroupDescription('');
        setSelectedNewGroupFriendIds([]);
    };

    const closeGroupInviteModal = () => {
        setIsGroupInviteModalOpen(false);
        setGroupInviteTargetId('');
        setSelectedGroupInviteFriendIds([]);
    };

    const handleCreateGroup = async (event: FormEvent) => {
        event.preventDefault();
        if (editingGroup) {
            await groupsApi.updateGroup(editingGroup.id, { name: groupName, description: groupDescription || undefined });
            showMessage('Группа обновлена');
        } else {
            const createdGroup = await groupsApi.createGroup({ name: groupName, description: groupDescription || undefined });
            const inviteResults = selectedNewGroupFriendIds.length > 0
                ? await Promise.allSettled(
                    selectedNewGroupFriendIds.map(friendId => groupsApi.inviteUser({ groupId: createdGroup.id, inviteeId: friendId }))
                )
                : [];

            const sentInvitesCount = inviteResults.filter(result => result.status === 'fulfilled').length;
            const failedInvitesCount = inviteResults.length - sentInvitesCount;

            if (inviteResults.length === 0) {
                showMessage('Группа создана');
            } else if (failedInvitesCount === 0) {
                showMessage(`Группа создана, приглашения отправлены: ${sentInvitesCount}`);
            } else {
                setSuccess(`Группа создана, приглашения отправлены: ${sentInvitesCount}`);
                setError(`Не удалось отправить ${failedInvitesCount} приглашения`);
            }
        }

        closeGroupModal();
        await loadNetwork();
    };

    const openGroupInvite = (friend: Friend) => {
        if (manageableGroups.length === 0) {
            setError('Вы можете приглашать друзей только в группы, где вы владелец или администратор');
            return;
        }

        setGroupInviteTargetId(manageableGroups[0].id);
        setSelectedGroupInviteFriendIds([friend.id]);
        setIsGroupInviteModalOpen(true);
        setError('');
    };

    const openExistingGroupInvite = (group: Group) => {
        if (!canInviteMembers(findUserMembership(group, user?.id))) {
            setError('Вы можете приглашать друзей только в группы, где вы владелец или администратор');
            return;
        }

        setGroupInviteTargetId(group.id);
        setSelectedGroupInviteFriendIds([]);
        setIsGroupInviteModalOpen(true);
        setError('');
    };

    const handleInviteToGroup = async (event: FormEvent) => {
        event.preventDefault();
        if (!activeInviteGroup) {
            return;
        }

        if (selectedGroupInviteFriendIds.length === 0) {
            setError('Выберите хотя бы одного друга для приглашения');
            return;
        }

        const inviteResults = await Promise.allSettled(
            selectedGroupInviteFriendIds.map(friendId => groupsApi.inviteUser({ groupId: activeInviteGroup.id, inviteeId: friendId }))
        );

        const sentInvitesCount = inviteResults.filter(result => result.status === 'fulfilled').length;
        const failedInvitesCount = inviteResults.length - sentInvitesCount;

        closeGroupInviteModal();

        if (failedInvitesCount === 0) {
            showMessage(sentInvitesCount === 1 ? 'Приглашение в группу отправлено' : `Приглашения отправлены: ${sentInvitesCount}`);
        } else {
            setSuccess(`Приглашения отправлены: ${sentInvitesCount}`);
            setError(`Не удалось отправить ${failedInvitesCount} приглашения`);
        }

        await loadNetwork();
    };

    const openMeetingPlanner = async (target: MeetingTarget) => {
        setMeetingTarget(target);
        setMeetingTitle(target.titleSuggestion);
        setMeetingDescription(target.descriptionSuggestion ?? '');
        setMeetingDate(todayInput());
        setMeetingStartTime('09:00');
        setMeetingEndTime('10:00');
        setMeetingDurationInput('60');
        setMeetingSlots([]);
        setError('');

        setMeetingSlotsLoading(true);
        try {
            const slots = await calculateSlots(target.participantIds);
            setMeetingSlots(slots);
            if (slots[0]) {
                setMeetingDate(toDateInput(slots[0].startTime));
                setMeetingStartTime(toTimeInput(slots[0].startTime));
                setMeetingEndTime(toTimeInput(slots[0].endTime));
                setMeetingDurationInput(String(Math.max(15, Math.round((new Date(slots[0].endTime).getTime() - new Date(slots[0].startTime).getTime()) / 60000))));
                if (target.slotCacheKey) {
                    setNearestSlots(current => ({ ...current, [target.slotCacheKey!]: slots[0] }));
                }
            }
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Не удалось подобрать время встречи');
        } finally {
            setMeetingSlotsLoading(false);
        }
    };

    const openMeetingInvite = async (friend: Friend) => {
        await openMeetingPlanner({
            mode: 'create',
            kind: 'friend',
            label: friend.fullName,
            participantIds: [friend.id],
            titleSuggestion: `Встреча с ${friend.fullName}`,
            slotCacheKey: friend.id
        });
    };

    const openGroupMeeting = async (group: Group) => {
        const participantIds = Array.from(new Set(
            group.members
                .map(member => member.userId)
                .filter(memberId => memberId !== user?.id)
        ));

        if (participantIds.length === 0) {
            setError('В группе пока нет других участников для встречи');
            return;
        }

        await openMeetingPlanner({
            mode: 'create',
            kind: 'group',
            label: group.name,
            participantIds,
            groupId: group.id,
            titleSuggestion: `Встреча группы ${group.name}`,
            descriptionSuggestion: group.description
        });
    };

    const openMeetingEditor = async (meeting: ManagedMeeting) => {
        const target: MeetingTarget = {
            mode: 'edit',
            kind: meeting.relatedGroupId ? 'group' : 'friend',
            label: meeting.relatedGroupName ?? (meeting.participantNames.join(', ') || 'Встреча'),
            participantIds: meeting.participantIds,
            participantNames: meeting.participantNames,
            groupId: meeting.relatedGroupId ?? undefined,
            meetingId: meeting.meetingId,
            titleSuggestion: meeting.title,
            descriptionSuggestion: meeting.description
        };

        setMeetingTarget(target);
        setMeetingTitle(meeting.title);
        setMeetingDescription(meeting.description ?? '');
        setMeetingDate(toDateInput(meeting.startTime));
        setMeetingStartTime(toTimeInput(meeting.startTime));
        setMeetingEndTime(toTimeInput(meeting.endTime));
        setMeetingDurationInput(String(Math.max(15, Math.round((new Date(meeting.endTime).getTime() - new Date(meeting.startTime).getTime()) / 60000))));
        setMeetingSlots([]);
        setError('');

        await refreshMeetingSlotsForTarget(
            target,
            toDateInput(meeting.startTime),
            String(Math.max(15, Math.round((new Date(meeting.endTime).getTime() - new Date(meeting.startTime).getTime()) / 60000)))
        );
    };

    const refreshMeetingSlotsForTarget = async (target: MeetingTarget, startDate = meetingDate, durationInput = meetingDurationInput) => {
        const duration = Number(durationInput.replace(/\D/g, '').slice(0, 3));
        if (!Number.isFinite(duration) || duration < 15) {
            setError('Укажите длительность встречи не меньше 15 минут');
            return;
        }

        setMeetingSlotsLoading(true);
        try {
            const slots = await calculateSlots(target.participantIds, Math.min(duration, 720), startDate);
            setMeetingSlots(slots);
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Не удалось обновить варианты времени');
        } finally {
            setMeetingSlotsLoading(false);
        }
    };

    const refreshMeetingSlots = async () => {
        if (!meetingTarget) {
            return;
        }

        await refreshMeetingSlotsForTarget(meetingTarget);
        showMessage('Варианты времени обновлены');
    };

    const handleCreateMeeting = async (event: FormEvent) => {
        event.preventDefault();
        if (!meetingTarget) {
            return;
        }

        if (meetingEndTime <= meetingStartTime) {
            setError('Время окончания должно быть позже времени начала');
            return;
        }

        const payload = {
            title: meetingTitle,
            description: meetingDescription || undefined,
            startTime: new Date(`${meetingDate}T${meetingStartTime}`).toISOString(),
            endTime: new Date(`${meetingDate}T${meetingEndTime}`).toISOString(),
            participantIds: meetingTarget.participantIds,
            groupId: meetingTarget.groupId
        };

        if (meetingTarget.mode === 'edit' && meetingTarget.meetingId) {
            await scheduleApi.updateMeeting(meetingTarget.meetingId, payload);
        } else {
            await scheduleApi.createMeeting(payload);
        }

        setMeetingTarget(null);
        setActiveTab('meetings');
        showMessage(meetingTarget.mode === 'edit'
            ? 'Встреча обновлена'
            : meetingTarget.kind === 'group'
                ? 'Приглашения группе отправлены'
                : 'Приглашение на встречу отправлено');
        await loadNetwork();
    };

    const handleRespond = async (groupId: string, isAccepted: boolean) => {
        await groupsApi.respondToInvite({ groupId, isAccepted });
        showMessage(isAccepted ? 'Приглашение принято' : 'Приглашение отклонено');
        await loadNetwork();
    };

    const handleFriendRequestRespond = async (requesterId: string, isAccepted: boolean) => {
        if (isAccepted) {
            await socialApi.acceptFriendRequest(requesterId);
        } else {
            await socialApi.declineFriendRequest(requesterId);
        }

        showMessage(isAccepted ? 'Заявка в друзья принята' : 'Заявка в друзья отклонена');
        await loadNetwork();
    };

    const handleDeleteFriend = async (friendId: string) => {
        await socialApi.deleteFriend(friendId);
        showMessage('Друг удалён');
        await loadNetwork();
    };

    const handleDeleteGroup = async (groupId: string) => {
        await groupsApi.deleteGroup(groupId);
        showMessage('Группа удалена');
        await loadNetwork();
    };

    const handleLeaveGroup = async (groupId: string) => {
        await groupsApi.leaveGroup(groupId);
        showMessage('Вы покинули группу');
        await loadNetwork();
    };

    const handleUpdateMemberRole = async (groupId: string, memberId: string, role: number) => {
        await groupsApi.updateMemberRole(groupId, memberId, role);
        showMessage(role === 1 ? 'Участник повышен до администратора' : 'Роль участника обновлена');
        await loadNetwork();
    };

    const handleRemoveMember = async (groupId: string, memberId: string) => {
        await groupsApi.removeMember(groupId, memberId);
        showMessage('Участник удалён из группы');
        await loadNetwork();
    };

    const handleMeetingInviteRespond = async (meetingId: string, isAccepted: boolean) => {
        await scheduleApi.respondToMeetingInvite({ meetingId, isAccepted });
        showMessage(isAccepted ? 'Приглашение на встречу принято' : 'Приглашение на встречу отклонено');
        await loadNetwork();
    };

    const handleCancelOutgoingMeeting = async (meetingId: string) => {
        await scheduleApi.deleteMeeting(meetingId);
        showMessage('Ожидающая встреча отменена');
        await loadNetwork();
    };

    const getMyMembership = (group: Group) =>
        findUserMembership(group, user?.id);

    const renderScheduledMeetingCard = (
        header: string,
        meeting?: Friend['upcomingMeeting'] | Group['upcomingMeeting'] | null,
        earlierSlot?: TimeSlot | null
    ) => {
        if (!meeting) {
            return null;
        }

        return (
            <div className="mt-4 rounded-2xl border border-indigo-100 bg-indigo-50/60 p-4">
                <div className="flex items-center gap-2 text-xs font-black uppercase text-indigo-600">
                    <MessageCircleMore size={14} /> {header}
                </div>
                <div className="mt-2 font-black text-gray-900">{meeting.title}</div>
                <div className="mt-2 text-sm font-bold text-gray-900">{formatTimeRange(meeting.startTime, meeting.endTime)}</div>
                {meeting.relatedGroupName && (
                    <div className="mt-1 text-xs font-bold text-gray-500">Группа: {meeting.relatedGroupName}</div>
                )}
                {earlierSlot && (
                    <div className="mt-3 rounded-xl bg-white/80 px-3 py-2 text-xs text-indigo-700">
                        Эту встречу можно перенести на более раннее время: {formatSlot(earlierSlot)}
                    </div>
                )}
            </div>
        );
    };

    const renderTabs = () => (
        <div className="rounded-2xl bg-gray-100 p-1 text-xs font-bold">
            {[
                { id: 'friends' as const, label: 'Друзья', count: friends.length },
                { id: 'groups' as const, label: 'Группы', count: groups.length },
                { id: 'invites' as const, label: 'Приглашения', count: invites.length + friendRequests.length + meetingInvites.length },
                { id: 'meetings' as const, label: 'Встречи', count: outgoingMeetingInvites.length + myMeetings.length }
            ].map(tab => (
                <button
                    key={tab.id}
                    onClick={() => {
                        setActiveTab(tab.id);
                        setSearch('');
                    }}
                    className={`rounded-xl px-4 py-2 transition ${activeTab === tab.id ? 'bg-white text-gray-900 shadow-sm' : 'text-gray-500 hover:text-gray-700'}`}
                >
                    {tab.label}
                    <span className="ml-2 rounded-full bg-gray-200 px-2 py-0.5 text-[10px] text-gray-500">{tab.count}</span>
                </button>
            ))}
        </div>
    );

    const renderFriends = () => (
        <section className="rounded-[28px] border border-gray-100 bg-white p-6 shadow-sm">
            <div className="mb-5 flex items-center justify-between">
                <div>
                    <h2 className="text-xl font-black text-gray-900">Друзья</h2>
                </div>
                <Users className="text-indigo-500" size={22} />
            </div>

            {loading ? (
                <p className="text-sm text-gray-400">Загрузка...</p>
            ) : filteredFriends.length === 0 ? (
                <div className="rounded-2xl border-2 border-dashed border-gray-200 bg-gray-50 p-8 text-center">
                    <p className="font-bold text-gray-500">Друзей пока нет</p>
                    <button onClick={() => setIsFriendModalOpen(true)} className="mt-4 rounded-xl bg-indigo-600 px-4 py-2 text-sm font-bold text-white">
                        Добавить друга
                    </button>
                </div>
            ) : (
                <div className="grid gap-4 lg:grid-cols-2">
                    {filteredFriends.map(friend => {
                        const nearest = nearestSlots[friend.id] ?? friend.nearestAvailableSlot;
                        return (
                            <article key={friend.id} className="rounded-3xl border border-gray-100 bg-gray-50/70 p-5">
                                <div className="flex items-start justify-between gap-4">
                                    <div className="flex min-w-0 items-center gap-3">
                                        <div className="relative flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl bg-indigo-100 font-black text-indigo-700">
                                            {friend.fullName[0] ?? 'F'}
                                            <span className={`absolute -bottom-1 -right-1 h-4 w-4 rounded-full border-2 border-white ${friend.isBusy ? 'bg-rose-500' : 'bg-emerald-500'}`} />
                                        </div>
                                        <div className="min-w-0">
                                            <h3 className="break-words font-black text-gray-900">{friend.fullName}</h3>
                                            <div className="mt-1 text-xs font-bold text-gray-500">{friend.status}</div>
                                        </div>
                                    </div>
                                </div>

                                <div className="mt-4 rounded-2xl bg-white p-4">
                                    <div className="flex items-start justify-between gap-3">
                                        <div>
                                            <div className="flex items-center gap-2 text-xs font-black uppercase text-gray-400">
                                                <CalendarClock size={14} /> Ближайшее время
                                            </div>
                                            <p className="mt-1 text-sm font-bold text-gray-800">
                                                {nearest === undefined && 'Пока не рассчитано'}
                                                {nearest === null && 'Подходящих слотов не найдено'}
                                                {nearest && formatSlot(nearest)}
                                            </p>
                                        </div>
                                        <button
                                            onClick={() => void loadNearestSlot(friend)}
                                            disabled={slotLoadingId === friend.id}
                                            className="rounded-xl bg-gray-100 px-3 py-2 text-xs font-bold text-gray-600 disabled:opacity-50"
                                        >
                                            {slotLoadingId === friend.id ? 'Ищу...' : nearest ? 'Обновить' : 'Подобрать'}
                                        </button>
                                    </div>
                                </div>

                                {renderScheduledMeetingCard('Уже назначено', friend.upcomingMeeting, friend.earlierAvailableSlot)}

                                <div className="mt-4 grid gap-2 sm:grid-cols-2">
                                    <button onClick={() => void openMeetingInvite(friend)} className="rounded-xl bg-indigo-600 px-4 py-3 text-sm font-bold text-white">
                                        Пригласить на встречу
                                    </button>
                                    <button
                                        onClick={() => openGroupInvite(friend)}
                                        disabled={manageableGroups.length === 0}
                                        className="rounded-xl bg-white px-4 py-3 text-sm font-bold text-gray-700 disabled:cursor-not-allowed disabled:opacity-50"
                                        title={manageableGroups.length === 0 ? 'Нет групп, где вы можете приглашать участников' : undefined}
                                    >
                                        Пригласить в группу
                                    </button>
                                    <button
                                        onClick={() => openConfirmAction({
                                            title: 'Удалить из друзей',
                                            description: `Удалить ${friend.fullName} из списка друзей?`,
                                            confirmLabel: 'Удалить',
                                            tone: 'danger',
                                            onConfirm: async () => {
                                                await handleDeleteFriend(friend.id);
                                            }
                                        })}
                                        className="rounded-xl bg-red-50 px-4 py-3 text-sm font-bold text-red-600 sm:col-span-2"
                                    >
                                        <Trash2 size={16} className="inline" /> Удалить из друзей
                                    </button>
                                </div>
                            </article>
                        );
                    })}
                </div>
            )}
        </section>
    );

    const renderGroups = () => (
        <section className="rounded-[28px] border border-gray-100 bg-white p-6 shadow-sm">
            <div className="mb-5 flex items-center justify-between">
                <div>
                    <h2 className="text-xl font-black text-gray-900">Группы</h2>
                </div>
                <button onClick={openCreateGroupModal} className="rounded-xl bg-indigo-600 px-4 py-2 text-sm font-bold text-white">
                    <Plus size={18} className="inline" /> Группа
                </button>
            </div>

            {filteredGroups.length === 0 ? (
                <p className="rounded-2xl border-2 border-dashed border-gray-200 bg-gray-50 p-8 text-center text-sm text-gray-400">Групп пока нет.</p>
            ) : (
                <div className="grid gap-4 lg:grid-cols-2">
                    {filteredGroups.map(group => {
                        const myMembership = getMyMembership(group);
                        const canEdit = canInviteMembers(myMembership);
                        const canDelete = myMembership?.isOwner || myMembership?.role === 0;
                        const canLeave = Boolean(myMembership) && !myMembership?.isOwner;

                        return (
                            <article key={group.id} className="rounded-3xl border border-gray-100 bg-gray-50/70 p-5">
                                <div className="flex items-start justify-between gap-3">
                                    <div>
                                        <h3 className="font-black text-gray-900">{group.name}</h3>
                                        {group.description && <p className="mt-1 text-sm text-gray-500">{group.description}</p>}
                                    </div>
                                    <div className="flex items-center gap-2">
                                        {canEdit && (
                                            <button
                                                onClick={() => openExistingGroupInvite(group)}
                                                className="rounded-xl bg-white p-2 text-gray-500 hover:text-indigo-600"
                                                title="Пригласить друзей"
                                            >
                                                <UserPlus size={16} />
                                            </button>
                                        )}
                                        {canEdit && (
                                            <button onClick={() => openEditGroupModal(group)} className="rounded-xl bg-white p-2 text-gray-500 hover:text-indigo-600">
                                                <Pencil size={16} />
                                            </button>
                                        )}
                                        {canDelete && (
                                            <button
                                                onClick={() => openConfirmAction({
                                                    title: 'Удалить группу',
                                                    description: `Удалить группу «${group.name}»?`,
                                                    confirmLabel: 'Удалить',
                                                    tone: 'danger',
                                                    onConfirm: async () => {
                                                        await handleDeleteGroup(group.id);
                                                    }
                                                })}
                                                className="rounded-xl bg-white p-2 text-gray-500 hover:text-red-600"
                                            >
                                                <Trash2 size={16} />
                                            </button>
                                        )}
                                    </div>
                                </div>
                                <div className="mt-4 space-y-2">
                                    {group.members.map(member => {
                                        const isSelf = member.userId === user?.id;
                                        const canChangeRole = myMembership?.isOwner && !member.isOwner && !isSelf;
                                        const canRemoveMember = (
                                            (myMembership?.isOwner && !member.isOwner && !isSelf)
                                            || (myMembership?.role === 1 && member.role === 2 && !isSelf)
                                        );

                                        return (
                                            <div key={member.userId} className="flex flex-wrap items-center justify-between gap-2 rounded-2xl bg-white px-3 py-3 text-sm">
                                                <div className="min-w-0">
                                                    <div className="font-bold text-gray-800">{member.name}{isSelf ? ' · вы' : ''}</div>
                                                    <div className="text-xs text-gray-500">{roleLabel(member.role)}</div>
                                                </div>
                                                <div className="flex flex-wrap gap-2">
                                                    {canChangeRole && member.role === 2 && (
                                                        <button
                                                            onClick={() => openConfirmAction({
                                                                title: 'Сделать администратором',
                                                                description: `Назначить ${member.name} администратором группы «${group.name}»?`,
                                                                confirmLabel: 'Назначить',
                                                                tone: 'primary',
                                                                onConfirm: async () => {
                                                                    await handleUpdateMemberRole(group.id, member.userId, 1);
                                                                }
                                                            })}
                                                            className="rounded-xl bg-indigo-50 px-3 py-2 text-xs font-bold text-indigo-700"
                                                        >
                                                            Сделать админом
                                                        </button>
                                                    )}
                                                    {canChangeRole && member.role === 1 && (
                                                        <button
                                                            onClick={() => openConfirmAction({
                                                                title: 'Сделать участником',
                                                                description: `Снять роль администратора с ${member.name} в группе «${group.name}»?`,
                                                                confirmLabel: 'Изменить роль',
                                                                tone: 'primary',
                                                                onConfirm: async () => {
                                                                    await handleUpdateMemberRole(group.id, member.userId, 2);
                                                                }
                                                            })}
                                                            className="rounded-xl bg-slate-100 px-3 py-2 text-xs font-bold text-slate-700"
                                                        >
                                                            Сделать участником
                                                        </button>
                                                    )}
                                                    {canRemoveMember && (
                                                        <button
                                                            onClick={() => openConfirmAction({
                                                                title: 'Удалить участника',
                                                                description: `Удалить ${member.name} из группы «${group.name}»?`,
                                                                confirmLabel: 'Удалить',
                                                                tone: 'danger',
                                                                onConfirm: async () => {
                                                                    await handleRemoveMember(group.id, member.userId);
                                                                }
                                                            })}
                                                            className="rounded-xl bg-red-50 px-3 py-2 text-xs font-bold text-red-600"
                                                        >
                                                            Удалить
                                                        </button>
                                                    )}
                                                </div>
                                            </div>
                                        );
                                    })}
                                </div>
                                {renderScheduledMeetingCard('Встреча уже стоит в плане', group.upcomingMeeting, group.earlierAvailableSlot)}
                                <div className="mt-4 grid gap-2 sm:grid-cols-2">
                                    <button onClick={() => void openGroupMeeting(group)} className="rounded-xl bg-indigo-600 px-4 py-3 text-sm font-bold text-white">
                                        <CalendarClock size={16} className="inline" /> Подобрать встречу
                                    </button>
                                    {canLeave && (
                                        <button onClick={() => void handleLeaveGroup(group.id)} className="rounded-xl bg-white px-4 py-3 text-sm font-bold text-gray-700">
                                            Покинуть группу
                                        </button>
                                    )}
                                </div>
                            </article>
                        );
                    })}
                </div>
            )}
        </section>
    );

    const renderInvites = () => (
        <section className="rounded-[28px] border border-gray-100 bg-white p-6 shadow-sm">
            <h2 className="mb-5 text-xl font-black text-gray-900">Приглашения</h2>

            {filteredInvites.length === 0 && filteredFriendRequests.length === 0 && filteredMeetingInvites.length === 0 ? (
                <p className="rounded-2xl border-2 border-dashed border-gray-200 bg-gray-50 p-8 text-center text-sm text-gray-400">Нет приглашений, которые требуют ответа.</p>
            ) : (
                <div className="grid gap-4 lg:grid-cols-2">
                    {filteredInvites.map(invite => (
                        <article key={invite.groupId} className="rounded-3xl border border-cyan-100 bg-cyan-50/50 p-5">
                            <div className="text-xs font-bold uppercase text-cyan-600">Приглашение в группу от {invite.inviterName}</div>
                            <h3 className="mt-1 font-black text-gray-900">{invite.groupName}</h3>
                            <p className="mb-4 mt-1 text-xs text-gray-500">{formatDateTime(invite.createdDate)}</p>
                            <div className="flex gap-2">
                                <button onClick={() => void handleRespond(invite.groupId, true)} className="rounded-xl bg-green-600 px-4 py-2 text-xs font-bold text-white">
                                    <Check size={14} className="inline" /> Принять
                                </button>
                                <button onClick={() => void handleRespond(invite.groupId, false)} className="rounded-xl bg-red-500 px-4 py-2 text-xs font-bold text-white">
                                    <X size={14} className="inline" /> Отклонить
                                </button>
                            </div>
                        </article>
                    ))}
                    {filteredFriendRequests.map(request => (
                        <article key={request.requesterId} className="rounded-3xl border border-indigo-100 bg-indigo-50/50 p-5">
                            <div className="text-xs font-bold uppercase text-indigo-600">Заявка в друзья</div>
                            <h3 className="mt-1 font-black text-gray-900">{request.requesterName || 'Пользователь'}</h3>
                            <p className="mb-4 mt-1 break-all text-xs text-gray-500">{request.requesterEmail || request.requesterId}</p>
                            <div className="flex gap-2">
                                <button onClick={() => void handleFriendRequestRespond(request.requesterId, true)} className="rounded-xl bg-green-600 px-4 py-2 text-xs font-bold text-white">
                                    <Check size={14} className="inline" /> Принять
                                </button>
                                <button onClick={() => void handleFriendRequestRespond(request.requesterId, false)} className="rounded-xl bg-red-500 px-4 py-2 text-xs font-bold text-white">
                                    <X size={14} className="inline" /> Отклонить
                                </button>
                            </div>
                        </article>
                    ))}
                    {filteredMeetingInvites.map(invite => (
                        <article key={invite.meetingId} className="rounded-3xl border border-violet-100 bg-violet-50/50 p-5">
                            <div className="text-xs font-bold uppercase text-violet-600">Встреча от {invite.organizerName}</div>
                            <h3 className="mt-1 font-black text-gray-900">{invite.title}</h3>
                            {invite.description && <p className="mt-1 text-sm text-gray-500">{invite.description}</p>}
                            <p className="mb-4 mt-2 text-xs text-gray-500">{formatTimeRange(invite.startTime, invite.endTime)}</p>
                            <div className="flex gap-2">
                                <button onClick={() => void handleMeetingInviteRespond(invite.meetingId, true)} className="rounded-xl bg-green-600 px-4 py-2 text-xs font-bold text-white">
                                    <Check size={14} className="inline" /> Принять
                                </button>
                                <button onClick={() => void handleMeetingInviteRespond(invite.meetingId, false)} className="rounded-xl bg-red-500 px-4 py-2 text-xs font-bold text-white">
                                    <X size={14} className="inline" /> Отклонить
                                </button>
                            </div>
                        </article>
                    ))}
                </div>
            )}
        </section>
    );

    const renderMeetings = () => (
        <section className="rounded-[28px] border border-gray-100 bg-white p-6 shadow-sm">
            <h2 className="mb-5 text-xl font-black text-gray-900">Встречи</h2>

            {filteredMyMeetings.length === 0 && filteredOutgoingMeetingInvites.length === 0 ? (
                <p className="rounded-2xl border-2 border-dashed border-gray-200 bg-gray-50 p-8 text-center text-sm text-gray-400">Пока нет встреч и ожидающих подтверждения приглашений.</p>
            ) : (
                <div className="space-y-5">
                    {filteredOutgoingMeetingInvites.length > 0 && (
                        <div>
                            <div className="mb-3 flex items-center gap-2 text-sm font-black text-gray-900">
                                <CalendarClock size={16} className="text-amber-600" /> Ожидают подтверждения
                            </div>
                            <div className="grid gap-4 lg:grid-cols-2">
                                {filteredOutgoingMeetingInvites.map(invite => (
                                    <article key={invite.meetingId} className="rounded-3xl border border-amber-100 bg-amber-50/60 p-5">
                                        <div className="flex items-start justify-between gap-3">
                                            <div>
                                                <div className="text-xs font-bold uppercase text-amber-700">Ожидает подтверждения</div>
                                                <h3 className="mt-1 font-black text-gray-900">{invite.title}</h3>
                                                {invite.description && <p className="mt-1 text-sm text-gray-500">{invite.description}</p>}
                                            </div>
                                            <span className="rounded-full bg-white px-3 py-1 text-[10px] font-black uppercase tracking-wide text-amber-700">
                                                {invite.acceptedParticipantsCount}/{invite.totalParticipantsCount} приняли
                                            </span>
                                        </div>
                                        <p className="mt-3 text-xs text-gray-500">{formatTimeRange(invite.startTime, invite.endTime)}</p>
                                        <div className="mt-4 rounded-2xl bg-white/80 p-4">
                                            <div className="text-xs font-black uppercase text-gray-400">Кого ждём</div>
                                            <p className="mt-1 text-sm font-bold text-gray-800">
                                                {invite.pendingParticipantNames.length > 0
                                                    ? invite.pendingParticipantNames.join(', ')
                                                    : 'Ожидаем финальное подтверждение'}
                                            </p>
                                        </div>
                                        <div className="mt-4 flex gap-2">
                                            <button onClick={() => void handleCancelOutgoingMeeting(invite.meetingId)} className="rounded-xl bg-white px-4 py-2 text-xs font-bold text-gray-700">
                                                Отменить встречу
                                            </button>
                                        </div>
                                    </article>
                                ))}
                            </div>
                        </div>
                    )}

                    {filteredMyMeetings.length > 0 && (
                        <div>
                            <div className="mb-3 flex items-center gap-2 text-sm font-black text-gray-900">
                                <CalendarClock size={16} className="text-indigo-500" /> Мои встречи
                            </div>
                            <div className="grid gap-4 lg:grid-cols-2">
                                {filteredMyMeetings.map(meeting => (
                                    <article key={meeting.meetingId} className="rounded-3xl border border-gray-100 bg-gray-50/70 p-5">
                                        <div className="flex items-start justify-between gap-3">
                                            <div>
                                                <div className="text-xs font-bold uppercase text-indigo-600">{meetingStatusLabel(meeting.status)}</div>
                                                <h3 className="mt-1 font-black text-gray-900">{meeting.title}</h3>
                                                {meeting.description && <p className="mt-1 text-sm text-gray-500">{meeting.description}</p>}
                                            </div>
                                            {meeting.relatedGroupName && (
                                                <span className="rounded-full bg-white px-3 py-1 text-[10px] font-black uppercase tracking-wide text-gray-600">
                                                    {meeting.relatedGroupName}
                                                </span>
                                            )}
                                        </div>
                                        <p className="mt-3 text-sm font-bold text-gray-900">{formatTimeRange(meeting.startTime, meeting.endTime)}</p>
                                        <div className="mt-2 text-xs text-gray-500">Участники: {meeting.participantNames.join(', ') || '—'}</div>
                                        {meeting.canEdit && (
                                            <div className="mt-4 flex flex-wrap gap-2">
                                                <button onClick={() => void openMeetingEditor(meeting)} className="rounded-xl bg-indigo-600 px-4 py-2 text-xs font-bold text-white">
                                                    Редактировать
                                                </button>
                                                <button
                                                    onClick={() => openConfirmAction({
                                                        title: 'Удалить встречу',
                                                        description: `Удалить встречу «${meeting.title}»?`,
                                                        confirmLabel: 'Удалить',
                                                        tone: 'danger',
                                                        onConfirm: async () => {
                                                            await scheduleApi.deleteMeeting(meeting.meetingId);
                                                            showMessage('Встреча удалена');
                                                            await loadNetwork();
                                                        }
                                                    })}
                                                    className="rounded-xl bg-white px-4 py-2 text-xs font-bold text-gray-700"
                                                >
                                                    Удалить
                                                </button>
                                            </div>
                                        )}
                                    </article>
                                ))}
                            </div>
                        </div>
                    )}
                </div>
            )}
        </section>
    );

    return (
        <div className="w-full max-w-[1300px] mx-auto space-y-6 p-4 md:p-8">
            <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
                <div>
                    <h1 className="text-3xl font-black text-gray-900">Люди</h1>
                </div>
                <div className="flex flex-wrap gap-2">
                    <button onClick={() => setIsFriendModalOpen(true)} className="rounded-xl bg-white px-4 py-2 text-sm font-bold text-gray-700 shadow-sm">
                        <UserPlus size={18} className="inline" /> Добавить друга
                    </button>
                    <button onClick={openCreateGroupModal} className="rounded-xl bg-indigo-600 px-4 py-2 text-sm font-bold text-white shadow-lg shadow-indigo-100">
                        <Plus size={18} className="inline" /> Создать группу
                    </button>
                </div>
            </div>

            {error && <div className="rounded-2xl border border-red-100 bg-red-50 p-4 text-sm text-red-600">{error}</div>}
            {success && <div className="rounded-2xl border border-emerald-100 bg-emerald-50 p-4 text-sm text-emerald-700">{success}</div>}

            <div className="flex flex-col gap-3 rounded-[28px] border border-gray-100 bg-white p-3 shadow-sm lg:flex-row lg:items-center lg:justify-between">
                {renderTabs()}
                <div className="flex min-w-0 items-center gap-2 rounded-2xl bg-gray-50 px-4 py-3 lg:w-80">
                    <Search size={18} className="text-gray-400" />
                    <input
                        value={search}
                        onChange={event => setSearch(event.target.value)}
                        placeholder={searchPlaceholder}
                        className="w-full bg-transparent text-sm text-gray-900 outline-none placeholder:text-gray-400"
                    />
                </div>
            </div>

            {activeTab === 'friends' && renderFriends()}
            {activeTab === 'groups' && renderGroups()}
            {activeTab === 'invites' && renderInvites()}
            {activeTab === 'meetings' && renderMeetings()}

            {isFriendModalOpen && (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/50 p-4">
                    <form onSubmit={safeSubmit(handleFriendRequest)} className="w-full max-w-md rounded-[28px] bg-white p-6 shadow-2xl">
                        <h2 className="mb-1 text-2xl font-black text-gray-900">Добавить друга</h2>
                        <p className="mb-4 text-sm text-gray-500">Заявка отправляется по email пользователя.</p>
                        <input value={friendEmail} onChange={event => setFriendEmail(event.target.value)} required type="email" placeholder="email пользователя" className="w-full rounded-xl border border-gray-200 px-4 py-3 text-sm text-gray-900 outline-none focus:ring-2 focus:ring-indigo-500" />
                        <div className="mt-5 flex justify-end gap-2">
                            <button type="button" onClick={() => setIsFriendModalOpen(false)} className="rounded-xl bg-gray-100 px-4 py-2 text-sm font-bold text-gray-600">Отмена</button>
                            <button type="submit" className="rounded-xl bg-indigo-600 px-4 py-2 text-sm font-bold text-white">
                                <MailPlus size={16} className="inline" /> Отправить
                            </button>
                        </div>
                    </form>
                </div>
            )}

            {isGroupModalOpen && (
                <div className="fixed inset-0 z-50 overflow-y-auto bg-slate-950/50 p-3 sm:p-4">
                    <div className="flex min-h-full items-start justify-center py-2 sm:items-center sm:py-6">
                        <form onSubmit={safeSubmit(handleCreateGroup)} className="max-h-[calc(100vh-1.5rem)] w-full max-w-2xl overflow-y-auto rounded-[28px] bg-white p-5 shadow-2xl sm:p-6">
                        <h2 className="mb-1 text-2xl font-black text-gray-900">{editingGroup ? 'Редактировать группу' : 'Новая группа'}</h2>
                        <p className="mb-4 text-sm text-gray-500">
                            {editingGroup ? 'Обновите название или описание группы.' : 'Создайте пространство для постоянных встреч.'}
                        </p>
                        <div className="space-y-3">
                            <input value={groupName} onChange={event => setGroupName(event.target.value)} required placeholder="Название" className="w-full rounded-xl border border-gray-200 px-4 py-3 text-sm text-gray-900 outline-none focus:ring-2 focus:ring-indigo-500" />
                            <textarea value={groupDescription} onChange={event => setGroupDescription(event.target.value)} placeholder="Описание" className="w-full rounded-xl border border-gray-200 px-4 py-3 text-sm text-gray-900 outline-none focus:ring-2 focus:ring-indigo-500" />
                        </div>
                        {!editingGroup && (
                            <div className="mt-4 rounded-2xl border border-gray-100 bg-gray-50 p-4">
                                <div className="flex items-center justify-between gap-3">
                                    <div>
                                        <div className="text-sm font-black text-gray-900">Сразу пригласить друзей</div>
                                        <div className="text-xs text-gray-500">После создания группы отправим приглашения выбранным друзьям.</div>
                                    </div>
                                    {selectedNewGroupFriendIds.length > 0 && (
                                        <span className="rounded-full bg-white px-3 py-1 text-xs font-black text-indigo-600">
                                            {selectedNewGroupFriendIds.length} выбрано
                                        </span>
                                    )}
                                </div>
                                {friends.length === 0 ? (
                                    <div className="mt-3 rounded-2xl border border-dashed border-gray-200 bg-white px-4 py-6 text-center text-sm text-gray-400">
                                        Сначала добавьте друзей, чтобы звать их в новые группы.
                                    </div>
                                ) : (
                                    <div className="mt-3 grid max-h-72 gap-2 overflow-y-auto sm:grid-cols-2">
                                        {friends.map(friend => {
                                            const isSelected = selectedNewGroupFriendIds.includes(friend.id);
                                            return (
                                                <button
                                                    key={friend.id}
                                                    type="button"
                                                    onClick={() => setSelectedNewGroupFriendIds(current => toggleSelection(current, friend.id))}
                                                    className={`rounded-2xl border px-4 py-3 text-left transition ${
                                                        isSelected
                                                            ? 'border-indigo-200 bg-indigo-50 text-indigo-700 shadow-sm'
                                                            : 'border-gray-100 bg-white text-gray-700 hover:border-gray-200'
                                                    }`}
                                                >
                                                    <div className="flex items-start justify-between gap-3">
                                                        <div className="min-w-0">
                                                            <div className="font-black text-gray-900">{friend.fullName}</div>
                                                            <div className="mt-1 truncate text-xs text-gray-500">{friend.email}</div>
                                                        </div>
                                                        <span className={`flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-white transition ${isSelected ? 'opacity-100' : 'opacity-0'}`}>
                                                            <Check size={15} className="text-indigo-600" />
                                                        </span>
                                                    </div>
                                                </button>
                                            );
                                        })}
                                    </div>
                                )}
                            </div>
                        )}
                        <div className="sticky bottom-0 mt-5 flex justify-end gap-2 border-t border-gray-100 bg-white/95 pt-4 backdrop-blur">
                            <button type="button" onClick={closeGroupModal} className="rounded-xl bg-gray-100 px-4 py-2 text-sm font-bold text-gray-600">Отмена</button>
                            <button type="submit" className="rounded-xl bg-indigo-600 px-4 py-2 text-sm font-bold text-white">
                                {editingGroup ? 'Сохранить' : 'Создать'}
                            </button>
                        </div>
                        </form>
                    </div>
                </div>
            )}

            {isGroupInviteModalOpen && (
                <div className="fixed inset-0 z-50 overflow-y-auto bg-slate-950/50 p-3 sm:p-4">
                    <div className="flex min-h-full items-start justify-center py-2 sm:items-center sm:py-6">
                        <form onSubmit={safeSubmit(handleInviteToGroup)} className="max-h-[calc(100vh-1.5rem)] w-full max-w-2xl overflow-y-auto rounded-[28px] bg-white p-5 shadow-2xl sm:p-6">
                        <h2 className="mb-1 text-2xl font-black text-gray-900">Пригласить в группу</h2>
                        <p className="mb-4 text-sm text-gray-500">
                            Выберите группу и друзей из вашего списка, которым нужно отправить приглашение.
                        </p>
                        {manageableGroups.length === 0 ? (
                            <div className="rounded-2xl border-2 border-dashed border-gray-200 bg-gray-50 p-8 text-center">
                                <p className="font-bold text-gray-500">Нет групп для приглашения</p>
                                <p className="mt-1 text-sm text-gray-400">Приглашать участников можно только в группы, где вы владелец или администратор.</p>
                            </div>
                        ) : (
                            <div className="space-y-4">
                                <div>
                                    <div className="mb-2 text-xs font-black uppercase tracking-wide text-gray-400">Группа</div>
                                    <div className="grid gap-3 sm:grid-cols-2">
                                        {manageableGroups.map(group => {
                                            const isSelected = groupInviteTargetId === group.id;
                                            return (
                                                <button
                                                    key={group.id}
                                                    type="button"
                                                    onClick={() => setGroupInviteTargetId(group.id)}
                                                    className={`rounded-2xl border p-4 text-left transition ${isSelected ? 'border-indigo-200 bg-indigo-50 shadow-sm' : 'border-gray-100 bg-gray-50 hover:bg-white'}`}
                                                >
                                                    <div className="flex items-start justify-between gap-3">
                                                        <div className="min-w-0">
                                                            <div className="font-black text-gray-900">{group.name}</div>
                                                            {group.description && <div className="mt-1 text-sm text-gray-500">{group.description}</div>}
                                                        </div>
                                                        <span className={`flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-white transition ${isSelected ? 'opacity-100' : 'opacity-0'}`}>
                                                            <Check size={15} className="text-indigo-600" />
                                                        </span>
                                                    </div>
                                                </button>
                                            );
                                        })}
                                    </div>
                                </div>

                                <div>
                                    <div className="mb-2 flex items-center justify-between gap-3">
                                        <div className="text-xs font-black uppercase tracking-wide text-gray-400">Друзья</div>
                                        {selectedGroupInviteFriendIds.length > 0 && (
                                            <span className="rounded-full bg-gray-100 px-3 py-1 text-[11px] font-black text-gray-600">
                                                {selectedGroupInviteFriendIds.length} выбрано
                                            </span>
                                        )}
                                    </div>
                                    {!activeInviteGroup ? (
                                        <div className="rounded-2xl border border-dashed border-gray-200 bg-gray-50 px-4 py-6 text-center text-sm text-gray-400">
                                            Сначала выберите группу.
                                        </div>
                                    ) : availableGroupInviteFriends.length === 0 ? (
                                        <div className="rounded-2xl border border-dashed border-gray-200 bg-gray-50 px-4 py-6 text-center text-sm text-gray-400">
                                            Среди друзей сейчас нет тех, кого можно добавить в эту группу.
                                        </div>
                                    ) : (
                                        <div className="grid max-h-80 gap-2 overflow-y-auto sm:grid-cols-2">
                                            {availableGroupInviteFriends.map(friend => {
                                                const isSelected = selectedGroupInviteFriendIds.includes(friend.id);
                                                return (
                                                    <button
                                                        key={friend.id}
                                                        type="button"
                                                        onClick={() => setSelectedGroupInviteFriendIds(current => toggleSelection(current, friend.id))}
                                                        className={`rounded-2xl border px-4 py-3 text-left transition ${
                                                            isSelected
                                                                ? 'border-indigo-200 bg-indigo-50 text-indigo-700 shadow-sm'
                                                                : 'border-gray-100 bg-white text-gray-700 hover:border-gray-200'
                                                        }`}
                                                    >
                                                        <div className="flex items-start justify-between gap-3">
                                                            <div className="min-w-0">
                                                                <div className="font-black text-gray-900">{friend.fullName}</div>
                                                                <div className="mt-1 truncate text-xs text-gray-500">{friend.email}</div>
                                                            </div>
                                                            <span className={`flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-white transition ${isSelected ? 'opacity-100' : 'opacity-0'}`}>
                                                                <Check size={15} className="text-indigo-600" />
                                                            </span>
                                                        </div>
                                                    </button>
                                                );
                                            })}
                                        </div>
                                    )}
                                </div>
                            </div>
                        )}
                        <div className="sticky bottom-0 mt-5 flex justify-end gap-2 border-t border-gray-100 bg-white/95 pt-4 backdrop-blur">
                            <button type="button" onClick={closeGroupInviteModal} className="rounded-xl bg-gray-100 px-4 py-2 text-sm font-bold text-gray-600">Отмена</button>
                            <button disabled={!groupInviteTargetId || selectedGroupInviteFriendIds.length === 0} type="submit" className="rounded-xl bg-indigo-600 px-4 py-2 text-sm font-bold text-white disabled:opacity-50">
                                <Send size={16} className="inline" /> Пригласить
                            </button>
                        </div>
                        </form>
                    </div>
                </div>
            )}

            {meetingTarget && (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/50 p-4">
                    <form onSubmit={safeSubmit(handleCreateMeeting)} className="max-h-[90vh] w-full max-w-3xl overflow-y-auto rounded-[28px] bg-white p-6 shadow-2xl">
                        <h2 className="mb-1 text-2xl font-black text-gray-900">
                            {meetingTarget.mode === 'edit'
                                ? 'Управление встречей'
                                : meetingTarget.kind === 'group'
                                    ? 'Назначить встречу группы'
                                    : 'Пригласить на встречу'}
                        </h2>
                        <p className="mb-4 text-sm text-gray-500">
                            {meetingTarget.kind === 'group'
                                ? `Группа: ${meetingTarget.label} · участников: ${meetingTarget.participantIds.length + 1}`
                                : `Участник: ${meetingTarget.label}`}
                        </p>
                        {meetingTarget.participantNames && meetingTarget.participantNames.length > 0 && (
                            <div className="mb-4 rounded-2xl border border-gray-100 bg-gray-50 px-4 py-3 text-sm text-gray-600">
                                Участники: <span className="font-bold text-gray-900">{meetingTarget.participantNames.join(', ')}</span>
                            </div>
                        )}
                        <div className="grid gap-3 lg:grid-cols-2">
                            <label className="lg:col-span-2">
                                <div className="mb-1 text-xs font-black uppercase tracking-wide text-gray-400">Название</div>
                                <input value={meetingTitle} onChange={event => setMeetingTitle(event.target.value)} required placeholder="Название встречи" className="w-full rounded-xl border border-gray-200 px-4 py-3 text-sm text-gray-900 outline-none focus:ring-2 focus:ring-indigo-500" />
                            </label>
                            <label className="lg:col-span-2">
                                <div className="mb-1 text-xs font-black uppercase tracking-wide text-gray-400">Описание</div>
                                <textarea value={meetingDescription} onChange={event => setMeetingDescription(event.target.value)} placeholder="Что важно обсудить на встрече" className="w-full rounded-xl border border-gray-200 px-4 py-3 text-sm text-gray-900 outline-none focus:ring-2 focus:ring-indigo-500" />
                            </label>
                            <label>
                                <div className="mb-1 text-xs font-black uppercase tracking-wide text-gray-400">Дата</div>
                                <input type="date" value={meetingDate} onChange={event => setMeetingDate(event.target.value)} className="w-full rounded-xl border border-gray-200 px-4 py-3 text-sm text-gray-900" />
                            </label>
                            <label>
                                <div className="mb-1 text-xs font-black uppercase tracking-wide text-gray-400">Длительность</div>
                                <input
                                    type="text"
                                    inputMode="numeric"
                                    value={meetingDurationInput}
                                    onChange={event => handleMeetingDurationInputChange(event.target.value)}
                                    onBlur={normalizeMeetingDurationInput}
                                    placeholder="60"
                                    className="w-full rounded-xl border border-gray-200 px-4 py-3 text-sm text-gray-900 outline-none focus:ring-2 focus:ring-indigo-500"
                                />
                            </label>
                            <label>
                                <div className="mb-1 text-xs font-black uppercase tracking-wide text-gray-400">Начало</div>
                                <input
                                    type="time"
                                    value={meetingStartTime}
                                    onChange={event => {
                                        const nextStart = event.target.value;
                                        setMeetingStartTime(nextStart);
                                        syncMeetingEndTimeWithDuration(meetingDate, nextStart);
                                    }}
                                    className="w-full rounded-xl border border-gray-200 px-4 py-3 text-sm text-gray-900"
                                />
                            </label>
                            <label>
                                <div className="mb-1 text-xs font-black uppercase tracking-wide text-gray-400">Конец</div>
                                <input
                                    type="time"
                                    value={meetingEndTime}
                                    onChange={event => {
                                        const nextEnd = event.target.value;
                                        setMeetingEndTime(nextEnd);
                                        const diff = getDurationMinutesBetween(meetingStartTime, nextEnd);
                                        if (diff > 0) {
                                            setMeetingDurationInput(String(diff));
                                        }
                                    }}
                                    className="w-full rounded-xl border border-gray-200 px-4 py-3 text-sm text-gray-900"
                                />
                            </label>
                        </div>

                        <div className="mt-4 rounded-2xl border border-gray-100 bg-gray-50 p-4">
                            <div className="mb-2 text-xs font-black uppercase tracking-wide text-gray-400">Быстрая длительность</div>
                            <div className="flex flex-wrap gap-2">
                                {[30, 60, 90, 120].map(minutes => (
                                    <button
                                        key={minutes}
                                        type="button"
                                        onClick={() => applyMeetingDurationPreset(minutes)}
                                        className={`rounded-xl border px-3 py-2 text-sm font-bold transition ${
                                            Number(meetingDurationInput) === minutes
                                                ? 'border-indigo-200 bg-indigo-50 text-indigo-700'
                                                : 'border-transparent bg-white text-gray-600 hover:bg-indigo-50'
                                        }`}
                                    >
                                        {minutes} мин
                                    </button>
                                ))}
                            </div>
                        </div>

                        <div className="mt-4 rounded-2xl bg-gray-50 p-4">
                            <div className="mb-3 flex items-center justify-between">
                                <div>
                                    <h3 className="font-black text-gray-900">Подходящее время</h3>
                                    <p className="text-xs text-gray-500">Можно выбрать слот и подставить время в форму.</p>
                                </div>
                                <button type="button" onClick={() => void refreshMeetingSlots()} disabled={meetingSlotsLoading} className="rounded-xl bg-white px-3 py-2 text-xs font-bold text-gray-600 disabled:opacity-50">
                                    {meetingSlotsLoading ? 'Ищу...' : 'Обновить'}
                                </button>
                            </div>
                            {meetingSlots.length === 0 ? (
                                <p className="text-sm text-gray-400">{meetingSlotsLoading ? 'Подбираю варианты...' : 'Слоты не найдены.'}</p>
                            ) : (
                                <div className="grid gap-2 lg:grid-cols-2">
                                    {meetingSlots.slice(0, 4).map(slot => {
                                        const meta = suitabilityMeta[slot.suitability] ?? { label: slot.suitability, className: 'bg-white text-gray-700 border-gray-100' };
                                        return (
                                            <button
                                                key={`${slot.startTime}-${slot.endTime}`}
                                                type="button"
                                                onClick={() => {
                                                    setMeetingDate(toDateInput(slot.startTime));
                                                    setMeetingStartTime(toTimeInput(slot.startTime));
                                                    setMeetingEndTime(toTimeInput(slot.endTime));
                                                    setMeetingDurationInput(String(Math.max(15, Math.round((new Date(slot.endTime).getTime() - new Date(slot.startTime).getTime()) / 60000))));
                                                }}
                                                className={`rounded-2xl border p-4 text-left ${meta.className}`}
                                            >
                                                <div className="text-xs font-black uppercase">{meta.label}</div>
                                                <div className="mt-1 font-black text-gray-900">{formatSlot(slot)}</div>
                                                <p className="mt-1 text-xs text-gray-500">{slot.description}</p>
                                            </button>
                                        );
                                    })}
                                </div>
                            )}
                        </div>

                        <div className="mt-5 flex flex-wrap justify-between gap-2">
                            <div>
                                {meetingTarget.mode === 'edit' && meetingTarget.meetingId && (
                                    <button
                                        type="button"
                                        onClick={() => openConfirmAction({
                                            title: 'Удалить встречу',
                                            description: `Удалить встречу «${meetingTitle}»?`,
                                            confirmLabel: 'Удалить',
                                            tone: 'danger',
                                            onConfirm: async () => {
                                                await scheduleApi.deleteMeeting(meetingTarget.meetingId!);
                                                setMeetingTarget(null);
                                                showMessage('Встреча удалена');
                                                await loadNetwork();
                                            }
                                        })}
                                        className="rounded-xl bg-red-50 px-4 py-2 text-sm font-bold text-red-600"
                                    >
                                        Удалить
                                    </button>
                                )}
                            </div>
                            <button type="button" onClick={() => setMeetingTarget(null)} className="rounded-xl bg-gray-100 px-4 py-2 text-sm font-bold text-gray-600">Отмена</button>
                            <button type="submit" className="rounded-xl bg-indigo-600 px-4 py-2 text-sm font-bold text-white">
                                {meetingTarget.mode === 'edit'
                                    ? 'Сохранить встречу'
                                    : meetingTarget.kind === 'group'
                                        ? 'Отправить приглашения'
                                        : 'Отправить приглашение'}
                            </button>
                        </div>
                    </form>
                </div>
            )}

            {confirmAction && (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/50 p-4">
                    <div className="w-full max-w-md rounded-[28px] bg-white p-6 shadow-2xl">
                        <h2 className="text-2xl font-black text-gray-900">{confirmAction.title}</h2>
                        <p className="mt-2 text-sm text-gray-500">{confirmAction.description}</p>
                        <div className="mt-6 flex justify-end gap-2">
                            <button
                                type="button"
                                onClick={() => setConfirmAction(null)}
                                className="rounded-xl bg-gray-100 px-4 py-2 text-sm font-bold text-gray-600"
                            >
                                Отмена
                            </button>
                            <button
                                type="button"
                                onClick={() => void handleConfirmAction()}
                                disabled={confirmingAction}
                                className={`rounded-xl px-4 py-2 text-sm font-bold text-white disabled:opacity-50 ${confirmAction.tone === 'danger' ? 'bg-red-600' : 'bg-indigo-600'}`}
                            >
                                {confirmingAction ? 'Секунду...' : confirmAction.confirmLabel}
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
};
