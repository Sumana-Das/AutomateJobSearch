import { Buttons, Files, Labels, Messages, StyleParameters, Placeholders, Scores } from '../constants';
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
  const [modelId, setModelId] = useState<string>(Placeholders.ModelId);

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
      setError(e.message ?? Messages.FailedToGetSuggestions);
    } finally {
      setLoading(false);
    }
  };

  const narrative = suggestions?.suggestionsText ?? '';

  const scoreValue = Scores.ValueByLabel[suggestions?.ourScoring.matchScore ?? ''] ?? 0;

  const hue = StyleParameters.HueMax * scoreValue; // 0 = red, 120 = green
  const scoreBarStyle: React.CSSProperties = {
    width: `${Math.max(0, Math.min(1, scoreValue)) * 100}%`,
    background: scoreValue ? Scores.Gradient(hue) : StyleParameters.ScoreGradient,
  };

  return (
    <>
      <section className="form-section suggestions-grid">
        <div className="field full-width-mobile">
          <h2 className="section-title">{Labels.JobDescriptionTitle}</h2>
          <p className="section-help">{Labels.JobDescriptionHelp}</p>
          <div className="jd-textarea-wrapper">
            <button
              type="button"
              className="jd-input-icon"
              aria-label={Buttons.UploadResumeAria(resumeFile?.name)}
              onClick={() => resumeInputRef.current?.click()}
            >
              +
            </button>
            <textarea
              id="jd"
              rows={StyleParameters.TextareaRowsSmall}
              value={jobDescription}
              onChange={(e) => setJobDescription(e.target.value)}
              placeholder={Placeholders.JobDescription}
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
              accept={Files.AcceptResume}
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
                {Labels.AttachedResume} <strong>{resumeFile.name}</strong>
              </p>
              <label className="checkbox-inline" style={{ marginTop: 8 }}>
                <input
                  type="checkbox"
                  checked={persistResume}
                  onChange={(e) => setPersistResume(e.target.checked)}
                />
                <span>{Labels.SaveResumeForSuggestions}</span>
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
              {loading ? Buttons.GettingSuggestions : Buttons.GetSuggestions}
            </button>
          </div>

          {error && <div className="error">{error}</div>}
        </div>
      </section>

      {suggestions && (
        <section className="suggestions-section">
          <h2>{Labels.ResumeSuggestionsTitle}</h2>

          <p className="section-help">
            {Labels.MatchScoreLabel} <strong>{suggestions.ourScoring.matchScore}</strong>
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
                <h3 className="section-title">{Labels.ImportantKeywordsTitle}</h3>
                <div className="suggestions-text-box">
                  <p className="section-help">{combined.join(', ')}</p>
                </div>
              </>
            );
          })()}

          <h3 className="section-title" style={{ marginTop: 14 }}>{Labels.DetailedExplanationTitle}</h3>
          <div className="suggestions-narrative">
            <ReactMarkdown>{narrative}</ReactMarkdown>
          </div>
        </section>
      )}
    </>
  );
};
