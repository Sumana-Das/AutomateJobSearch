export const ApiEndpoints = {
  Templates: '/api/templates',
  SendEmails: '/api/outreach/send',
  Suggestions: '/api/outreach/suggestions',
  Me: '/api/me',
  AuthGoogle: '/auth/google',
} as const;

export const Messages = {
  // Success
  EmailsSentSuccess: 'Emails sent successfully.',

  // Errors
  FailedToLoadTemplates: 'Failed to load templates',
  FailedToSendEmails: 'Failed to send emails',
  FailedToGetSuggestions: 'Failed to get suggestions',
  SessionExpired: 'Your session has expired. Please sign in again.',

  // Network formatted
  LoadTemplatesStatus: (status: number) => `Failed to load templates (status ${status})`,
  SendStatus: (status: number) => `Send request failed with status ${status}`,
  SuggestionsStatus: (status: number) => `Suggestions request failed with status ${status}`,
} as const;

export const Labels = {
  Role: 'Role *',
  RoleHelp: 'Choose the role you want to apply for',
  Template: 'Template *',
  TemplateHelp: 'Choose the email template variant to use',
  RecruiterContact: 'Recruiter contact *',
  RecruiterHelp: 'Enter email IDs (allowed - commas, semicolons, spaces, new lines)',
  Resume: 'Resume',
  ResumeHelp: 'Upload updated resume / Use existing resume on portal',
  SaveResume: 'Save this resume for future emails',
  EmailSubject: 'Email subject',
  EmailSubjectHelp: 'You can customize the subject before sending',
  EmailBody: 'Email body',
  EmailBodyHelp: 'You can customize the email body before sending.',
  AttachedResume: 'Attached resume:',
  SaveResumeForSuggestions: 'Save uploaded resume for future suggestions',
  JobDescriptionTitle: 'Job description',
  JobDescriptionHelp: 'Paste the JD you are targeting along with your resume* (optional* in case you have resume already saved in location)',
  ResumeSuggestionsTitle: 'Resume suggestions',
  ImportantKeywordsTitle: 'Important keywords to add or emphasize',
  DetailedExplanationTitle: 'Detailed explanation & summary',
  MatchScoreLabel: 'Match score:',
  SelectRole: 'Select role',
  SelectTemplate: 'Select template',
} as const;

export const Placeholders = {
  Recruiters: 'e.g. recruiter1@example.com, recruiter2@example.com',
  EmailSubject: 'Subject line for your email',
  EmailBody: 'Write or paste the email you want to send to recruiters',
  JobDescription: 'The Job Description',
  // Template placeholders used in server-provided templates
  RoleToken: '{Role}',
  ModelId: 'gemini-3.5-flash',
} as const;

export const Buttons = {
  SendNow: 'Send emails now',
  Sending: 'Sending…',
  GetSuggestions: 'Get suggestions',
  GettingSuggestions: 'Getting suggestions…',
  UploadResumeAria: (name?: string | null) =>
    name ? `Change uploaded resume (${name})` : 'Upload resume to use for suggestions',
  SignInSignUp: 'Sign in / Sign up',
  SignOut: 'Sign out',
  ContinueWithGoogle: 'Continue with Google',
  Continue: 'Continue',
  Cancel: 'Cancel',
} as const;

export const TemplateKinds = {
  Hr: 'Hr',
  Referral: 'Referral',
  Labels: {
    Hr: 'HR – Direct',
    Referral: 'Referral',
  },
} as const;

export const Files = {
  AcceptResume: '.pdf,.doc,.docx,.txt',
} as const;

export const StyleParameters = {
  TextareaRowsSmall: 4,
  HueMax: 120, // 0 red, 120 green
  ScoreGradient: 'transparent',

} as const;

export const Scores = {
  // Mapping UI score bar values from textual match scores
  ValueByLabel: {
    High: 0.9,
    Medium: 0.6,
    Low: 0.3,
  } as Record<string, number>,
  Gradient: (hue: number) => `linear-gradient(90deg, #f97373, hsl(${hue}, 85%, 55%))`,
} as const;

export const Values = {
  EmptyCompany: '',
  SessionExpiredCode: 'SESSION_EXPIRED',
} as const;

export const App = {
  BrandTitle: 'TailorMailer AI',
  DocumentTitle: 'TailorMailer AI',
  LandingIntro: 'Welcome! Sign in to get started with Email outreach and AI suggestions.',
} as const;

export const StorageKeys = {
  Auth: 'recruiterOutreachAuth',
  UserEmail: 'recruiterOutreachUserEmail',
  UserName: 'recruiterOutreachUserName',
  Secrets: 'recruiterOutreachSecrets',
} as const;

export const Auth = {
  LoginQueryKey: 'login',
  LoginSuccessValue: 'success',
  ApiBaseEnvKey: 'REACT_APP_API_BASE_URL',
  LocalhostApiBase: 'https://localhost:7200',
} as const;

export const Modal = {
  SignInTitle: 'Sign in to TailorMailer',
  CloseAria: 'Close',
  OrContinueWithEmail: 'or continue with email',
  EmailLabel: 'Email',
  EmailPlaceholder: 'you@example.com',
} as const;
