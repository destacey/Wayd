This is a [Next.js](https://nextjs.org/) project bootstrapped with [`create-next-app`](https://github.com/vercel/next.js/tree/canary/packages/create-next-app).

## Getting Started
First, create a .env.local file in the root of this project with the following variable, without quotes:

```
NEXT_PUBLIC_API_BASE_URL='Get this from the launch settings of Wayd.Web.Api (ex: https://localhost:7021)'
```

Identity providers (Microsoft Entra ID, etc.) are no longer configured via env vars — the login page discovers them at runtime from the API, and admins manage them in **Settings → Identity Providers**.

Second, run the development server:

```bash
npm run dev
# or
yarn dev
# or
pnpm dev
```

Open [http://localhost:3000](http://localhost:3000) with your browser to see the result.

## Core Libraries
- Next.js (16.x)
- React (19.x)
- Typescript (6.x)
- Ant Design (6.x)
- RTK (2.x)
- oidc-client-ts (3.x) - OIDC / PKCE authentication
- Axios
