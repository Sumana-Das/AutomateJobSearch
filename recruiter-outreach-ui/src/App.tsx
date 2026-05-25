import React, { useEffect, useState } from 'react';
import './App.css';

type SuggestionsResponse = {
  suggestionsText: string;
  keywordsToAdd: string[];
  jdExcerpt: string;
};

type TemplateVariant = {
  subjectTemplate: string;
  bodyTemplate: string;
};

type TemplateDto = {
  key: string;
  displayName: string;
  availableTemplates: string[]; // e.g., ["Hr", "Referral"]
  templates?: {
    hr?: TemplateVariant;
    referral?: TemplateVariant;
  };
};

function App() {
  const [templates, setTemplates] = useState<TemplateDto[]>([]);
  const [roleKey, setRoleKey] = useState<string>(''); // empty means 'Select role'
  const [templateKind, setTemplateKind] = useState<string>('');
  const [jobDescription, setJobDescription] = useState('');
  const [recruiterEmails, setRecruiterEmails] = useState('');
  const [emailSubject, setEmailSubject] = useState('');
  const [emailBody, setEmailBody] = useState('');
  const [loadingSuggestions, setLoadingSuggestions] = useState(false);
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [suggestions, setSuggestions] = useState<SuggestionsResponse | null>(null);
  const [activeTab, setActiveTab] = useState<'setup' | 'email' | 'suggestions'>('setup');
  const [isAuthenticated, setIsAuthenticated] = useState<boolean>(() => {
    return window.localStorage.getItem('recruiterOutreachAuth') === '1';
  });
  const [userEmail, setUserEmail] = useState<string>(() => {
    return window.localStorage.getItem('recruiterOutreachUserEmail') ?? '';
  });
  const [authMode, setAuthMode] = useState<'signin' | 'create'>('signin');
  const [gmailAppPassword, setGmailAppPassword] = useState('');
  const [geminiApiKey, setGeminiApiKey] = useState('');
  const [checkingStatus, setCheckingStatus] = useState(false);
  const [status, setStatus] = useState<{ gmailConfigured: boolean; geminiConfigured: boolean; smtpUserName?: string } | null>(null);

  useEffect(() => {
    const loadTemplates = async () => {
      try {
        const response = await fetch('/api/templates');
        if (!response.ok) {
          throw new Error(`Failed to load templates (status ${response.status})`);
        }

        const raw = (await response.json()) as any[];

        const data: TemplateDto[] = raw.map((r) => ({
          key: r.key,
          displayName: r.displayName,
          availableTemplates: (r.availableTemplates ?? []) as string[],
          templates: r.templates
            ? {
                hr: r.templates.hr
                  ? {
                      subjectTemplate:
                        r.templates.hr.subjectTemplate ?? r.templates.hr.SubjectTemplate ?? '',
                      bodyTemplate:
                        r.templates.hr.bodyTemplate ?? r.templates.hr.BodyTemplate ?? '',
                    }
                  : undefined,
                referral: r.templates.referral
                  ? {
                      subjectTemplate:
                        r.templates.referral.subjectTemplate ?? r.templates.referral.SubjectTemplate ?? '',
                      bodyTemplate:
                        r.templates.referral.bodyTemplate ?? r.templates.referral.BodyTemplate ?? '',
                    }
                  : undefined,
              }
            : undefined,
        }));

        setTemplates(data);
      } catch (e: any) {
        setError(e.message ?? 'Failed to load templates');
      }
    };

    loadTemplates();
  }, []);

  // Load current status for setup tab
  useEffect(() => {
    if (!isAuthenticated || activeTab !== 'setup') {
      return;
    }

    const loadStatus = async () => {
      setCheckingStatus(true);
      try {
        const response = await fetch('/api/status');
        if (!response.ok) {
          throw new Error(`Failed to load status (status ${response.status})`);
        }
        const data = (await response.json()) as { gmailConfigured: boolean; geminiConfigured: boolean; smtpUserName?: string };
        setStatus(data);
      } catch (e: any) {
        setError(e.message ?? 'Failed to load status');
      } finally {
        setCheckingStatus(false);
      }
    };

    loadStatus();
  }, [isAuthenticated, activeTab]);

  // After sign-in, try to rehydrate any previously stored secrets into the API
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

  // Whenever role/template selection changes, update subject/body from cached templates
  useEffect(() => {
    if (!roleKey || !templateKind || templates.length === 0) {
      return;
    }

    const selected = templates.find((t) => t.key === roleKey);
    if (!selected || !selected.templates) {
      return;
    }

    const variant =
      templateKind === 'Referral' ? selected.templates.referral ?? selected.templates.hr : selected.templates.hr;

    if (variant) {
      const roleName = selected.displayName || selected.key;

      const resolvedSubject = (variant.subjectTemplate ?? '')
        .replaceAll('{Role}', roleName);

      const resolvedBody = (variant.bodyTemplate ?? '')
        .replaceAll('{Role}', roleName);

      setEmailSubject(resolvedSubject);
      setEmailBody(resolvedBody);
    }
  }, [roleKey, templateKind, templates]);

  const handleGetSuggestions = async () => {
    setError(null);
    setLoadingSuggestions(true);
    setSuggestions(null);

    try {
      const response = await fetch('/api/outreach/suggestions', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          jobDescription,
        }),
      });

      if (!response.ok) {
        throw new Error(`Suggestions request failed with status ${response.status}`);
      }

      const data = (await response.json()) as SuggestionsResponse;
      setSuggestions(data);
    } catch (e: any) {
      setError(e.message ?? 'Failed to get suggestions');
    } finally {
      setLoadingSuggestions(false);
    }
  };

  const handleSendEmails = async () => {
    setError(null);
    setSending(true);

    try {
      const formData = new FormData();
      formData.append('recruiters', recruiterEmails);
      formData.append('roleKey', roleKey);
      formData.append('templateKind', templateKind);
      formData.append('company', '');
      formData.append('subject', emailSubject);
      formData.append('emailBody', emailBody);

      const response = await fetch('/api/outreach/send', {
        method: 'POST',
        body: formData,
      });

      if (!response.ok) {
        throw new Error(`Send request failed with status ${response.status}`);
      }
    } catch (e: any) {
      setError(e.message ?? 'Failed to send emails');
    } finally {
      setSending(false);
    }
  };

  const canSubmit =
    recruiterEmails.trim().length > 0 &&
    emailSubject.trim().length > 0 &&
    emailBody.trim().length > 0 &&
    roleKey.trim().length > 0 &&
    templateKind.trim().length > 0 &&
    templates.length > 0;

  const isConfigured = !!status?.gmailConfigured && !!status?.geminiConfigured;

  // Once setup is fully configured, don't keep the user on the onboarding tab
  useEffect(() => {
    if (isAuthenticated && isConfigured && activeTab === 'setup') {
      setActiveTab('email');
    }
  }, [isAuthenticated, isConfigured, activeTab]);

  const handleSetupSave = async () => {
    setError(null);
    try {
      const response = await fetch('/api/setup', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          gmailAppPassword: gmailAppPassword || undefined,
          geminiApiKey: geminiApiKey || undefined,
        }),
      });

      if (!response.ok) {
        throw new Error(`Setup request failed with status ${response.status}`);
      }

      // Persist secrets client-side so they can be re-applied after reloads/restarts
      window.localStorage.setItem(
        'recruiterOutreachSecrets',
        JSON.stringify({
          gmailAppPassword: gmailAppPassword || undefined,
          geminiApiKey: geminiApiKey || undefined,
        })
      );

      // Refresh status so the UI immediately reflects the new configuration
      try {
        const statusResponse = await fetch('/api/status');
        if (statusResponse.ok) {
          const data = (await statusResponse.json()) as {
            gmailConfigured: boolean;
            geminiConfigured: boolean;
            smtpUserName?: string;
          };
          setStatus(data);
        }
      } catch {
        // best-effort; if this fails, status will refresh next time the setup tab becomes active
      }

      setGmailAppPassword('');
      setGeminiApiKey('');
      // Refresh status after saving
      setActiveTab('setup');
    } catch (e: any) {
      setError(e.message ?? 'Failed to save setup');
    }
  };

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
    setActiveTab('setup');
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
            style={{ background: 'transparent', border: '1px solid #4b5563', color: '#e5e7eb', padding: '4px 10px', borderRadius: 4, cursor: 'pointer', fontSize: '0.8rem' }}
          >
            Sign out
          </button>
        </div>
      )}
      <div className="tabs">
        <button
          type="button"
          className={activeTab === 'setup' ? 'tab active' : 'tab'}
          onClick={() => setActiveTab('setup')}
        >
          Setup & Onboarding
        </button>
        <button
          type="button"
          className={activeTab === 'email' ? 'tab active' : 'tab'}
          onClick={() => {
            if (isConfigured) setActiveTab('email');
          }}
          disabled={!isConfigured}
        >
          Email outreach
        </button>
        <button
          type="button"
          className={activeTab === 'suggestions' ? 'tab active' : 'tab'}
          onClick={() => {
            if (isConfigured) setActiveTab('suggestions');
          }}
          disabled={!isConfigured}
        >
          Resume suggestions (JD-based)
        </button>
      </div>

      {activeTab === 'setup' && (
        <section className="form-section setup-grid">
          <div className="setup-left">
            <div className="field">
              <p style={{ margin: 0, fontSize: '0.95rem' }}>
                To send emails and generate resume suggestions, follow the steps on the right to configure a Gmail app
                password and a Gemini API key. Values entered here are stored in your browser and applied to the
                backend for this machine.
              </p>
            </div>
            <div className="field">
              <strong>Setup flow</strong>
              <ol style={{ margin: '4px 0 0 18px', padding: 0, fontSize: '0.9rem' }}>
                <li>Generate a Gmail app password for the account you want to send from.</li>
                <li>Create a Gemini API key in Google AI Studio.</li>
                <li>Paste both values on the right and click "Save setup for this session".</li>
                <li>Once status shows configured, switch to Email outreach or Resume suggestions tabs.</li>
              </ol>
            </div>
          </div>

          <div className="setup-right">
            <div className="field">
              <strong>Current status</strong>
              <p style={{ margin: 0, fontSize: '0.9rem' }}>
                {checkingStatus && 'Checking status…'}
                {!checkingStatus && status && (
                  <>
                    <span>
                      Gmail: {status.gmailConfigured ? 'Configured' : 'Not configured'}
                      {status.smtpUserName ? ` (user: ${status.smtpUserName})` : ''}
                    </span>
                    <br />
                    <span>Gemini: {status.geminiConfigured ? 'Configured' : 'Not configured'}</span>
                  </>
                )}
                {!checkingStatus && !status && 'Status not loaded yet.'}
              </p>
            </div>

            <div className="field">
              <label htmlFor="gmail-password">Gmail app password</label>
              <input
                id="gmail-password"
                type="password"
                value={gmailAppPassword}
                onChange={(e) => setGmailAppPassword(e.target.value)}
                placeholder="Paste your Gmail app password (for this session)"
              />
            </div>

            <div className="field">
              <label htmlFor="gemini-key">Gemini API key</label>
              <input
                id="gemini-key"
                type="password"
                value={geminiApiKey}
                onChange={(e) => setGeminiApiKey(e.target.value)}
                placeholder="Paste your Gemini API key (for this session)"
              />
            </div>

            <div className="actions">
              <button type="button" onClick={handleSetupSave} disabled={!gmailAppPassword && !geminiApiKey}>
                Save setup for this session
              </button>
            </div>

            {error && <div className="error">{error}</div>}
          </div>
        </section>
      )}

      {activeTab === 'email' && (
        <section className="form-section email-grid">
          <div className="field">
            <label htmlFor="roleKey">Role</label>
            <select
              id="roleKey"
              value={roleKey}
              onChange={(e) => {
                const newRoleKey = e.target.value;
                setRoleKey(newRoleKey);
                if (!newRoleKey) {
                  // Reset subject/body when clearing selection
                  setTemplateKind('');
                  setEmailSubject('');
                  setEmailBody('');
                  return;
                }
                // Clear template kind so user explicitly chooses a template type
                setTemplateKind('');
              }}
              disabled={templates.length === 0}
            >
              <option value="">Select role</option>
              {templates.map((t) => (
                <option key={t.key} value={t.key}>
                  {t.displayName || t.key}
                </option>
              ))}
            </select>
          </div>

          <div className="field">
            <label htmlFor="templateKind">Template type</label>
            <select
              id="templateKind"
              value={templateKind}
              onChange={(e) => setTemplateKind(e.target.value)}
              disabled={templates.length === 0 || !roleKey}
            >
              <option value="">Select template type</option>
              {(templates.find((t) => t.key === roleKey)?.availableTemplates ?? []).map((k) => (
                <option key={k} value={k}>
                  {k === 'Hr' ? 'HR – Direct' : k === 'Referral' ? 'Referral' : k}
                </option>
              ))}
            </select>
          </div>

          <div className="field">
            <label htmlFor="recruiters">Recruiter email IDs (comma-separated)</label>
            <textarea
              id="recruiters"
              rows={3}
              value={recruiterEmails}
              onChange={(e) => setRecruiterEmails(e.target.value)}
              placeholder="e.g. recruiter1@example.com, recruiter2@example.com"
            />
          </div>

          <div className="field">
            <label htmlFor="emailSubject">Email subject (required)</label>
            <input
              id="emailSubject"
              type="text"
              value={emailSubject}
              onChange={(e) => setEmailSubject(e.target.value)}
              placeholder="Subject line for your email"
            />
          </div>

          <div className="field full-width">
            <label htmlFor="emailBody">Email body (required, you can edit before sending)</label>
            <textarea
              id="emailBody"
              rows={8}
              value={emailBody}
              onChange={(e) => setEmailBody(e.target.value)}
              placeholder="Write or paste the email you want to send to recruiters"
            />
          </div>

          <div className="actions full-width">
            <button
              type="button"
              onClick={handleSendEmails}
              disabled={sending || !canSubmit}
            >
              {sending ? 'Sending…' : 'Send emails now'}
            </button>
          </div>

          {error && <div className="error">{error}</div>}
        </section>
      )}

      {activeTab === 'suggestions' && (
        <>
          <section className="form-section suggestions-grid">
            <div className="field full-width-mobile">
              <label htmlFor="jd">Job description (for resume suggestions)</label>
              <textarea
                id="jd"
                rows={8}
                value={jobDescription}
                onChange={(e) => setJobDescription(e.target.value)}
                placeholder="Paste the JD here to get resume suggestions"
              />
            </div>

            <div className="field suggestions-side">
              <strong>Run suggestions</strong>
              <p style={{ margin: '4px 0 8px', fontSize: '0.9rem' }}>
                Paste a JD on the left, then click the button below to generate suggested resume tweaks and keywords.
              </p>
              <div className="actions">
                <button
                  type="button"
                  onClick={handleGetSuggestions}
                  disabled={loadingSuggestions || !jobDescription.trim()}
                >
                  {loadingSuggestions ? 'Getting suggestions…' : 'Get suggestions'}
                </button>
              </div>

              {error && <div className="error">{error}</div>}
            </div>
          </section>

          {suggestions && (
            <section className="suggestions-section">
              <h2>Resume suggestions</h2>
              {suggestions.keywordsToAdd?.length > 0 && (
                <div className="keywords">
                  <strong>Keywords to consider adding:</strong>
                  <ul>
                    {suggestions.keywordsToAdd.map((kw) => (
                      <li key={kw}>{kw}</li>
                    ))}
                  </ul>
                </div>
              )}

              <div className="suggestions-text">
                <pre>{suggestions.suggestionsText}</pre>
              </div>

              <details>
                <summary>JD excerpt used</summary>
                <pre>{suggestions.jdExcerpt}</pre>
              </details>
            </section>
          )}
        </>
      )}
    </div>
  );
}

export default App;
