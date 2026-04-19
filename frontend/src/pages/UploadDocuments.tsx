import React, { useState, useRef } from 'react';
import { 
  Box, 
  Typography, 
  Container, 
  Paper, 
  Button, 
  List, 
  ListItem, 
  ListItemIcon, 
  ListItemText, 
  IconButton, 
  LinearProgress, 
  Divider,
  Stack,
  Tooltip
} from '@mui/material';
import { 
  CloudUpload as UploadIcon, 
  FolderOpen as FolderIcon,
  Description as FileIcon,
  Delete as DeleteIcon,
  CheckCircle as SuccessIcon,
  Error as ErrorIcon,
  PlayArrow as StartIcon
} from '@mui/icons-material';
import { useAuth } from '../contexts/AuthContext';

interface UploadTask {
  file: File;
  progress: number;
  status: 'pending' | 'uploading' | 'success' | 'error';
  error?: string;
}

const UploadDocuments: React.FC = () => {
  const { user } = useAuth();
  const [tasks, setTasks] = useState<UploadTask[]>([]);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const folderInputRef = useRef<HTMLInputElement>(null);

  const addFiles = (files: FileList | null) => {
    if (!files) return;
    const newTasks: UploadTask[] = Array.from(files).map(file => ({
      file,
      progress: 0,
      status: 'pending'
    }));
    setTasks(prev => [...prev, ...newTasks]);
  };

  const removeTask = (index: number) => {
    setTasks(prev => prev.filter((_, i) => i !== index));
  };

  const uploadFile = async (index: number) => {
    const task = tasks[index];
    if (task.status === 'success' || task.status === 'uploading') return;

    const formData = new FormData();
    formData.append('file', task.file);

    try {
      setTasks(prev => {
        const next = [...prev];
        next[index] = { ...next[index], status: 'uploading', progress: 0 };
        return next;
      });

      // Using XHR for progress tracking (fetch doesn't support upload progress yet)
      await new Promise((resolve, reject) => {
        const xhr = new XMLHttpRequest();
        xhr.open('POST', '/api/upload');
        xhr.setRequestHeader('Authorization', `Bearer ${user?.access_token}`);

        xhr.upload.onprogress = (event) => {
          if (event.lengthComputable) {
            const progress = Math.round((event.loaded / event.total) * 100);
            setTasks(prev => {
              const next = [...prev];
              next[index] = { ...next[index], progress };
              return next;
            });
          }
        };

        xhr.onload = () => {
          if (xhr.status >= 200 && xhr.status < 300) {
            setTasks(prev => {
              const next = [...prev];
              next[index] = { ...next[index], status: 'success', progress: 100 };
              return next;
            });
            resolve(xhr.response);
          } else {
            reject(new Error(xhr.statusText));
          }
        };

        xhr.onerror = () => reject(new Error('Network Error'));
        xhr.send(formData);
      });

    } catch (err) {
      setTasks(prev => {
        const next = [...prev];
        next[index] = { 
          ...next[index], 
          status: 'error', 
          error: err instanceof Error ? err.message : 'Upload failed' 
        };
        return next;
      });
    }
  };

  const uploadAll = async () => {
    const pendingIndices = tasks
      .map((t, i) => t.status === 'pending' || t.status === 'error' ? i : -1)
      .filter(i => i !== -1);
    
    // Upload 3 at a time for efficiency
    for (let i = 0; i < pendingIndices.length; i++) {
      await uploadFile(pendingIndices[i]);
    }
  };

  return (
    <Container maxWidth={false} sx={{ p: 2 }}>
      <Box sx={{ mb: 4, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Box>
          <Typography variant="h4" fontWeight={800} gutterBottom>
            Upload Documents
          </Typography>
          <Typography variant="body1" color="text.secondary">
            Select files or entire folders to ingest into your RAG pipeline.
          </Typography>
        </Box>
        <Stack direction="row" spacing={2}>
          <input
            type="file"
            multiple
            ref={fileInputRef}
            style={{ display: 'none' }}
            onChange={(e) => addFiles(e.target.files)}
          />
          <input
            type="file"
            // @ts-ignore
            webkitdirectory=""
            directory=""
            ref={folderInputRef}
            style={{ display: 'none' }}
            onChange={(e) => addFiles(e.target.files)}
          />
          <Button 
            variant="outlined" 
            startIcon={<FileIcon />} 
            onClick={() => fileInputRef.current?.click()}
          >
            Select Files
          </Button>
          <Button 
            variant="outlined" 
            startIcon={<FolderIcon />} 
            onClick={() => folderInputRef.current?.click()}
          >
            Select Folder
          </Button>
          <Button 
            variant="contained" 
            startIcon={<StartIcon />} 
            disabled={tasks.length === 0 || tasks.every(t => t.status === 'success')}
            onClick={uploadAll}
          >
            Upload All
          </Button>
        </Stack>
      </Box>

      <Paper elevation={0} sx={{ border: '1px solid #e0e0e0', borderRadius: 2, overflow: 'hidden' }}>
        {tasks.length === 0 ? (
          <Box sx={{ p: 10, textAlign: 'center' }}>
            <UploadIcon sx={{ fontSize: 60, color: 'action.disabled', mb: 2 }} />
            <Typography color="text.secondary">No files selected for upload.</Typography>
          </Box>
        ) : (
          <List disablePadding>
            {tasks.map((task, index) => (
              <React.Fragment key={index}>
                <ListItem
                  secondaryAction={
                    task.status === 'success' ? (
                      <SuccessIcon color="success" />
                    ) : task.status === 'error' ? (
                      <Tooltip title={task.error}>
                        <ErrorIcon color="error" />
                      </Tooltip>
                    ) : (
                      <IconButton edge="end" onClick={() => removeTask(index)} disabled={task.status === 'uploading'}>
                        <DeleteIcon />
                      </IconButton>
                    )
                  }
                >
                  <ListItemIcon>
                    <FileIcon color={task.status === 'success' ? 'success' : 'action'} />
                  </ListItemIcon>
                  <ListItemText 
                    primary={task.file.name} 
                    secondary={`${(task.file.size / 1024).toFixed(2)} KB • ${task.status}`} 
                  />
                </ListItem>
                {(task.status === 'uploading' || task.status === 'success') && (
                  <LinearProgress 
                    variant="determinate" 
                    value={task.progress} 
                    sx={{ height: 2 }} 
                    color={task.status === 'success' ? 'success' : 'primary'}
                  />
                )}
                <Divider />
              </React.Fragment>
            ))}
          </List>
        )}
      </Paper>
    </Container>
  );
};

export default UploadDocuments;
