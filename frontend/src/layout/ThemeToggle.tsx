import React from 'react';
import { IconButton, Tooltip } from '@mui/material';
import { 
  Brightness4 as DarkModeIcon, 
  Brightness7 as LightModeIcon 
} from '@mui/icons-material';
import { useColorMode } from '../contexts/ThemeContext';

const ThemeToggle: React.FC = () => {
  const { mode, toggleColorMode } = useColorMode();

  return (
    <Tooltip title={`Toggle ${mode === 'dark' ? 'light' : 'dark'} mode`}>
      <IconButton onClick={toggleColorMode} color="inherit">
        {mode === 'dark' ? <LightModeIcon /> : <DarkModeIcon />}
      </IconButton>
    </Tooltip>
  );
};

export default ThemeToggle;
