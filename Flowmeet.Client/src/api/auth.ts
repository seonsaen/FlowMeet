import { api } from './index';
import type { PasswordResetRequestResponse, User } from '../types';

interface LoginPayload {
    email: string;
    password: string;
}

interface RegisterPayload extends LoginPayload {
    firstName: string;
    lastName: string;
}

export const authApi = {
    login: (data: LoginPayload) => api.post('/Auth/login', data) as Promise<User>,
    register: (data: RegisterPayload) => api.post('/Auth/register', data) as Promise<{ message: string }>,
    confirmRegistration: (email: string, code: string) =>
        api.post('/Auth/register/confirm', { email, code }) as Promise<{ message: string }>,
    requestPasswordReset: (email: string) =>
        api.post('/Auth/password-reset/request', { email }) as Promise<PasswordResetRequestResponse>,
    confirmPasswordReset: (email: string, code: string, newPassword: string) =>
        api.post('/Auth/password-reset/confirm', { email, code, newPassword }) as Promise<{ message: string }>
};
