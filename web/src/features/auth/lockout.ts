/** When a blocked login can be tried again, in the viewer's local time (AC-06). */
export function formatRetryTime(lockoutEnd: Date): string {
  return lockoutEnd.toLocaleString(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  })
}
