FROM node:20-slim

# Create non-root user for security
RUN useradd -m -u 1001 executor && \
    mkdir -p /app /output /tmp && \
    chown executor:executor /app /output /tmp

WORKDIR /app

# Switch to non-root user
USER executor

# Default command
CMD ["node"]
