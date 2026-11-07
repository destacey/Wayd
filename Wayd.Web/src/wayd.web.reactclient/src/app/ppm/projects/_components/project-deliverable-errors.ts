import { ApiError } from '@/src/utils'

/**
 * What to tell someone whose deliverable change failed. A 409 means another
 * change to the project saved first, and a 404 that the deliverable was
 * deleted; either way what they are looking at is out of date, so both say to
 * reload rather than repeating the server's text.
 */
export const deliverableErrorMessage = (
  apiError: ApiError,
  fallback: string,
): string => {
  switch (apiError.status) {
    case 409:
      return 'The project changed since you opened it. Reload it and try again.'
    case 404:
      return 'This deliverable no longer exists. Reload the project.'
    default:
      return apiError.detail ?? fallback
  }
}
