export function startGoogleLogin(returnUrl?: string) {
  const redirect = encodeURIComponent(
    (returnUrl || (window.location.origin + window.location.pathname + window.location.search))
  );
  const apiBase =
    (process.env.REACT_APP_API_BASE_URL as string | undefined) ||
    (window.location.hostname === 'localhost' ? 'https://localhost:7200' : '');
  const authUrl = `${apiBase}/auth/google?redirect=${redirect}`;
  window.location.href = authUrl;
}
