// Custom Worker entry (replaces the plain static-assets-only default) so /api/*
// can be proxied server-side to the DigitalOcean API server over plain HTTP.
// This is the key trick: a browser on an HTTPS page is blocked from calling an
// HTTP endpoint directly ("mixed active content"), but a fetch made here, at
// Cloudflare's edge, isn't a browser request at all — mixed-content rules don't
// apply to server-side fetches. Everything else falls through to the static
// assets binding (the built React app).
const API_ORIGIN = 'http://138.197.142.126'

export default {
  async fetch(request, env) {
    const url = new URL(request.url)

    if (url.pathname.startsWith('/api/')) {
      const target = new URL(url.pathname + url.search, API_ORIGIN)
      return fetch(new Request(target, request))
    }

    return env.ASSETS.fetch(request)
  },
}
