import React from 'react';
import { 
  Box, 
  Typography, 
  Grid, 
  Paper, 
  Card, 
  CardContent, 
  Container,
  Stack,
  Divider
} from '@mui/material';
import { 
  Description as DocIcon, 
  Autorenew as ProcessingIcon, 
  CheckCircle as SuccessIcon,
  Storage as StorageIcon
} from '@mui/icons-material';
import { useAuth } from '../contexts/AuthContext';

const Dashboard: React.FC = () => {
  const { user } = useAuth();

  const stats = [
    { label: 'Total Documents', value: '124', icon: <DocIcon color="primary" />, color: 'primary.main' },
    { label: 'In Processing', value: '3', icon: <ProcessingIcon color="warning" />, color: 'warning.main' },
    { label: 'Vectorized', value: '121', icon: <SuccessIcon color="success" />, color: 'success.main' },
    { label: 'Storage Used', value: '450 MB', icon: <StorageIcon color="info" />, color: 'info.main' },
  ];

  return (
    <Container maxWidth={false} sx={{ p: 2 }}>
      <Box sx={{ mb: 4 }}>
        <Typography variant="h4" fontWeight={800} gutterBottom>
          Dashboard
        </Typography>
        <Typography variant="body1" color="text.secondary">
          Welcome back, {user?.profile?.name || 'Admin'}. Here is your pipeline overview.
        </Typography>
      </Box>

      <Grid container spacing={3} sx={{ mb: 4 }}>
        {stats.map((stat, index) => (
          <Grid size={{ xs: 12, sm: 6, md: 3 }} key={index}>
            <Paper elevation={0} sx={{ p: 3, border: '1px solid #e0e0e0', borderRadius: 2 }}>
              <Stack direction="row" spacing={2} alignItems="center">
                <Box sx={{ p: 1, borderRadius: 1, bgcolor: 'background.default', display: 'flex' }}>
                  {stat.icon}
                </Box>
                <Box>
                  <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 'bold', textTransform: 'uppercase' }}>
                    {stat.label}
                  </Typography>
                  <Typography variant="h5" fontWeight={700}>
                    {stat.value}
                  </Typography>
                </Box>
              </Stack>
            </Paper>
          </Grid>
        ))}
      </Grid>

      <Grid container spacing={3}>
        <Grid size={{ xs: 12, md: 8 }}>
          <Paper elevation={0} sx={{ p: 3, border: '1px solid #e0e0e0', borderRadius: 2, height: '100%' }}>
            <Typography variant="h6" fontWeight={700} gutterBottom>
              Recent Pipeline Activity
            </Typography>
            <Divider sx={{ my: 2 }} />
            <Stack spacing={2}>
              {[
                { file: 'annual_report_2025.pdf', status: 'Completed', time: '2 mins ago' },
                { file: 'technical_specs.docx', status: 'Processing', time: '15 mins ago' },
                { file: 'knowledge_base_v2.txt', status: 'Completed', time: '1 hour ago' },
                { file: 'api_documentation.pdf', status: 'Completed', time: '3 hours ago' },
              ].map((activity, i) => (
                <Box key={i} sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <Box>
                    <Typography variant="body2" fontWeight={600}>{activity.file}</Typography>
                    <Typography variant="caption" color="text.secondary">{activity.time}</Typography>
                  </Box>
                  <Chip 
                    label={activity.status} 
                    size="small" 
                    color={activity.status === 'Completed' ? 'success' : 'warning'} 
                    variant="soft" // Note: soft is not standard MUI but we can use sx for it
                    sx={{ fontWeight: 'bold' }}
                  />
                </Box>
              ))}
            </Stack>
          </Paper>
        </Grid>
        <Grid size={{ xs: 12, md: 4 }}>
          <Paper elevation={0} sx={{ p: 3, border: '1px solid #e0e0e0', borderRadius: 2, height: '100%' }}>
            <Typography variant="h6" fontWeight={700} gutterBottom>
              System Health
            </Typography>
            <Divider sx={{ my: 2 }} />
            <Stack spacing={3}>
              <Box>
                <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
                  <Typography variant="body2">LLM Engine</Typography>
                  <Typography variant="body2" color="success.main" fontWeight="bold">Healthy</Typography>
                </Box>
                <LinearProgress variant="determinate" value={100} color="success" />
              </Box>
              <Box>
                <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
                  <Typography variant="body2">Vector DB Latency</Typography>
                  <Typography variant="body2" color="success.main" fontWeight="bold">24ms</Typography>
                </Box>
                <LinearProgress variant="determinate" value={95} color="success" />
              </Box>
              <Box>
                <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
                  <Typography variant="body2">Worker Memory</Typography>
                  <Typography variant="body2" color="warning.main" fontWeight="bold">78%</Typography>
                </Box>
                <LinearProgress variant="determinate" value={78} color="warning" />
              </Box>
            </Stack>
          </Paper>
        </Grid>
      </Grid>
    </Container>
  );
};

import { Chip, LinearProgress } from '@mui/material';
export default Dashboard;
