import { authApi } from "@/api";
import type { GoogleLoginRequest } from "@/types";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AUTH_QUERY_KEY } from "./queryKey";

export const useLogin = () => {
  return useMutation({
    mutationFn: (body: GoogleLoginRequest) => authApi.googleLogin(body),
  });
};

export const useLogout = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => authApi.logout(),
    onSuccess: () => queryClient.clear(),
  });
};

export const useGetCurrentUser = (enabled = true) =>
  useQuery({
    queryKey: AUTH_QUERY_KEY,
    queryFn: authApi.getCurrentUser,
    enabled,
    retry: false, // 401 Unauthorized is expected when the user is not logged in, so we don't want to retry.
    staleTime: 5 * 60 * 1000,
  });
