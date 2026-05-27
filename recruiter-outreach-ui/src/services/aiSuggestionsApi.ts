export type ScoringView = {
  keywordsToAdd: string[];
  missingKeywords: string[];
  matchScore: string;
};

export type SuggestionsData = {
  suggestionsText: string;
  ourScoring: ScoringView;
  geminiScoring: ScoringView;
  jdExcerpt: string;
};

export async function fetchSuggestions(input: {
  jobDescription: string;
  resumeFile?: File | null;
  persistResume: boolean;
}): Promise<SuggestionsData> {
  const formData = new FormData();
  formData.append('jobDescription', input.jobDescription);
  formData.append('persistResume', String(input.persistResume));

  if (input.resumeFile) {
    formData.append('resume', input.resumeFile);
  }

  const response = await fetch('/api/outreach/suggestions', {
    method: 'POST',
    body: formData,
  });

  if (!response.ok) {
    throw new Error(`Suggestions request failed with status ${response.status}`);
  }

  return (await response.json()) as SuggestionsData;
}
