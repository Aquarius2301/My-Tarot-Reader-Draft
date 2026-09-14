export const API_URL = {
  auth: {
    login: "/api/auth/oauth",
    refresh: "/api/auth/refresh",
    logout: "/api/auth/logout",
    getCurrentUser: "/api/auth/me",
  },
  tarot: {
    getLastDrawnCardForGuest: "/api/tarot/guest-draw",
    createDrawForGuest: "/api/tarot/guest-draw",
  },
} as const;
