import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { PostRequestPage } from '@/features/requests'

export const Route = createFileRoute('/_authed/requests/new')({
  component: function PostRequest() {
    const navigate = useNavigate()
    return <PostRequestPage onPosted={() => void navigate({ to: '/requests' })} />
  },
})
