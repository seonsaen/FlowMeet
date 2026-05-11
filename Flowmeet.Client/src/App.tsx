import React from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { Login } from './components/Login';
import { Register } from './components/Register';
import { AuthProvider } from './hooks/AuthProvider';
import { useAuth } from './hooks/useAuth';
import { MainLayout } from './components/layout/MainLayout';
import { DashboardPage } from './pages/Dashboard/DashboardPage';
import { SchedulePage } from './pages/Schedule/SchedulePage';
import { NetworkPage } from './pages/Network/NetworkPage';
import { ProfilePage } from './pages/Profile/ProfilePage';
const ProtectedRoute = ({ children }: { children: React.ReactNode }) => {
  const { user } = useAuth();
  if (!user) {
    return <Navigate to="/login" replace />;
  }
  return children;
};
function AppRouter() {
  const { user, logout } = useAuth();

  return (
    <BrowserRouter>
      <Routes>
        {/* Auth Routes */}
        <Route path="/login" element={
          user ? <Navigate to="/dashboard" replace /> : 
          <div className="flex justify-center items-center min-h-screen bg-gray-100 font-sans p-4 sm:p-8"><Login /></div>
        } />
        <Route path="/register" element={
          user ? <Navigate to="/dashboard" replace /> : 
          <div className="flex justify-center items-center min-h-screen bg-gray-100 font-sans p-4 sm:p-8"><Register /></div>
        } />

        {/* Dashboard Routes wrapped in MainLayout */}
        <Route path="/" element={
          <ProtectedRoute>
            <MainLayout />
          </ProtectedRoute>
        }>
          <Route index element={<Navigate to="/dashboard" replace />} />
          <Route path="dashboard" element={user ? <DashboardPage user={user} /> : null} />
          <Route path="schedule" element={<SchedulePage />} />
          <Route path="network" element={<NetworkPage />} />
          <Route path="profile" element={user ? <ProfilePage user={user} onLogout={logout} /> : null} />
        </Route>

        {/* Catch-all redirect */}
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}

function App() {
  return (
    <AuthProvider>
      <AppRouter />
    </AuthProvider>
  );
}

export default App;
