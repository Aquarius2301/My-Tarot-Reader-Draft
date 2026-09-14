import { tarotReadingApi } from "@/api/tarotReading.api";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { GET_CARD_FOR_GUEST_QUERY_KEY } from "./queryKey";
import type { CreateDrawForGuestRequest } from "@/types";

export const useGetLastDrawnCardForGuest = (enabled: boolean = true) => {
  return useQuery({
    queryKey: GET_CARD_FOR_GUEST_QUERY_KEY,
    queryFn: async () => tarotReadingApi.getLastDrawnCardForGuest(),
    enabled,
    staleTime: Infinity, // The result is valid until the next day, so we can cache it indefinitely.
  });
};

export const useCreateDrawForGuest = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (request: CreateDrawForGuestRequest) =>
      tarotReadingApi.createDrawForGuestAsync(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: GET_CARD_FOR_GUEST_QUERY_KEY });
    },
  });
};
