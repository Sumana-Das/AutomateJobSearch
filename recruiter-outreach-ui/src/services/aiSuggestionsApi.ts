import { ApiEndpoints, Messages } from '../constants';

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
  debugResumePreview: string;
};

export async function fetchSuggestions(input: {
  jobDescription: string;
  resumeFile?: File | null;
  persistResume: boolean;
  modelId?: string;
}): Promise<SuggestionsData> {
  const formData = new FormData();
  formData.append('jobDescription', input.jobDescription);
  formData.append('persistResume', String(input.persistResume));

  if (input.modelId) {
    formData.append('modelId', input.modelId);
  }

  if (input.resumeFile) {
    formData.append('resume', input.resumeFile);
  }

  const response = await fetch(ApiEndpoints.Suggestions, {
    method: 'POST',
    body: formData,
  });

  if (!response.ok) {
    throw new Error(Messages.SuggestionsStatus(response.status));
  }

  return (await response.json()) as SuggestionsData;
}
