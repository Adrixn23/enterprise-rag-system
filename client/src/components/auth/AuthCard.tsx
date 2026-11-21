import React, { useState } from 'react';
import { Bot, ChevronRight, ShieldCheck } from 'lucide-react';

interface AuthCardProps {
  onLogin: (email: string, pass: string) => Promise<any>;
  onRegister: (fullName: string, email: string, pass: string, tenantId?: string) => Promise<any>;
  isLoading: boolean;
  error: string | null;
  onErrorChange: (err: string | null) => void;
}

export const AuthCard: React.FC<AuthCardProps> = ({
  onLogin,
  onRegister,
  isLoading,
  error,
  onErrorChange,
}) => {
  const [isRegisterMode, setIsRegisterMode] = useState(false);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [fullName, setFullName] = useState('');
  const [tenantId, setTenantId] = useState('');
  const [successNotice, setSuccessNotice] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    onErrorChange(null);
    setSuccessNotice(null);

    try {
      if (isRegisterMode) {
        await onRegister(
          fullName,
          email,
          password,
          tenantId.trim() ? tenantId.trim() : undefined
        );
        setIsRegisterMode(false);
        setSuccessNotice('Cuenta registrada con éxito. Ya puedes iniciar sesión.');
      } else {
        await onLogin(email, password);
      }
    } catch {
      // El error ya es manejado y expuesto a través de las props
    }
  };

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
            onClick={() => {
              setIsRegisterMode(false);
              onErrorChange(null);
              setSuccessNotice(null);
            }}
            className={`pb-3 font-medium text-sm flex-1 text-center transition-colors ${
              !isRegisterMode ? 'text-indigo-400 border-b-2 border-indigo-500' : 'text-slate-400 hover:text-slate-200'
            }`}
          >
            Iniciar Sesión
          </button>
          <button
            onClick={() => {
              setIsRegisterMode(true);
              onErrorChange(null);
              setSuccessNotice(null);
            }}
            className={`pb-3 font-medium text-sm flex-1 text-center transition-colors ${
              isRegisterMode ? 'text-indigo-400 border-b-2 border-indigo-500' : 'text-slate-400 hover:text-slate-200'
            }`}
          >
            Registrarse
          </button>
        </div>

        {error && (
          <div className="mb-5 p-3 rounded-xl bg-red-950/40 border border-red-800/60 text-red-300 text-xs leading-relaxed">
            {error}
          </div>
        )}

        {successNotice && (
          <div className="mb-5 p-3 rounded-xl bg-emerald-950/40 border border-emerald-800/60 text-emerald-300 text-xs leading-relaxed">
            {successNotice}
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
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
                <div className="flex items-center justify-between mb-1.5">
                  <label className="text-xs font-medium text-slate-300">Tenant ID (Opcional)</label>
                  <span className="text-[10px] text-slate-500 flex items-center gap-1">
                    <ShieldCheck className="w-3 h-3 text-indigo-400" /> Auto-asignado si está vacío
                  </span>
                </div>
                <input
                  type="text"
                  value={tenantId}
                  onChange={e => setTenantId(e.target.value)}
                  placeholder="Dejar en blanco para crear nuevo tenant"
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
            disabled={isLoading}
            className="w-full mt-2 bg-indigo-600 hover:bg-indigo-500 text-white font-medium py-2.5 px-4 rounded-xl text-sm transition-all shadow-lg shadow-indigo-600/20 disabled:opacity-50 flex items-center justify-center gap-2"
          >
            {isLoading ? (
              <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
            ) : (
              <>
                <span>{isRegisterMode ? 'Crear Cuenta Segura' : 'Acceder al Sistema'}</span>
                <ChevronRight className="w-4 h-4" />
              </>
            )}
          </button>
        </form>
      </div>
    </div>
  );
};
