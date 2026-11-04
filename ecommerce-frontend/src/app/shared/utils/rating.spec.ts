import { countWords, ratingBand, ratingLabel } from './rating';

describe('rating utils', () => {

  it('maps ratings to color bands', () => {
    expect([1, 1.4, 2, 2.5, 3.4, 3.5, 4.4, 4.5, 5].map(ratingBand)).toEqual([1, 1, 2, 3, 3, 4, 4, 5, 5]);
  });

  it('keeps out-of-range values inside the bands', () => {
    expect(ratingBand(0)).toBe(1);
    expect(ratingBand(7)).toBe(5);
  });

  it('labels each band', () => {
    expect([1, 2, 3, 4, 5].map(ratingLabel)).toEqual(['Poor', 'Below average', 'Average', 'Good', 'Excellent']);
  });

  it('counts words across any whitespace', () => {
    expect(countWords('')).toBe(0);
    expect(countWords('   ')).toBe(0);
    expect(countWords(null)).toBe(0);
    expect(countWords(' one  two\nthree\tfour ')).toBe(4);
  });
});
