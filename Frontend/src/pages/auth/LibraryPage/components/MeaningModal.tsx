import { ResponsiveModal, TarotCard } from "@/components";
import { Button, Card, Flex, Typography } from "antd";
import { RetweetOutlined } from "@ant-design/icons";
import { TAROT_SECTIONS, type TarotCardCode } from "@/constants";
import { useTranslation } from "react-i18next";
import { useState } from "react";
import i18n from "@/i18n";

interface MeaningModalProps {
  selectedCard: TarotCardCode | null;
  onSelectedCard: (card: TarotCardCode | null) => void;
}
export default function MeaningModal({
  selectedCard,
  onSelectedCard,
}: MeaningModalProps) {
  const { t } = useTranslation();

  const [isReversed, setIsReversed] = useState(false);

  const orientation = isReversed
    ? t("tarot.position.reversed")
    : t("tarot.position.upright");

  const name = t(`tarot.meaning.${selectedCard}.name`);

  const meaningKey = (section: string) =>
    `tarot.meaning.${selectedCard}.${isReversed ? "reversed" : "upright"}.${section}`;

  return (
    <ResponsiveModal
      title={
        <>
          {t("page.library.meaning", { card: name, orientation })}{" "}
          <Button onClick={() => setIsReversed(!isReversed)} type="text">
            <RetweetOutlined />
          </Button>
        </>
      }
      footer={null}
      open={!!selectedCard}
      onClose={() => onSelectedCard(null)}
    >
      <Flex vertical align="center">
        <div
          style={{
            display: "flex",
            justifyContent: "center",
            margin: "16px 0",
          }}
        >
          <TarotCard
            cardCode={selectedCard as TarotCardCode}
            isUpright={!isReversed}
            isFlipped
            size="md"
          />
        </div>

        {/* <Flex align="center" gap={8}>
          <Title level={4} style={{ margin: 0 }}>
            {name} · {orientation}
          </Title>
          <Button onClick={() => setIsReversed(!isReversed)} type="text">
            <RetweetOutlined />
          </Button>
        </Flex> */}

        <Card style={{ textAlign: "left", marginTop: 16 }}>
          {TAROT_SECTIONS.map((section) => {
            const key = meaningKey(section);
            const text = i18n.exists(key)
              ? t(key)
              : t(`tarot.placeholder.${section}`);
            return (
              <div key={section} style={{ marginBottom: 16 }}>
                <Typography.Text strong>
                  {t(`tarot.section.${section}`)}
                </Typography.Text>
                <Typography.Paragraph style={{ marginTop: 4 }}>
                  {text}
                </Typography.Paragraph>
              </div>
            );
          })}
        </Card>
      </Flex>
    </ResponsiveModal>
  );
}
