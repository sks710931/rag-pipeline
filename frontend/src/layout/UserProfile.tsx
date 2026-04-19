import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { 
  Avatar, 
  Menu, 
  MenuItem, 
  IconButton, 
  ListItemIcon, 
  ListItemText, 
  Tooltip, 
  Divider,
  Typography,
  Box
} from '@mui/material';
import { 
  Person as AccountIcon, 
  Settings as SettingsIcon, 
  Logout as LogoutIcon
} from '@mui/icons-material';
import { useAuth } from '../contexts/AuthContext';
import CryptoJS from 'crypto-js';

const UserProfile: React.FC = () => {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);
  const open = Boolean(anchorEl);

  const handleClick = (event: React.MouseEvent<HTMLElement>) => {
    setAnchorEl(event.currentTarget);
  };

  const handleClose = () => {
    setAnchorEl(null);
  };

  const handleAccountClick = () => {
    handleClose();
    navigate('/account');
  };

  const handleLogout = () => {
    handleClose();
    logout();
  };

  const email = user?.profile?.email || '';
  const fullName = user?.profile?.name || user?.profile?.preferred_username || 'User';
  const hash = CryptoJS.MD5(email.trim().toLowerCase()).toString();
  const gravatarUrl = `https://www.gravatar.com/avatar/${hash}?d=identicon&s=200`;

  return (
    <React.Fragment>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
        <Typography 
          variant="body1" 
          sx={{ 
            color: 'white', 
            fontWeight: 500,
            mr: 1,
            display: { xs: 'none', sm: 'block' }
          }}
        >
          Hi, {fullName}
        </Typography>
        
        <Tooltip title="Account settings">
          <IconButton
            onClick={handleClick}
            size="small"
            aria-controls={open ? 'account-menu' : undefined}
            aria-haspopup="true"
            aria-expanded={open ? 'true' : undefined}
          >
            <Avatar 
              alt={fullName} 
              src={gravatarUrl} 
              sx={{ width: 32, height: 32, border: '2px solid white' }}
            />
          </IconButton>
        </Tooltip>
      </Box>
      <Menu
        anchorEl={anchorEl}
        id="account-menu"
        open={open}
        onClose={handleClose}
        slotProps={{
          paper: {
            elevation: 0,
            sx: {
              overflow: 'visible',
              filter: 'drop-shadow(0px 2px 8px rgba(0,0,0,0.32))',
              mt: 1.5,
              '& .MuiAvatar-root': {
                width: 32,
                height: 32,
                ml: -0.5,
                mr: 1,
              },
              '&::before': {
                content: '""',
                display: 'block',
                position: 'absolute',
                top: 0,
                right: 14,
                width: 10,
                height: 10,
                bgcolor: 'background.paper',
                transform: 'translateY(-50%) rotate(45deg)',
                zIndex: 0,
              },
            },
          },
        }}
        transformOrigin={{ horizontal: 'right', vertical: 'top' }}
        anchorOrigin={{ horizontal: 'right', vertical: 'bottom' }}
      >
        <MenuItem onClick={handleAccountClick}>
          <ListItemIcon>
            <AccountIcon fontSize="small" />
          </ListItemIcon>
          <ListItemText primary="Account" />
        </MenuItem>
        <MenuItem onClick={handleClose}>
          <ListItemIcon>
            <SettingsIcon fontSize="small" />
          </ListItemIcon>
          <ListItemText primary="Settings" />
        </MenuItem>
        <Divider />
        <MenuItem onClick={handleLogout}>
          <ListItemIcon>
            <LogoutIcon fontSize="small" />
          </ListItemIcon>
          <ListItemText primary="Logout" />
        </MenuItem>
      </Menu>
    </React.Fragment>
  );
};

export default UserProfile;
