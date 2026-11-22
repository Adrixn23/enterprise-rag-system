import React, { useState } from 'react';
import { Search, FileText, AlertCircle } from 'lucide-react';
import { api } from '../../services/api';
import type { SearchResultChunk } from '../../types/api';

export const SemanticSearch: React.FC = () => {
  const [searchQuery, setSearchQuery] = useState('');
  const [searchResults, setSearchResults] = useState<SearchResultChunk[]>([]);
  const [searchLoading, setSearchLoading] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);
  const [hasSearched, setHasSearched] = useState(false);

  const handleSearch = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!searchQuery.trim() || searchLoading) return;

    setSearchLoading(true);
    setSearchError(null);
    setHasSearched(true);
    try {
      const results = await api.search(searchQuery);
      setSearchResults(results);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Error en la búsqueda';
      setSearchError(msg);
    } finally {
      setSearchLoading(false);
    }
  };

  return (
    <div className="space-y-6 animate-soft-enter">
      <div className="bg-[var(--bg-surface)] border border-[var(--border-main)] rounded-2xl p-5 sm:p-6 shadow-[0_10px_30px_-20px_rgba(0,0,0,0.18)] space-y-4 theme-slow">
        <div>
          <h2 className="font-display text-xl sm:text-2xl font-semibold text-[var(--text-main)] m-0 theme-slow">
            Encuentra respuestas al instante
          </h2>
          <p className="text-xs sm:text-sm text-[var(--text-muted)] mt-1 m-0 theme-slow">
            Busca por idea o concepto dentro de los documentos de tu empresa y consulta su nivel de coincidencia.
          </p>
        </div>

        <form onSubmit={handleSearch} className="flex flex-col sm:flex-row gap-2.5 pt-1">
          <input
            type="text"
            value={searchQuery}
            onChange={e => setSearchQuery(e.target.value)}
            placeholder="Escribe tu consulta..."
            className="flex-1 bg-[var(--bg-input)] border border-[var(--border-main)] rounded-xl px-4 py-3 text-sm text-[var(--text-main)] placeholder-[var(--text-faint)] focus:outline-none focus:border-[var(--accent)] theme-slow"
          />
          <button
            type="submit"
            disabled={searchLoading || !searchQuery.trim()}
            className="bg-[var(--cta-bg)] hover:bg-[var(--cta-hover)] text-[var(--cta-text)] font-semibold px-5 py-3 rounded-xl text-xs sm:text-sm flex items-center justify-center gap-2 disabled:opacity-45 cursor-pointer shrink-0 shadow-sm theme-slow"
          >
            {searchLoading ? (
              <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
            ) : (
              <Search className="w-4 h-4" />
            )}
            <span>Buscar</span>
          </button>
        </form>
      </div>

      {searchError && (
        <div className="p-3.5 rounded-2xl bg-red-500/10 border border-red-500/25 text-red-600 dark:text-red-300 text-xs flex items-center gap-2.5">
          <AlertCircle className="w-4 h-4 shrink-0" />
          <span>{searchError}</span>
        </div>
      )}

      <div className="space-y-3">
        {searchResults.map((item, idx) => {
          const docName = item.documentFileName || item.fileName || 'Documento';
          return (
            <article
              key={`${item.documentId}-${item.chunkIndex}-${idx}`}
              className="bg-[var(--bg-surface)] border border-[var(--border-main)] hover:border-[var(--accent-border)] rounded-2xl p-4 sm:p-5 space-y-2.5 shadow-[0_6px_18px_-14px_rgba(0,0,0,0.16)] theme-slow"
            >
              <div className="flex items-center justify-between gap-3 text-xs">
                <span className="font-semibold text-[var(--text-main)] flex items-center gap-2 min-w-0 truncate theme-slow">
                  <FileText className="w-3.5 h-3.5 text-[var(--accent)] shrink-0" />
                  <span className="truncate">{docName}</span>
                </span>
                <span className="px-2 py-0.5 rounded-md bg-[var(--accent-soft)] border border-[var(--accent-border)] text-[var(--accent)] font-mono tabular-nums text-[11px] shrink-0 theme-slow">
                  {(item.score * 100).toFixed(1)}%
                </span>
              </div>
              <p className="text-xs sm:text-[13px] text-[var(--text-muted)] leading-relaxed m-0 max-w-[75ch] theme-slow">
                {item.content}
              </p>
            </article>
          );
        })}

        {hasSearched && !searchLoading && !searchError && searchResults.length === 0 && (
          <div className="p-10 text-center text-[var(--text-muted)] text-xs bg-[var(--bg-surface)] border border-[var(--border-main)] rounded-2xl theme-slow">
            No se encontraron fragmentos relevantes para esa búsqueda.
          </div>
        )}
      </div>
    </div>
  );
};
