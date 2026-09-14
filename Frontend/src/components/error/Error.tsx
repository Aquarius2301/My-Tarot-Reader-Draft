import { useEffect, useState } from "react";
import { Button, Typography, theme } from "antd";
import {
  DisconnectOutlined,
  ReloadOutlined,
  ExclamationCircleOutlined,
  ClockCircleOutlined,
} from "@ant-design/icons";
import { useTranslation } from "react-i18next";

const { Title, Paragraph } = Typography;

export type ErrorType = "offline" | "server" | "timeout" | "generic";

export interface ErrorProps {
  /** Explicit error type. If omitted the component auto-detects via navigator.onLine. */
  type?: ErrorType;
  /** Called when the user presses the retry button. If omitted the button is hidden. */
  onRetry?: () => void;
}

const ERROR_CONFIG: Record<
  ErrorType,
  {
    titleKey: string;
    descKey: string;
    Icon: typeof DisconnectOutlined;
  }
> = {
  offline: {
    titleKey: "components.error.offlineTitle",
    descKey: "components.error.offlineDesc",
    Icon: DisconnectOutlined,
  },
  server: {
    titleKey: "components.error.serverTitle",
    descKey: "components.error.serverDesc",
    Icon: ExclamationCircleOutlined,
  },
  timeout: {
    titleKey: "components.error.timeoutTitle",
    descKey: "components.error.timeoutDesc",
    Icon: ClockCircleOutlined,
  },
  generic: {
    titleKey: "components.error.genericTitle",
    descKey: "components.error.genericDesc",
    Icon: ReloadOutlined,
  },
};

/**
 * Full-bleed error screen that displays an icon, title, description, and an
 * optional retry button.  Supports four error types: offline, server, timeout,
 * and generic.
 *
 * When `type` is omitted the component auto-detects the offline state via
 * `navigator.onLine` and online/offline window events.
 */
export default function Error({ type, onRetry }: ErrorProps) {
  const { t } = useTranslation();
  const { token } = theme.useToken();

  // ---------- auto-detect offline when no explicit type is given ----------
  const [isOffline, setIsOffline] = useState(!navigator.onLine);

  useEffect(() => {
    if (type) return; // explicit type takes precedence — skip listener

    const handleOnline = () => setIsOffline(false);
    const handleOffline = () => setIsOffline(true);

    window.addEventListener("online", handleOnline);
    window.addEventListener("offline", handleOffline);
    return () => {
      window.removeEventListener("online", handleOnline);
      window.removeEventListener("offline", handleOffline);
    };
  }, [type]);

  const resolvedType: ErrorType = type ?? (isOffline ? "offline" : "generic");
  const { titleKey, descKey, Icon } = ERROR_CONFIG[resolvedType];

  // ---------- render ----------
  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        minHeight: "100%",
        padding: "40px 16px",
        textAlign: "center",
      }}
    >
      <Icon
        style={{
          fontSize: 72,
          color: token.colorTextSecondary,
          marginBottom: 24,
        }}
      />

      <Title level={4} style={{ margin: 0, marginBottom: 8 }}>
        {t(titleKey)}
      </Title>

      <Paragraph
        type="secondary"
        style={{ maxWidth: 400, margin: 0, marginBottom: onRetry ? 24 : 0 }}
      >
        {t(descKey)}
      </Paragraph>

      {onRetry && (
        <Button
          type="primary"
          icon={<ReloadOutlined />}
          onClick={onRetry}
          size="large"
          style={{ marginTop: 24 }}
        >
          {t("components.error.retry")}
        </Button>
      )}
    </div>
  );
}
