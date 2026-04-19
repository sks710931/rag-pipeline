import React, { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { 
  Box, 
  Typography, 
  Container, 
  Paper, 
  Divider,
  Stack,
  Grid,
  CircularProgress
} from '@mui/material';
import FileUpload from '../components/FileUpload';

const Admin: React.FC = () => {
  const { user, isAuthenticated, isLoading } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      navigate('/login');
    }
  }, [isAuthenticated, isLoading, navigate]);

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', mt: 10 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (!isAuthenticated) return null;

  return (
    <Container maxWidth={false} sx={{ p: 2 }}>
      <Box sx={{ mb: 6 }}>
        <Typography variant="h3" component="h1" gutterBottom sx={{ fontWeight: 800 }}>
          Admin Dashboard
        </Typography>
        <Typography variant="body1" color="text.secondary">
          Welcome, <strong>{user?.profile?.name || user?.profile?.email || 'Admin'}</strong>. 
          Manage your RAG pipeline data here.
        </Typography>
      </Box>

      <Grid container spacing={4}>
        <Grid size={{ xs: 12, md: 6 }}>
          <Paper elevation={0} sx={{ p: 4, border: '1px solid #e0e0e0', borderRadius: 2, height: '100%' }}>
            <Typography variant="h5" gutterBottom sx={{ fontWeight: 600 }}>
              Ingest Documents
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 4 }}>
              Upload new files to the system. These will be automatically processed and vectorized.
            </Typography>
            <FileUpload />
          </Paper>
        </Grid>

        <Grid size={{ xs: 12, md: 6 }}>
          <Paper elevation={0} sx={{ p: 4, border: '1px solid #e0e0e0', borderRadius: 2, height: '100%' }}>
            <Typography variant="h5" gutterBottom sx={{ fontWeight: 600 }}>
              System Status
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 4 }}>
              Current state of the ingestion pipeline and vector storage.
            </Typography>
            <Divider sx={{ my: 2 }} />
            <Stack spacing={2}>
              <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
                <Typography variant="body2">Watcher Service</Typography>
                <Typography variant="body2" color="success.main" sx={{ fontWeight: 'bold' }}>Active</Typography>
              </Box>
              <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
                <Typography variant="body2">Backend API</Typography>
                <Typography variant="body2" color="success.main" sx={{ fontWeight: 'bold' }}>Online</Typography>
              </Box>
              <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
                <Typography variant="body2">Vector Storage</Typography>
                <Typography variant="body2" color="text.secondary">Ready</Typography>
              </Box>
            </Stack>
          </Paper>
        </Grid>
      </Grid>
    </Container>
  );
};

export default Admin;
