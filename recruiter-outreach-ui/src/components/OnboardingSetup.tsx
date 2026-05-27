import React, { useEffect, useState } from 'react';
import { fetchStatus, saveSetup, StatusInfo } from '../services/onboardingApi';

export const OnboardingSetup: React.FC = () => {
  const [checkingStatus, setCheckingStatus] = useState(false);
  const [status, setStatus] = useState<StatusInfo | null>(null);
  const [gmailAppPassword, setGmailAppPassword] = useState('');
  const [geminiApiKey, setGeminiApiKey] = useState('');
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const load = async () => {
      setCheckingStatus(true);
      try {
        const data = await fetchStatus();
        setStatus(data);
      } catch (e: any) {
        setError(e.message ?? 'Failed to load status');
      } finally {
        setCheckingStatus(false);
      }
    };

    load();
  }, []);

  const handleSave = async () => {
    setError(null);
    try {
      await saveSetup({
        gmailAppPassword: gmailAppPassword || undefined,
        geminiApiKey: geminiApiKey || undefined,
      });

      window.localStorage.setItem(
        'recruiterOutreachSecrets',
        JSON.stringify({
          gmailAppPassword: gmailAppPassword || undefined,
          geminiApiKey: geminiApiKey || undefined,
        })
      );

      try {
        const data = await fetchStatus();
        setStatus(data);
      } catch {
        // best-effort; status will refresh next time
      }

      setGmailAppPassword('');
      setGeminiApiKey('');
    } catch (e: any) {
      setError(e.message ?? 'Failed to save setup');
    }
  };
  return (
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
          <button type="button" onClick={handleSave} disabled={!gmailAppPassword && !geminiApiKey}>
            Save setup for this session
          </button>
        </div>

        {error && <div className="error">{error}</div>}
      </div>
    </section>
  );
};
