import React, { useState, useRef, useEffect } from 'react';
import { Bot, Sparkles, Send, ShieldCheck, AlertCircle, RotateCcw } from 'lucide-react';
import { api } from '../../services/api';
import type { ChatMessage } from '../../types/api';

interface RagChatWindowProps {
  userId: string;
}

interface ChatState {
  ownerUserId: string;
  messages: ChatMessage[];
}

function loadMessagesForUser(userId: string): ChatMessage[] {
  if (!userId) return [];
  try {
    const saved = localStorage.getItem(`rag_chat_history_${userId}`);
    if (!saved) return [];
    const parsed = JSON.parse(saved);
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

export const RagChatWindow: React.FC<RagChatWindowProps> = ({ userId }) => {
  const chatStorageKey = `rag_chat_history_${userId}`;

  const [chatState, setChatState] = useState<ChatState>(() => ({
    ownerUserId: userId,
    messages: loadMessagesForUser(userId),
  }));
  const [chatInput, setChatInput] = useState('');
  const [chatLoading, setChatLoading] = useState(false);
  const [elapsedMs, setElapsedMs] = useState(0);
  const [chatError, setChatError] = useState<string | null>(null);
  const [failedQuery, setFailedQuery] = useState<string | null>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!chatLoading) {
      setElapsedMs(0);
      return;
    }

    const startedAt = Date.now();
    const timer = window.setInterval(() => {
      setElapsedMs(Date.now() - startedAt);
    }, 200);

    return () => window.clearInterval(timer);
  }, [chatLoading]);

  const activeUserIdRef = useRef<string>(userId);
  activeUserIdRef.current = userId;

  // Sincronizar el estado del historial cuando cambie 'userId' sin desmontar el componente
  useEffect(() => {
    setChatState({
      ownerUserId: userId,
      messages: loadMessagesForUser(userId),
    });
    setChatError(null);
    setFailedQuery(null);
    setChatInput('');
    setChatLoading(false);
  }, [userId, chatStorageKey]);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  // Persistir únicamente cuando el estado en memoria pertenece al userId activo (previene escrituras obsoletas)
  useEffect(() => {
    if (!userId || chatState.ownerUserId !== userId) return;

    try {
      localStorage.setItem(chatStorageKey, JSON.stringify(chatState.messages));
    } catch {
      // Ignorar si el almacenamiento local está lleno
    }
    scrollToBottom();
  }, [chatState, chatLoading, chatStorageKey, userId]);

  const chatMessages = chatState.ownerUserId === userId ? chatState.messages : [];

  const formatFriendlyError = (rawMessage: string): string => {
    const trimmed = rawMessage.trim();
    if (!trimmed) {
      return 'Ocurrió un error temporal al consultar el asistente. Por favor, intenta nuevamente.';
    }
    if (trimmed.includes('{') && (trimmed.includes('UNAVAILABLE') || trimmed.includes('503'))) {
      return 'El asistente está experimentando alta demanda temporal. Puedes reintentar tu consulta en unos momentos.';
    }
    if (trimmed.includes('{')) {
      return 'Ocurrió un inconveniente temporal al procesar la solicitud. Intenta nuevamente.';
    }
    return trimmed;
  };

  const executeQuery = async (queryText: string, isRetry: boolean) => {
    if (!queryText.trim() || chatLoading) return;

    const requestUserId = userId;
    const userQuery = queryText.trim();

    if (!isRetry) {
      const userMsg: ChatMessage = {
        id: Date.now().toString(),
        role: 'user',
        content: userQuery,
        timestamp: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
      };

      setChatState(prev => ({
        ownerUserId: requestUserId,
        messages: [...(prev.ownerUserId === requestUserId ? prev.messages : []), userMsg],
      }));
    } else {
      // Al reintentar, elimina la burbuja de error anterior sin duplicar el mensaje del usuario
      setChatState(prev => {
        if (prev.ownerUserId !== requestUserId) return prev;
        const msgs = [...prev.messages];
        if (
          msgs.length > 0 &&
          msgs[msgs.length - 1].role === 'assistant' &&
          msgs[msgs.length - 1].content.startsWith('Error al procesar la respuesta:')
        ) {
          msgs.pop();
        }
        return { ownerUserId: requestUserId, messages: msgs };
      });
    }

    setChatLoading(true);
    setChatError(null);

    try {
      const res = await api.ask(userQuery);
      // Descartar respuesta si el usuario cambió durante la petición en vuelo
      if (activeUserIdRef.current !== requestUserId) return;

      const assistantMsg: ChatMessage = {
        id: (Date.now() + 1).toString(),
        role: 'assistant',
        content: res.answer,
        sources: res.sources,
        timestamp: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
      };
      setChatState(prev => ({
        ownerUserId: requestUserId,
        messages: [...(prev.ownerUserId === requestUserId ? prev.messages : []), assistantMsg],
      }));
      setChatInput('');
      setFailedQuery(null);
    } catch (err: unknown) {
      if (activeUserIdRef.current !== requestUserId) return;

      const rawMsg = err instanceof Error ? err.message : 'Error desconocido al procesar respuesta';
      const friendlyMsg = formatFriendlyError(rawMsg);
      setChatError(friendlyMsg);
      setFailedQuery(userQuery);

      const errorMsg: ChatMessage = {
        id: (Date.now() + 1).toString(),
        role: 'assistant',
        content: `Error al procesar la respuesta: ${friendlyMsg}`,
        timestamp: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
      };
      setChatState(prev => ({
        ownerUserId: requestUserId,
        messages: [...(prev.ownerUserId === requestUserId ? prev.messages : []), errorMsg],
      }));
    } finally {
      if (activeUserIdRef.current === requestUserId) {
        setChatLoading(false);
      }
    }
  };

  const handleSendMessage = async (e: React.FormEvent) => {
    e.preventDefault();
    await executeQuery(chatInput, false);
  };

  const handleRetry = async () => {
    if (!failedQuery || chatLoading) return;
    await executeQuery(failedQuery, true);
  };

  const handleClearChat = () => {
    setChatState({ ownerUserId: userId, messages: [] });
    setChatError(null);
    setFailedQuery(null);
    localStorage.removeItem(chatStorageKey);
  };

  return (
    <div className="h-[calc(100vh-9rem)] flex flex-col bg-[var(--bg-surface)] border border-[var(--border-main)] rounded-2xl overflow-hidden shadow-[0_14px_36px_-22px_rgba(0,0,0,0.22)] theme-slow animate-soft-enter">
      {/* Header del Chat */}
      <div className="px-5 py-3.5 border-b border-[var(--border-main)] bg-[var(--bg-surface)] flex items-center justify-between gap-3 theme-slow">
        <div className="flex items-center gap-2.5">
          <div className="w-7 h-7 rounded-lg bg-[var(--cta-bg)] text-white flex items-center justify-center theme-slow">
            <Sparkles className="w-3.5 h-3.5" />
          </div>
          <span className="font-display text-sm font-semibold text-[var(--text-main)] theme-slow">
            Asistente documental
          </span>
        </div>
        <div className="flex items-center gap-3">
          <span className="text-[11px] text-[var(--text-muted)] font-medium bg-[var(--bg-subtle)] border border-[var(--border-main)] px-2.5 py-0.5 rounded-md hidden sm:inline theme-slow">
            Respuestas con citas
          </span>
          {chatMessages.length > 0 && (
            <button
              type="button"
              onClick={handleClearChat}
              className="text-[11px] font-medium text-[var(--text-faint)] hover:text-red-500 transition-colors cursor-pointer"
            >
              Limpiar chat
            </button>
          )}
        </div>
      </div>

      {/* Mensajes */}
      <div className="flex-1 overflow-y-auto p-4 sm:p-6 space-y-5 bg-[var(--bg-canvas)]/55 theme-slow">
        {chatMessages.length === 0 ? (
          <div className="h-full flex flex-col items-center justify-center text-center p-6">
            <div className="w-14 h-14 rounded-2xl crimson-panel-gradient border border-[var(--border-main)] flex items-center justify-center mb-4 text-[var(--accent)] shadow-sm theme-slow">
              <Bot className="w-6 h-6" />
            </div>
            <p className="font-display text-lg font-semibold text-[var(--text-main)] m-0 theme-slow">
              Haz una pregunta sobre tus documentos
            </p>
            <p className="text-xs sm:text-sm text-[var(--text-muted)] mt-1.5 max-w-md m-0 leading-relaxed theme-slow">
              El asistente lee los documentos de tu empresa y responde citando exactamente de dónde obtuvo cada dato.
            </p>
          </div>
        ) : (
          chatMessages.map(msg => (
            <div
              key={msg.id}
              className={`flex flex-col animate-soft-enter ${msg.role === 'user' ? 'items-end' : 'items-start'}`}
            >
              <div
                className={`max-w-[85%] sm:max-w-[78%] rounded-2xl px-4 py-3.5 text-[13px] leading-relaxed theme-slow ${
                  msg.role === 'user'
                    ? 'bg-[var(--user-bubble-bg)] text-[var(--user-bubble-text)] shadow-sm'
                    : 'bg-[var(--bg-surface)] border border-[var(--border-main)] text-[var(--text-main)] shadow-[0_6px_18px_-14px_rgba(0,0,0,0.18)]'
                }`}
              >
                <p className="m-0 whitespace-pre-wrap max-w-[70ch]">{msg.content}</p>

                {/* Citas y Fuentes consultadas */}
                {msg.sources && msg.sources.length > 0 && (
                  <div className="mt-3.5 pt-3 border-t border-[var(--border-soft)]">
                    <p className="text-[11px] font-semibold text-[var(--accent)] mb-2 flex items-center gap-1.5 m-0">
                      <ShieldCheck className="w-3.5 h-3.5" /> Fuentes consultadas ({msg.sources.length})
                    </p>
                    <div className="space-y-1.5">
                      {msg.sources.map((s, i) => {
                        const fileName = s.documentFileName || s.fileName || 'Documento';
                        const rawScore = s.relevanceScore ?? s.score ?? 0;
                        const snippet = s.content || s.snippet || '';
                        return (
                          <div
                            key={i}
                            className="text-[11px] text-[var(--text-muted)] bg-[var(--bg-input)] px-3 py-2 rounded-xl border border-[var(--border-main)] theme-slow"
                          >
                            <div className="flex items-center justify-between gap-2">
                              <span className="font-semibold text-[var(--text-main)] truncate">{fileName}</span>
                              <span className="font-mono tabular-nums text-[10px] bg-[var(--accent-soft)] border border-[var(--accent-border)] text-[var(--accent)] px-1.5 py-0.5 rounded-md shrink-0">
                                {(rawScore * 100).toFixed(0)}%
                              </span>
                            </div>
                            {snippet && (
                              <p className="text-[var(--text-faint)] text-[10px] mt-1 m-0 line-clamp-2 leading-relaxed">
                                “{snippet}”
                              </p>
                            )}
                          </div>
                        );
                      })}
                    </div>
                  </div>
                )}
              </div>
              <span className="text-[10px] font-mono tabular-nums text-[var(--text-faint)] mt-1.5 px-1">
                {msg.timestamp}
              </span>
            </div>
          ))
        )}

        {chatLoading && (() => {
          const seconds = elapsedMs / 1000;
          const stage = seconds < 1.5 ? 1 : seconds < 3.5 ? 2 : 3;
          const progress =
            stage === 1
              ? Math.min(28, 10 + (seconds / 1.5) * 18)
              : stage === 2
                ? Math.min(58, 28 + ((seconds - 1.5) / 2) * 30)
                : Math.min(93, 58 + (1 - Math.exp(-(seconds - 3.5) / 12)) * 35);

          const stageLabel =
            stage === 1
              ? '1/3 · Analizando tu pregunta...'
              : stage === 2
                ? '2/3 · Buscando en tus documentos...'
                : '3/3 · Redactando la respuesta...';

          return (
            <div className="flex flex-col items-start animate-soft-enter">
              <div className="w-full max-w-md midnight-progress-gradient border border-red-950/60 rounded-2xl p-4 text-xs text-zinc-100 space-y-2.5 shadow-[0_14px_32px_-14px_rgba(0,0,0,0.65)]">
                <div className="flex items-center justify-between gap-3">
                  <div className="flex items-center gap-2">
                    <div className="w-3.5 h-3.5 border-2 border-red-400 border-t-transparent rounded-full animate-spin shrink-0" />
                    <span className="font-medium">{stageLabel}</span>
                  </div>
                  <span className="text-[11px] font-mono tabular-nums text-red-200 shrink-0">
                    {seconds.toFixed(1)}s
                  </span>
                </div>

                <div className="w-full h-1.5 bg-white/15 rounded-full overflow-hidden">
                  <div
                    className="h-full bg-gradient-to-r from-rose-400 via-red-500 to-red-600 rounded-full transition-all duration-300 ease-out"
                    style={{ width: `${progress.toFixed(0)}%` }}
                  />
                </div>

                <div className="flex items-center justify-between text-[10px] text-zinc-400">
                  <span className={stage >= 1 ? 'text-red-200 font-medium' : ''}>1. Lectura</span>
                  <span className={stage >= 2 ? 'text-red-200 font-medium' : ''}>2. Búsqueda</span>
                  <span className={stage >= 3 ? 'text-red-200 font-medium' : ''}>3. Respuesta</span>
                </div>
              </div>
            </div>
          );
        })()}
        <div ref={messagesEndRef} />
      </div>

      {chatError && (
        <div className="px-4 py-2.5 bg-red-500/10 border-t border-red-500/25 text-red-600 dark:text-red-300 text-[11px] flex items-center justify-between gap-3">
          <div className="flex items-center gap-2">
            <AlertCircle className="w-3.5 h-3.5 shrink-0" />
            <span>{chatError}</span>
          </div>
          {failedQuery && (
            <button
              type="button"
              onClick={handleRetry}
              disabled={chatLoading}
              className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg bg-red-600 hover:bg-red-500 text-white text-[11px] font-semibold transition-colors shrink-0 disabled:opacity-50 cursor-pointer"
            >
              <RotateCcw className="w-3 h-3" />
              Reintentar
            </button>
          )}
        </div>
      )}

      {/* Input del Chat */}
      <form onSubmit={handleSendMessage} className="p-3.5 border-t border-[var(--border-main)] bg-[var(--bg-surface)] flex gap-2.5 theme-slow">
        <input
          type="text"
          value={chatInput}
          onChange={e => setChatInput(e.target.value)}
          placeholder="Pregunta algo sobre los documentos de tu organización..."
          className="flex-1 bg-[var(--bg-input)] border border-[var(--border-main)] rounded-xl px-4 py-2.5 text-xs sm:text-sm text-[var(--text-main)] placeholder-[var(--text-faint)] focus:outline-none focus:border-[var(--accent)] theme-slow"
        />
        <button
          type="submit"
          disabled={chatLoading || !chatInput.trim()}
          aria-label="Enviar consulta"
          className="bg-[var(--cta-bg)] hover:bg-[var(--cta-hover)] text-[var(--cta-text)] p-2.5 px-4 rounded-xl disabled:opacity-40 flex items-center justify-center cursor-pointer shrink-0 shadow-sm theme-slow"
        >
          {chatLoading ? (
            <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
          ) : (
            <Send className="w-4 h-4" />
          )}
        </button>
      </form>
    </div>
  );
};
