import React, { useState } from 'react';
import { Upload, FileText, Database, CheckCircle2, Trash2 } from 'lucide-react';
import type { DocumentItem } from '../../types/api';

interface DocumentsManagerProps {
  documents: DocumentItem[];
  isLoading: boolean;
  onUpload: (file: File) => Promise<void>;
  onDelete: (id: string) => Promise<void>;
}

export const DocumentsManager: React.FC<DocumentsManagerProps> = ({
  documents,
  isLoading,
  onUpload,
  onDelete,
}) => {
  const [uploadLoading, setUploadLoading] = useState(false);
  const [uploadError, setUploadError] = useState<string | null>(null);

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setUploadLoading(true);
    setUploadError(null);
    try {
      await onUpload(file);
    } catch (err: any) {
      setUploadError(err.message || 'Error al procesar el archivo');
    } finally {
      setUploadLoading(false);
      e.target.value = '';
    }
  };

  const handleDelete = async (id: string, fileName: string) => {
    if (!confirm(`¿Eliminar "${fileName}" y todos sus vectores asociados en Qdrant?`)) return;
    try {
      await onDelete(id);
    } catch (err: any) {
      alert(err.message || 'Error al eliminar documento');
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h2 className="text-lg font-semibold text-white m-0">Repositorio Documental</h2>
          <p className="text-xs text-slate-400 mt-1">
            Los archivos subidos son validados por Magic Bytes, divididos en chunks y vectorizados en Qdrant.
          </p>
        </div>

        <div>
          <label className="cursor-pointer inline-flex items-center gap-2 bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-medium px-4 py-2.5 rounded-xl shadow-lg shadow-indigo-600/20 transition-all disabled:opacity-50">
            <Upload className="w-4 h-4" />
            <span>{uploadLoading ? 'Procesando Archivo...' : 'Subir Documento (PDF/TXT)'}</span>
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

      {uploadError && (
        <div className="p-3.5 rounded-xl bg-red-950/40 border border-red-800/60 text-red-300 text-xs">
          {uploadError}
        </div>
      )}

      {/* Listado de Documentos */}
      <div className="bg-[#0f131c] border border-slate-800 rounded-2xl overflow-hidden shadow-xl">
        {isLoading ? (
          <div className="p-12 text-center text-slate-500 text-xs">Cargando catálogo...</div>
        ) : documents.length === 0 ? (
          <div className="p-16 text-center">
            <Database className="w-10 h-10 text-slate-600 mx-auto mb-3" />
            <p className="text-sm font-medium text-slate-300">No hay documentos registrados</p>
            <p className="text-xs text-slate-500 mt-1">Sube un archivo PDF o TXT para indexarlo en la base vectorial.</p>
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
                  onClick={() => handleDelete(doc.id, doc.fileName)}
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
  );
};
