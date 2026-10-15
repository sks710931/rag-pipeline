import { Navigate, Route, Routes } from 'react-router-dom';
import { LoginPage } from './LoginPage';
import { SetupPage } from './SetupPage';
import { MaintenancePage } from './MaintenancePage';
import { StatusPage } from './StatusPage';
import { NotFoundPage } from './NotFoundPage';

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/" element={<Navigate to="/login" replace />} />
      <Route path="/login" element={<LoginPage />} />
      <Route path="/setup" element={<SetupPage />} />
      <Route path="/maintenance" element={<MaintenancePage />} />
      <Route path="/status" element={<StatusPage />} />
      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  );
}
