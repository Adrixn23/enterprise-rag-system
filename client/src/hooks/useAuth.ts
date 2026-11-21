import { useState, useCallback, useEffect } from 'react';
import { api } from '../services/api';
import type { UserProfile } from '../types/api';

export function useAuth() {
  const [user, setUser] = useState<UserProfile | null>(() => api.getCurrentUser());
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Escuchar eventos globales de 401 para desloguear automáticamente
  useEffect(() => {
    const handleUnauthorized = () => {
      setUser(null);
      setError('Tu sesión ha expirado. Por favor inicia sesión nuevamente.');
    };

    window.addEventListener('rag_unauthorized', handleUnauthorized);
    return () => window.removeEventListener('rag_unauthorized', handleUnauthorized);
  }, []);

  const login = useCallback(async (email: string, pass: string) => {
    setIsLoading(true);
    setError(null);
    try {
      const profile = await api.login(email, pass);
      setUser(profile);
      return profile;
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Error en la autenticación';
      setError(msg);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const register = useCallback(async (fullName: string, email: string, pass: string, confirmPass: string) => {
    setIsLoading(true);
    setError(null);
    try {
      const userId = await api.register(fullName, email, pass, confirmPass);
      return userId;
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Error en el registro';
      setError(msg);
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
