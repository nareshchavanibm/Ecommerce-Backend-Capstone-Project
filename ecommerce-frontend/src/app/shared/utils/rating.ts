export type RatingBand = 1 | 2 | 3 | 4 | 5;

export const MAX_REVIEW_WORDS = 1000;

export const MAX_REVIEW_TITLE = 100;

const LABELS: Record<RatingBand, string> = {
  1: 'Poor',
  2: 'Below average',
  3: 'Average',
  4: 'Good',
  5: 'Excellent'
};

// Maps a rating (1-5, decimals allowed) to its color band.
export function ratingBand(value: number): RatingBand {
  return Math.min(5, Math.max(1, Math.round(value))) as RatingBand;
}

export function ratingLabel(value: number): string {
  return LABELS[ratingBand(value)];
}

export function countWords(text: string | null | undefined): number {
  const trimmed = text?.trim();

  return trimmed ? trimmed.split(/\s+/).length : 0;
}
