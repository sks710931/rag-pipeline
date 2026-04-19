import React, { useEffect, useState, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { userManager } from '../contexts/AuthContext';
import { Box, CircularProgress, Typography, Alert, Button } from '@mui/material';

export const SigninCallback: React.FC = () => {
  const navigate = useNavigate();
  const [error, setError] = useState<string | null>(null);
  const isProcessing = useRef(false);

  useEffect(() => {
    // Prevent double execution in React 18 StrictMode
    if (isProcessing.current) return;
    isProcessing.current = true;

    userManager.signinRedirectCallback()
      .then((user) => {
        console.log('Login successful', user);
        navigate('/admin');
      })
      .catch((err) => {
        console.error('Error handling signin callback:', err);
        setError(err instanceof Error ? err.message : String(err));
      });
  }, [navigate]);

  if (error) {
    return (
      <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', mt: 10, p: 3 }}>
        <Alert severity="error" sx={{ width: '100%', maxWidth: '600px', mb: 2 }}>
          <Typography variant="h6">Authentication Failed</Typography>
          <Typography variant="body2">{error}</Typography>
          <Typography variant="caption" sx={{ mt: 1, display: 'block', color: 'text.secondary' }}>
            Tip: Ensure your client allows the 'identity.manage' scope and that the redirect URI matches exactly.
          </Typography>
        </Alert>
        <Button variant="contained" onClick={() => navigate('/login')}>
          Back to Login
        </Button>
      </Box>
    );
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', height: '60vh' }}>
      <CircularProgress sx={{ mb: 2 }} />
      <Typography>Completing secure login, please wait...</Typography>
    </Box>
  );
};

export const SignoutCallback: React.FC = () => {
  const navigate = useNavigate();
  const isProcessing = useRef(false);

  useEffect(() => {
    if (isProcessing.current) return;
    isProcessing.current = true;

    userManager.signoutRedirectCallback()
      .then(() => {
        navigate('/');
      })
      .catch((error) => {
        console.error('Error handling signout callback:', error);
        navigate('/');
      });
  }, [navigate]);

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', height: '60vh' }}>
      <CircularProgress sx={{ mb: 2 }} />
      <Typography>Completing logout, please wait...</Typography>
    </Box>
  );
};
