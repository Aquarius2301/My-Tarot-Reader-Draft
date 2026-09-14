import type {
  CreateDrawForGuestRequest,
  GetLastDrawnCardForGuestResult,
} from "@/types";
import axiosClient from "./config.api";
import { API_URL } from "./url.api";

export const tarotReadingApi = {
  getLastDrawnCardForGuest: (): Promise<GetLastDrawnCardForGuestResult> =>
    axiosClient.get(API_URL.tarot.getLastDrawnCardForGuest),

  createDrawForGuestAsync: (
    request: CreateDrawForGuestRequest,
  ): Promise<void> =>
    axiosClient.post(API_URL.tarot.createDrawForGuest, request),
};
