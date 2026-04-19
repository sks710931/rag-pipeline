import React from 'react';
import { 
  Box, 
  Typography, 
  Container, 
  Paper, 
  Avatar, 
  Grid, 
  Divider, 
  Chip,
  Stack
} from '@mui/material';
import { useAuth } from '../contexts/AuthContext';
import CryptoJS from 'crypto-js';

const Account: React.FC = () => {
  const { user } = useAuth();

  if (!user) return null;

  const profile = user.profile;
  const email = profile.email || '';
  const hash = CryptoJS.MD5(email.trim().toLowerCase()).toString();
  const gravatarUrl = `https://www.gravatar.com/avatar/${hash}?d=identicon&s=400`;

  const infoItems = [
    { label: 'Full Name', value: profile.name },
    { label: 'Email', value: profile.email },
    { label: 'Username', value: profile.preferred_username },
    { label: 'Subject ID', value: profile.sub },
    { label: 'Authority', value: user.profile.iss },
  ];

  const roles = (profile.roles as string[]) || [];

  return (
    <Container maxWidth={false} sx={{ p: 2 }}>
      <Paper elevation={0} sx={{ p: 4, border: '1px solid #e0e0e0', borderRadius: 3 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 4, mb: 4 }}>
          <Avatar 
            src={gravatarUrl} 
            sx={{ width: 120, height: 120, border: '4px solid #f4f6f8' }} 
          />
          <Box>
            <Typography variant="h4" fontWeight={800} gutterBottom>
              {profile.name || 'User Profile'}
            </Typography>
            <Typography variant="body1" color="text.secondary">
              Manage your personal information and session data.
            </Typography>
            <Stack direction="row" spacing={1} sx={{ mt: 2 }}>
              {roles.map((role) => (
                <Chip key={role} label={role} color="primary" variant="outlined" size="small" />
              ))}
            </Stack>
          </Box>
        </Box>

        <Divider sx={{ my: 4 }} />

        <Typography variant="h6" gutterBottom sx={{ fontWeight: 600, mb: 3 }}>
          Token Claims
        </Typography>

        <Grid container spacing={3}>
          {infoItems.map((item, index) => (
            <Grid size={{ xs: 12, md: 4, lg: 3 }} key={index}>
              <Typography variant="caption" color="text.secondary" sx={{ textTransform: 'uppercase', fontWeight: 'bold' }}>
                {item.label}
              </Typography>
              <Typography variant="body1" sx={{ mt: 0.5, wordBreak: 'break-all' }}>
                {item.value || 'N/A'}
              </Typography>
            </Grid>
          ))}
        </Grid>

        <Box sx={{ mt: 6, p: 3, bgcolor: 'background.default', borderRadius: 2 }}>
          <Typography variant="subtitle2" color="primary" fontWeight="bold" gutterBottom>
            Session Info
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Your session expires at: {new Date(user.expires_at! * 1000).toLocaleString()}
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Scopes: {user.scope}
          </Typography>
        </Box>
      </Paper>
    </Container>
  );
};

export default Account;
