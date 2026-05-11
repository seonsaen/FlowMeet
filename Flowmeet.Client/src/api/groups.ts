import { api } from './index';
import type { Group, GroupInvite } from '../types';

export const groupsApi = {
    getUserGroups: () => api.get('/Group/me') as Promise<Group[]>,
    createGroup: (data: { name: string; description?: string }) => api.post('/Group', data) as Promise<Group>,
    updateGroup: (groupId: string, data: { name: string; description?: string }) =>
        api.put(`/Group/${groupId}`, data) as Promise<Group>,
    deleteGroup: (groupId: string) => api.delete(`/Group/${groupId}`),
    leaveGroup: (groupId: string) => api.post(`/Group/${groupId}/leave`, {}) as Promise<{ message: string }>,
    updateMemberRole: (groupId: string, memberId: string, role: number) =>
        api.put(`/Group/${groupId}/members/${memberId}/role`, { role }) as Promise<Group>,
    removeMember: (groupId: string, memberId: string) =>
        api.delete(`/Group/${groupId}/members/${memberId}`) as Promise<Group>,
    inviteUser: (data: { groupId: string; inviteeId: string }) => api.post('/Group/invite', data),
    getIncomingInvites: () => api.get('/Group/invites/incoming') as Promise<GroupInvite[]>,
    respondToInvite: (data: { groupId: string; isAccepted: boolean }) => api.post('/Group/invites/respond', data)
};
