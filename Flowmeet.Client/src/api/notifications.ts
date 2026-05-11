import { api } from './index';
import type { NotificationDto } from '../types';

export const notificationsApi = {
    getMyNotifications: (unreadOnly = false) =>
        api.get(`/Notifications/me?unreadOnly=${unreadOnly}`) as Promise<NotificationDto[]>,
    markAsRead: (id: string) => api.put(`/Notifications/${id}/read`, {})
};
