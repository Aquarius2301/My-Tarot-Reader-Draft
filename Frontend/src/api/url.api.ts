export const API_URL = {
  auth: {
    login: "/api/auth/oauth",
    refresh: "/api/auth/refresh",
    logout: "/api/auth/logout",
    getCurrentUser: "/api/auth/me",
  },
} as const;
