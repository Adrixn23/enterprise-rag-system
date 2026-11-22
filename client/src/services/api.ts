import type { UserProfile, DocumentItem, PagedResponse, SearchResultChunk, ChatResponse } from '../types/api';

const API_BASE = '/api/v1';

function getAuthHeader(): Record<string, string> {
  const token = localStorage.getItem('rag_token');
  return token ? { Authorization: `Bearer ${token}` } : {};
}

async function handleResponse<T>(res: Response): Promise<T> {
  if (res.status === 401) {
    // Expiración o token inválido: limpiar sesión y notificar
    localStorage.removeItem('rag_token');
    localStorage.removeItem('rag_user');
    window.dispatchEvent(new Event('rag_unauthorized'));
    throw new Error('Tu sesión ha expirado o no es válida. Por favor, inicia sesión nuevamente.');
  }

  if (!res.ok) {
    let errorDetail = 'Ocurrió un error en la solicitud.';
    try {
      const errJson = await res.json();
      errorDetail = errJson.detail || errJson.title || errorDetail;
    } catch {
      errorDetail = `Error ${res.status}: ${res.statusText}`;
    }
    throw new Error(errorDetail);
  }

  if (res.status === 204) {
    return {} as T;
  }

  return res.json();
}

export const api = {
  // Autenticación
  async login(email: string, password: string): Promise<UserProfile> {
    const res = await fetch(`${API_BASE}/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password }),
    });

    const data = await handleResponse<UserProfile>(res);
    localStorage.setItem('rag_token', data.token);
    localStorage.setItem('rag_user', JSON.stringify(data));
    return data;
  },

  async register(fullName: string, email: string, password: string, confirmPassword?: string): Promise<string> {
    const res = await fetch(`${API_BASE}/auth/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        fullName,
        email,
        password,
        confirmPassword: confirmPassword || password,
      }),
    });

    const data = await handleResponse<{ userId: string }>(res);
    return data.userId;
  },

  logout(): void {
    localStorage.removeItem('rag_token');
    localStorage.removeItem('rag_user');
  },

  getCurrentUser(): UserProfile | null {
    const raw = localStorage.getItem('rag_user');
    if (!raw) return null;
    try {
      return JSON.parse(raw) as UserProfile;
    } catch (e) {
      console.warn('Datos de sesión corruptos en localStorage. Limpiando...', e);
      localStorage.removeItem('rag_user');
      localStorage.removeItem('rag_token');
      return null;
    }
  },

  // Documentos
  async getDocuments(page = 1, pageSize = 20): Promise<PagedResponse<DocumentItem>> {
    const res = await fetch(`${API_BASE}/documents?page=${page}&pageSize=${pageSize}`, {
      headers: { ...getAuthHeader() },
    });
    return handleResponse<PagedResponse<DocumentItem>>(res);
  },

  async uploadDocument(file: File): Promise<DocumentItem> {
    const formData = new FormData();
    formData.append('file', file);

    const res = await fetch(`${API_BASE}/documents/upload`, {
      method: 'POST',
      headers: { ...getAuthHeader() },
      body: formData,
    });

    return handleResponse<DocumentItem>(res);
  },

  async deleteDocument(id: string): Promise<void> {
    const res = await fetch(`${API_BASE}/documents/${id}`, {
      method: 'DELETE',
      headers: { ...getAuthHeader() },
    });
    return handleResponse<void>(res);
  },

  // Búsqueda Semántica
  async search(query: string, topK = 5, minScore = 0.6): Promise<SearchResultChunk[]> {
    const res = await fetch(`${API_BASE}/search`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...getAuthHeader(),
      },
      body: JSON.stringify({ query, limit: topK, minScore }),
    });

    return handleResponse<SearchResultChunk[]>(res);
  },

  // RAG Chat
  async ask(query: string): Promise<ChatResponse> {
    const res = await fetch(`${API_BASE}/chat`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...getAuthHeader(),
      },
      body: JSON.stringify({ message: query }),
    });

    return handleResponse<ChatResponse>(res);
  },
};
