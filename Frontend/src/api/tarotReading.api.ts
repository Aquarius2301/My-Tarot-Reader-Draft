import type {
  CreateDrawForAuthRequest,
  CreateDrawForGuestRequest,
  GetLastDrawnCardForAuthResult,
  GetLastDrawnCardForGuestResult,
} from "@/types";
import axiosClient from "./config.api";
import { API_URL } from "./url.api";

export const tarotReadingApi = {
  getLastDrawnCardForGuest: (): Promise<GetLastDrawnCardForGuestResult> =>
    axiosClient.get(API_URL.tarot.getLastDrawnCardForGuest),

  createDrawForGuest: (request: CreateDrawForGuestRequest): Promise<void> =>
    axiosClient.post(API_URL.tarot.createDrawForGuest, request),

  getLastDrawnCardForAuth: (): Promise<GetLastDrawnCardForAuthResult> =>
    axiosClient.get(API_URL.tarot.getLastDrawnCardForAuth),

  createDrawForAuth: (request: CreateDrawForAuthRequest): Promise<void> =>
    axiosClient.post(API_URL.tarot.createDrawForAuth, request),
};
