/** A user-facing message for any thrown value. */
export function messageOf(error: unknown): string {
  return error instanceof Error ? error.message : 'Something went wrong. Please try again.'
}
