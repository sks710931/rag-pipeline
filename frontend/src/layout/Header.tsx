import React from 'react';
import { Link as RouterLink } from 'react-router-dom';
import { 
  AppBar, 
  Toolbar, 
  Typography, 
  Button, 
  IconButton,
  Box
} from '@mui/material';
import { Menu as MenuIcon } from '@mui/icons-material';
import { useAuth } from '../contexts/AuthContext';
import UserProfile from './UserProfile';
import ThemeToggle from './ThemeToggle';

interface HeaderProps {
  onToggleDrawer: () => void;
}

const Header: React.FC<HeaderProps> = ({ onToggleDrawer }) => {
  const { isAuthenticated, isLoading } = useAuth();

  return (
    <AppBar position="fixed" sx={{ zIndex: (theme) => theme.zIndex.drawer + 1 }}>
      <Toolbar>
        {isAuthenticated && (
          <IconButton
            color="inherit"
            aria-label="toggle drawer"
            onClick={onToggleDrawer}
            edge="start"
            sx={{ marginRight: 2 }}
          >
            <MenuIcon />
          </IconButton>
        )}
        <Typography variant="h6" component="div" sx={{ flexGrow: 1, fontWeight: 'bold' }}>
          <RouterLink to="/" style={{ textDecoration: 'none', color: 'white' }}>
            RAG Pipeline
          </RouterLink>
        </Typography>
        
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          {!isLoading && (
            isAuthenticated ? (
              <>
                <UserProfile />
                <ThemeToggle />
              </>
            ) : (
              <>
                <Button component={RouterLink} to="/login" sx={{ color: 'white' }}>Login</Button>
                <ThemeToggle />
              </>
            )
          )}
        </Box>
      </Toolbar>
    </AppBar>
  );
};

export default Header;
