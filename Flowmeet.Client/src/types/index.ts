export interface User {
    id: string;
    email: string;
    firstName: string;
    lastName?: string;
    token?: string;
}

export interface BaseScheduleEntry {
    id?: string;
    dayOfWeek: number;
    title: string;
    description?: string;
    startTime: string;
    endTime: string;
    type: number;
    effectiveFromDate?: string;
    effectiveToDate?: string | null;
}

export interface BaseScheduleOccurrenceException {
    id: string;
    baseScheduleEntryId: string;
    date: string;
    overrideEventId?: string | null;
}

export type EventTypeName = 'Mandatory' | 'Flexible' | 'Desirable';

export interface EventResponse {
    id: string;
    title: string;
    description?: string;
    startTime: string;
    endTime: string;
    type: EventTypeName | string;
    source?: 'event' | 'meeting';
    isEditable?: boolean;
}

export interface CreateEventRequest {
    title: string;
    description?: string;
    startTime: string;
    endTime: string;
    type: number;
}

export type UpdateEventRequest = CreateEventRequest;

export interface OverrideBaseScheduleOccurrenceRequest {
    baseScheduleEntryId: string;
    occurrenceDate: string;
    title: string;
    description?: string;
    startTime: string;
    endTime: string;
    type: number;
}

export interface CancelBaseScheduleOccurrenceRequest {
    baseScheduleEntryId: string;
    occurrenceDate: string;
}

export interface ProfileResponse {
    id: string;
    email: string;
    firstName: string;
    lastName: string;
    settingsJson: string;
}

export interface UpdateProfileRequest {
    firstName?: string;
    lastName?: string;
    settingsJson?: string;
}

export interface NotificationSettings {
    emailNotifications: boolean;
    meetingInvites: boolean;
    groupInvites: boolean;
    reminders: boolean;
}

export type MeetingTimePreset = 'morning' | 'day' | 'evening' | 'any';

export interface MeetingPreferences {
    preset: MeetingTimePreset;
    earliestTime: string;
    latestTime: string;
}

export interface UserSettings extends NotificationSettings {
    meetingPreferences: MeetingPreferences;
    meetingReminderLeadMinutes: number;
}

export interface ScheduledMeetingCard {
    meetingId: string;
    title: string;
    description?: string;
    startTime: string;
    endTime: string;
    relatedGroupId?: string | null;
    relatedGroupName?: string | null;
}

export interface Friend {
    id: string;
    fullName: string;
    email: string;
    status: string;
    isBusy: boolean;
    nearestAvailableSlot?: TimeSlot | null;
    upcomingMeeting?: ScheduledMeetingCard | null;
    earlierAvailableSlot?: TimeSlot | null;
}

export interface IncomingFriendRequest {
    requesterId: string;
    requesterName: string;
    requesterEmail: string;
}

export interface GroupMember {
    userId: string;
    name: string;
    email: string;
    role: number;
    isOwner: boolean;
    joinDate: string;
}

export interface Group {
    id: string;
    name: string;
    description?: string;
    members: GroupMember[];
    upcomingMeeting?: ScheduledMeetingCard | null;
    earlierAvailableSlot?: TimeSlot | null;
}

export interface GroupInvite {
    groupId: string;
    groupName: string;
    inviterId: string;
    inviterName: string;
    createdDate: string;
}

export interface MeetingInvite {
    meetingId: string;
    organizerName: string;
    title: string;
    description?: string;
    startTime: string;
    endTime: string;
}

export interface OutgoingMeetingInvite {
    meetingId: string;
    title: string;
    description?: string;
    startTime: string;
    endTime: string;
    pendingParticipantsCount: number;
    acceptedParticipantsCount: number;
    totalParticipantsCount: number;
    pendingParticipantNames: string[];
}

export interface CreateMeetingRequest {
    title: string;
    description?: string;
    startTime: string;
    endTime: string;
    participantIds: string[];
    groupId?: string;
}

export type UpdateMeetingRequest = CreateMeetingRequest;

export interface ResourceResponse {
    resourceLevel: number;
    rawBalance: number;
    statusMessage: string;
    moodLevel: number;
    sleepQuality: number;
    backgroundLoadLevel: number;
}

export interface TimeSlot {
    startTime: string;
    endTime: string;
    suitability: 'Optimal' | 'RequiresMoving' | 'LowEnergy' | 'Compromise' | string;
    description: string;
}

export type NotificationType = 'Info' | 'FriendRequest' | 'GroupInvite' | 'MeetingInvite' | number;

export interface NotificationDto {
    id: string;
    type: NotificationType;
    title: string;
    message: string;
    relatedEntityId?: string | null;
    scheduledFor?: string | null;
    createdAt: string;
    readAt?: string | null;
    isRead: boolean;
}

export interface PasswordResetRequestResponse {
    message: string;
}

export interface ManagedMeeting {
    meetingId: string;
    title: string;
    description?: string;
    startTime: string;
    endTime: string;
    status: number;
    organizerId: string;
    organizerName: string;
    relatedGroupId?: string | null;
    relatedGroupName?: string | null;
    canEdit: boolean;
    participantIds: string[];
    participantNames: string[];
}

export interface FrequentParticipant {
    userId: string;
    name: string;
    meetingsCount: number;
    lastMeetingAt?: string | null;
}

export interface DashboardRecommendation {
    type: string;
    title: string;
    message: string;
    userId?: string | null;
    userName?: string | null;
    suggestedStartTime?: string | null;
    suggestedEndTime?: string | null;
}

export interface DashboardInsights {
    meetingsLast30Days: number;
    totalPastMeetings: number;
    confirmedFutureMeetings: number;
    averageMoodLast14Days?: number | null;
    averageResourceLast14Days?: number | null;
    overloadWarning?: string | null;
    frequentParticipants: FrequentParticipant[];
    recommendations: DashboardRecommendation[];
}
