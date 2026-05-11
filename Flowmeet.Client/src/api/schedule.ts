import { api } from './index';
import type {
    CancelBaseScheduleOccurrenceRequest,
    CreateEventRequest,
    CreateMeetingRequest,
    EventResponse,
    ManagedMeeting,
    MeetingInvite,
    OutgoingMeetingInvite,
    OverrideBaseScheduleOccurrenceRequest,
    UpdateEventRequest,
    UpdateMeetingRequest
} from '../types';

export const scheduleApi = {
    getUserSchedule: () => api.get('/Event/me') as Promise<EventResponse[]>,
    createEvent: (data: CreateEventRequest) => api.post('/Event', data),
    updateEvent: (eventId: string, data: UpdateEventRequest) => api.put(`/Event/${eventId}`, data),
    overrideBaseOccurrence: (data: OverrideBaseScheduleOccurrenceRequest) => api.post('/Event/base-occurrence/override', data),
    cancelBaseOccurrence: (data: CancelBaseScheduleOccurrenceRequest) => api.post('/Event/base-occurrence/cancel', data),
    deleteEvent: (eventId: string) => api.delete(`/Event/${eventId}`),
    
    createMeeting: (data: CreateMeetingRequest) => api.post('/Meeting', data),
    updateMeeting: (meetingId: string, data: UpdateMeetingRequest) => api.put(`/Meeting/${meetingId}`, data),
    deleteMeeting: (meetingId: string) => api.delete(`/Meeting/${meetingId}`),
    getMeetingInvites: () => api.get('/Meeting/incoming') as Promise<MeetingInvite[]>,
    getOutgoingMeetingInvites: () => api.get('/Meeting/outgoing') as Promise<OutgoingMeetingInvite[]>,
    getMyMeetings: () => api.get('/Meeting/mine') as Promise<ManagedMeeting[]>,
    getMeetingHistory: () => api.get('/Meeting/history') as Promise<ManagedMeeting[]>,
    respondToMeetingInvite: (data: { meetingId: string; isAccepted: boolean }) => api.post('/Meeting/respond', data)
};
