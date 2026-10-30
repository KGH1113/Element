/**
 * Lenis smooth scroll 전역 설정
 * 모든 컴포넌트에서 일관된 스크롤 경험을 위해 사용
 */
export const LENIS_CONFIG = {
  /** 스크롤 애니메이션 지속 시간 (초) */
  // 낮을수록 빠르게 스크롤
  duration: 0.7,
  /** 휠 스크롤 속도 multiplier */
  // 낮을수록 느리게 스크롤
  wheelMultiplier: 0.9,
} as const;

export type LenisConfig = typeof LENIS_CONFIG;
