import { useState } from 'react';
import type { ReactNode } from 'react';
import type { User } from '../types';
import { AuthContext } from './authContext';

export const AuthProvider = ({ children }: { children: ReactNode }) => {
    const [user, setUser] = useState<User | null>(() => {
        const saved = localStorage.getItem('user');
        return saved ? JSON.parse(saved) : null;
    });

    const login = (userData: User) => {
        localStorage.setItem('user', JSON.stringify(userData));
        setUser(userData);
    };

    const updateUser = (partialUser: Partial<User>) => {
        setUser(current => {
            if (!current) {
                return current;
            }

            const nextUser = { ...current, ...partialUser };
            localStorage.setItem('user', JSON.stringify(nextUser));
            return nextUser;
        });
    };

    const logout = () => {
        localStorage.removeItem('user');
        setUser(null);
    };

    return (
        <AuthContext.Provider value={{ user, login, updateUser, logout }}>
            {children}
        </AuthContext.Provider>
    );
};
