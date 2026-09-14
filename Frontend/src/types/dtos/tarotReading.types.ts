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
