FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine

# Create non-root user for security
RUN adduser -D -u 1000 executor && \
    mkdir -p /app /output /tmp && \
    chown executor:executor /app /output /tmp

WORKDIR /app

# Switch to non-root user
USER executor

# Default command
CMD ["dotnet"]
