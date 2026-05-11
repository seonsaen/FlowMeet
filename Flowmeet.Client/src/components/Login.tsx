import { type FormEvent, useState } from 'react';
import { authApi } from '../api/auth';
import { useAuth } from '../hooks/useAuth';
import { useNavigate, Link } from 'react-router-dom';

export function Login() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [isResetOpen, setIsResetOpen] = useState(false);
  const [resetEmail, setResetEmail] = useState('');
  const [resetCode, setResetCode] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [resetInfo, setResetInfo] = useState('');
  const [resetError, setResetError] = useState('');
  const [resetLoading, setResetLoading] = useState(false);
  
  const { login } = useAuth();
  const navigate = useNavigate();

  const openReset = () => {
    setResetEmail(email);
    setNewPassword('');
    setResetInfo('');
    setResetError('');
    setResetCode('');
    setIsResetOpen(true);
  };

  const closeReset = () => {
    setIsResetOpen(false);
    setResetInfo('');
    setResetError('');
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    setIsLoading(true);

    try {
      const response = await authApi.login({ email, password });
      login(response);
      navigate('/dashboard');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Не удалось войти');
    } finally {
      setIsLoading(false);
    }
  };

  const handleResetRequest = async (event: FormEvent) => {
    event.preventDefault();
    setResetLoading(true);
    setResetError('');
    setResetInfo('');

    try {
      const response = await authApi.requestPasswordReset(resetEmail);
      setResetInfo(response.message);
      setResetCode('');
      setNewPassword('');
    } catch (err) {
      setResetError(err instanceof Error ? err.message : 'Не удалось запросить сброс пароля');
    } finally {
      setResetLoading(false);
    }
  };

  const handleResetConfirm = async (event: FormEvent) => {
    event.preventDefault();
    setResetLoading(true);
    setResetError('');
    setResetInfo('');

    try {
      const response = await authApi.confirmPasswordReset(resetEmail, resetCode, newPassword);
      setResetInfo(response.message);
      setPassword('');
      setTimeout(() => {
        closeReset();
      }, 1200);
    } catch (err) {
      setResetError(err instanceof Error ? err.message : 'Не удалось обновить пароль');
    } finally {
      setResetLoading(false);
    }
  };

  return (
    <>
      <div className="w-full max-w-sm bg-white p-6 sm:p-8 rounded-3xl shadow-xl border border-gray-100 mx-auto">
        <div className="flex justify-center mb-4 text-indigo-600">
          <div className="w-12 h-12 bg-indigo-50 rounded-2xl flex items-center justify-center text-xl font-black">F</div>
        </div>
        <h2 className="text-2xl font-bold text-gray-800 text-center mb-1">С возвращением</h2>
        <p className="text-gray-500 text-sm text-center mb-6">Войдите в свой аккаунт FlowMeet</p>

        {error && <div className="bg-red-50 text-red-600 p-3 rounded-xl text-sm mb-4 border border-red-100 text-center">{error}</div>}

        <form onSubmit={handleSubmit}>
          <div className="mb-4">
            <label className="block text-sm font-bold text-gray-700 mb-1.5 ml-1">Email</label>
            <input
              type="email"
              className="w-full bg-gray-50 border border-gray-200 rounded-xl px-4 py-3 text-sm text-gray-900 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:bg-white transition-colors"
              placeholder="you@example.com"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
            />
          </div>

          <div className="mb-2">
            <label className="block text-sm font-bold text-gray-700 mb-1.5 ml-1">Пароль</label>
            <input
              type="password"
              className="w-full bg-gray-50 border border-gray-200 rounded-xl px-4 py-3 text-sm text-gray-900 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:bg-white transition-colors"
              placeholder="••••••••"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
            />
          </div>

          <div className="mb-6 flex justify-end">
            <button
              type="button"
              onClick={openReset}
              className="text-sm font-bold text-indigo-600 hover:underline"
            >
              Забыли пароль?
            </button>
          </div>

          <button
            type="submit"
            className="w-full bg-indigo-600 hover:bg-indigo-700 active:scale-[0.98] text-white font-bold py-3 rounded-xl transition-all shadow-md shadow-indigo-200 disabled:opacity-50 disabled:active:scale-100 flex justify-center items-center"
            disabled={isLoading}
          >
            {isLoading ? 'Вход...' : 'Войти'}
          </button>
        </form>

        <div className="text-center mt-6 text-sm text-gray-500">
          Нет аккаунта?{' '}
          <Link to="/register" className="text-indigo-600 font-bold hover:underline">
            Зарегистрируйтесь
          </Link>
        </div>
      </div>

      {isResetOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/55 p-4">
          <div className="w-full max-w-md rounded-[28px] bg-white p-6 shadow-2xl">
            <div className="mb-4">
              <h3 className="text-2xl font-black text-gray-900">Сброс пароля</h3>
              <p className="mt-1 text-sm text-gray-500">
                Запросите письмо, затем введите 6-значный код из письма и новый пароль.
              </p>
            </div>

            {resetError && <div className="mb-4 rounded-xl border border-red-100 bg-red-50 p-3 text-sm text-red-600">{resetError}</div>}
            {resetInfo && <div className="mb-4 rounded-xl border border-emerald-100 bg-emerald-50 p-3 text-sm text-emerald-700">{resetInfo}</div>}

            <form onSubmit={handleResetRequest} className="rounded-2xl border border-gray-100 bg-gray-50/70 p-4">
              <div className="mb-3 text-sm font-black text-gray-900">1. Получить код</div>
              <input
                type="email"
                value={resetEmail}
                onChange={(event) => setResetEmail(event.target.value)}
                required
                placeholder="you@example.com"
                className="w-full rounded-xl border border-gray-200 bg-white px-4 py-3 text-sm text-gray-900 outline-none focus:ring-2 focus:ring-indigo-500"
              />
              <button
                type="submit"
                disabled={resetLoading}
                className="mt-3 w-full rounded-xl bg-indigo-600 px-4 py-3 text-sm font-bold text-white disabled:opacity-50"
              >
                {resetLoading ? 'Отправляю...' : 'Отправить письмо'}
              </button>
            </form>

            <form onSubmit={handleResetConfirm} className="mt-4 rounded-2xl border border-gray-100 bg-gray-50/70 p-4">
              <div className="mb-3 text-sm font-black text-gray-900">2. Подтвердить сброс</div>
              <div className="space-y-3">
                <input
                  value={resetCode}
                  onChange={(event) => setResetCode(event.target.value.replace(/\D/g, '').slice(0, 6))}
                  required
                  inputMode="numeric"
                  pattern="[0-9]{6}"
                  placeholder="Код из письма"
                  className="w-full rounded-xl border border-gray-200 bg-white px-4 py-3 text-sm font-mono text-gray-900 outline-none focus:ring-2 focus:ring-indigo-500"
                />
                <input
                  type="password"
                  value={newPassword}
                  onChange={(event) => setNewPassword(event.target.value)}
                  required
                  minLength={6}
                  placeholder="Новый пароль"
                  className="w-full rounded-xl border border-gray-200 bg-white px-4 py-3 text-sm text-gray-900 outline-none focus:ring-2 focus:ring-indigo-500"
                />
              </div>

              <button
                type="submit"
                disabled={resetLoading}
                className="mt-3 w-full rounded-xl bg-gray-900 px-4 py-3 text-sm font-bold text-white disabled:opacity-50"
              >
                {resetLoading ? 'Сохраняю...' : 'Обновить пароль'}
              </button>
            </form>

            <div className="mt-5 flex justify-end">
              <button
                type="button"
                onClick={closeReset}
                className="rounded-xl bg-gray-100 px-4 py-2 text-sm font-bold text-gray-600"
              >
                Закрыть
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
