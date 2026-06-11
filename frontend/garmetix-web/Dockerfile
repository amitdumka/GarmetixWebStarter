FROM node:22-alpine AS build
WORKDIR /app

# Nuxt/Nitro production build can exceed Node's default ~2GB heap after Stage 7 dashboard pages.
# Keep this inside Docker so local npm dev workflow remains unchanged.
ENV NODE_OPTIONS=--max-old-space-size=4096
ENV NUXT_TELEMETRY_DISABLED=1

COPY frontend/garmetix-web/package*.json ./
RUN npm install
COPY frontend/garmetix-web/ ./
RUN npm run build

FROM node:22-alpine
WORKDIR /app
ENV NODE_OPTIONS=--max-old-space-size=1024
COPY --from=build /app/.output ./.output
ENV HOST=0.0.0.0
ENV PORT=3000
EXPOSE 3000
CMD ["node", ".output/server/index.mjs"]
