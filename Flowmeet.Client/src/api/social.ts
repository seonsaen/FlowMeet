import { api } from './index';
import type { Friend, IncomingFriendRequest } from '../types';

export const socialApi = {
    getFriends: () => api.get('/Social/me/friends') as Promise<Friend[]>,
    getIncomingRequests: () => api.get('/Social/requests/incoming') as Promise<IncomingFriendRequest[]>,
    sendFriendRequest: (targetEmail: string) => api.post('/Social/request', { targetEmail }),
    acceptFriendRequest: (requesterId: string) => api.post('/Social/accept', { requesterId }),
    declineFriendRequest: (requesterId: string) => api.post('/Social/decline', { requesterId }),
    deleteFriend: (friendId: string) => api.delete(`/Social/me/friends/${friendId}`)
};
