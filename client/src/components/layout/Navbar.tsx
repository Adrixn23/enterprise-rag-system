import React from 'react';
import { Bot, LogOut, FileText, Search, Sparkles } from 'lucide-react';
import type { UserProfile } from '../../types/api';

interface NavbarProps {
  user: UserProfile;
  activeTab: 'documents' | 'search' | 'chat';
  onTabChange: (tab: 'documents' | 'search' | 'chat') => void;
  onLogout: () => void;
}

export const Navbar: React.FC<NavbarProps> = ({ user, activeTab, onTabChange, onLogout }) => {
  return (
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

      <nav className="flex items-center gap-1 bg-[#121622] p-1 rounded-xl border border-slate-800">
        <button
          onClick={() => onTabChange('documents')}
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
          onClick={() => onTabChange('search')}
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
          onClick={() => onTabChange('chat')}
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

      <div className="flex items-center gap-4">
        <div className="text-right hidden sm:block">
          <p className="text-xs font-medium text-white m-0 leading-tight">{user.fullName}</p>
          <p className="text-[10px] text-slate-400 m-0 leading-tight">{user.email}</p>
        </div>
        <button
          onClick={onLogout}
          title="Cerrar Sesión"
          className="p-2 text-slate-400 hover:text-red-400 hover:bg-red-500/10 rounded-xl transition-colors border border-transparent hover:border-red-500/20"
        >
          <LogOut className="w-4 h-4" />
        </button>
      </div>
    </header>
  );
};
