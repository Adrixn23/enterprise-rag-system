import React, { useState, useRef, useEffect } from 'react';
import { Bot, Sparkles, Send, ShieldCheck, AlertCircle } from 'lucide-react';
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
  const [chatError, setChatError] = useState<string | null>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const activeUserIdRef = useRef<string>(userId);
  activeUserIdRef.current = userId;

  // Sincronizar el estado del historial cuando cambie 'userId' sin desmontar el componente
  useEffect(() => {
    setChatState({
      ownerUserId: userId,
      messages: loadMessagesForUser(userId),
    });
    setChatError(null);
    setChatInput('');
    setChatLoading(false);
  }, [userId]);

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

  const handleSendMessage = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!chatInput.trim() || chatLoading) return;

    const requestUserId = userId;
    const userQuery = chatInput.trim();
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
    setChatInput('');
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
    } catch (err: unknown) {
      if (activeUserIdRef.current !== requestUserId) return;

      const msg = err instanceof Error ? err.message : 'Error desconocido al procesar respuesta';
      setChatError(msg);
      const errorMsg: ChatMessage = {
        id: (Date.now() + 1).toString(),
        role: 'assistant',
        content: `Error al procesar la respuesta: ${msg}`,
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

  const handleClearChat = () => {
    setChatState({ ownerUserId: userId, messages: [] });
    localStorage.removeItem(chatStorageKey);
  };

  return (
    <div className="h-[calc(100vh-10rem)] flex flex-col bg-[#0f131c] border border-slate-800 rounded-2xl overflow-hidden shadow-2xl">
      {/* Header del Chat */}
      <div className="px-5 py-3.5 border-b border-slate-800/80 bg-[#121622] flex items-center justify-between">
        <div className="flex items-center gap-2.5">
          <Sparkles className="w-4 h-4 text-indigo-400" />
          <span className="text-xs font-semibold text-white">Asistente Corporativo Gemini RAG</span>
        </div>
        <div className="flex items-center gap-3">
          <span className="text-[10px] text-slate-500 font-mono hidden sm:inline">gemini-embedding-001 (768d) + Gemini 2.5 Flash</span>
          {chatMessages.length > 0 && (
            <button
              onClick={handleClearChat}
              className="text-[10px] text-slate-400 hover:text-red-400 transition-colors"
            >
              Limpiar chat
            </button>
          )}
        </div>
      </div>

      {/* Mensajes */}
      <div className="flex-1 overflow-y-auto p-5 space-y-4">
        {chatMessages.length === 0 ? (
          <div className="h-full flex flex-col items-center justify-center text-center p-6 text-slate-500">
            <Bot className="w-10 h-10 text-slate-600 mb-2" />
            <p className="text-xs font-medium text-slate-300">Haz una pregunta sobre tus documentos</p>
            <p className="text-[11px] text-slate-500 mt-0.5">El sistema recuperará el contexto en Qdrant y generará una respuesta fundamentada con citas.</p>
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

                {/* Citas y Fuentes consultadas */}
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

        {chatLoading && (
          <div className="flex flex-col items-start">
            <div className="bg-[#151a27] border border-slate-800 rounded-2xl p-4 text-xs text-slate-400 flex items-center gap-2">
              <div className="w-3.5 h-3.5 border-2 border-indigo-400 border-t-transparent rounded-full animate-spin" />
              <span>Consultando índice vectorial y sintetizando respuesta...</span>
            </div>
          </div>
        )}
        <div ref={messagesEndRef} />
      </div>

      {chatError && (
        <div className="px-4 py-2 bg-red-950/40 border-t border-red-800/60 text-red-300 text-[11px] flex items-center gap-2">
          <AlertCircle className="w-3.5 h-3.5 shrink-0" />
          <span>{chatError}</span>
        </div>
      )}

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
  );
};
