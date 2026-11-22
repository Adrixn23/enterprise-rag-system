import React, { useState } from 'react';
import { ArrowRight, ShieldCheck, Layers, Moon, Sun } from 'lucide-react';
import type { ThemeMode } from '../../hooks/useTheme';

interface AuthCardProps {
  onLogin: (email: string, pass: string) => Promise<any>;
  onRegister: (fullName: string, email: string, pass: string, confirmPass: string) => Promise<any>;
  isLoading: boolean;
  error: string | null;
  onErrorChange: (err: string | null) => void;
  theme: ThemeMode;
  onToggleTheme: () => void;
}

export const AuthCard: React.FC<AuthCardProps> = ({
  onLogin,
  onRegister,
  isLoading,
  error,
  onErrorChange,
  theme,
  onToggleTheme,
}) => {
  const [isRegisterMode, setIsRegisterMode] = useState(false);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [fullName, setFullName] = useState('');
  const [successNotice, setSuccessNotice] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    onErrorChange(null);
    setSuccessNotice(null);

    if (isRegisterMode && password !== confirmPassword) {
      onErrorChange('La contraseña y la confirmación no coinciden.');
      return;
    }

    try {
      if (isRegisterMode) {
        await onRegister(fullName, email, password, confirmPassword);
        setIsRegisterMode(false);
        setSuccessNotice('Organización y cuenta creadas con éxito. Ya puedes iniciar sesión.');
      } else {
        await onLogin(email, password);
      }
    } catch {
      // El error ya es manejado y expuesto a través de las props
    }
  };

  return (
    <div className="min-h-screen bg-[var(--bg-canvas)] text-[var(--text-main)] flex items-center justify-center p-4 sm:p-8 relative theme-slow">
      <div
        aria-hidden="true"
        className="pointer-events-none fixed inset-x-0 bottom-0 h-80 crimson-bottom-wash"
      />

      <div className="w-full max-w-[440px] relative animate-soft-enter">
        <div className="flex items-center justify-between gap-3 mb-6">
          <div className="flex items-center gap-3">
            <div className="w-9 h-9 rounded-xl bg-[var(--cta-bg)] text-white flex items-center justify-center shadow-sm theme-slow">
              <Layers className="w-4 h-4" />
            </div>
            <div>
              <h1 className="font-display text-lg font-semibold text-[var(--text-main)] m-0 theme-slow">
                Enterprise RAG
              </h1>
              <p className="text-xs text-[var(--text-muted)] m-0 theme-slow">
                Espacio privado de conocimiento corporativo
              </p>
            </div>
          </div>

          <button
            type="button"
            onClick={onToggleTheme}
            title={theme === 'dark' ? 'Cambiar a modo claro' : 'Cambiar a modo noche'}
            aria-label={theme === 'dark' ? 'Cambiar a modo claro' : 'Cambiar a modo noche'}
            className="p-2 rounded-xl border border-[var(--border-main)] bg-[var(--bg-surface)] text-[var(--text-muted)] hover:text-[var(--accent)] cursor-pointer theme-slow"
          >
            {theme === 'dark' ? <Sun className="w-4 h-4" /> : <Moon className="w-4 h-4" />}
          </button>
        </div>

        <div className="bg-[var(--bg-surface)] border border-[var(--border-main)] rounded-3xl p-6 sm:p-7 shadow-[0_20px_50px_-22px_rgba(0,0,0,0.35)] theme-slow">
          <div className="grid grid-cols-2 p-1 rounded-xl bg-[var(--bg-subtle)] border border-[var(--border-main)] mb-6 theme-slow">
            <button
              type="button"
              onClick={() => {
                setIsRegisterMode(false);
                onErrorChange(null);
                setSuccessNotice(null);
              }}
              className={`py-2 rounded-lg text-xs font-semibold cursor-pointer theme-slow ${
                !isRegisterMode
                  ? 'bg-[var(--bg-surface)] text-[var(--text-main)] shadow-sm'
                  : 'text-[var(--text-muted)] hover:text-[var(--text-main)]'
              }`}
            >
              Iniciar Sesión
            </button>
            <button
              type="button"
              onClick={() => {
                setIsRegisterMode(true);
                onErrorChange(null);
                setSuccessNotice(null);
              }}
              className={`py-2 rounded-lg text-xs font-semibold cursor-pointer theme-slow ${
                isRegisterMode
                  ? 'bg-[var(--bg-surface)] text-[var(--text-main)] shadow-sm'
                  : 'text-[var(--text-muted)] hover:text-[var(--text-main)]'
              }`}
            >
              Crear Organización
            </button>
          </div>

          {error && (
            <div className="mb-4 p-3 rounded-xl bg-red-500/10 border border-red-500/25 text-red-500 text-xs leading-relaxed">
              {error}
            </div>
          )}

          {successNotice && (
            <div className="mb-4 p-3 rounded-xl bg-emerald-500/10 border border-emerald-500/25 text-emerald-500 text-xs leading-relaxed">
              {successNotice}
            </div>
          )}

          <form onSubmit={handleSubmit} className="space-y-4">
            {isRegisterMode && (
              <>
                <div>
                  <label className="block text-xs font-medium text-[var(--text-muted)] mb-1.5 theme-slow">
                    Nombre completo
                  </label>
                  <input
                    type="text"
                    required
                    value={fullName}
                    onChange={e => setFullName(e.target.value)}
                    placeholder="Ej. Adrián Brito"
                    className="w-full bg-[var(--bg-input)] border border-[var(--border-main)] rounded-xl px-3.5 py-2.5 text-sm text-[var(--text-main)] placeholder-[var(--text-faint)] focus:outline-none focus:border-[var(--accent)] theme-slow"
                  />
                </div>

                <div className="p-3 rounded-xl bg-[var(--accent-soft)] border border-[var(--accent-border)] text-[11px] text-[var(--text-muted)] flex items-start gap-2 leading-relaxed theme-slow">
                  <ShieldCheck className="w-4 h-4 text-[var(--accent)] shrink-0 mt-0.5" />
                  <span>
                    Cada registro crea un <strong className="text-[var(--text-main)]">espacio privado exclusivo</strong> para los documentos de tu empresa.
                  </span>
                </div>
              </>
            )}

            <div>
              <label className="block text-xs font-medium text-[var(--text-muted)] mb-1.5 theme-slow">
                Correo electrónico
              </label>
              <input
                type="email"
                required
                value={email}
                onChange={e => setEmail(e.target.value)}
                placeholder="usuario@empresa.com"
                className="w-full bg-[var(--bg-input)] border border-[var(--border-main)] rounded-xl px-3.5 py-2.5 text-sm text-[var(--text-main)] placeholder-[var(--text-faint)] focus:outline-none focus:border-[var(--accent)] theme-slow"
              />
            </div>

            <div>
              <label className="block text-xs font-medium text-[var(--text-muted)] mb-1.5 theme-slow">
                Contraseña
              </label>
              <input
                type="password"
                required
                value={password}
                onChange={e => setPassword(e.target.value)}
                placeholder="••••••••••••"
                className="w-full bg-[var(--bg-input)] border border-[var(--border-main)] rounded-xl px-3.5 py-2.5 text-sm text-[var(--text-main)] placeholder-[var(--text-faint)] focus:outline-none focus:border-[var(--accent)] theme-slow"
              />
            </div>

            {isRegisterMode && (
              <div>
                <label className="block text-xs font-medium text-[var(--text-muted)] mb-1.5 theme-slow">
                  Confirmar contraseña
                </label>
                <input
                  type="password"
                  required
                  value={confirmPassword}
                  onChange={e => setConfirmPassword(e.target.value)}
                  placeholder="••••••••••••"
                  className="w-full bg-[var(--bg-input)] border border-[var(--border-main)] rounded-xl px-3.5 py-2.5 text-sm text-[var(--text-main)] placeholder-[var(--text-faint)] focus:outline-none focus:border-[var(--accent)] theme-slow"
                />
              </div>
            )}

            <button
              type="submit"
              disabled={isLoading}
              className="w-full mt-2 bg-[var(--cta-bg)] hover:bg-[var(--cta-hover)] text-[var(--cta-text)] font-semibold py-2.5 px-4 rounded-xl text-sm disabled:opacity-50 flex items-center justify-center gap-2 cursor-pointer shadow-sm theme-slow"
            >
              {isLoading ? (
                <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
              ) : (
                <>
                  <span>{isRegisterMode ? 'Crear organización' : 'Entrar al espacio'}</span>
                  <ArrowRight className="w-4 h-4" />
                </>
              )}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
};
