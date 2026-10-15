import { BrowserRouter } from 'react-router-dom';
import { AppShell } from '../shared/layout/AppShell';
import { AppRoutes } from '../routes/AppRoutes';

export function AppContent() {
  return (
    <AppShell session={{ kind: 'anonymous' }}>
      <AppRoutes />
    </AppShell>
  );
}

export function App() {
  return (
    <BrowserRouter>
      <AppContent />
    </BrowserRouter>
  );
}
