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
          disabled={searchLoading || !searchQuery.trim()}
          className="bg-indigo-600 hover:bg-indigo-500 text-white font-medium px-5 rounded-xl text-sm flex items-center gap-2 transition-all disabled:opacity-50"
        >
          {searchLoading ? (
            <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
          ) : (
            <Search className="w-4 h-4" />
          )}
          <span>Buscar</span>
        </button>
      </form>

      {searchError && (
        <div className="p-3.5 rounded-xl bg-red-950/40 border border-red-800/60 text-red-300 text-xs flex items-center gap-2">
          <AlertCircle className="w-4 h-4 shrink-0" />
          <span>{searchError}</span>
        </div>
      )}

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

        {hasSearched && !searchLoading && !searchError && searchResults.length === 0 && (
          <div className="p-8 text-center text-slate-500 text-xs bg-[#0f131c] border border-slate-800 rounded-xl">
            No se encontraron fragmentos relevantes que superen el umbral de similitud.
          </div>
        )}
      </div>
    </div>
  );
};
