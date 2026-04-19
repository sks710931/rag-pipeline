import React, { useState, useEffect } from 'react';
import { 
  Box, 
  Typography, 
  Container, 
  Paper,
  Tabs,
  Tab,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Chip,
  CircularProgress,
  IconButton,
  Tooltip
} from '@mui/material';
import { 
  Refresh as RefreshIcon,
  CheckCircle as SuccessIcon,
  Error as ErrorIcon,
  HourglassEmpty as PendingIcon,
  PlayArrow as ProcessIcon
} from '@mui/icons-material';
import { useAuth } from '../contexts/AuthContext';

interface IngestionRecord {
  ingestion_id: string;
  filename: string;
  status: string;
  mime_type: string;
  size: number;
  created_at: string;
  error?: string;
}

interface TabPanelProps {
  children?: React.ReactNode;
  index: number;
  value: number;
}

function CustomTabPanel(props: TabPanelProps) {
  const { children, value, index, ...other } = props;

  return (
    <div
      role="tabpanel"
      hidden={value !== index}
      id={`simple-tabpanel-${index}`}
      aria-labelledby={`simple-tab-${index}`}
      {...other}
    >
      {value === index && (
        <Box sx={{ py: 3 }}>
          {children}
        </Box>
      )}
    </div>
  );
}

const ProcessDocuments: React.FC = () => {
  const { user } = useAuth();
  const [tabValue, setTabValue] = useState(0);
  const [loading, setLoading] = useState(true);
  const [ingestions, setIngestions] = useState<IngestionRecord[]>([]);

  const fetchIngestions = async () => {
    try {
      setLoading(true);
      const response = await fetch('/api/ingestions', {
        headers: {
          'Authorization': `Bearer ${user?.access_token}`,
        },
      });
      if (response.ok) {
        const data = await response.json();
        setIngestions(data);
      }
    } catch (error) {
      console.error('Error fetching ingestions:', error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchIngestions();
    // Poll every 10 seconds
    const interval = setInterval(fetchIngestions, 10000);
    return () => clearInterval(interval);
  }, []);

  const handleTabChange = (_event: React.SyntheticEvent, newValue: number) => {
    setTabValue(newValue);
  };

  const pendingFiles = ingestions.filter(i => i.status === 'Pending' || i.status === 'Ingesting');
  const processedFiles = ingestions.filter(i => i.status === 'Completed' || i.status === 'Error');

  const renderTable = (data: IngestionRecord[]) => (
    <TableContainer component={Paper} elevation={0} sx={{ border: '1px solid #e0e0e0', borderRadius: 2 }}>
      <Table sx={{ minWidth: 650 }}>
        <TableHead sx={{ bgcolor: 'background.default' }}>
          <TableRow>
            <TableCell sx={{ fontWeight: 'bold' }}>Filename</TableCell>
            <TableCell sx={{ fontWeight: 'bold' }}>Format</TableCell>
            <TableCell sx={{ fontWeight: 'bold' }}>Size</TableCell>
            <TableCell sx={{ fontWeight: 'bold' }}>Created At</TableCell>
            <TableCell sx={{ fontWeight: 'bold' }}>Status</TableCell>
            <TableCell sx={{ fontWeight: 'bold' }}>Actions</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {data.length === 0 ? (
            <TableRow>
              <TableCell colSpan={6} align="center" sx={{ py: 4 }}>
                <Typography color="text.secondary">No documents found.</Typography>
              </TableCell>
            </TableRow>
          ) : (
            data.map((row) => (
              <TableRow key={row.ingestion_id} sx={{ '&:last-child td, &:last-child th': { border: 0 } }}>
                <TableCell>{row.filename}</TableCell>
                <TableCell>
                  <Chip label={row.mime_type.split('/')[1] || row.mime_type} size="small" variant="outlined" />
                </TableCell>
                <TableCell>{(row.size / 1024).toFixed(2)} KB</TableCell>
                <TableCell>{new Date(row.created_at).toLocaleString()}</TableCell>
                <TableCell>
                  <Chip 
                    label={row.status} 
                    size="small" 
                    icon={
                      row.status === 'Completed' ? <SuccessIcon /> : 
                      row.status === 'Error' ? <ErrorIcon /> : 
                      <PendingIcon />
                    }
                    color={
                      row.status === 'Completed' ? 'success' : 
                      row.status === 'Error' ? 'error' : 
                      'primary'
                    }
                  />
                </TableCell>
                <TableCell>
                  {row.status === 'Pending' && (
                    <Tooltip title="Trigger Processing">
                      <IconButton size="small" color="primary">
                        <ProcessIcon />
                      </IconButton>
                    </Tooltip>
                  )}
                </TableCell>
              </TableRow>
            ))
          )}
        </TableBody>
      </Table>
    </TableContainer>
  );

  return (
    <Container maxWidth={false} sx={{ p: 2 }}>
      <Box sx={{ mb: 4, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Box>
          <Typography variant="h4" fontWeight={800} gutterBottom>
            Process Documents
          </Typography>
          <Typography variant="body1" color="text.secondary">
            Manage and monitor the document ingestion pipeline.
          </Typography>
        </Box>
        <IconButton onClick={fetchIngestions} disabled={loading} color="primary">
          {loading ? <CircularProgress size={24} /> : <RefreshIcon />}
        </IconButton>
      </Box>

      <Box sx={{ width: '100%' }}>
        <Box sx={{ borderBottom: 1, borderColor: 'divider' }}>
          <Tabs value={tabValue} onChange={handleTabChange} aria-label="ingestion tabs">
            <Tab label={`Pending (${pendingFiles.length})`} />
            <Tab label={`Processed (${processedFiles.length})`} />
          </Tabs>
        </Box>
        
        <CustomTabPanel value={tabValue} index={0}>
          {renderTable(pendingFiles)}
        </CustomTabPanel>
        
        <CustomTabPanel value={tabValue} index={1}>
          {renderTable(processedFiles)}
        </CustomTabPanel>
      </Box>
    </Container>
  );
};

export default ProcessDocuments;
