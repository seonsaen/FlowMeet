import { type FormEvent, useState } from 'react';
import { authApi } from '../api/auth';
import { Link, useNavigate } from 'react-router-dom';

export function Register() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [verificationCode, setVerificationCode] = useState('');
  const [isCodeStep, setIsCodeStep] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const navigate = useNavigate();

  const handleRequestCode = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    setSuccess('');
    setIsLoading(true);

    try {
      const response = await authApi.register({ email, password, firstName, lastName });
      setIsCodeStep(true);
      setSuccess(response.message || 'Код подтверждения отправлен.');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Ошибка регистрации');
    } finally {
      setIsLoading(false);
    }
  };

  const handleConfirmRegistration = async (event: FormEvent) => {
    event.preventDefault();
    setError('');
    setSuccess('');
    setIsLoading(true);

    try {
      const response = await authApi.confirmRegistration(email, verificationCode);
      setSuccess(response.message || 'Регистрация подтверждена! Теперь вы можете войти.');
      setVerificationCode('');
      setTimeout(() => navigate('/login'), 1600);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Не удалось подтвердить регистрацию');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="w-full max-w-sm bg-white p-6 sm:p-8 rounded-3xl shadow-xl border border-gray-100 mx-auto">
      <div className="flex justify-center mb-4 text-indigo-600">
         <div className="w-12 h-12 bg-indigo-50 rounded-2xl flex items-center justify-center text-xl font-black">F</div>
      </div>
      <h2 className="text-2xl font-bold text-gray-800 text-center mb-1">Создать аккаунт</h2>
      <p className="text-gray-500 text-sm text-center mb-6">Присоединяйтесь к FlowMeet сегодня</p>

      {error && <div className="bg-red-50 text-red-600 p-3 rounded-xl text-sm mb-4 border border-red-100 text-center">{error}</div>}
      {success && <div className="bg-green-50 text-green-600 p-3 rounded-xl text-sm mb-4 border border-green-100 text-center">{success}</div>}

      <form onSubmit={isCodeStep ? handleConfirmRegistration : handleRequestCode}>
        <div className="flex gap-3 mb-4">
          <div className="flex-1">
            <label className="block text-sm font-bold text-gray-700 mb-1.5 ml-1">Имя</label>
            <input
              type="text"
              className="w-full bg-gray-50 border border-gray-200 rounded-xl px-4 py-3 text-sm text-gray-900 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:bg-white transition-colors"
              placeholder="Иван"
            value={firstName}
            onChange={(e) => setFirstName(e.target.value)}
            disabled={isCodeStep}
          />
          </div>
          <div className="flex-1">
            <label className="block text-sm font-bold text-gray-700 mb-1.5 ml-1">Фамилия</label>
            <input
              type="text"
              className="w-full bg-gray-50 border border-gray-200 rounded-xl px-4 py-3 text-sm text-gray-900 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:bg-white transition-colors"
              placeholder="Иванов"
            value={lastName}
            onChange={(e) => setLastName(e.target.value)}
            disabled={isCodeStep}
          />
          </div>
        </div>

        <div className="mb-4">
          <label className="block text-sm font-bold text-gray-700 mb-1.5 ml-1">Email</label>
          <input
            type="email"
            className="w-full bg-gray-50 border border-gray-200 rounded-xl px-4 py-3 text-sm text-gray-900 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:bg-white transition-colors"
            placeholder="you@example.com"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
            disabled={isCodeStep}
          />
        </div>

        <div className="mb-6">
          <label className="block text-sm font-bold text-gray-700 mb-1.5 ml-1">Пароль</label>
          <input
            type="password"
            className="w-full bg-gray-50 border border-gray-200 rounded-xl px-4 py-3 text-sm text-gray-900 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:bg-white transition-colors"
            placeholder="••••••••"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            minLength={6}
            disabled={isCodeStep}
          />
        </div>

        {isCodeStep && (
          <div className="mb-6">
            <label className="block text-sm font-bold text-gray-700 mb-1.5 ml-1">Код подтверждения</label>
            <input
              type="text"
              className="w-full bg-gray-50 border border-gray-200 rounded-xl px-4 py-3 text-sm text-gray-900 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:bg-white transition-colors"
              placeholder="6 цифр из письма"
              value={verificationCode}
              onChange={(e) => setVerificationCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
              required
            />
          </div>
        )}

        <button 
           type="submit" 
           className="w-full bg-indigo-600 hover:bg-indigo-700 active:scale-[0.98] text-white font-bold py-3 rounded-xl transition-all shadow-md shadow-indigo-200 disabled:opacity-50 disabled:active:scale-100 flex justify-center items-center" 
           disabled={isLoading}
        >
          {isLoading ? (isCodeStep ? 'Подтверждение...' : 'Отправка кода...') : (isCodeStep ? 'Подтвердить регистрацию' : 'Получить код')}
        </button>
      </form>

      <div className="text-center mt-6 text-sm text-gray-500">
        Уже есть аккаунт?{' '}
        <Link to="/login" className="text-indigo-600 font-bold hover:underline">
          Войти
        </Link>
      </div>
    </div>
  );
}
