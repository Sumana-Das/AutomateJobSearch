import React, { useEffect, useState } from 'react';

type AuthShellProps = {
  children: React.ReactNode;
};

export const AuthShell: React.FC<AuthShellProps> = ({ children }) => {
  const [isAuthenticated, setIsAuthenticated] = useState<boolean>(() => {
    return window.localStorage.getItem('recruiterOutreachAuth') === '1';
  });
  const [userEmail, setUserEmail] = useState<string>(() => {
    return window.localStorage.getItem('recruiterOutreachUserEmail') ?? '';
  });
  const [authMode, setAuthMode] = useState<'signin' | 'create'>('signin');

  useEffect(() => {
    if (!isAuthenticated) {
      return;
    }

    const raw = window.localStorage.getItem('recruiterOutreachSecrets');
    if (!raw) {
      return;
    }

    try {
      const parsed = JSON.parse(raw) as { gmailAppPassword?: string; geminiApiKey?: string };
      if (!parsed.gmailAppPassword && !parsed.geminiApiKey) {
        return;
      }

      const applySecrets = async () => {
        try {
          await fetch('/api/setup', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              gmailAppPassword: parsed.gmailAppPassword,
              geminiApiKey: parsed.geminiApiKey,
            }),
          });
        } catch {
          // best-effort: if this fails, user can still re-save from Setup tab
        }
      };

      applySecrets();
    } catch {
      // ignore parse errors; user can re-enter values
    }
  }, [isAuthenticated]);

  const handleSignIn = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const formData = new FormData(e.currentTarget);
    const email = (formData.get('signin-email') as string | null) ?? '';

    window.localStorage.setItem('recruiterOutreachAuth', '1');
    if (email) {
      window.localStorage.setItem('recruiterOutreachUserEmail', email);
      setUserEmail(email);
    }

    setIsAuthenticated(true);
  };

  const handleSignOut = () => {
    window.localStorage.removeItem('recruiterOutreachAuth');
    window.localStorage.removeItem('recruiterOutreachUserEmail');
    setIsAuthenticated(false);
    setUserEmail('');
    setAuthMode('signin');
  };

  if (!isAuthenticated) {
    return (
      <div className="App">
        <h1>Recruiter Outreach Automation</h1>
        <section className="form-section">
          {authMode === 'signin' && (
            <>
              <div className="field">
                <p style={{ margin: 0, fontSize: '0.95rem' }}>
                  Sign in to configure your email and Gemini setup and start using the tool. For now this is a simple
                  single-user sign in stored in your browser only.
                </p>
              </div>
              <form className="field" onSubmit={handleSignIn}>
                <label htmlFor="signin-email">Email</label>
                <input id="signin-email" name="signin-email" type="email" required placeholder="you@example.com" />
                <label htmlFor="signin-password">Password</label>
                <input id="signin-password" type="password" required placeholder="Choose any password" />
                <div className="actions">
                  <button type="submit">Sign in</button>
                </div>
              </form>
              <div className="field" style={{ fontSize: '0.85rem', textAlign: 'right' }}>
                <button
                  type="button"
                  onClick={() => setAuthMode('create')}
                  style={{ background: 'none', border: 'none', color: '#a5b4fc', cursor: 'pointer', padding: 0 }}
                >
                  Create account
                </button>
              </div>
            </>
          )}

          {authMode === 'create' && (
            <>
              <div className="field">
                <p style={{ margin: 0, fontSize: '0.95rem' }}>
                  Account creation flow will go here in the future. For now this is a placeholder screen.
                </p>
              </div>
              <div className="actions">
                <button type="button" onClick={() => setAuthMode('signin')}>
                  Back to sign in
                </button>
              </div>
            </>
          )}
        </section>
      </div>
    );
  }

  return (
    <div className="App">
      <h1>Recruiter Outreach Automation</h1>
      {(userEmail || true) && (
        <div
          style={{
            maxWidth: 980,
            margin: '0 auto 8px',
            fontSize: '0.85rem',
            textAlign: 'right',
            opacity: 0.85,
            display: 'flex',
            justifyContent: 'flex-end',
            gap: '8px',
            alignItems: 'center',
          }}
        >
          {userEmail && (
            <span>
              Signed in as <span style={{ fontWeight: 500 }}>{userEmail}</span>
            </span>
          )}
          <button
            type="button"
            onClick={handleSignOut}
            style={{
              background: 'transparent',
              border: '1px solid #4b5563',
              color: '#e5e7eb',
              padding: '4px 10px',
              borderRadius: 4,
              cursor: 'pointer',
              fontSize: '0.8rem',
            }}
          >
            Sign out
          </button>
        </div>
      )}
      {children}
    </div>
  );
};
