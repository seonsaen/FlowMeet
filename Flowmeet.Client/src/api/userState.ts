import { api } from './index';
import type { DashboardInsights, ResourceResponse } from '../types';

export const userStateApi = {
    getResource: () => api.get('/UserState/me/resource') as Promise<ResourceResponse>,
    getDashboardInsights: () => api.get('/Dashboard/me/insights') as Promise<DashboardInsights>,
    setMood: (data: { moodLevel: number; sleepQuality: number; backgroundLoadLevel: number }) =>
        api.post('/UserState/mood', data) as Promise<{ message: string; currentResource: number }>
};
