import { useState, useCallback } from 'react';
import { api } from '../services/api';
import type { UserProfile } from '../types/api';

export function useAuth() {
  const [user, setUser] = useState<UserProfile | null>(() => api.getCurrentUser());
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const login = useCallback(async (email: string, pass: string) => {
    setIsLoading(true);
    setError(null);
    try {
      const profile = await api.login(email, pass);
      setUser(profile);
      return profile;
    } catch (err: any) {
      setError(err.message || 'Error en la autenticación');
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const register = useCallback(async (fullName: string, email: string, pass: string, tenantId?: string) => {
    setIsLoading(true);
    setError(null);
    try {
      const userId = await api.register(fullName, email, pass, tenantId);
      return userId;
    } catch (err: any) {
      setError(err.message || 'Error en el registro');
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const logout = useCallback(() => {
    api.logout();
    setUser(null);
  }, []);

  return {
    user,
    isAuthenticated: !!user,
    isLoading,
    error,
    setError,
    login,
    register,
    logout,
  };
}
