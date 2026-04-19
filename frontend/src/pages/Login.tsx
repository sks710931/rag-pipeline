import React, { useEffect } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { 
  Typography, 
  Button, 
  Paper,
  Container,
  CircularProgress 
} from '@mui/material';
import { Login as LoginIcon } from '@mui/icons-material';

const Login: React.FC = () => {
  const { login, isAuthenticated, isLoading } = useAuth();

  useEffect(() => {
    if (isAuthenticated) {
      window.location.href = '/admin';
    }
  }, [isAuthenticated]);

  if (isLoading) {
    return (
      <Container maxWidth="xs" sx={{ mt: 10, textAlign: 'center' }}>
        <CircularProgress />
      </Container>
    );
  }

  return (
    <Container maxWidth="xs" sx={{ mt: 10 }}>
      <Paper elevation={0} sx={{ p: 4, border: '1px solid #e0e0e0', borderRadius: 2, textAlign: 'center' }}>
        <Typography variant="h4" component="h1" gutterBottom sx={{ fontWeight: 700 }}>
          Welcome Back
        </Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 4 }}>
          Access the secure RAG Pipeline admin panel via SSO.
        </Typography>
        <Button 
          variant="contained" 
          size="large" 
          fullWidth
          startIcon={<LoginIcon />}
          onClick={login}
          sx={{ py: 1.5, borderRadius: 2 }}
        >
          Sign in with SSO
        </Button>
      </Paper>
    </Container>
  );
};

export default Login;
