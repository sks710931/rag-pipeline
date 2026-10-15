import { Header } from '../shared/components/Header';
import { SkipLink } from '../shared/components/SkipLink';
import { SystemHealth } from '../features/system-health/SystemHealth';

const throwawayGateProbe: number = 'not a number';

export function App() {
  return (
    <div className="min-h-screen bg-slate-50 text-slate-900">
      <SkipLink />
      <Header />
      <main
        id="main-content"
        tabIndex={-1}
        className="mx-auto max-w-7xl px-4 py-8 focus:outline-none sm:px-6 lg:px-8"
      >
        <div className="max-w-xl">
          <SystemHealth />
        </div>
      </main>
    </div>
  );
}
