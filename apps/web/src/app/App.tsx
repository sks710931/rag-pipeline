import { BrowserRouter } from 'react-router-dom';
import { Header } from '../shared/components/Header';
import { SkipLink } from '../shared/components/SkipLink';
import { AppRoutes } from '../routes/AppRoutes';

export function AppContent() {
  return (
    <div className="min-h-screen bg-neutral-50 text-neutral-900">
      <SkipLink />
      <Header />
      <main
        id="main-content"
        tabIndex={-1}
        className="mx-auto max-w-7xl px-4 py-8 focus:outline-none sm:px-6 lg:px-8"
      >
        <AppRoutes />
      </main>
    </div>
  );
}

export function App() {
  return (
    <BrowserRouter>
      <AppContent />
    </BrowserRouter>
  );
}
