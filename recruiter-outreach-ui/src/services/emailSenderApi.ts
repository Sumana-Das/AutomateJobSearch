export type TemplateVariant = {
  subjectTemplate: string;
  bodyTemplate: string;
};

export type TemplateDto = {
  key: string;
  displayName: string;
  availableTemplates: string[];
  templates?: {
    hr?: TemplateVariant;
    referral?: TemplateVariant;
  };
};

export async function fetchTemplates(): Promise<TemplateDto[]> {
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

  return data;
}

export async function sendEmails(input: {
  recruiters: string;
  roleKey: string;
  templateKind: string;
  subject: string;
  emailBody: string;
  resumeFile?: File | null;
  persistResume: boolean;
}): Promise<void> {
  const formData = new FormData();
  formData.append('recruiters', input.recruiters);
  formData.append('roleKey', input.roleKey);
  formData.append('templateKind', input.templateKind);
  formData.append('company', '');
  formData.append('subject', input.subject);
  formData.append('emailBody', input.emailBody);
  formData.append('persistResume', String(input.persistResume));

  if (input.resumeFile) {
    formData.append('resume', input.resumeFile);
  }

  const response = await fetch('/api/outreach/send', {
    method: 'POST',
    body: formData,
  });

  if (!response.ok) {
    throw new Error(`Send request failed with status ${response.status}`);
  }
}
