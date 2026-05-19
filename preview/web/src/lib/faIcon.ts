const FA_STYLES: Record<string, string> = {
  Light: "fal",
  Solid: "fas",
  Regular: "far",
  Brands: "fab",
};

export interface FaIconMeta {
  prefix: string;
  iconClass: string;
  color: string | null;
}

export function parseQuickerFaSpec(spec: string | null | undefined): FaIconMeta | null {
  if (!spec) return null;
  const raw = spec.trim();
  if (!raw.toLowerCase().startsWith("fa:")) return null;

  let body = raw.slice(3).trim();
  if (!body) return null;

  let color: string | null = null;
  const hashIdx = body.indexOf("#");
  if (hashIdx >= 0) {
    color = body.slice(hashIdx).trim();
    body = body.slice(0, hashIdx).replace(/:+$/, "").trim();
  } else {
    const parts = body.split(":");
    if (parts.length > 1) {
      const tail = parts[parts.length - 1]?.trim() ?? "";
      if (tail && !tail.includes("_") && !/^[A-Z]/.test(tail)) {
        body = parts.slice(0, -1).join(":").trim();
      }
    }
  }

  const underscore = body.indexOf("_");
  if (underscore <= 0) return null;

  const styleKey = body.slice(0, underscore);
  const glyph = body.slice(underscore + 1);
  if (!glyph) return null;

  const prefix = FA_STYLES[styleKey] ?? "fal";
  const iconClass = `fa-${pascalToKebab(glyph)}`;
  return { prefix, iconClass, color };
}

function pascalToKebab(name: string): string {
  return name
    .replace(/_/g, "-")
    .replace(/([a-z0-9])([A-Z])/g, "$1-$2")
    .toLowerCase();
}

export function isHttpIconUrl(iconUrl: string | null | undefined): boolean {
  if (!iconUrl) return false;
  const s = iconUrl.trim().toLowerCase();
  return s.startsWith("http://") || s.startsWith("https://") || s.startsWith("//");
}

export function normalizeHttpIconUrl(iconUrl: string | null | undefined): string | null {
  if (!isHttpIconUrl(iconUrl)) return null;
  const s = iconUrl!.trim();
  return s.startsWith("//") ? `https:${s}` : s;
}
