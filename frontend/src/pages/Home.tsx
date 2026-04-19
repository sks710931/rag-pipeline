import React from 'react';
import { Link as RouterLink } from 'react-router-dom';
import { 
  Box, 
  Typography, 
  Button, 
  Grid, 
  Card, 
  CardContent,
  Container
} from '@mui/material';
import { 
  Search as SearchIcon, 
  Chat as ChatIcon,
  AutoFixHigh as AutomationIcon
} from '@mui/icons-material';

const Home: React.FC = () => {
  return (
    <Container maxWidth={false} sx={{ p: 2 }}>
      <Box sx={{ py: 8, textAlign: 'center' }}>
        <Typography variant="h2" component="h1" color="primary" gutterBottom sx={{ fontWeight: 800 }}>
          RAG Pipeline
        </Typography>
        <Typography variant="h5" color="text.secondary" sx={{ maxWidth: '800px', mx: 'auto', mb: 4 }}>
          Your personal, AI-powered knowledge management system. 
          Easily upload documents, and get intelligent answers based on your data.
        </Typography>
        <Button 
          component={RouterLink} 
          to="/login" 
          variant="contained" 
          size="large"
          sx={{ px: 4, py: 1.5, fontSize: '1.1rem', borderRadius: 2, mb: 8 }}
        >
          Access Admin Panel
        </Button>

        <Grid container spacing={4}>
          {[
            { 
              title: 'Smart Search', 
              desc: 'Find exact information across all your uploaded documents instantly.',
              icon: <SearchIcon fontSize="large" color="primary" />
            },
            { 
              title: 'Automated Ingestion', 
              desc: 'Real-time file monitoring and processing of your uploads.',
              icon: <AutomationIcon fontSize="large" color="primary" />
            },
            { 
              title: 'AI Chat', 
              desc: 'Interact with your data using advanced natural language models.',
              icon: <ChatIcon fontSize="large" color="primary" />
            }
          ].map((feature, idx) => (
            <Grid size={{ xs: 12, md: 4 }} key={idx}>
              <Card elevation={0} sx={{ height: '100%', border: '1px solid #e0e0e0', borderRadius: 2 }}>
                <CardContent sx={{ p: 4, textAlign: 'center' }}>
                  <Box sx={{ mb: 2 }}>{feature.icon}</Box>
                  <Typography variant="h5" component="h3" gutterBottom sx={{ fontWeight: 600 }}>
                    {feature.title}
                  </Typography>
                  <Typography variant="body1" color="text.secondary">
                    {feature.desc}
                  </Typography>
                </CardContent>
              </Card>
            </Grid>
          ))}
        </Grid>
      </Box>
    </Container>
  );
};

export default Home;
