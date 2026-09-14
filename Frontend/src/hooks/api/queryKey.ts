// auth.hooks.ts
export const AUTH_QUERY_KEY = ["auth"] as const;

// tarotReading.hooks.ts
export const TAROT_READING_QUERY_KEY = ["tarotReading"] as const;
export const GET_CARD_FOR_GUEST_QUERY_KEY = [
  ...TAROT_READING_QUERY_KEY,
  "getCardForGuest",
] as const;
