import React, { createContext, useContext, useEffect, useState, useCallback } from 'react';
import { UserManager, User, WebStorageStateStore } from 'oidc-client-ts';
import type { UserManagerSettings } from 'oidc-client-ts';

interface AuthContextType {
  user: User | null;
  isLoading: boolean;
  login: () => Promise<void>;
  logout: () => Promise<void>;
  isAuthenticated: boolean;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

const oidcConfig: UserManagerSettings = {
  authority: 'https://authserver-cwa3c8ddgydva8e9.eastus-01.azurewebsites.net/',
  client_id: 'sso-admin-ui',
  redirect_uri: 'http://localhost:5173/signin-oidc',
  post_logout_redirect_uri: 'http://localhost:5173/signout-callback-oidc', // Adjusting to http to match redirect_uri
  response_type: 'code',
  scope: 'email profile roles offline_access openid identity.manage',
  automaticSilentRenew: true,
  userStore: new WebStorageStateStore({ store: window.localStorage }),
  loadUserInfo: true,
  monitorSession: true,
};

const userManager = new UserManager(oidcConfig);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    // Check for an existing session on load
    userManager.getUser().then((currentUser) => {
      setUser(currentUser);
      setIsLoading(false);
    });

    // Event listeners for session changes
    const onUserLoaded = (loadedUser: User) => {
      setUser(loadedUser);
    };

    const onUserUnloaded = () => {
      setUser(null);
    };

    const onAccessTokenExpired = () => {
      userManager.signinSilent().catch(() => {
        setUser(null);
      });
    };

    userManager.events.addUserLoaded(onUserLoaded);
    userManager.events.addUserUnloaded(onUserUnloaded);
    userManager.events.addAccessTokenExpired(onAccessTokenExpired);

    return () => {
      userManager.events.removeUserLoaded(onUserLoaded);
      userManager.events.removeUserUnloaded(onUserUnloaded);
      userManager.events.removeAccessTokenExpired(onAccessTokenExpired);
    };
  }, []);

  const login = useCallback(async () => {
    await userManager.signinRedirect();
  }, []);

  const logout = useCallback(async () => {
    await userManager.signoutRedirect();
  }, []);

  const isAuthenticated = !!user && !user.expired;

  return (
    <AuthContext.Provider value={{ user, isLoading, login, logout, isAuthenticated }}>
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};

export { userManager };
