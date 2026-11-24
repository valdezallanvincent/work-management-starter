import { EnumLabelPipe } from './enum-label.pipe';

describe('EnumLabelPipe', () => {
  const pipe = new EnumLabelPipe();

  it('splits PascalCase values into a sentence-case label', () => {
    expect(pipe.transform('InProgress')).toBe('In progress');
    expect(pipe.transform('InReview')).toBe('In review');
  });

  it('leaves single words capitalised and handles empty values', () => {
    expect(pipe.transform('Open')).toBe('Open');
    expect(pipe.transform(null)).toBe('');
  });
});
