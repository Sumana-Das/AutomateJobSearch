import React, { useState } from 'react';
import './App.css';
import { LandingPage } from './components/LandingPage';
import { EmailSender } from './components/EmailSender';
import { AISuggestions } from './components/AISuggestions';

function App() {
  const [activeTab, setActiveTab] = useState<'email' | 'suggestions'>('email');
  const isConfigured = true;

  return (
    <LandingPage>
      <div className="tabs">
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

      {activeTab === 'email' && <EmailSender />}
      {activeTab === 'suggestions' && <AISuggestions />}
    </LandingPage>
  );
}

export default App;
