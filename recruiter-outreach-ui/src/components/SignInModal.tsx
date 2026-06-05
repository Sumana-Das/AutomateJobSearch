import { startGoogleLogin } from '../services/authService';
import React, { useState } from 'react';
import { Buttons, Modal } from '../constants';

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
          <h2 className="modal-title">{Modal.SignInTitle}</h2>
          <button onClick={onClose} aria-label={Modal.CloseAria} className="icon-button">
            ×
          </button>
        </div>

        <div className="modal-body">
          <button type="button" className="btn-google" onClick={() => {
              startGoogleLogin();
            }}>
            {Buttons.ContinueWithGoogle}
          </button>

          <div className="text-muted-small">{Modal.OrContinueWithEmail}</div>

          <div className="form-vertical">
            <label htmlFor="stub-email" className="label-sm">
              {Modal.EmailLabel}
            </label>
            <input
              id="stub-email"
              type="email"
              placeholder={Modal.EmailPlaceholder}
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className="input-text"
            />
          </div>

          <div className="actions mt-8">
            <button type="button" onClick={() => onStubComplete(email)} disabled={!email} className="btn-primary">
              {Buttons.Continue}
            </button>
            <button type="button" onClick={onClose} className="btn-secondary">
              {Buttons.Cancel}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

export default SignInModal;
