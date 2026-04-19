import { BrowserRouter as Router } from 'react-router-dom';
import { CssBaseline } from '@mui/material';
import { AuthProvider } from './contexts/AuthContext';
import { ThemeModeProvider } from './contexts/ThemeContext';
import MainLayout from './layout/MainLayout';

function App() {
  return (
    <AuthProvider>
      <ThemeModeProvider>
        <CssBaseline />
        <Router>
          <MainLayout />
        </Router>
      </ThemeModeProvider>
    </AuthProvider>
  );
}

export default App;
