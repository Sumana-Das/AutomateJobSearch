import React, { useState, useRef } from 'react';
import ReactMarkdown from 'react-markdown';
import { fetchSuggestions, SuggestionsData } from '../services/aiSuggestionsApi';

export const AISuggestions: React.FC = () => {
  const [jobDescription, setJobDescription] = useState('');
  const [suggestions, setSuggestions] = useState<SuggestionsData | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [resumeFile, setResumeFile] = useState<File | null>(null);
  const [persistResume, setPersistResume] = useState(false);
  const [modelId, setModelId] = useState<string>('gemini-3.5-flash');

  const resumeInputRef = useRef<HTMLInputElement | null>(null);

  const handleGetSuggestions = async () => {
    setError(null);
    setLoading(true);
    setSuggestions(null);

    try {
      const data = await fetchSuggestions({
        jobDescription,
        resumeFile,
        persistResume,
        modelId,
      });
      setSuggestions(data);
    } catch (e: any) {
      setError(e.message ?? 'Failed to get suggestions');
    } finally {
      setLoading(false);
    }
  };

  const narrative = suggestions?.suggestionsText ?? '';

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
          <p className="section-help">Paste the JD you are targeting along with your resume* (optional* in case you have resume already saved in location)</p>
          <div className="jd-textarea-wrapper">
            <button
              type="button"
              className="jd-input-icon"
              aria-label={resumeFile ? `Change uploaded resume (${resumeFile.name})` : 'Upload resume to use for suggestions'}
              onClick={() => resumeInputRef.current?.click()}
            >
              +
            </button>
            <textarea
              id="jd"
              rows={4}
              value={jobDescription}
              onChange={(e) => setJobDescription(e.target.value)}
              placeholder="The Job Description"
            />
            <select
              className="jd-corner-model-select"
              value={modelId}
              onChange={(e) => setModelId(e.target.value)}
            >
              <option value="gemini-3.1-pro">AI - Advanced</option>
              <option value="gemini-3.5-flash">AI - Standard</option>
              <option value="gemini-3.1-flash-lite">AI - Lite</option>
            </select>
            <input
              ref={resumeInputRef}
              type="file"
              accept=".pdf,.doc,.docx,.txt"
              style={{ display: 'none' }}
              onChange={(e) => {
                const file = e.target.files?.[0] ?? null;
                setResumeFile(file);
              }}
            />
          </div>
          {resumeFile && (
            <>
              <p className="section-help" style={{ marginTop: 6 }}>
                Attached resume: <strong>{resumeFile.name}</strong>
              </p>
              <label className="checkbox-inline" style={{ marginTop: 8 }}>
                <input
                  type="checkbox"
                  checked={persistResume}
                  onChange={(e) => setPersistResume(e.target.checked)}
                />
                <span>Save uploaded resume for future suggestions</span>
              </label>
            </>
          )}
        </div>

        <div className="field suggestions-side">
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
          <div className="suggestions-narrative">
            <ReactMarkdown>{narrative}</ReactMarkdown>
          </div>
        </section>
      )}
    </>
  );
};
