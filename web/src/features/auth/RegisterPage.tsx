import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'
import { ApiError, apiFetch } from '../../shared/api/client.ts'
import { setMe } from '../../shared/api/me.ts'
import type { FieldErrors, OwnProfile } from '../../shared/api/types.ts'
import { Button } from '../../shared/ui/Button.tsx'
import { FormError } from '../../shared/ui/FormError.tsx'
import { TextField } from '../../shared/ui/TextField.tsx'

interface Registration {
  username: string
  email: string
  password: string
}

/** Which field a conflict belongs to (AC-03). */
const conflictField: Record<string, keyof Registration> = {
  'identity.username_taken': 'username',
  'identity.email_unavailable': 'email',
}

/** US-01: register and land on the own Profile, logged in as a Player (AC-01–AC-03, AC-15). */
export function RegisterPage() {
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const [form, setForm] = useState<Registration>({ username: '', email: '', password: '' })
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [formError, setFormError] = useState<string>()

  const register = useMutation({
    mutationFn: (registration: Registration) =>
      apiFetch<OwnProfile>('/api/v1/accounts', { method: 'POST', body: registration }),
    onSuccess: (profile) => {
      setMe(queryClient, profile)
      navigate('/profile')
    },
    onError: (error) => {
      // Keep what the Visitor typed, except the password (AC-02).
      setForm((current) => ({ ...current, password: '' }))
      if (error instanceof ApiError && error.errors) {
        setFieldErrors(error.errors)
      } else if (error instanceof ApiError && conflictField[error.errorCode]) {
        setFieldErrors({ [conflictField[error.errorCode]]: [error.title] })
      } else {
        setFormError(
          error instanceof ApiError ? error.title : 'Something went wrong. Please try again.',
        )
      }
    },
  })

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (register.isPending) {
      return
    }
    setFieldErrors({})
    setFormError(undefined)
    register.mutate(form)
  }

  function field(name: keyof Registration) {
    return {
      name,
      value: form[name],
      errors: fieldErrors[name],
      onChange: (event: { target: { value: string } }) =>
        setForm((current) => ({ ...current, [name]: event.target.value })),
    }
  }

  return (
    <main className="mx-auto flex min-h-screen max-w-sm flex-col justify-center gap-6 p-6">
      <h1 className="text-3xl font-bold">Create your account</h1>
      <form noValidate onSubmit={submit} className="flex flex-col gap-4">
        <FormError message={formError} />
        <TextField label="Username" autoComplete="username" {...field('username')} />
        <TextField label="Email" type="email" autoComplete="email" {...field('email')} />
        <TextField
          label="Password"
          type="password"
          autoComplete="new-password"
          {...field('password')}
        />
        <Button type="submit" disabled={register.isPending}>
          {register.isPending ? 'Creating account…' : 'Create account'}
        </Button>
      </form>
      <p className="text-sm text-gray-600">
        Already have an account?{' '}
        <Link to="/login" className="text-blue-700 underline">
          Log in
        </Link>
      </p>
    </main>
  )
}
