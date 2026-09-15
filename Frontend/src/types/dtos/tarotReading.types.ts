import type { TarotCardCode } from "@/constants";

export interface GetLastDrawnCardForGuestResult {
  cardCode: TarotCardCode;
  isReversed: boolean;
  remainingSeconds: number;
}

export interface CreateDrawForGuestRequest {
  cardCode: TarotCardCode;
  isReversed: boolean;
}

export interface GetLastDrawnCardForAuthResult {
  cardCode: TarotCardCode;
  isReversed: boolean;
}

export interface CreateDrawForAuthRequest {
  cardCode: TarotCardCode;
  isReversed: boolean;
}
