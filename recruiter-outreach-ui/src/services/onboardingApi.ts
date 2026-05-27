export type StatusInfo = {
  gmailConfigured: boolean;
  geminiConfigured: boolean;
  smtpUserName?: string;
};

export async function fetchStatus(): Promise<StatusInfo> {
  const response = await fetch('/api/status');
  if (!response.ok) {
    throw new Error(`Failed to load status (status ${response.status})`);
  }
  return (await response.json()) as StatusInfo;
}

export async function saveSetup(input: { gmailAppPassword?: string; geminiApiKey?: string }): Promise<void> {
  const response = await fetch('/api/setup', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  });

  if (!response.ok) {
    throw new Error(`Setup request failed with status ${response.status}`);
  }
}
