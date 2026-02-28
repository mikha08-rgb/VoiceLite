import React from "react";

const VoiceLiteLogo = ({
  width,
  height,
  className,
}: {
  width?: number;
  height?: number;
  className?: string;
}) => {
  return (
    <svg
      width={width}
      height={height}
      className={className}
      viewBox="0 0 200 40"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
    >
      <text
        x="100"
        y="30"
        textAnchor="middle"
        className="logo-primary"
        fill="currentColor"
        fontFamily="system-ui, -apple-system, sans-serif"
        fontSize="32"
        fontWeight="700"
        letterSpacing="-0.5"
      >
        VoiceLite
      </text>
    </svg>
  );
};

export default VoiceLiteLogo;
