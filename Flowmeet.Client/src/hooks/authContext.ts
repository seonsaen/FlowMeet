import { createContext } from 'react';
import type { User } from '../types';

export interface AuthContextType {
    user: User | null;
    login: (userData: User) => void;
    updateUser: (partialUser: Partial<User>) => void;
    logout: () => void;
}

export const AuthContext = createContext<AuthContextType | undefined>(undefined);
