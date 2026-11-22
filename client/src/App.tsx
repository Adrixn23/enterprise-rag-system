import { useState, useEffect, useCallback } from 'react';
import { useAuth } from './hooks/useAuth';
import { Navbar } from './components/layout/Navbar';
import { AuthCard } from './components/auth/AuthCard';
import { DocumentsManager } from './components/documents/DocumentsManager';
import { SemanticSearch } from './components/search/SemanticSearch';
import { RagChatWindow } from './components/chat/RagChatWindow';
import { api } from './services/api';
import type { DocumentItem } from './types/api';

export function App() {
  const { user, isAuthenticated, isLoading: authLoading, error: authError, setError: setAuthError, login, register, logout } = useAuth();
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
      />
    );
  }

  return (
    <div className="min-h-screen bg-[#07090e] text-slate-200 flex flex-col font-sans selection:bg-indigo-500 selection:text-white">
      <Navbar
        user={user}
        activeTab={activeTab}
        onTabChange={setActiveTab}
        onLogout={logout}
      />

      <main className="flex-1 max-w-6xl w-full mx-auto p-6">
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
