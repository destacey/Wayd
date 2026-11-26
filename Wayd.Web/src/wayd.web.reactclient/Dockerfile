# Debian rather than Alpine: CI builds the client on Ubuntu and the prebuilt image ships that build, whose
# node_modules carry glibc binaries (sharp, which next/image loads at run time). They do not load on musl.
FROM node:26-slim AS base

# Install dependencies only when needed
FROM base AS deps
WORKDIR /app

# Install dependencies based on the preferred package manager
COPY Wayd.Web/src/wayd.web.reactclient/package.json Wayd.Web/src/wayd.web.reactclient/package-lock.json* ./
RUN npm ci

# Rebuild the source code only when needed
FROM base AS builder
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY Wayd.Web/src/wayd.web.reactclient/ .

# Copy shared docs folder for static page generation during build.
COPY docs/ /docs/
ENV DOCS_PATH=/docs

# Next.js collects completely anonymous telemetry data about general usage.
# Learn more here: https://nextjs.org/telemetry
ENV NEXT_TELEMETRY_DISABLED=1

# The placeholder entrypoint.sh replaces with the real API URL when the container starts. CI's
# check-and-build-client builds with the same value, since the prebuilt image ships its build.
RUN NEXT_PUBLIC_API_BASE_URL=APP_NEXT_PUBLIC_API_BASE_URL \
    npm run build

# What both production images share: the user the server runs as and its environment.
FROM base AS runtime
WORKDIR /app

ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1

RUN groupadd --system --gid 1001 nodejs \
    && useradd --system --uid 1001 --gid nodejs --create-home nextjs

# Docs pages render per request (the Content-Security-Policy nonce needs it), so
# the server reads the docs at run time, not only at build.
ENV DOCS_PATH=/docs

EXPOSE 3000
ENV PORT=3000

ENTRYPOINT [ "/app/entrypoint.sh" ]
CMD ["node", "server.js"]

# The image CI publishes: the build context is check-and-build-client's smoke-tested build, already laid out
# as app/ (the standalone server with .next/static, public and entrypoint.sh in place) and docs/, so this
# builds nothing. Built with `--target prebuilt`; the runner stage below builds the same image from source,
# for docker compose and local builds.
FROM runtime AS prebuilt
COPY --chown=nextjs:nodejs docs/ /docs/
COPY --chown=nextjs:nodejs app/ ./
RUN chmod +x /app/entrypoint.sh
USER nextjs

# Production image, copy all the files and run next
FROM runtime AS runner

COPY --from=builder /app/public ./public
COPY --chown=nextjs:nodejs docs/ /docs/

# Automatically leverage output traces to reduce image size
# https://nextjs.org/docs/advanced-features/output-file-tracing
COPY --from=builder --chown=nextjs:nodejs /app/.next/standalone ./
COPY --from=builder --chown=nextjs:nodejs /app/.next/static ./.next/static
RUN chown -R nextjs:nodejs /app/.next

# Must follow the standalone COPY above: output tracing sweeps the project root into
# .next/standalone, so that copy of entrypoint.sh lands on /app and overwrites whatever
# was there. Copying last is what puts the intended script in the final image; the chmod
# holds the exec bit regardless of the mode the traced copy arrives with.
COPY --from=builder /app/entrypoint.sh ./
RUN chmod +x /app/entrypoint.sh && chown nextjs:nodejs /app/entrypoint.sh

USER nextjs
