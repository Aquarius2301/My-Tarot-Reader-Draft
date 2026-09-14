import i18n from "i18next";
import { initReactI18next } from "react-i18next";
import { viPages } from "./locales/pages/vi.pages";
import { enPages } from "./locales/pages/en.pages";
import { viComponents } from "./locales/components/vi.components";
import { enComponents } from "./locales/components/en.components";

/**
 * Merge tarot meanings data into i18n resources under tarot.meanings
 */
function buildResources() {
  return {
    vi: {
      translation: {
        ...viPages,
        ...viComponents,
      },
    },
    en: {
      translation: {
        ...enPages,
        ...enComponents,
      },
    },
  };
}

export const resources = buildResources();

i18n.use(initReactI18next).init({
  resources,
  lng: "vi",
  fallbackLng: "en",
  interpolation: {
    escapeValue: false,
  },
});

export default i18n;
