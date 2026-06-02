import { startGoogleLogin } from '../services/authService';
import React, { useState } from 'react';

type SignInModalProps = {
  onClose: () => void;
  onStubComplete: (email: string) => void;
};
export const SignInModal: React.FC<SignInModalProps> = ({ onClose, onStubComplete }) => {
  const [email, setEmail] = useState('');

  return (
    <div role="dialog" aria-modal="true" className="modal-overlay" onClick={onClose}>
      <div className="modal-card" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h2 className="modal-title">Sign in to TailorMailer</h2>
          <button onClick={onClose} aria-label="Close" className="icon-button">
            ×
          </button>
        </div>

        <div className="modal-body">
          <button type="button" className="btn-google" onClick={() => {
              startGoogleLogin();
            }}>
            Continue with Google
          </button>

          <div className="text-muted-small">or continue with email</div>

          <div className="form-vertical">
            <label htmlFor="stub-email" className="label-sm">
              Email
            </label>
            <input
              id="stub-email"
              type="email"
              placeholder="you@example.com"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className="input-text"
            />
          </div>

          <div className="actions mt-8">
            <button type="button" onClick={() => onStubComplete(email)} disabled={!email} className="btn-primary">
              Continue
            </button>
            <button type="button" onClick={onClose} className="btn-secondary">
              Cancel
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

export default SignInModal;
