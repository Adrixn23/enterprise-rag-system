import React from 'react';
import { Layers, LogOut, FileText, Search, MessageSquareText, Moon, Sun } from 'lucide-react';
import type { UserProfile } from '../../types/api';
import type { ThemeMode } from '../../hooks/useTheme';

interface NavbarProps {
  user: UserProfile;
  activeTab: 'documents' | 'search' | 'chat';
  documentCount: number;
  theme: ThemeMode;
  onToggleTheme: () => void;
  onTabChange: (tab: 'documents' | 'search' | 'chat') => void;
  onLogout: () => void;
}

export const Navbar: React.FC<NavbarProps> = ({
  user,
  activeTab,
  documentCount,
  theme,
  onToggleTheme,
  onTabChange,
  onLogout,
}) => {
  return (
    <header className="h-16 border-b border-[var(--border-main)] bg-[var(--bg-header)] backdrop-blur-md sticky top-0 z-20 px-4 sm:px-6 theme-slow">
      <div className="max-w-6xl mx-auto h-full flex items-center justify-between gap-4">
        <div className="flex items-center gap-2.5 min-w-0">
          <div className="w-8 h-8 rounded-xl bg-[var(--cta-bg)] text-white flex items-center justify-center shrink-0 shadow-sm theme-slow">
            <Layers className="w-4 h-4" />
          </div>
          <span className="font-display font-semibold text-base text-[var(--text-main)] truncate theme-slow">
            Enterprise RAG
          </span>
        </div>

        <nav
          aria-label="Navegación principal"
          className="flex items-center gap-1 bg-[var(--bg-subtle)] p-1 rounded-2xl border border-[var(--border-main)] theme-slow"
        >
          <button
            type="button"
            onClick={() => onTabChange('documents')}
            className={`flex items-center gap-1.5 px-3.5 py-1.5 rounded-xl text-xs font-medium cursor-pointer theme-slow ${
              activeTab === 'documents'
                ? 'bg-[var(--bg-surface)] text-[var(--text-main)] shadow-sm'
                : 'text-[var(--text-muted)] hover:text-[var(--text-main)]'
            }`}
          >
            <FileText className={`w-3.5 h-3.5 ${activeTab === 'documents' ? 'text-[var(--accent)]' : ''}`} />
            <span>Documentos</span>
            <span className="ml-0.5 px-1.5 py-0.2 rounded-md bg-[var(--bg-subtle)] text-[10px] font-mono tabular-nums text-[var(--text-muted)] theme-slow">
              {documentCount}
            </span>
          </button>

          <button
            type="button"
            onClick={() => onTabChange('search')}
            className={`flex items-center gap-1.5 px-3.5 py-1.5 rounded-xl text-xs font-medium cursor-pointer theme-slow ${
              activeTab === 'search'
                ? 'bg-[var(--bg-surface)] text-[var(--text-main)] shadow-sm'
                : 'text-[var(--text-muted)] hover:text-[var(--text-main)]'
            }`}
          >
            <Search className={`w-3.5 h-3.5 ${activeTab === 'search' ? 'text-[var(--accent)]' : ''}`} />
            <span className="hidden sm:inline">Búsqueda inteligente</span>
            <span className="sm:hidden">Buscar</span>
          </button>

          <button
            type="button"
            onClick={() => onTabChange('chat')}
            className={`flex items-center gap-1.5 px-3.5 py-1.5 rounded-xl text-xs font-medium cursor-pointer theme-slow ${
              activeTab === 'chat'
                ? 'bg-[var(--bg-surface)] text-[var(--text-main)] shadow-sm'
                : 'text-[var(--text-muted)] hover:text-[var(--text-main)]'
            }`}
          >
            <MessageSquareText className={`w-3.5 h-3.5 ${activeTab === 'chat' ? 'text-[var(--accent)]' : ''}`} />
            <span>Asistente</span>
          </button>
        </nav>

        <div className="flex items-center gap-2.5 shrink-0">
          <div className="text-right hidden md:block mr-1">
            <p className="text-xs font-semibold text-[var(--text-main)] m-0 leading-tight theme-slow">{user.fullName}</p>
            <p className="text-[11px] text-[var(--text-faint)] m-0 leading-tight theme-slow">{user.email}</p>
          </div>

          <button
            type="button"
            onClick={onToggleTheme}
            title={theme === 'dark' ? 'Cambiar a modo claro' : 'Cambiar a modo noche'}
            aria-label={theme === 'dark' ? 'Cambiar a modo claro' : 'Cambiar a modo noche'}
            className="p-2 text-[var(--text-muted)] hover:text-[var(--accent)] rounded-xl border border-[var(--border-main)] bg-[var(--bg-surface)] cursor-pointer theme-slow"
          >
            {theme === 'dark' ? <Sun className="w-3.5 h-3.5" /> : <Moon className="w-3.5 h-3.5" />}
          </button>

          <button
            type="button"
            onClick={onLogout}
            title="Cerrar sesión"
            aria-label="Cerrar sesión"
            className="p-2 text-[var(--text-muted)] hover:text-red-500 rounded-xl border border-[var(--border-main)] bg-[var(--bg-surface)] cursor-pointer theme-slow"
          >
            <LogOut className="w-3.5 h-3.5" />
          </button>
        </div>
      </div>
    </header>
  );
};
