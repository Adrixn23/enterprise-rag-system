import { useState, useEffect, useCallback } from 'react';
import { useAuth } from './hooks/useAuth';
import { useTheme } from './hooks/useTheme';
import { Navbar } from './components/layout/Navbar';
import { AuthCard } from './components/auth/AuthCard';
import { DocumentsManager } from './components/documents/DocumentsManager';
import { SemanticSearch } from './components/search/SemanticSearch';
import { RagChatWindow } from './components/chat/RagChatWindow';
import { api } from './services/api';
import type { DocumentItem } from './types/api';

export function App() {
  const { user, isAuthenticated, isLoading: authLoading, error: authError, setError: setAuthError, login, register, logout } = useAuth();
  const { theme, toggleTheme } = useTheme();
  const [activeTab, setActiveTab] = useState<'documents' | 'search' | 'chat'>('documents');

  // Estado de Documentos
  const [documents, setDocuments] = useState<DocumentItem[]>([]);
  const [docsLoading, setDocsLoading] = useState(false);

  const fetchDocs = useCallback(async () => {
    try {
      setDocsLoading(true);
      const paged = await api.getDocuments(1, 50);
      setDocuments(paged.items);
    } catch (e: any) {
      console.error('Error fetching documents:', e);
    } finally {
      setDocsLoading(false);
    }
  }, []);

  useEffect(() => {
    if (isAuthenticated) {
      fetchDocs();
    } else {
      setDocuments([]);
    }
  }, [isAuthenticated, fetchDocs]);

  // Polling ligero mientras existan documentos en cola o procesamiento asíncrono
  useEffect(() => {
    if (!isAuthenticated) return;
    const hasActiveJobs = documents.some(d => d.status === 'Pending' || d.status === 'Processing');
    if (!hasActiveJobs) return;

    const timer = window.setInterval(() => {
      void fetchDocs();
    }, 3000);

    return () => window.clearInterval(timer);
  }, [isAuthenticated, documents, fetchDocs]);

  const handleFileUpload = async (file: File) => {
    await api.uploadDocument(file);
    await fetchDocs();
  };

  const handleDeleteDoc = async (id: string) => {
    await api.deleteDocument(id);
    await fetchDocs();
  };

  if (!isAuthenticated || !user) {
    return (
      <AuthCard
        onLogin={login}
        onRegister={register}
        isLoading={authLoading}
        error={authError}
        onErrorChange={setAuthError}
        theme={theme}
        onToggleTheme={toggleTheme}
      />
    );
  }

  return (
    <div className="min-h-screen bg-[var(--bg-canvas)] text-[var(--text-main)] flex flex-col relative theme-slow">
      <div
        aria-hidden="true"
        className="pointer-events-none fixed inset-x-0 bottom-0 h-80 crimson-bottom-wash z-0"
      />
      <Navbar
        user={user}
        activeTab={activeTab}
        documentCount={documents.length}
        theme={theme}
        onToggleTheme={toggleTheme}
        onTabChange={setActiveTab}
        onLogout={logout}
      />

      <main className="relative z-10 flex-1 max-w-6xl w-full mx-auto px-4 sm:px-6 py-7 sm:py-9 animate-soft-enter">
        {activeTab === 'documents' && (
          <DocumentsManager
            documents={documents}
            isLoading={docsLoading}
            onUpload={handleFileUpload}
            onDelete={handleDeleteDoc}
          />
        )}

        {activeTab === 'search' && <SemanticSearch />}

        {activeTab === 'chat' && <RagChatWindow userId={user.id} />}
      </main>
    </div>
  );
}

export default App;
