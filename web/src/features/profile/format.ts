import type { AccountRole } from '../../shared/api/types.ts'

const roleLabels: Record<AccountRole, string> = {
  player: 'Player',
  administrator: 'Administrator',
}

/** The role as a Profile shows it (AC-09, AC-13). */
export function roleLabel(role: AccountRole): string {
  return roleLabels[role]
}

/** The join date in the viewer's locale. */
export function formatJoinDate(joinedAt: string): string {
  return new Date(joinedAt).toLocaleDateString(undefined, { dateStyle: 'long' })
}
