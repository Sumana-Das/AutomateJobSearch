import { ApiEndpoints, Auth } from '../constants';

export function startGoogleLogin(returnUrl?: string) {
  const redirect = encodeURIComponent(
    (returnUrl || (window.location.origin + window.location.pathname + window.location.search))
  );
  const apiBase =
    (process.env[Auth.ApiBaseEnvKey] as string | undefined) ||
    (window.location.hostname === 'localhost' ? Auth.LocalhostApiBase : '');
  const authUrl = `${apiBase}${ApiEndpoints.AuthGoogle}?redirect=${redirect}`;
  window.location.href = authUrl;
}
