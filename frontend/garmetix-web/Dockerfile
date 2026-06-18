FROM node:22-alpine AS build
WORKDIR /app

# Nuxt/Nitro production build can exceed Node's default ~2GB heap after Stage 7 dashboard pages.
# Keep this inside Docker so local npm dev workflow remains unchanged.
ENV NODE_OPTIONS=--max-old-space-size=4096
ENV NUXT_TELEMETRY_DISABLED=1

COPY frontend/garmetix-web/package*.json ./
COPY frontend/garmetix-web/.npmrc ./
RUN npm config set registry https://registry.npmjs.org/ \
    && npm config set fetch-retries 5 \
    && npm config set fetch-retry-mintimeout 20000 \
    && npm config set fetch-retry-maxtimeout 120000 \
    && npm ci --prefer-online --no-audit --no-fund
COPY frontend/garmetix-web/ ./
RUN npm run build

FROM node:22-alpine
WORKDIR /app
ENV NODE_OPTIONS=--max-old-space-size=1024
COPY --from=build /app/.output ./.output
# Keep the local Lucide Iconify collection available at runtime for the custom
# /api/_nuxt_icon/lucide.json handler. Copying from the build layer avoids
# production image network installs and avoids Nuxt Icon's JSON import-attribute
# runtime issue on Node 22.
COPY --from=build /app/node_modules/@iconify-json ./node_modules/@iconify-json
ENV HOST=0.0.0.0
ENV PORT=3000
EXPOSE 3000
CMD ["node", ".output/server/index.mjs"]
