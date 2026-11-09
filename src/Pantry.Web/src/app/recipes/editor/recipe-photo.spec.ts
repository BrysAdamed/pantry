import { KEEP_PHOTO, photoProblemOf, removedPhoto } from './recipe-photo';

describe('photoProblemOf', () => {
  it('accepts JPEG, PNG and WebP up to 10 MB', () => {
    expect(photoProblemOf({ type: 'image/jpeg', size: 10 * 1024 * 1024 })).toBeNull();
    expect(photoProblemOf({ type: 'image/png', size: 1 })).toBeNull();
    expect(photoProblemOf({ type: 'image/webp', size: 1 })).toBeNull();
  });

  it('refuses other types before the size', () => {
    expect(photoProblemOf({ type: 'image/heic', size: 1 })).toBe('type');
    expect(photoProblemOf({ type: 'application/pdf', size: 20 * 1024 * 1024 })).toBe('type');
    expect(photoProblemOf({ type: '', size: 1 })).toBe('type');
  });

  it('refuses a photo over 10 MB', () => {
    expect(photoProblemOf({ type: 'image/jpeg', size: 10 * 1024 * 1024 + 1 })).toBe('size');
  });
});

describe('removedPhoto', () => {
  it('removes the saved photo', () => {
    expect(removedPhoto(true)).toEqual({ kind: 'remove' });
  });

  it('only forgets the picked file when nothing is saved', () => {
    expect(removedPhoto(false)).toBe(KEEP_PHOTO);
  });
});
