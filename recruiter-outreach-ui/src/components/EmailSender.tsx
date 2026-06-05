import { Buttons, Labels, Placeholders, TemplateKinds, Messages, Values } from '../constants';
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
  const [success, setSuccess] = useState<string | null>(null);
  const [resumeFile, setResumeFile] = useState<File | null>(null);
  const [persistResume, setPersistResume] = useState(false);

  useEffect(() => {
    const load = async () => {
      try {
        const data = await fetchTemplates();
        setTemplates(data);
      } catch (e: any) {
        setError(e.message ?? Messages.FailedToLoadTemplates);
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
      templateKind === TemplateKinds.Referral ? selected.templates.referral ?? selected.templates.hr : selected.templates.hr;

    if (variant) {
      const roleName = selected.displayName || selected.key;
      const resolvedSubject = (variant.subjectTemplate ?? '').replaceAll(Placeholders.RoleToken, roleName);
      const resolvedBody = (variant.bodyTemplate ?? '').replaceAll(Placeholders.RoleToken, roleName);
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
    setSuccess(null);
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
      setSuccess(Messages.EmailsSentSuccess);
    } catch (e: any) {
      if (e && (e.code === Values.SessionExpiredCode || e.status === 401)) {
        setError(Messages.SessionExpired);
      } else {
        setError(e?.message ?? Messages.FailedToSendEmails);
      }
    } finally {
      setSending(false);
    }
  };

  return (
    <section className="form-section email-grid">
      <div className="field">
        <label htmlFor="roleKey">{Labels.Role}</label>
        <p className="section-help">{Labels.RoleHelp}</p>
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
          <option value="">{Labels.SelectRole}</option>
          {templates.map((t) => (
            <option key={t.key} value={t.key}>
              {t.displayName || t.key}
            </option>
          ))}
        </select>
      </div>

      <div className="field">
        <label htmlFor="templateKind">{Labels.Template}</label>
        <p className="section-help">{Labels.TemplateHelp}</p>
        <select
          id="templateKind"
          value={templateKind}
          onChange={(e) => setTemplateKind(e.target.value)}
          disabled={templates.length === 0 || !roleKey}
        >
          <option value="">{Labels.SelectTemplate}</option>
          {(templates.find((t) => t.key === roleKey)?.availableTemplates ?? []).map((k) => (
            <option key={k} value={k}>
              {k === TemplateKinds.Hr ? TemplateKinds.Labels.Hr : k === TemplateKinds.Referral ? TemplateKinds.Labels.Referral : k}
            </option>
          ))}
        </select>
      </div>

      {/* Left column: recruiter contact + resume */}
      <div className="field recruiters-field">
        <label htmlFor="recruiters">{Labels.RecruiterContact}</label>
        <p className="section-help">{Labels.RecruiterHelp}</p>
        <textarea
          id="recruiters"
          rows={4}
          value={recruiterEmails}
          onChange={(e) => setRecruiterEmails(e.target.value)}
          placeholder={Placeholders.Recruiters}
        />

        <label htmlFor="resumeFile">{Labels.Resume}</label>
        <p className="section-help">{Labels.ResumeHelp}</p>
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
          <span className="section-help">{Labels.SaveResume}</span>
        </label>
      </div>

      {/* Right column: subject + body */}
      <div className="field body-field">
        <label htmlFor="emailSubject">{Labels.EmailSubject}</label>
        <p className="section-help">{Labels.EmailSubjectHelp}</p>
        <input
          id="emailSubject"
          type="text"
          value={emailSubject}
          onChange={(e) => setEmailSubject(e.target.value)}
          placeholder={Placeholders.EmailSubject}
        />

        <label htmlFor="emailBody">{Labels.EmailBody}</label>
        <p className="section-help">{Labels.EmailBodyHelp}</p>
        <textarea
          id="emailBody"
          rows={4}
          value={emailBody}
          onChange={(e) => setEmailBody(e.target.value)}
          placeholder={Placeholders.EmailBody}
        />
      </div>

      <div className="actions full-width">
        <button type="button" onClick={handleSend} disabled={sending || !canSubmit}>
          {sending ? Buttons.Sending : Buttons.SendNow}
        </button>
      </div>

      {success && <div className="success">{success}</div>}
      {error && <div className="error">{error}</div>}
    </section>
  );
};
