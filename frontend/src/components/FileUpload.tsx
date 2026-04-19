import React, { useState } from 'react';
import { 
  Box, 
  Button, 
  Typography, 
  Alert, 
  LinearProgress, 
  Paper,
  Input
} from '@mui/material';
import { CloudUpload as CloudUploadIcon } from '@mui/icons-material';
import { useAuth } from '../contexts/AuthContext';

const FileUpload: React.FC = () => {
  const { user } = useAuth();
  const [file, setFile] = useState<File | null>(null);
  const [status, setStatus] = useState<{ message: string; severity: 'info' | 'success' | 'error' } | null>(null);
  const [loading, setLoading] = useState(false);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files) {
      setFile(e.target.files[0]);
    }
  };

  const handleUpload = async () => {
    if (!file) {
      setStatus({ message: 'Please select a file first.', severity: 'error' });
      return;
    }

    const formData = new FormData();
    formData.append('file', file);

    try {
      setLoading(true);
      setStatus({ message: 'Uploading...', severity: 'info' });
      
      const response = await fetch('/api/upload', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${user?.access_token}`,
        },
        body: formData,
      });

      if (response.ok) {
        const data = await response.json();
        setStatus({ message: `Upload successful: ${data.filename}`, severity: 'success' });
      } else if (response.status === 401) {
        setStatus({ message: 'Session expired or unauthorized. Please log in again.', severity: 'error' });
      } else {
        setStatus({ message: 'Upload failed.', severity: 'error' });
      }
    } catch (error) {
      console.error('Error uploading file:', error);
      setStatus({ message: 'Error uploading file.', severity: 'error' });
    } finally {
      setLoading(false);
    }
  };

  return (
    <Paper elevation={0} sx={{ p: 4, border: '1px solid #e0e0e0', borderRadius: 2 }}>
      <Typography variant="h6" gutterBottom>
        Upload File
      </Typography>
      
      <Box sx={{ my: 3 }}>
        <label htmlFor="upload-button">
          <Input 
            inputProps={{ accept: "*" }} 
            id="upload-button" 
            type="file" 
            sx={{ display: 'none' }}
            onChange={handleFileChange}
          />
          <Button 
            variant="outlined" 
            component="span" 
            startIcon={<CloudUploadIcon />}
            fullWidth
            sx={{ py: 2, borderStyle: 'dashed' }}
          >
            {file ? file.name : "Select Document"}
          </Button>
        </label>
      </Box>

      {loading && <LinearProgress sx={{ mb: 2 }} />}

      <Button 
        variant="contained" 
        onClick={handleUpload} 
        disabled={loading || !file}
        fullWidth
        size="large"
      >
        Upload to Pipeline
      </Button>

      {status && (
        <Alert severity={status.severity} sx={{ mt: 2 }}>
          {status.message}
        </Alert>
      )}
    </Paper>
  );
};

export default FileUpload;
