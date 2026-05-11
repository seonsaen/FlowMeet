import { api } from './index';
import type { TimeSlot } from '../types';

export const planningApi = {
    findSlots: (data: { participantIds: string[]; startDate: string; durationMinutes: number }) =>
        api.post('/Planning/find-slots', data) as Promise<TimeSlot[]>
};
