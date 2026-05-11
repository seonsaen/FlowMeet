import { api } from './index';
import type { BaseScheduleEntry, BaseScheduleOccurrenceException, ProfileResponse, UpdateProfileRequest } from '../types';

export const profileApi = {
    getProfile: () => api.get('/Profile/me') as Promise<ProfileResponse>,
    updateProfile: (data: UpdateProfileRequest) => api.put('/Profile/me', data) as Promise<ProfileResponse>,
    requestEmailChange: (newEmail: string) =>
        api.post('/Profile/me/email-change/request', { newEmail }) as Promise<{ message: string }>,
    confirmEmailChange: (newEmail: string, code: string) =>
        api.post('/Profile/me/email-change/confirm', { newEmail, code }) as Promise<ProfileResponse>,
    changePassword: (currentPassword: string, newPassword: string) =>
        api.post('/Profile/me/change-password', { currentPassword, newPassword }) as Promise<{ message: string }>,
    getBaseSchedule: () => api.get('/Profile/me/base-schedule') as Promise<BaseScheduleEntry[]>,
    getBaseScheduleHistory: (fromDate: string, toDate: string) => api.get(`/Profile/me/base-schedule/history?fromDate=${fromDate}&toDate=${toDate}`) as Promise<BaseScheduleEntry[]>,
    getBaseScheduleExceptions: () => api.get('/Profile/me/base-schedule/exceptions') as Promise<BaseScheduleOccurrenceException[]>,
    updateBaseSchedule: (entries: BaseScheduleEntry[]) => api.put('/Profile/me/base-schedule', entries) as Promise<BaseScheduleEntry[]>
};
