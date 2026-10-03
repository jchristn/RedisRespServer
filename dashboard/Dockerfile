FROM node:20-alpine AS builder

WORKDIR /app

# Copy package files
COPY package*.json ./

# Install dependencies
RUN npm ci

# Copy source code
COPY . .

# Build the frontend
RUN npm run build

# Production stage
FROM node:20-alpine AS production

# curl backs the container healthcheck
RUN apk add --no-cache curl

WORKDIR /app

# Copy package files for production dependencies
COPY package*.json ./

# Install production dependencies only
RUN npm ci --omit=dev

# Copy built frontend and server
COPY --from=builder /app/dist ./dist
COPY --from=builder /app/server ./server

# Expose ports: web UI/API and the Prometheus scrape endpoint (telemetry)
EXPOSE 3002
EXPOSE 9465

# Run the server
CMD ["node", "server/index.js"]
