import React, { useState } from 'react';
import {
  Upload,
  FileText,
  Database,
  CheckCircle2,
  Clock,
  Loader2,
  XCircle,
  Trash2,
  AlertCircle,
} from 'lucide-react';
import type { DocumentItem } from '../../types/api';

interface DocumentsManagerProps {
  documents: DocumentItem[];
  isLoading: boolean;
  onUpload: (file: File) => Promise<void>;
  onDelete: (id: string) => Promise<void>;
}

const StatusBadge: React.FC<{ status: DocumentItem['status'] }> = ({ status }) => {
  switch (status) {
    case 'Pending':
      return (
        <span className="inline-flex items-center gap-1.5 text-[11px] font-medium text-amber-600 dark:text-amber-300 bg-amber-500/10 border border-amber-500/25 px-2 py-0.5 rounded-md theme-slow">
          <Clock className="w-3 h-3" /> En cola
        </span>
      );
    case 'Processing':
      return (
        <span className="inline-flex items-center gap-1.5 text-[11px] font-medium text-[var(--accent)] bg-[var(--accent-soft)] border border-[var(--accent-border)] px-2 py-0.5 rounded-md theme-slow">
          <Loader2 className="w-3 h-3 animate-spin" /> Procesando
        </span>
      );
    case 'Failed':
      return (
        <span className="inline-flex items-center gap-1.5 text-[11px] font-medium text-red-600 dark:text-red-300 bg-red-500/10 border border-red-500/25 px-2 py-0.5 rounded-md theme-slow">
          <XCircle className="w-3 h-3" /> Error
        </span>
      );
    default:
      return (
        <span className="inline-flex items-center gap-1.5 text-[11px] font-medium text-emerald-700 dark:text-emerald-300 bg-emerald-500/10 border border-emerald-500/25 px-2 py-0.5 rounded-md theme-slow">
          <CheckCircle2 className="w-3 h-3" /> Listo para consultar
        </span>
      );
  }
};

export const DocumentsManager: React.FC<DocumentsManagerProps> = ({
  documents,
  isLoading,
  onUpload,
  onDelete,
}) => {
  const [uploadLoading, setUploadLoading] = useState(false);
  const [operationError, setOperationError] = useState<string | null>(null);
  const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setUploadLoading(true);
    setOperationError(null);
    try {
      await onUpload(file);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Error al procesar el archivo';
      setOperationError(msg);
    } finally {
      setUploadLoading(false);
      e.target.value = '';
    }
  };

  const handleConfirmDelete = async (id: string) => {
    setOperationError(null);
    setDeletingId(id);
    try {
      await onDelete(id);
      setConfirmDeleteId(null);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Error al eliminar documento';
      setOperationError(msg);
    } finally {
      setDeletingId(null);
    }
  };

  return (
    <div className="space-y-6 animate-soft-enter">
      {/* Barra superior del workspace documental */}
      <div className="bg-[var(--bg-surface)] border border-[var(--border-main)] rounded-2xl p-5 sm:p-6 shadow-[0_10px_30px_-20px_rgba(0,0,0,0.22)] flex flex-col lg:flex-row lg:items-center justify-between gap-5 theme-slow">
        <div className="space-y-1.5">
          <h2 className="font-display text-xl sm:text-2xl font-semibold text-[var(--text-main)] m-0 theme-slow">
            Biblioteca de documentos
          </h2>
          <p className="text-xs sm:text-sm text-[var(--text-muted)] m-0 max-w-xl theme-slow">
            Sube archivos PDF, TXT o MD de tu organización para organizarlos y consultarlos desde el asistente.
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-3">
          <div className="crimson-panel-gradient border border-[var(--border-main)] rounded-xl px-3.5 py-2 flex items-center gap-4 text-xs theme-slow">
            <div>
              <span className="text-[10px] text-[var(--text-faint)] block">Total</span>
              <span className="font-display font-semibold text-[var(--text-main)] tabular-nums">{documents.length}</span>
            </div>
            <div className="w-px h-6 bg-[var(--border-main)]" />
            <div>
              <span className="text-[10px] text-[var(--text-faint)] block">Listos</span>
              <span className="font-display font-semibold text-emerald-600 dark:text-emerald-400 tabular-nums">
                {documents.filter(d => d.status === 'Completed').length}
              </span>
            </div>
            <div className="w-px h-6 bg-[var(--border-main)]" />
            <div>
              <span className="text-[10px] text-[var(--text-faint)] block">En cola</span>
              <span className="font-display font-semibold text-[var(--accent)] tabular-nums">
                {documents.filter(d => d.status === 'Pending' || d.status === 'Processing').length}
              </span>
            </div>
          </div>

          <label
            className={`inline-flex items-center gap-2 bg-[var(--cta-bg)] hover:bg-[var(--cta-hover)] text-[var(--cta-text)] text-xs font-semibold px-4 py-2.5 rounded-xl shadow-sm theme-slow ${
              uploadLoading ? 'opacity-60 pointer-events-none' : 'cursor-pointer'
            }`}
          >
            {uploadLoading ? (
              <Loader2 className="w-4 h-4 animate-spin" />
            ) : (
              <Upload className="w-4 h-4" />
            )}
            <span>{uploadLoading ? 'Subiendo archivo...' : 'Subir documento'}</span>
            <input
              type="file"
              accept=".pdf,.txt,.md"
              className="hidden"
              disabled={uploadLoading}
              onChange={handleFileChange}
            />
          </label>
        </div>
      </div>

      {operationError && (
        <div className="p-3.5 rounded-2xl bg-red-500/10 border border-red-500/25 text-red-600 dark:text-red-300 text-xs flex items-center gap-2.5 animate-soft-enter">
          <AlertCircle className="w-4 h-4 shrink-0" />
          <span>{operationError}</span>
        </div>
      )}

      <div className="bg-[var(--bg-surface)] border border-[var(--border-main)] rounded-2xl overflow-hidden shadow-[0_8px_24px_-16px_rgba(0,0,0,0.18)] theme-slow">
        {isLoading ? (
          <div className="p-12 text-center text-[var(--text-muted)] text-xs flex items-center justify-center gap-2">
            <Loader2 className="w-4 h-4 animate-spin text-[var(--accent)]" />
            <span>Cargando biblioteca...</span>
          </div>
        ) : documents.length === 0 ? (
          <div className="p-16 text-center crimson-panel-gradient">
            <div className="w-11 h-11 rounded-2xl bg-[var(--bg-surface)] border border-[var(--border-main)] flex items-center justify-center mx-auto mb-3.5 text-[var(--accent)] shadow-sm theme-slow">
              <Database className="w-5 h-5" />
            </div>
            <p className="text-sm font-semibold text-[var(--text-main)] m-0 theme-slow">
              No hay documentos registrados
            </p>
            <p className="text-xs text-[var(--text-muted)] mt-1.5 m-0 theme-slow">
              Sube un archivo PDF, TXT o MD para empezar a hacerle preguntas.
            </p>
          </div>
        ) : (
          <div className="divide-y divide-[var(--border-soft)]">
            {documents.map(doc => {
              const isConfirming = confirmDeleteId === doc.id;
              const isDeleting = deletingId === doc.id;

              return (
                <div
                  key={doc.id}
                  className="px-4 sm:px-5 py-3.5 flex flex-col sm:flex-row sm:items-center justify-between gap-3 hover:bg-[var(--bg-subtle)]/60 theme-slow"
                >
                  <div className="flex items-center gap-3.5 min-w-0">
                    <div className="p-2 rounded-xl bg-[var(--bg-subtle)] text-[var(--accent)] border border-[var(--border-main)] shrink-0 theme-slow">
                      <FileText className="w-4 h-4" />
                    </div>
                    <div className="min-w-0">
                      <p className="text-sm font-semibold text-[var(--text-main)] m-0 truncate theme-slow">
                        {doc.fileName}
                      </p>
                      <div className="flex flex-wrap items-center gap-2 mt-1 text-[11px] text-[var(--text-faint)]">
                        <span className="font-mono tabular-nums">
                          {(doc.fileSizeBytes / 1024).toFixed(1)} KB
                        </span>
                        <span>•</span>
                        <StatusBadge status={doc.status} />
                      </div>
                    </div>
                  </div>

                  <div className="flex items-center gap-2 self-end sm:self-auto shrink-0">
                    {isConfirming ? (
                      <>
                        <button
                          type="button"
                          disabled={isDeleting}
                          onClick={() => handleConfirmDelete(doc.id)}
                          className="px-2.5 py-1.5 rounded-lg bg-red-600 hover:bg-red-500 text-white text-[11px] font-semibold transition-colors cursor-pointer disabled:opacity-50"
                        >
                          {isDeleting ? 'Eliminando...' : 'Confirmar'}
                        </button>
                        <button
                          type="button"
                          disabled={isDeleting}
                          onClick={() => setConfirmDeleteId(null)}
                          className="px-2.5 py-1.5 rounded-lg bg-[var(--bg-subtle)] text-[var(--text-muted)] text-[11px] font-medium transition-colors cursor-pointer"
                        >
                          Cancelar
                        </button>
                      </>
                    ) : (
                      <button
                        type="button"
                        onClick={() => setConfirmDeleteId(doc.id)}
                        className="p-2 text-[var(--text-faint)] hover:text-red-500 hover:bg-red-500/10 rounded-xl transition-colors border border-transparent hover:border-red-500/25 cursor-pointer"
                        title="Eliminar documento"
                        aria-label={`Eliminar ${doc.fileName}`}
                      >
                        <Trash2 className="w-4 h-4" />
                      </button>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
};
