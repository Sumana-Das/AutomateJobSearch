import React, { useState } from 'react';
import './App.css';
import { AuthShell } from './components/AuthShell';
import { OnboardingSetup } from './components/OnboardingSetup';
import { EmailSender } from './components/EmailSender';
import { AISuggestions } from './components/AISuggestions';

function App() {
  const [activeTab, setActiveTab] = useState<'setup' | 'email' | 'suggestions'>('setup');
  const isConfigured = true; // TODO: wire from onboarding if needed later

  return (
    <AuthShell>
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

      {activeTab === 'setup' && <OnboardingSetup />}
      {activeTab === 'email' && <EmailSender />}
      {activeTab === 'suggestions' && <AISuggestions />}
    </AuthShell>
  );
}

export default App;
