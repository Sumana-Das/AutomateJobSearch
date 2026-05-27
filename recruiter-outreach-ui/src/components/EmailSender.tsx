import React, { useEffect, useMemo, useState } from 'react';
import { fetchTemplates, sendEmails, TemplateDto } from '../services/emailSenderApi';

export type SimpleTemplate = {
  key: string;
  displayName: string;
  availableTemplates: string[];
};

export const EmailSender: React.FC = () => {
  const [templates, setTemplates] = useState<TemplateDto[]>([]);
  const [roleKey, setRoleKey] = useState<string>('');
  const [templateKind, setTemplateKind] = useState<string>('');
  const [recruiterEmails, setRecruiterEmails] = useState('');
  const [emailSubject, setEmailSubject] = useState('');
  const [emailBody, setEmailBody] = useState('');
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [resumeFile, setResumeFile] = useState<File | null>(null);
  const [persistResume, setPersistResume] = useState(false);

  useEffect(() => {
    const load = async () => {
      try {
        const data = await fetchTemplates();
        setTemplates(data);
      } catch (e: any) {
        setError(e.message ?? 'Failed to load templates');
      }
    };

    load();
  }, []);

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
      const resolvedSubject = (variant.subjectTemplate ?? '').replaceAll('{Role}', roleName);
      const resolvedBody = (variant.bodyTemplate ?? '').replaceAll('{Role}', roleName);
      setEmailSubject(resolvedSubject);
      setEmailBody(resolvedBody);
    }
  }, [roleKey, templateKind, templates]);

  const canSubmit = useMemo(
    () =>
      recruiterEmails.trim().length > 0 &&
      emailSubject.trim().length > 0 &&
      emailBody.trim().length > 0 &&
      roleKey.trim().length > 0 &&
      templateKind.trim().length > 0 &&
      templates.length > 0,
    [recruiterEmails, emailSubject, emailBody, roleKey, templateKind, templates]
  );

  const handleSend = async () => {
    setError(null);
    setSending(true);

    try {
      await sendEmails({
        recruiters: recruiterEmails,
        roleKey,
        templateKind,
        subject: emailSubject,
        emailBody,
        resumeFile,
        persistResume,
      });
    } catch (e: any) {
      setError(e.message ?? 'Failed to send emails');
    } finally {
      setSending(false);
    }
  };

  return (
    <section className="form-section email-grid">
      <div className="field">
        <label htmlFor="roleKey">Role *</label>
        <p className="section-help">Choose the role you want to apply for</p>
        <select
          id="roleKey"
          value={roleKey}
          onChange={(e) => {
            const newRoleKey = e.target.value;
            setRoleKey(newRoleKey);
            if (!newRoleKey) {
              setTemplateKind('');
              setEmailSubject('');
              setEmailBody('');
              return;
            }
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
        <label htmlFor="templateKind">Template *</label>
        <p className="section-help">Choose the email template variant to use</p>
        <select
          id="templateKind"
          value={templateKind}
          onChange={(e) => setTemplateKind(e.target.value)}
          disabled={templates.length === 0 || !roleKey}
        >
          <option value="">Select template</option>
          {(templates.find((t) => t.key === roleKey)?.availableTemplates ?? []).map((k) => (
            <option key={k} value={k}>
              {k === 'Hr' ? 'HR – Direct' : k === 'Referral' ? 'Referral' : k}
            </option>
          ))}
        </select>
      </div>

      {/* Left column: recruiter contact + resume */}
      <div className="field recruiters-field">
        <label htmlFor="recruiters">Recruiter contact *</label>
        <p className="section-help">Enter email IDs (allowed - commas, semicolons, spaces, new lines)</p>
        <textarea
          id="recruiters"
          rows={10}
          value={recruiterEmails}
          onChange={(e) => setRecruiterEmails(e.target.value)}
          placeholder="e.g. recruiter1@example.com, recruiter2@example.com"
        />

        <label htmlFor="resumeFile">Resume</label>
        <input
          id="resumeFile"
          type="file"
          onChange={(e) => {
            const file = e.target.files?.[0] ?? null;
            setResumeFile(file);
          }}
        />

        <label className="checkbox-inline">
          <input
            type="checkbox"
            checked={persistResume}
            onChange={(e) => setPersistResume(e.target.checked)}
          />
          <span className="section-help">Save this resume for future emails</span>
        </label>
      </div>

      {/* Right column: subject + body */}
      <div className="field body-field">
        <label htmlFor="emailSubject">Email subject</label>
        <p className="section-help">You can customize the subject before sending</p>
        <input
          id="emailSubject"
          type="text"
          value={emailSubject}
          onChange={(e) => setEmailSubject(e.target.value)}
          placeholder="Subject line for your email"
        />

        <label htmlFor="emailBody">Email body</label>
        <p className="section-help">You can customize the email body before sending.</p>
        <textarea
          id="emailBody"
          rows={10}
          value={emailBody}
          onChange={(e) => setEmailBody(e.target.value)}
          placeholder="Write or paste the email you want to send to recruiters"
        />
      </div>

      <div className="actions full-width">
        <button type="button" onClick={handleSend} disabled={sending || !canSubmit}>
          {sending ? 'Sending…' : 'Send emails now'}
        </button>
      </div>

      {error && <div className="error">{error}</div>}
    </section>
  );
};
