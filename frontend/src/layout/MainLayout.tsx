import React, { useState } from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import { 
  Box,
  Typography
} from '@mui/material';
import { useAuth } from '../contexts/AuthContext';
import Header from './Header';
import Sidebar from './Sidebar';
import Home from '../pages/Home';
import Login from '../pages/Login';
import Admin from '../pages/Admin';
import Account from '../pages/Account';
import Dashboard from '../pages/Dashboard';
import ProcessDocuments from '../pages/ProcessDocuments';
import UploadDocuments from '../pages/UploadDocuments';
import { SigninCallback, SignoutCallback } from '../pages/Callbacks';

const MainLayout: React.FC = () => {
  const { isAuthenticated, isLoading } = useAuth();
  const [open, setOpen] = useState(true);

  const toggleDrawer = () => {
    setOpen(!open);
  };

  return (
    <Box sx={{ display: 'flex' }}>
      <Header onToggleDrawer={toggleDrawer} />

      {isAuthenticated && <Sidebar open={open} />}

      <Box component="main" sx={{ flexGrow: 1, p: 3, mt: 8 }}>
        <Routes>
          <Route path="/" element={
            isLoading ? null : (isAuthenticated ? <Dashboard /> : <Home />)
          } />
          
          <Route path="/login" element={<Login />} />
          <Route path="/admin" element={<Admin />} />
          <Route path="/account" element={<Account />} />
          <Route path="/upload" element={<UploadDocuments />} />
          <Route path="/process" element={<ProcessDocuments />} />
          <Route path="/signin-oidc" element={<SigninCallback />} />
          <Route path="/signout-callback-oidc" element={<SignoutCallback />} />
          
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
        
        <Box component="footer" sx={{ py: 3, px: 2, mt: 8, textAlign: 'center', borderTop: '1px solid #e0e0e0' }}>
          <Typography variant="body2" color="text.secondary">
            {'© '} {new Date().getFullYear()} RAG Pipeline Project.
          </Typography>
        </Box>
      </Box>
    </Box>
  );
};

export default MainLayout;
