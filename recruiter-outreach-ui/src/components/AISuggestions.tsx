import React, { useState } from 'react';
import { fetchSuggestions, SuggestionsData } from '../services/aiSuggestionsApi';

export const AISuggestions: React.FC = () => {
  const [jobDescription, setJobDescription] = useState('');
  const [suggestions, setSuggestions] = useState<SuggestionsData | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [resumeFile, setResumeFile] = useState<File | null>(null);
  const [persistResume, setPersistResume] = useState(false);

  const handleGetSuggestions = async () => {
    setError(null);
    setLoading(true);
    setSuggestions(null);

    try {
      const data = await fetchSuggestions({
        jobDescription,
        resumeFile,
        persistResume,
      });
      setSuggestions(data);
    } catch (e: any) {
      setError(e.message ?? 'Failed to get suggestions');
    } finally {
      setLoading(false);
    }
  };

  const cleanedNarrative = suggestions?.suggestionsText
    ? suggestions.suggestionsText.replace(/\*\*/g, '')
    : '';

  let scoreValue = 0;
  if (suggestions?.ourScoring.matchScore === 'High') {
    scoreValue = 0.9;
  } else if (suggestions?.ourScoring.matchScore === 'Medium') {
    scoreValue = 0.6;
  } else if (suggestions?.ourScoring.matchScore === 'Low') {
    scoreValue = 0.3;
  }

  const hue = 120 * scoreValue; // 0 = red, 120 = green
  const scoreBarStyle: React.CSSProperties = {
    width: `${Math.max(0, Math.min(1, scoreValue)) * 100}%`,
    background: scoreValue
      ? `linear-gradient(90deg, #f97373, hsl(${hue}, 85%, 55%))`
      : 'transparent',
  };

  return (
    <>
      <section className="form-section suggestions-grid">
        <div className="field full-width-mobile">
          <h2 className="section-title">Job description</h2>
          <p className="section-help">Paste the JD you are targeting. The tool will suggest resume tweaks based on this.</p>
          <textarea
            id="jd"
            rows={6}
            value={jobDescription}
            onChange={(e) => setJobDescription(e.target.value)}
            placeholder="Paste the JD here to get resume suggestions"
          />
        </div>

        <div className="field suggestions-side">
          <div className="field">
            <label htmlFor="suggestionsResumeFile">Resume (optional)</label>
            <p className="section-help">Upload a resume file to compare against this JD. You can also choose to update the saved default resume.</p>
            <input
              id="suggestionsResumeFile"
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
              <span>Update saved resume for future suggestions</span>
            </label>
          </div>
          <div className="actions">
            <button
              type="button"
              onClick={handleGetSuggestions}
              disabled={loading || !jobDescription.trim()}
            >
              {loading ? 'Getting suggestions…' : 'Get suggestions'}
            </button>
          </div>

          {error && <div className="error">{error}</div>}
        </div>
      </section>

      {suggestions && (
        <section className="suggestions-section">
          <h2>Resume suggestions</h2>

          <p className="section-help">
            Match score: <strong>{suggestions.ourScoring.matchScore}</strong>
          </p>
          <div className="match-score-track">
            <div className="match-score-fill" style={scoreBarStyle} />
          </div>

          {(() => {
            const our = suggestions.ourScoring;
            const combined = Array.from(
              new Set<string>([...(our.keywordsToAdd || []), ...(our.missingKeywords || [])])
            );

            if (!combined.length) {
              return null;
            }

            return (
              <>
                <h3 className="section-title">Important keywords to add or emphasize</h3>
                <div className="suggestions-text-box">
                  <p className="section-help">{combined.join(', ')}</p>
                </div>
              </>
            );
          })()}

          <h3 className="section-title" style={{ marginTop: 14 }}>Detailed explanation &amp; summary</h3>
          <div className="suggestions-narrative">{cleanedNarrative}</div>
        </section>
      )}
    </>
  );
};
