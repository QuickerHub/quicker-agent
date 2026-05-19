import { useState } from "react";
import type { ActionSummary } from "../types";
import {
  normalizeHttpIconUrl,
  parseQuickerFaSpec,
} from "../lib/faIcon";

interface ActionIconProps {
  action: ActionSummary;
}

function FaIcon({
  classes,
  color,
}: {
  classes: string;
  color?: string | null;
}) {
  return (
    <span className="action-icon-container">
      <i
        className={classes}
        style={{
          fontSize: "22px",
          lineHeight: "24px",
          color: color?.trim() || "#999999",
        }}
        aria-hidden
      />
    </span>
  );
}

export function ActionIcon({ action }: ActionIconProps) {
  const [imgFailed, setImgFailed] = useState(false);

  if (action.listIconKind === "img" && action.listIconImgUrl) {
    return (
      <span className="action-icon-container">
        <img className="action-icon" src={action.listIconImgUrl} alt="" />
      </span>
    );
  }

  if (action.listIconKind === "fa" && action.listIconFaClasses) {
    return (
      <FaIcon classes={action.listIconFaClasses} color={action.listIconColor} />
    );
  }

  const httpSrc = normalizeHttpIconUrl(action.iconUrl);
  if (httpSrc && !imgFailed) {
    return (
      <span className="action-icon-container">
        <img
          className="action-icon"
          src={httpSrc}
          alt=""
          onError={() => setImgFailed(true)}
        />
      </span>
    );
  }

  const parsed = parseQuickerFaSpec(action.iconUrl);
  if (parsed) {
    return (
      <FaIcon
        classes={`${parsed.prefix} ${parsed.iconClass}`}
        color={parsed.color}
      />
    );
  }

  return (
    <span
      className="action-icon-container action-icon-container--empty"
      aria-hidden
    />
  );
}
