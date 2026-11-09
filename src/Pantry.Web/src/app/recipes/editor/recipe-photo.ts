import { RECIPE_PHOTO_LIMITS } from '../recipes-api';

/** What the editor does with the photo when the recipe saves: nothing, upload a new one, or remove the current one. */
export type PhotoChange = { kind: 'keep' } | { kind: 'set'; file: File } | { kind: 'remove' };

export type PhotoProblem = 'type' | 'size';

export const KEEP_PHOTO: PhotoChange = { kind: 'keep' };

/** Why the API would refuse `file`, checked before anything is sent; the API checks the content again. */
export function photoProblemOf(file: Pick<File, 'type' | 'size'>): PhotoProblem | null {
  if (!(RECIPE_PHOTO_LIMITS.types as readonly string[]).includes(file.type)) {
    return 'type';
  }
  return file.size > RECIPE_PHOTO_LIMITS.maxBytes ? 'size' : null;
}

/** The change after removing the photo: a recipe without a saved photo just forgets the picked file. */
export function removedPhoto(hasSavedPhoto: boolean): PhotoChange {
  return hasSavedPhoto ? { kind: 'remove' } : KEEP_PHOTO;
}
