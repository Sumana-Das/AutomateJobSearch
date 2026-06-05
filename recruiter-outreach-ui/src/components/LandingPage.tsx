import React, { useEffect, useState } from 'react';
import SignInModal from './SignInModal';
import { ApiEndpoints, App, Auth, Buttons, StorageKeys } from '../constants';

type LandingPageProps = {
  children: React.ReactNode;
};

export const LandingPage: React.FC<LandingPageProps> = ({ children }) => {
  const [isAuthenticated, setIsAuthenticated] = useState<boolean>(() => {
    return window.localStorage.getItem(StorageKeys.Auth) === '1';
  });
  const [userEmail, setUserEmail] = useState<string>(() => {
    return window.localStorage.getItem(StorageKeys.UserEmail) ?? '';
  });
  const [userName, setUserName] = useState<string>(() => {
    return window.localStorage.getItem(StorageKeys.UserName) ?? '';
  });
  const [showSignIn, setShowSignIn] = useState<boolean>(false);

  // Handle Google OAuth callback: if redirected back with ?login=success,
  // mark the user as authenticated and clean up the URL.
  useEffect(() => {
    try {
      const url = new URL(window.location.href);
      if (url.searchParams.get(Auth.LoginQueryKey) === Auth.LoginSuccessValue) {
        window.localStorage.setItem(StorageKeys.Auth, '1');
        setIsAuthenticated(true);
        url.searchParams.delete(Auth.LoginQueryKey);
        const newUrl = url.pathname + (url.search ? url.search : '');
        window.history.replaceState({}, '', newUrl);
      }
    } catch {
      // ignore URL parsing issues
    }
  }, []);

  // Set browser tab title
  useEffect(() => {
    document.title = App.DocumentTitle;
  }, []);

  useEffect(() => {
    try {
      window.localStorage.removeItem(StorageKeys.Secrets);
    } catch {
      // ignore storage errors
    }
  }, []);

  // When authenticated, fetch the user profile from the backend and cache name/email
  useEffect(() => {
    if (!isAuthenticated) return;
    let aborted = false;
    (async () => {
      try {
        const res = await fetch(ApiEndpoints.Me, { credentials: 'include' });
        if (!res.ok) return;
        const data: { name?: string; email?: string } = await res.json();
        if (aborted) return;
        if (data?.email) {
          window.localStorage.setItem(StorageKeys.UserEmail, data.email);
          setUserEmail(data.email);
        }
        if (data?.name) {
          window.localStorage.setItem(StorageKeys.UserName, data.name);
          setUserName(data.name);
        }
      } catch {
        // ignore network/parse errors
      }
    })();
    return () => {
      aborted = true;
    };
  }, [isAuthenticated]);

  const completeStubSignIn = (email: string) => {
    window.localStorage.setItem(StorageKeys.Auth, '1');
    if (email) {
      window.localStorage.setItem(StorageKeys.UserEmail, email);
      setUserEmail(email);
    }
    // For stub, also store a friendly name derived from email local part
    if (email && !userName) {
      const derived = email.split('@')[0].replace(/\W+/g, ' ');
      window.localStorage.setItem(StorageKeys.UserName, derived);
      setUserName(derived);
    }
    setIsAuthenticated(true);
    setShowSignIn(false);
  };

  const handleSignOut = () => {
    window.localStorage.removeItem(StorageKeys.Auth);
    window.localStorage.removeItem(StorageKeys.UserEmail);
    window.localStorage.removeItem(StorageKeys.UserName);
    setIsAuthenticated(false);
    setUserEmail('');
    setUserName('');
  };

  if (!isAuthenticated) {
    return (
      <div className="App">
        <div className="app-header-bar">
          <div className="app-header">
            <h1 className="brand-title">{App.BrandTitle}</h1>
            <div />
          </div>
        </div>
        <section className="form-section center">
          <div className="field center-items">
            <p className="landing-intro">{App.LandingIntro}</p>
          </div>
          <div className="actions center">
            <button type="button" onClick={() => setShowSignIn(true)}>
              {Buttons.SignInSignUp}
            </button>
          </div>
        </section>
        {showSignIn && (
          <SignInModal onClose={() => setShowSignIn(false)} onStubComplete={completeStubSignIn} />
        )}
      </div>
    );
  }

  return (
    <div className="App">
      <div className="app-header-bar">
        <div className="app-header">
          <h1 className="brand-title">{App.BrandTitle}</h1>
          <div className="header-actions">
            {(userName || userEmail) && (
              <span className="signed-in-email">{userName || userEmail}</span>
            )}
            <button type="button" onClick={handleSignOut} className="btn-ghost small">
              {Buttons.SignOut}
            </button>
          </div>
        </div>
      </div>
      {children}
    </div>
  );
};
