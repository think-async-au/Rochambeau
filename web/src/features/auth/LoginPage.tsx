import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'
import { ApiError, apiFetch } from '../../shared/api/client.ts'
import { setMe } from '../../shared/api/me.ts'
import type { FieldErrors, OwnProfile } from '../../shared/api/types.ts'
import { Button } from '../../shared/ui/Button.tsx'
import { FormError } from '../../shared/ui/FormError.tsx'
import { TextField } from '../../shared/ui/TextField.tsx'
import { formatRetryTime } from './lockout.ts'

interface Credentials {
  email: string
  password: string
}

/** US-02: log in with email and password (AC-04–AC-06, AC-13). */
export function LoginPage() {
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const [form, setForm] = useState<Credentials>({ email: '', password: '' })
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [formError, setFormError] = useState<string>()

  const logIn = useMutation({
    mutationFn: (credentials: Credentials) =>
      apiFetch<OwnProfile>('/api/v1/session', { method: 'POST', body: credentials }),
    onSuccess: (profile) => {
      setMe(queryClient, profile)
      navigate('/profile')
    },
    onError: (error) => {
      setForm((current) => ({ ...current, password: '' }))
      if (error instanceof ApiError && error.errors) {
        setFieldErrors(error.errors)
      } else if (error instanceof ApiError && error.lockoutEnd) {
        setFormError(`${error.title} You can try again at ${formatRetryTime(error.lockoutEnd)}.`)
      } else {
        // Unknown email and wrong password get the same message (AC-05).
        setFormError(
          error instanceof ApiError ? error.title : 'Something went wrong. Please try again.',
        )
      }
    },
  })

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (logIn.isPending) {
      return
    }
    setFieldErrors({})
    setFormError(undefined)
    logIn.mutate(form)
  }

  function field(name: keyof Credentials) {
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
      <h1 className="text-3xl font-bold">Log in</h1>
      <form noValidate onSubmit={submit} className="flex flex-col gap-4">
        <FormError message={formError} />
        <TextField label="Email" type="email" autoComplete="email" {...field('email')} />
        <TextField
          label="Password"
          type="password"
          autoComplete="current-password"
          {...field('password')}
        />
        <Button type="submit" disabled={logIn.isPending}>
          Log in
        </Button>
      </form>
      <p className="text-sm text-gray-600">
        New here?{' '}
        <Link to="/register" className="text-blue-700 underline">
          Create an account
        </Link>
      </p>
    </main>
  )
}
