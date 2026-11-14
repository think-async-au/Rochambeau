import type { AccountRole } from '../../shared/api/types.ts'
import { formatJoinDate, roleLabel } from './format.ts'

interface ProfileDetailsProps {
  username: string
  joinedAt: string
  role: AccountRole
  /** Only the owner's own Profile passes this (AC-09, AC-11). */
  email?: string
}

/** The Profile card. Every value is rendered as plain text. */
export function ProfileDetails({ username, joinedAt, role, email }: ProfileDetailsProps) {
  return (
    <section className="flex flex-col gap-4 rounded-lg border border-gray-200 p-6">
      <h1 className="text-3xl font-bold break-all">{username}</h1>
      <dl className="grid grid-cols-[auto_1fr] gap-x-6 gap-y-2">
        {email !== undefined && (
          <>
            <dt className="text-gray-600">Email</dt>
            <dd className="break-all">{email}</dd>
          </>
        )}
        <dt className="text-gray-600">Joined</dt>
        <dd>{formatJoinDate(joinedAt)}</dd>
        <dt className="text-gray-600">Role</dt>
        <dd>{roleLabel(role)}</dd>
      </dl>
    </section>
  )
}
