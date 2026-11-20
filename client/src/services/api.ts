import type { UserProfile, DocumentItem, SearchResultChunk } from '../types/api';

const API_BASE = '/api/v1';

function getAuthHeader(): Record<string, string> {
  const token = localStorage.getItem('rag_token');
  return token ? { Authorization: `Bearer ${token}` } : {};
}

export const api = {
  // Autenticación
  async login(email: string, password: string): Promise<UserProfile> {
    const res = await fetch(`${API_BASE}/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password }),
    });

    if (!res.ok) {
      const err = await res.json().catch(() => ({ detail: 'Credenciales inválidas' }));
      throw new Error(err.detail || 'Fallo en la autenticación');
    }

    const data: UserProfile = await res.json();
    localStorage.setItem('rag_token', data.token);
    localStorage.setItem('rag_user', JSON.stringify(data));
    return data;
  },

  async register(fullName: string, email: string, password: string, tenantId: string): Promise<string> {
    const res = await fetch(`${API_BASE}/auth/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        fullName,
        email,
        password,
        confirmPassword: password,
        tenantId,
      }),
    });

    if (!res.ok) {
      const err = await res.json().catch(() => ({ detail: 'Error al registrar usuario' }));
      throw new Error(err.detail || 'Fallo al registrar usuario');
    }

    const data = await res.json();
    return data.userId;
  },

  logout(): void {
    localStorage.removeItem('rag_token');
    localStorage.removeItem('rag_user');
  },

  getCurrentUser(): UserProfile | null {
    const raw = localStorage.getItem('rag_user');
    return raw ? JSON.parse(raw) : null;
  },

  // Documentos
  async getDocuments(): Promise<DocumentItem[]> {
    const res = await fetch(`${API_BASE}/documents`, {
      headers: { ...getAuthHeader() },
    });
    if (!res.ok) throw new Error('Error al obtener documentos');
    return res.json();
  },

  async uploadDocument(file: File): Promise<DocumentItem> {
    const formData = new FormData();
    formData.append('file', file);

    const res = await fetch(`${API_BASE}/documents/upload`, {
      method: 'POST',
      headers: { ...getAuthHeader() },
      body: formData,
    });

    if (!res.ok) {
      const err = await res.json().catch(() => ({ detail: 'Error al subir documento' }));
      throw new Error(err.detail || 'Fallo en la carga del archivo');
    }

    return res.json();
  },

  async deleteDocument(id: string): Promise<void> {
    const res = await fetch(`${API_BASE}/documents/${id}`, {
      method: 'DELETE',
      headers: { ...getAuthHeader() },
    });
    if (!res.ok) throw new Error('Error al eliminar documento');
  },

  // Búsqueda Semántica
  async search(query: string, topK = 5, minScore = 0.6): Promise<SearchResultChunk[]> {
    const res = await fetch(`${API_BASE}/search`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...getAuthHeader(),
      },
      body: JSON.stringify({ query, topK, minScore }),
    });

    if (!res.ok) throw new Error('Error en la búsqueda semántica');
    return res.json();
  },

  // Chat RAG
  async ask(message: string, topK = 4, minScore = 0.65) {
    const res = await fetch(`${API_BASE}/chat`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...getAuthHeader(),
      },
      body: JSON.stringify({ message, topK, minScore }),
    });

    if (!res.ok) {
      const err = await res.json().catch(() => ({ detail: 'Error al consultar al asistente' }));
      throw new Error(err.detail || 'Error en la consulta');
    }

    return res.json() as Promise<{ answer: string; sources: Array<{ documentId: string; fileName: string; score: number; snippet: string }> }>;
  },
};
