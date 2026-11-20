import React, { useState } from 'react';
import { Bot, FileText, Search, Database, LogOut, Upload, Trash2, Send, ChevronRight, CheckCircle2, ShieldCheck, Sparkles } from 'lucide-react';
import { api } from './services/api';
import type { UserProfile, DocumentItem, SearchResultChunk, ChatMessage } from './types/api';

export function App() {
  const [user, setUser] = useState<UserProfile | null>(() => api.getCurrentUser());
  const [activeTab, setActiveTab] = useState<'documents' | 'search' | 'chat'>('documents');

  // Estado de Login / Registro
  const [isRegisterMode, setIsRegisterMode] = useState(false);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [fullName, setFullName] = useState('');
  const [tenantId, setTenantId] = useState('a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d');
  const [authError, setAuthError] = useState<string | null>(null);
  const [authLoading, setAuthLoading] = useState(false);

  // Estado de Documentos
  const [documents, setDocuments] = useState<DocumentItem[]>([]);
  const [docsLoading, setDocsLoading] = useState(false);
  const [uploadLoading, setUploadLoading] = useState(false);
  const [uploadError, setUploadError] = useState<string | null>(null);

  // Estado de Búsqueda
  const [searchQuery, setSearchQuery] = useState('');
  const [searchResults, setSearchResults] = useState<SearchResultChunk[]>([]);
  const [searchLoading, setSearchLoading] = useState(false);

  // Estado de Chat
  const [chatMessages, setChatMessages] = useState<ChatMessage[]>([]);
  const [chatInput, setChatInput] = useState('');
  const [chatLoading, setChatLoading] = useState(false);

  // Cargar documentos al autenticar
  const fetchDocs = async () => {
    try {
      setDocsLoading(true);
      const docs = await api.getDocuments();
      setDocuments(docs);
    } catch (e: any) {
      console.error(e);
    } finally {
      setDocsLoading(false);
    }
  };

  React.useEffect(() => {
    if (user) {
      fetchDocs();
    }
  }, [user]);

  // Manejadores de autenticación
  const handleAuthSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setAuthError(null);
    setAuthLoading(true);

    try {
      if (isRegisterMode) {
        await api.register(fullName, email, password, tenantId);
        setIsRegisterMode(false);
        setAuthError('Registro exitoso. Inicia sesión con tus credenciales.');
      } else {
        const profile = await api.login(email, password);
        setUser(profile);
      }
    } catch (err: any) {
      setAuthError(err.message || 'Error en la operación');
    } finally {
      setAuthLoading(false);
    }
  };

  const handleLogout = () => {
    api.logout();
    setUser(null);
    setDocuments([]);
    setSearchResults([]);
    setChatMessages([]);
  };

  // Subida de archivos
  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setUploadLoading(true);
    setUploadError(null);
    try {
      await api.uploadDocument(file);
      await fetchDocs();
    } catch (err: any) {
      setUploadError(err.message);
    } finally {
      setUploadLoading(false);
      e.target.value = '';
    }
  };

  // Eliminación de documento
  const handleDeleteDoc = async (id: string) => {
    if (!confirm('¿Eliminar este documento y sus vectores indexados?')) return;
    try {
      await api.deleteDocument(id);
      await fetchDocs();
    } catch (err: any) {
      alert(err.message);
    }
  };

  // Búsqueda Semántica
  const handleSearch = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!searchQuery.trim()) return;

    setSearchLoading(true);
    try {
      const results = await api.search(searchQuery);
      setSearchResults(results);
    } catch (err: any) {
      alert(err.message);
    } finally {
      setSearchLoading(false);
    }
  };

  // Envío de mensaje en Chat RAG
  const handleSendMessage = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!chatInput.trim() || chatLoading) return;

    const userMsg: ChatMessage = {
      id: Date.now().toString(),
      role: 'user',
      content: chatInput,
      timestamp: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
    };

    setChatMessages(prev => [...prev, userMsg]);
    const messageToSend = chatInput;
    setChatInput('');
    setChatLoading(true);

    try {
      const res = await api.ask(messageToSend);
      const assistantMsg: ChatMessage = {
        id: (Date.now() + 1).toString(),
        role: 'assistant',
        content: res.answer,
        sources: res.sources,
        timestamp: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
      };
      setChatMessages(prev => [...prev, assistantMsg]);
    } catch (err: any) {
      const errorMsg: ChatMessage = {
        id: (Date.now() + 1).toString(),
        role: 'assistant',
        content: `Error: ${err.message}`,
        timestamp: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
      };
      setChatMessages(prev => [...prev, errorMsg]);
    } finally {
      setChatLoading(false);
    }
  };

  // Pantalla de Autenticación
  if (!user) {
    return (
      <div className="min-h-screen bg-[#07090e] text-slate-200 flex flex-col justify-center items-center px-4 font-sans selection:bg-indigo-500 selection:text-white">
        <div className="w-full max-w-md bg-[#0f131c] border border-slate-800 rounded-2xl p-8 shadow-2xl backdrop-blur-xl">
          <div className="flex items-center gap-3 mb-6">
            <div className="p-2.5 bg-indigo-500/10 border border-indigo-500/30 rounded-xl text-indigo-400">
              <Bot className="w-6 h-6" />
            </div>
            <div>
              <h1 className="text-xl font-semibold tracking-tight text-white m-0">Enterprise RAG</h1>
              <p className="text-xs text-slate-400 m-0">Sistema Multitenant & Asistente B2B</p>
            </div>
          </div>

          <div className="flex border-b border-slate-800 mb-6">
            <button
              onClick={() => { setIsRegisterMode(false); setAuthError(null); }}
              className={`pb-3 font-medium text-sm flex-1 text-center transition-colors ${
                !isRegisterMode ? 'text-indigo-400 border-b-2 border-indigo-500' : 'text-slate-400 hover:text-slate-200'
              }`}
            >
              Iniciar Sesión
            </button>
            <button
              onClick={() => { setIsRegisterMode(true); setAuthError(null); }}
              className={`pb-3 font-medium text-sm flex-1 text-center transition-colors ${
                isRegisterMode ? 'text-indigo-400 border-b-2 border-indigo-500' : 'text-slate-400 hover:text-slate-200'
              }`}
            >
              Registrarse
            </button>
          </div>

          {authError && (
            <div className="mb-5 p-3 rounded-xl bg-red-950/40 border border-red-800/60 text-red-300 text-xs leading-relaxed">
              {authError}
            </div>
          )}

          <form onSubmit={handleAuthSubmit} className="space-y-4">
            {isRegisterMode && (
              <>
                <div>
                  <label className="block text-xs font-medium text-slate-300 mb-1.5">Nombre Completo</label>
                  <input
                    type="text"
                    required
                    value={fullName}
                    onChange={e => setFullName(e.target.value)}
                    placeholder="Ej. Juan Pérez"
                    className="w-full bg-[#151a27] border border-slate-800 rounded-xl px-3.5 py-2.5 text-sm text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500 transition-colors"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-slate-300 mb-1.5">Tenant ID (Identificador de Organización)</label>
                  <input
                    type="text"
                    required
                    value={tenantId}
                    onChange={e => setTenantId(e.target.value)}
                    className="w-full bg-[#151a27] border border-slate-800 rounded-xl px-3.5 py-2.5 text-sm font-mono text-slate-300 placeholder-slate-500 focus:outline-none focus:border-indigo-500 transition-colors"
                  />
                </div>
              </>
            )}

            <div>
              <label className="block text-xs font-medium text-slate-300 mb-1.5">Correo Electrónico</label>
              <input
                type="email"
                required
                value={email}
                onChange={e => setEmail(e.target.value)}
                placeholder="usuario@empresa.com"
                className="w-full bg-[#151a27] border border-slate-800 rounded-xl px-3.5 py-2.5 text-sm text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500 transition-colors"
              />
            </div>

            <div>
              <label className="block text-xs font-medium text-slate-300 mb-1.5">Contraseña</label>
              <input
                type="password"
                required
                value={password}
                onChange={e => setPassword(e.target.value)}
                placeholder="••••••••••••"
                className="w-full bg-[#151a27] border border-slate-800 rounded-xl px-3.5 py-2.5 text-sm text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500 transition-colors"
              />
            </div>

            <button
              type="submit"
              disabled={authLoading}
              className="w-full mt-2 bg-indigo-600 hover:bg-indigo-500 text-white font-medium py-2.5 px-4 rounded-xl text-sm transition-all shadow-lg shadow-indigo-600/20 disabled:opacity-50 flex items-center justify-center gap-2"
            >
              {authLoading ? (
                <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
              ) : (
                <>
                  <span>{isRegisterMode ? 'Crear Cuenta' : 'Acceder al Sistema'}</span>
                  <ChevronRight className="w-4 h-4" />
                </>
              )}
            </button>
          </form>
        </div>
      </div>
    );
  }

  // Interfaz Principal de la Aplicación
  return (
    <div className="min-h-screen bg-[#07090e] text-slate-200 flex flex-col font-sans selection:bg-indigo-500 selection:text-white">
      {/* Barra de Navegación Superior */}
      <header className="h-16 border-b border-slate-800/80 bg-[#0c1018]/90 backdrop-blur sticky top-0 z-20 px-6 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="p-2 bg-indigo-500/10 border border-indigo-500/30 rounded-xl text-indigo-400">
            <Bot className="w-5 h-5" />
          </div>
          <div>
            <span className="font-semibold text-white tracking-tight">Enterprise RAG</span>
            <span className="text-xs text-slate-500 ml-2 border border-slate-800 px-2 py-0.5 rounded-full">v1.0 .NET 9</span>
          </div>
        </div>

        {/* Pestañas de Navegación */}
        <nav className="flex items-center gap-1 bg-[#121622] p-1 rounded-xl border border-slate-800">
          <button
            onClick={() => setActiveTab('documents')}
            className={`flex items-center gap-2 px-3.5 py-1.5 rounded-lg text-xs font-medium transition-all ${
              activeTab === 'documents'
                ? 'bg-indigo-600 text-white shadow-sm'
                : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/50'
            }`}
          >
            <FileText className="w-3.5 h-3.5" />
            <span>Documentos</span>
          </button>
          <button
            onClick={() => setActiveTab('search')}
            className={`flex items-center gap-2 px-3.5 py-1.5 rounded-lg text-xs font-medium transition-all ${
              activeTab === 'search'
                ? 'bg-indigo-600 text-white shadow-sm'
                : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/50'
            }`}
          >
            <Search className="w-3.5 h-3.5" />
            <span>Búsqueda Vectorial</span>
          </button>
          <button
            onClick={() => setActiveTab('chat')}
            className={`flex items-center gap-2 px-3.5 py-1.5 rounded-lg text-xs font-medium transition-all ${
              activeTab === 'chat'
                ? 'bg-indigo-600 text-white shadow-sm'
                : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/50'
            }`}
          >
            <Sparkles className="w-3.5 h-3.5" />
            <span>RAG Chat</span>
          </button>
        </nav>

        {/* Perfil & Logout */}
        <div className="flex items-center gap-4">
          <div className="text-right hidden sm:block">
            <p className="text-xs font-medium text-white m-0 leading-tight">{user.fullName}</p>
            <p className="text-[10px] text-slate-400 m-0 leading-tight">{user.email}</p>
          </div>
          <button
            onClick={handleLogout}
            title="Cerrar Sesión"
            className="p-2 text-slate-400 hover:text-red-400 hover:bg-red-500/10 rounded-xl transition-colors border border-transparent hover:border-red-500/20"
          >
            <LogOut className="w-4 h-4" />
          </button>
        </div>
      </header>

      {/* Contenedor Principal */}
      <main className="flex-1 max-w-6xl w-full mx-auto p-6">
        {/* PESTAÑA: DOCUMENTOS */}
        {activeTab === 'documents' && (
          <div className="space-y-6">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
              <div>
                <h2 className="text-lg font-semibold text-white m-0">Repositorio Documental</h2>
                <p className="text-xs text-slate-400 mt-1">
                  Los archivos subidos son validados por Magic Bytes, divididos en chunks y vectorizados en Qdrant.
                </p>
              </div>

              <div>
                <label className="cursor-pointer inline-flex items-center gap-2 bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-medium px-4 py-2.5 rounded-xl shadow-lg shadow-indigo-600/20 transition-all">
                  <Upload className="w-4 h-4" />
                  <span>{uploadLoading ? 'Procesando Archivo...' : 'Subir Documento (PDF/TXT)'}</span>
                  <input
                    type="file"
                    accept=".pdf,.txt,.md"
                    className="hidden"
                    disabled={uploadLoading}
                    onChange={handleFileUpload}
                  />
                </label>
              </div>
            </div>

            {uploadError && (
              <div className="p-3.5 rounded-xl bg-red-950/40 border border-red-800/60 text-red-300 text-xs">
                {uploadError}
              </div>
            )}

            {/* Listado de Documentos */}
            <div className="bg-[#0f131c] border border-slate-800 rounded-2xl overflow-hidden shadow-xl">
              {docsLoading ? (
                <div className="p-12 text-center text-slate-500 text-xs">Cargando catálogo...</div>
              ) : documents.length === 0 ? (
                <div className="p-16 text-center">
                  <Database className="w-10 h-10 text-slate-600 mx-auto mb-3" />
                  <p className="text-sm font-medium text-slate-300">No hay documentos registrados</p>
                  <p className="text-xs text-slate-500 mt-1">Sube un archivo PDF para indexarlo en la base vectorial.</p>
                </div>
              ) : (
                <div className="divide-y divide-slate-800/70">
                  {documents.map(doc => (
                    <div key={doc.id} className="p-4 flex items-center justify-between hover:bg-slate-800/20 transition-colors">
                      <div className="flex items-center gap-3.5">
                        <div className="p-2 rounded-lg bg-indigo-500/10 text-indigo-400 border border-indigo-500/20">
                          <FileText className="w-5 h-5" />
                        </div>
                        <div>
                          <p className="text-sm font-medium text-white m-0">{doc.fileName}</p>
                          <div className="flex items-center gap-2 mt-1 text-[11px] text-slate-400">
                            <span>{(doc.fileSizeBytes / 1024).toFixed(1)} KB</span>
                            <span>•</span>
                            <span className="flex items-center gap-1 text-emerald-400">
                              <CheckCircle2 className="w-3 h-3" /> Indexado en Qdrant
                            </span>
                          </div>
                        </div>
                      </div>

                      <button
                        onClick={() => handleDeleteDoc(doc.id)}
                        className="p-2 text-slate-500 hover:text-red-400 hover:bg-red-500/10 rounded-lg transition-colors"
                        title="Eliminar documento y vectores"
                      >
                        <Trash2 className="w-4 h-4" />
                      </button>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        )}

        {/* PESTAÑA: BÚSQUEDA SEMÁNTICA */}
        {activeTab === 'search' && (
          <div className="space-y-6">
            <div>
              <h2 className="text-lg font-semibold text-white m-0">Recuperación Semántica Vectorial</h2>
              <p className="text-xs text-slate-400 mt-1">
                Consulta los fragmentos más relevantes ordenados por similitud coseno directa en Qdrant.
              </p>
            </div>

            <form onSubmit={handleSearch} className="flex gap-2">
              <input
                type="text"
                value={searchQuery}
                onChange={e => setSearchQuery(e.target.value)}
                placeholder="Escribe tu consulta conceptual..."
                className="flex-1 bg-[#0f131c] border border-slate-800 rounded-xl px-4 py-3 text-sm text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500 transition-colors"
              />
              <button
                type="submit"
                disabled={searchLoading}
                className="bg-indigo-600 hover:bg-indigo-500 text-white font-medium px-5 rounded-xl text-sm flex items-center gap-2 transition-all disabled:opacity-50"
              >
                {searchLoading ? <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" /> : <Search className="w-4 h-4" />}
                <span>Buscar</span>
              </button>
            </form>

            <div className="space-y-3">
              {searchResults.map((item, idx) => (
                <div key={idx} className="bg-[#0f131c] border border-slate-800 rounded-xl p-4.5 space-y-2">
                  <div className="flex items-center justify-between text-xs">
                    <span className="font-semibold text-indigo-400 flex items-center gap-1.5">
                      <FileText className="w-3.5 h-3.5" />
                      {item.fileName}
                    </span>
                    <span className="px-2 py-0.5 rounded-full bg-emerald-500/10 border border-emerald-500/30 text-emerald-400 font-mono text-[10px]">
                      Score: {(item.score * 100).toFixed(1)}%
                    </span>
                  </div>
                  <p className="text-xs text-slate-300 leading-relaxed font-sans m-0">{item.content}</p>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* PESTAÑA: RAG CHAT */}
        {activeTab === 'chat' && (
          <div className="h-[calc(100vh-10rem)] flex flex-col bg-[#0f131c] border border-slate-800 rounded-2xl overflow-hidden shadow-2xl">
            {/* Header del Chat */}
            <div className="px-5 py-3.5 border-b border-slate-800/80 bg-[#121622] flex items-center justify-between">
              <div className="flex items-center gap-2.5">
                <Sparkles className="w-4 h-4 text-indigo-400" />
                <span className="text-xs font-semibold text-white">Asistente Corporativo Gemini</span>
              </div>
              <span className="text-[10px] text-slate-500 font-mono">text-embedding-004 + Gemini</span>
            </div>

            {/* Mensajes */}
            <div className="flex-1 overflow-y-auto p-5 space-y-4">
              {chatMessages.length === 0 ? (
                <div className="h-full flex flex-col items-center justify-center text-center p-6 text-slate-500">
                  <Bot className="w-10 h-10 text-slate-600 mb-2" />
                  <p className="text-xs font-medium text-slate-300">Haz una pregunta sobre tus documentos</p>
                  <p className="text-[11px] text-slate-500 mt-0.5">El sistema recuperará el contexto en Qdrant y generará una respuesta fundamentada.</p>
                </div>
              ) : (
                chatMessages.map(msg => (
                  <div
                    key={msg.id}
                    className={`flex flex-col ${msg.role === 'user' ? 'items-end' : 'items-start'}`}
                  >
                    <div
                      className={`max-w-[85%] rounded-2xl p-4 text-xs leading-relaxed ${
                        msg.role === 'user'
                          ? 'bg-indigo-600 text-white'
                          : 'bg-[#151a27] border border-slate-800 text-slate-200'
                      }`}
                    >
                      <p className="m-0 whitespace-pre-wrap">{msg.content}</p>

                      {/* Citas y Fuentes */}
                      {msg.sources && msg.sources.length > 0 && (
                        <div className="mt-3 pt-3 border-t border-slate-700/60">
                          <p className="text-[10px] font-semibold text-indigo-300 mb-1.5 flex items-center gap-1">
                            <ShieldCheck className="w-3 h-3" /> Fuentes Consultadas ({msg.sources.length}):
                          </p>
                          <div className="space-y-1">
                            {msg.sources.map((s, i) => (
                              <div key={i} className="text-[10px] text-slate-400 bg-slate-900/50 p-1.5 rounded border border-slate-800">
                                <span className="font-medium text-slate-300">{s.fileName}</span> (Score: {(s.score * 100).toFixed(0)}%)
                                <p className="italic text-slate-400 text-[9px] mt-0.5 m-0 line-clamp-2">"{s.snippet}"</p>
                              </div>
                            ))}
                          </div>
                        </div>
                      )}
                    </div>
                    <span className="text-[9px] text-slate-600 mt-1 px-1">{msg.timestamp}</span>
                  </div>
                ))
              )}
            </div>

            {/* Input del Chat */}
            <form onSubmit={handleSendMessage} className="p-3.5 border-t border-slate-800 bg-[#121622] flex gap-2">
              <input
                type="text"
                value={chatInput}
                onChange={e => setChatInput(e.target.value)}
                placeholder="Pregunta algo sobre los expedientes..."
                className="flex-1 bg-[#151a27] border border-slate-800 rounded-xl px-4 py-2.5 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500 transition-colors"
              />
              <button
                type="submit"
                disabled={chatLoading || !chatInput.trim()}
                className="bg-indigo-600 hover:bg-indigo-500 text-white p-2.5 rounded-xl disabled:opacity-40 transition-all flex items-center justify-center"
              >
                {chatLoading ? <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" /> : <Send className="w-4 h-4" />}
              </button>
            </form>
          </div>
        )}
      </main>
    </div>
  );
}

export default App;
