export interface UserProfile {
  id: string;
  fullName: string;
  email: string;
  roles: string[];
  token: string;
  isAuthenticated: boolean;
}

export interface DocumentItem {
  id: string;
  fileName: string;
  contentType: string;
  fileSizeBytes: number;
  status: 'Pending' | 'Processing' | 'Completed' | 'Failed';
  createdAt: string;
}

export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface SearchResultChunk {
  documentId: string;
  fileName: string;
  chunkIndex: number;
  content: string;
  score: number;
}

export interface ChatSource {
  documentId: string;
  documentFileName?: string;
  fileName?: string;
  chunkIndex?: number;
  relevanceScore?: number;
  score?: number;
  content?: string;
  snippet?: string;
}

export interface ChatMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  sources?: ChatSource[];
  timestamp: string;
}

export interface ChatResponse {
  answer: string;
  sources: ChatSource[];
}

export interface ApiError {
  status: number;
  title: string;
  detail: string;
}
