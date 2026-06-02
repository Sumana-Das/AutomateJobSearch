import React, { useEffect, useState } from 'react';
import SignInModal from './SignInModal';

type LandingPageProps = {
  children: React.ReactNode;
};

export const LandingPage: React.FC<LandingPageProps> = ({ children }) => {
  const [isAuthenticated, setIsAuthenticated] = useState<boolean>(() => {
    return window.localStorage.getItem('recruiterOutreachAuth') === '1';
  });
  const [userEmail, setUserEmail] = useState<string>(() => {
    return window.localStorage.getItem('recruiterOutreachUserEmail') ?? '';
  });
  const [userName, setUserName] = useState<string>(() => {
    return window.localStorage.getItem('recruiterOutreachUserName') ?? '';
  });
  const [showSignIn, setShowSignIn] = useState<boolean>(false);

  // Handle Google OAuth callback: if redirected back with ?login=success,
  // mark the user as authenticated and clean up the URL.
  useEffect(() => {
    try {
      const url = new URL(window.location.href);
      if (url.searchParams.get('login') === 'success') {
        window.localStorage.setItem('recruiterOutreachAuth', '1');
        setIsAuthenticated(true);
        url.searchParams.delete('login');
        const newUrl = url.pathname + (url.search ? url.search : '');
        window.history.replaceState({}, '', newUrl);
      }
    } catch {
      // ignore URL parsing issues
    }
  }, []);

  // Set browser tab title
  useEffect(() => {
    document.title = 'TailorMailer AI';
  }, []);

  useEffect(() => {
    try {
      window.localStorage.removeItem('recruiterOutreachSecrets');
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
        const res = await fetch('/api/me', { credentials: 'include' });
        if (!res.ok) return;
        const data: { name?: string; email?: string } = await res.json();
        if (aborted) return;
        if (data?.email) {
          window.localStorage.setItem('recruiterOutreachUserEmail', data.email);
          setUserEmail(data.email);
        }
        if (data?.name) {
          window.localStorage.setItem('recruiterOutreachUserName', data.name);
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
    window.localStorage.setItem('recruiterOutreachAuth', '1');
    if (email) {
      window.localStorage.setItem('recruiterOutreachUserEmail', email);
      setUserEmail(email);
    }
    // For stub, also store a friendly name derived from email local part
    if (email && !userName) {
      const derived = email.split('@')[0].replace(/\W+/g, ' ');
      window.localStorage.setItem('recruiterOutreachUserName', derived);
      setUserName(derived);
    }
    setIsAuthenticated(true);
    setShowSignIn(false);
  };

  const handleSignOut = () => {
    window.localStorage.removeItem('recruiterOutreachAuth');
    window.localStorage.removeItem('recruiterOutreachUserEmail');
    window.localStorage.removeItem('recruiterOutreachUserName');
    setIsAuthenticated(false);
    setUserEmail('');
    setUserName('');
  };

  if (!isAuthenticated) {
    return (
      <div className="App">
        <div className="app-header-bar">
          <div className="app-header">
            <h1 className="brand-title">TailorMailer AI</h1>
            <div />
          </div>
        </div>
        <section className="form-section center">
          <div className="field center-items">
            <p className="landing-intro">
              Welcome! Sign in to get started with Email outreach and AI suggestions.
            </p>
          </div>
          <div className="actions center">
            <button type="button" onClick={() => setShowSignIn(true)}>
              Sign in / Sign up
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
          <h1 className="brand-title">TailorMailer AI</h1>
          <div className="header-actions">
            {(userName || userEmail) && (
              <span className="signed-in-email">{userName || userEmail}</span>
            )}
            <button type="button" onClick={handleSignOut} className="btn-ghost small">
              Sign out
            </button>
          </div>
        </div>
      </div>
      {children}
    </div>
  );
};
