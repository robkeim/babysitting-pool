import { execFileSync } from 'node:child_process'
import { API_PROJECT } from './paths.ts'

/** Builds the API once; each worker then runs the built output instead of paying for `dotnet run`. */
export default function globalSetup() {
  execFileSync('dotnet', ['build', API_PROJECT, '--nologo', '--verbosity', 'quiet'], {
    stdio: 'inherit',
  })
}
