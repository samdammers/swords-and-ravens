import { isMobile } from "react-device-detect";

type NavigatorWithUserAgentData = Navigator & {
  userAgentData?: {
    mobile?: boolean;
  };
};

export function usesMobileLayout(): boolean {
  if (isMobile) {
    return true;
  }

  if (typeof window === "undefined" || typeof navigator === "undefined") {
    return false;
  }

  const navigatorWithUserAgentData = navigator as NavigatorWithUserAgentData;
  if (navigatorWithUserAgentData.userAgentData?.mobile) {
    return true;
  }

  const hasCoarsePointer =
    typeof window.matchMedia === "function" &&
    window.matchMedia("(pointer: coarse)").matches;
  const hasTouchPoints = navigator.maxTouchPoints > 0;
  const smallestViewportDimension = Math.min(
    window.innerWidth,
    window.innerHeight
  );

  return (hasCoarsePointer || hasTouchPoints) && smallestViewportDimension <= 1024;
}
